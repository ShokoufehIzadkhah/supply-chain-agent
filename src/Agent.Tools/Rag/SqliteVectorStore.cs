using Microsoft.Data.Sqlite;

namespace Agent.Tools.Rag;

/// <summary>
/// Vector store backed by a single SQLite file. Chunk text, source file, heading and section live in
/// <c>policy_chunks</c>, the file hashes that drive incremental re-indexing live in
/// <c>policy_sources</c>, and each embedding is stored as a little-endian <c>float32</c> BLOB.
/// <para>
/// Ranking happens in memory after loading the chunks, because SQLite has no vector operator and the
/// policy corpus is tiny. Swapping in Azure AI Search later means implementing
/// <see cref="IVectorStore"/> and pushing the ranking down to the service.
/// </para>
/// </summary>
public sealed class SqliteVectorStore : IVectorStore
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS policy_sources (
            source_file  TEXT NOT NULL PRIMARY KEY,
            content_hash TEXT NOT NULL,
            indexed_at   TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS policy_chunks (
            source_file TEXT    NOT NULL,
            chunk_index INTEGER NOT NULL,
            heading     TEXT    NOT NULL,
            section     TEXT    NULL,
            text        TEXT    NOT NULL,
            vector      BLOB    NOT NULL,
            PRIMARY KEY (source_file, chunk_index)
        );
        """;

    private readonly string _connectionString;

    /// <summary>Creates the store for one database file, creating its directory when needed.</summary>
    /// <param name="databasePath">Path of the SQLite file, for example <c>policy-index.db</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="databasePath"/> is empty.</exception>
    public SqliteVectorStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_file FROM policy_sources ORDER BY source_file;";

        var sources = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            sources.Add(reader.GetString(0));
        }

        return sources;
    }

    /// <inheritdoc />
    public async Task<string?> GetContentHashAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT content_hash FROM policy_sources WHERE source_file = $source;";
        command.Parameters.AddWithValue("$source", sourceFile);

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    /// <inheritdoc />
    public async Task StoreAsync(
        string sourceFile,
        string contentHash,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);

        // One transaction so a re-index is all-or-nothing: readers never see a half-replaced file.
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = "DELETE FROM policy_chunks WHERE source_file = $source;";
            delete.Parameters.AddWithValue("$source", sourceFile);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO policy_chunks (source_file, chunk_index, heading, section, text, vector)
                VALUES ($source, $index, $heading, $section, $text, $vector);
                """;

            var source = insert.Parameters.Add("$source", SqliteType.Text);
            var index = insert.Parameters.Add("$index", SqliteType.Integer);
            var heading = insert.Parameters.Add("$heading", SqliteType.Text);
            var section = insert.Parameters.Add("$section", SqliteType.Text);
            var text = insert.Parameters.Add("$text", SqliteType.Text);
            var vector = insert.Parameters.Add("$vector", SqliteType.Blob);

            source.Value = sourceFile;

            foreach (var chunk in chunks)
            {
                index.Value = chunk.Chunk.ChunkIndex;
                heading.Value = chunk.Chunk.Heading;
                section.Value = chunk.Chunk.Section ?? (object)DBNull.Value;
                text.Value = chunk.Chunk.Text;
                vector.Value = ToBytes(chunk.Vector.Span);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = (SqliteTransaction)transaction;
            upsert.CommandText = """
                INSERT INTO policy_sources (source_file, content_hash, indexed_at)
                VALUES ($source, $hash, $indexedAt)
                ON CONFLICT (source_file) DO UPDATE SET
                    content_hash = excluded.content_hash,
                    indexed_at   = excluded.indexed_at;
                """;
            upsert.Parameters.AddWithValue("$source", sourceFile);
            upsert.Parameters.AddWithValue("$hash", contentHash);
            upsert.Parameters.AddWithValue("$indexedAt", DateTimeOffset.UtcNow.ToString("O"));
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var sql in new[]
                 {
                     "DELETE FROM policy_chunks WHERE source_file = $source;",
                     "DELETE FROM policy_sources WHERE source_file = $source;",
                 })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$source", sourceFile);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (topK <= 0)
        {
            return [];
        }

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_file, chunk_index, heading, section, text, vector
            FROM policy_chunks;
            """;

        var chunks = new List<IndexedChunk>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var chunk = new PolicyChunk(
                reader.GetString(0),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(1));

            chunks.Add(new IndexedChunk(chunk, ToVector((byte[])reader[5])));
        }

        return CosineSimilarity.TopK(query, chunks, topK);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static byte[] ToBytes(ReadOnlySpan<float> vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(float)), vector[i]);
        }

        return bytes;
    }

    private static float[] ToVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
        }

        return vector;
    }
}

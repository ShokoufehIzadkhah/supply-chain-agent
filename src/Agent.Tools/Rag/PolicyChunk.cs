namespace Agent.Tools.Rag;

/// <summary>
/// One retrievable piece of a policy document. Chunks are built on Markdown heading boundaries, so a
/// chunk never starts in the middle of a paragraph and the heading it belongs to is always known.
/// </summary>
/// <param name="SourceFile">File the chunk came from, for example <c>order-change-rules.md</c>.</param>
/// <param name="Heading">
/// Heading path of the first section in the chunk, for example
/// <c>Order Change Rules &gt; 3. The five-day rule</c>.
/// </param>
/// <param name="Section">
/// Numbered sections the chunk covers, for example <c>3</c> or <c>3.1-3.5</c>; <c>null</c> when the
/// section headings are not numbered.
/// </param>
/// <param name="Text">Body text of the covered sections, without the headings.</param>
/// <param name="ChunkIndex">Zero-based position of the chunk inside its source file.</param>
public sealed record PolicyChunk(
    string SourceFile,
    string Heading,
    string? Section,
    string Text,
    int ChunkIndex);

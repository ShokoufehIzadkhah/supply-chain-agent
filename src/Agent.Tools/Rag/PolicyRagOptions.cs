namespace Agent.Tools.Rag;

/// <summary>Configuration for the policy RAG index, read from the <c>Rag</c> configuration section.</summary>
public sealed class PolicyRagOptions
{
    /// <summary>
    /// Directory holding the policy Markdown documents. Relative paths are resolved against the
    /// application directory. Defaults to <c>docs/policies</c>.
    /// </summary>
    public string PolicyDirectory { get; set; } = Path.Combine("docs", "policies");

    /// <summary>
    /// SQLite file holding the vector index. Relative paths are resolved against the application
    /// directory. Defaults to <c>policy-index.db</c>.
    /// </summary>
    public string DatabasePath { get; set; } = "policy-index.db";
}

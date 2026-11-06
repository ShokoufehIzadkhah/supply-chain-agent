using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tools.Rag;

/// <summary>
/// Agent tool that answers policy questions from the indexed policy documents. It embeds the question,
/// ranks the stored chunks with cosine similarity and returns the best three with the file and section
/// they came from, so the agent can cite the rule it applied.
/// </summary>
public sealed class PolicyTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>How many chunks a policy question returns.</summary>
    private const int TopK = 3;

    private const string NoPoliciesIndexed =
        "No policy documents are indexed, so no policy could be checked.";

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly IVectorStore _store;
    private readonly ILogger<PolicyTools> _logger;

    /// <summary>Creates the tool.</summary>
    /// <param name="embeddings">Embedding generator, from the configured <c>AI:EmbeddingModel</c>.</param>
    /// <param name="store">Index holding the policy chunks.</param>
    /// <param name="logger">Logger used to record every tool call.</param>
    public PolicyTools(
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IVectorStore store,
        ILogger<PolicyTools> logger)
    {
        _embeddings = embeddings;
        _store = store;
        _logger = logger;
    }

    /// <summary>Searches the policy documents for the rules that answer a question.</summary>
    /// <param name="question">The policy question, in the user's own words.</param>
    /// <returns>
    /// Short JSON listing the matching rules with their source file, heading and section, or a message
    /// when nothing is indexed.
    /// </returns>
    [Description(
        "Searches the company's policy documents for the rules that apply to a question, for example " +
        "'can I move a delivery date?', 'how long is the production lead time?', 'which customers are " +
        "priority accounts?' or 'what is the minimum order quantity for corrugated boxes?'. Call it " +
        "before explaining or proposing any order change, and also when the user asks what a policy " +
        "says. It returns the three most relevant text passages, each with the source document name, " +
        "the heading and the numbered section, so you can cite the rule you used. If nothing relevant " +
        "is found, say so instead of inventing a rule.")]
    public async Task<string> SearchPolicies(
        [Description(
            "The policy question in natural language, for example 'how many days before production " +
            "start can a delivery date still be changed?'.")]
        string question)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await SearchAsync(question);
            var output = result is null
                ? NoPoliciesIndexed
                : JsonSerializer.Serialize(result, JsonOptions);

            _logger.LogInformation(
                "Tool {ToolName} called with {ToolArguments} in {DurationMs} ms",
                nameof(SearchPolicies),
                new { question },
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tool {ToolName} called with {ToolArguments} failed after {DurationMs} ms",
                nameof(SearchPolicies),
                new { question },
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Reads the JSON produced by <see cref="SearchPolicies"/> back into a result, so a host can report
    /// which documents an answer is grounded in without parsing text of its own.
    /// </summary>
    /// <param name="output">The value <see cref="SearchPolicies"/> returned.</param>
    /// <returns>The parsed result, or <c>null</c> when nothing was indexed.</returns>
    public static PolicySearchResult? ParseResult(string output)
        => output == NoPoliciesIndexed
            ? null
            : JsonSerializer.Deserialize<PolicySearchResult>(output, JsonOptions);

    private async Task<PolicySearchResult?> SearchAsync(string question)
    {
        var embedding = await _embeddings.GenerateAsync(question);
        var results = await _store.SearchAsync(embedding.Vector, TopK);

        if (results.Count == 0)
        {
            return null;
        }

        return new PolicySearchResult(
            question,
            [
                .. results.Select(result => new PolicySearchMatch(
                    result.Chunk.SourceFile,
                    result.Chunk.Heading,
                    result.Chunk.Section,
                    Math.Round(result.Score, 4),
                    result.Chunk.Text)),
            ]);
    }
}

/// <summary>One passage of a policy document that matched a search.</summary>
/// <param name="Source">File the passage came from, for example <c>order-change-rules.md</c>.</param>
/// <param name="Heading">Heading path of the passage.</param>
/// <param name="Section">Numbered section the passage belongs to; <c>null</c> when headings are unnumbered.</param>
/// <param name="Score">Cosine similarity between the question and the passage.</param>
/// <param name="Text">The passage itself.</param>
public sealed record PolicySearchMatch(string Source, string Heading, string? Section, double Score, string Text);

/// <summary>The passages a policy question matched.</summary>
/// <param name="Question">The question that was asked.</param>
/// <param name="Matches">The best matching passages, most similar first.</param>
public sealed record PolicySearchResult(string Question, IReadOnlyList<PolicySearchMatch> Matches);

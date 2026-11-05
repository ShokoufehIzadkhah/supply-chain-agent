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
            var output = await SearchAsync(question);

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

    private async Task<string> SearchAsync(string question)
    {
        var embedding = await _embeddings.GenerateAsync(question);
        var results = await _store.SearchAsync(embedding.Vector, TopK);

        if (results.Count == 0)
        {
            return NoPoliciesIndexed;
        }

        var matches = results.Select(result => new
        {
            source = result.Chunk.SourceFile,
            heading = result.Chunk.Heading,
            section = result.Chunk.Section,
            score = Math.Round(result.Score, 4),
            text = result.Chunk.Text,
        });

        return JsonSerializer.Serialize(new { question, matches }, JsonOptions);
    }
}

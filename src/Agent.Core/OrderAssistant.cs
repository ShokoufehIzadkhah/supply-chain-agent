using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agent.Core;

/// <summary>
/// Creates the order assistant: the system prompt that constrains what it may say and do, and the
/// Microsoft Agent Framework agent that runs it. The prompt is embedded in this assembly, so every host
/// gives the model the same instructions without copying files or keeping two versions in step.
/// </summary>
public static class OrderAssistant
{
    /// <summary>Name the agent reports to the framework.</summary>
    public const string Name = "OrderAssistant";

    /// <summary>One-line description of what the agent does.</summary>
    public const string Description =
        "Answers customer service questions about orders, inventory and shipments.";

    private const string SystemPromptResource = "Agent.Core.Prompts.system.md";

    /// <summary>Reads the system prompt embedded in this assembly.</summary>
    /// <returns>The prompt text.</returns>
    /// <exception cref="InvalidOperationException">The embedded prompt was not found.</exception>
    public static string LoadSystemPrompt()
    {
        using var stream = typeof(OrderAssistant).Assembly.GetManifestResourceStream(SystemPromptResource)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{SystemPromptResource}' was not found. " +
                "Check the EmbeddedResource item in Agent.Core.csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Creates the agent on the chat client described by <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">Configuration containing the <c>AI</c> section.</param>
    /// <param name="tools">Tools the agent may call, typically from <see cref="AgentToolset"/>.</param>
    /// <param name="loggerFactory">Optional factory used to log the agent's own diagnostics.</param>
    /// <returns>The configured agent.</returns>
    public static AIAgent Create(
        IConfiguration configuration,
        IEnumerable<AITool> tools,
        ILoggerFactory? loggerFactory = null)
        => Create(ChatClientFactory.Create(configuration), tools, loggerFactory);

    /// <summary>
    /// Creates the agent on an already-created chat client. A server takes this overload: it builds the
    /// client once and creates one agent per request, because the tools an agent reports on belong to
    /// the request that ran them.
    /// </summary>
    /// <param name="chatClient">Chat client the agent sends its requests to.</param>
    /// <param name="tools">Tools the agent may call, typically from <see cref="AgentToolset"/>.</param>
    /// <param name="loggerFactory">Optional factory used to log the agent's own diagnostics.</param>
    /// <returns>The configured agent.</returns>
    public static AIAgent Create(
        IChatClient chatClient,
        IEnumerable<AITool> tools,
        ILoggerFactory? loggerFactory = null)
        => chatClient.AsAIAgent(
            instructions: LoadSystemPrompt(),
            name: Name,
            description: Description,
            tools: tools.ToList(),
            loggerFactory: loggerFactory);
}

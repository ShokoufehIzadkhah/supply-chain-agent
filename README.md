# Supply Chain Order Assistant

An AI agent that helps a manufacturing company's Customer Service team answer questions about orders, inventory, and shipments, and propose order changes that a human must approve. The agent talks to a mock ERP over HTTP only, never applies changes itself, and is structured as production-minded .NET (API, shared tools, console host, tests) with a chat UI to follow.

## Getting started

Placeholder — local run steps will be added as the projects are wired up.

## MCP server

`src/Mcp.Server` exposes the same `Agent.Tools` functions over the Model Context Protocol on the stdio transport, so a coding agent (GitHub Copilot in VS Code) or a desktop LLM client (Claude Desktop) can call them. It is the tool surface, not the agent: there is no chat model and no system prompt in this process, so the client brings the model and the instructions.

- **One definition of every tool.** `AgentToolset` lists the six methods and both hosts project that list, so the MCP server never restates a tool name, description or parameter schema, and `Agent.Tools` gains no MCP dependency. The four read-only ERP lookups and `SearchPolicies` are advertised with `readOnlyHint`; `ProposeDeliveryDateChange` is not read-only, but it only files a `Pending` change request that a supervisor still has to approve.
- **Configuration is the console host's configuration.** The server reads `ErpApi:BaseUrl` and `ApiKeys:Agent` for the ERP API, and `AI:Provider` / `AI:EmbeddingModel` / `AI:ApiKey` for `SearchPolicies`, from `appsettings.json`, `dotnet user-secrets` (all agent-side hosts share one secrets file) or environment variables. `AI:ChatModel` is not needed. Start `src/Erp.Api` before you use the tools.
- **Paths do not depend on the working directory.** The client chooses the working directory, so configuration and `docs/policies` are anchored to the server assembly. The policy index is rebuilt at startup into `policy-index.db` next to it, exactly as in the console host.
- **stdout stays clean.** stdout carries the JSON-RPC protocol, so every log line goes to stderr, where the client shows it.

Build the solution first, then point the client at the built assembly:

```powershell
dotnet build
```

### VS Code (GitHub Copilot agent mode)

Add this to `.vscode/mcp.json` in the repository root and reload the window. Copilot agent mode then lists the server and its six tools in the chat tools picker:

```json
{
  "servers": {
    "supply-chain": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "${workspaceFolder}/src/Mcp.Server/bin/Debug/net10.0/Mcp.Server.dll"
      ]
    }
  }
}
```

VS Code expands `${workspaceFolder}`, so the same file works from a clone anywhere.

### Claude Desktop

Add this to `%APPDATA%\Claude\claude_desktop_config.json` and restart Claude Desktop (the Microsoft Store install keeps the file under `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "supply-chain": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\you\\source\\repos\\supply-chain-agent\\src\\Mcp.Server\\bin\\Debug\\net10.0\\Mcp.Server.dll"
      ]
    }
  }
}
```

Replace the path with the absolute path to your own build. Claude Desktop does not expand variables here, and it starts the process without a working directory of its own choosing, which is why the server resolves everything relative to its assembly.

Neither file contains a secret: both clients launch the server as a child process, so it reads the API keys from `dotnet user-secrets` or the environment itself. For a deployment, publish instead of pointing at `bin/Debug` and pass the keys as environment variables:

```powershell
dotnet publish src/Mcp.Server -c Release -o <folder>
```

## Security design

The agent can ask for a change, but it can never make one. That separation is enforced in three layers, none of which relies on the model behaving well.

- **Two roles, two keys.** The ERP API authenticates every request with an `X-Api-Key` header and a single authentication scheme. The **Agent** key may create and read change requests; the **Supervisor** key may read them and decide on them. A key that is missing or unknown gets `401`; a valid key used on an endpoint its role does not cover gets `403`.
- **Decisions are not in the tools library.** `Agent.Tools` only exposes `ProposeDeliveryDateChange`, which submits a `Pending` request, plus the read-only lookup tools. It holds the Agent key alone and contains no method that approves, rejects or applies a change, so there is no code path the agent could reach to approve its own request.
- **Hard rules live in the API.** `POST /change-requests` returns `400` for changes the ERP never accepts — an order that is already `Delivered` or `Cancelled`, or a new date earlier than the order date — regardless of what the agent asks. Softer business policies stay with the agent (checked before it proposes) and with the supervisor who decides.

Keys are never committed: supply them with `dotnet user-secrets` (`ApiKeys:Agent` for the API and the console host, `ApiKeys:Supervisor` for the API) or environment variables. `AddAgentTools` fails at startup when `ApiKeys:Agent` is missing.

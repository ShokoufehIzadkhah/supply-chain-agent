using System.Reflection;
using Agent.Tools;
using Agent.Tools.Rag;
using Microsoft.Extensions.AI;

namespace Agent.Core;

/// <summary>
/// The single definition of what the order assistant may call: four read-only ERP lookups, the policy
/// search, and the propose-only delivery date change. Hosts turn this list into whatever their runtime
/// needs — Microsoft.Extensions.AI functions for the console, MCP tools for <c>Mcp.Server</c> — so the
/// tool surface cannot drift between them and no host restates a tool's name, description or schema.
/// </summary>
public static class AgentToolset
{
    /// <summary>The tool methods, in the order they are advertised to a model.</summary>
    public static IReadOnlyList<MethodInfo> Methods { get; } =
    [
        typeof(OrderTools).GetMethod(nameof(OrderTools.GetOrderStatus))!,
        typeof(OrderTools).GetMethod(nameof(OrderTools.FindOrdersByCustomer))!,
        typeof(OrderTools).GetMethod(nameof(OrderTools.CheckInventory))!,
        typeof(OrderTools).GetMethod(nameof(OrderTools.GetShipmentInfo))!,
        typeof(OrderTools).GetMethod(nameof(OrderTools.ProposeDeliveryDateChange))!,
        typeof(PolicyTools).GetMethod(nameof(PolicyTools.SearchPolicies))!,
    ];

    /// <summary>
    /// Binds the tool methods to the instances that implement them, keeping the descriptions from the
    /// <c>[Description]</c> attributes on the original methods.
    /// </summary>
    /// <param name="orderTools">ERP lookup and change proposal tools.</param>
    /// <param name="policyTools">Policy search tool.</param>
    /// <returns>The agent's tools.</returns>
    public static IReadOnlyList<AIFunction> ToFunctions(OrderTools orderTools, PolicyTools policyTools) =>
    [
        .. Methods.Select(method =>
        {
            object target = method.DeclaringType == typeof(OrderTools) ? orderTools : policyTools;
            return AIFunctionFactory.Create(method, target, options: null);
        }),
    ];
}

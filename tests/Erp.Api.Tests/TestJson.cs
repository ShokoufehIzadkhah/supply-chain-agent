using System.Text.Json;
using System.Text.Json.Serialization;

namespace Erp.Api.Tests;

/// <summary>JSON settings that mirror the API's contract: camelCase properties and enums as names.</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

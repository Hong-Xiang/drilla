using System.Text.Json;

namespace DualDrill.Common;

public static class CustomJsonOption
{
    // TODO: migrate to  JsonSerializerOptions.Web when update to .NET 9
    public static JsonSerializerOptions Web = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

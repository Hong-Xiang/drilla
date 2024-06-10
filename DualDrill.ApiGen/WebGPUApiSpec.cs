using System.Text;
using System.Text.Json.Serialization;

namespace DualDrill.ApiGen;

public class AdHocData
{
    public ApiEnumType[] Enums { get; set; }
}

public sealed record class WebGPUApiSpec(
    IApiType[] Types,
    IApiValue[] Values,
    ApiMethod[] Methods
)
{
}

[JsonDerivedType(typeof(ApiEnumType), typeDiscriminator: "enum")]
[JsonDerivedType(typeof(ApiOpaqueHandleType), typeDiscriminator: "opaque-handle")]
[JsonDerivedType(typeof(ApiValueRecordType), typeDiscriminator: "value-record")]
public interface IApiType
{
    public string Name { get; }
}

public sealed record class ApiTypeReference(string Name, string? NativeName)
{
}

public interface IApiValue
{
    public string Name { get; }
}

public sealed record class Parameter(
    string Name,
    ApiTypeReference TypeReference
)
{
}

public sealed record class ApiMethod(
    string Name,
    string NativeName,
    Parameter[] Parameters)
{
}

public sealed record class ApiOpaqueHandleType(string Name, string NativeName, ApiMethod[] Methods) : IApiType
{
}

public sealed record class Field(string Name, ApiTypeReference TypeReference)
{
}

public sealed record class ApiValueRecordType(string Name, Field[] Fields) : IApiType
{
}

public sealed record class ApiEnumValue(
    string Name,
    string NativeName)
{
}

public sealed record class ApiEnumType(
    string Name,
    string NativeName,
    ApiEnumValue[] Values,
    bool IsFlag)
    : IApiType
{
}

public sealed class GraphicsCSharpApiSourceCodeBuilder
{
    public string BuildEnums(WebGPUApiSpec spec)
    {
        var result = new StringBuilder();
        foreach (var e in spec.Types)
        {
            if (e is ApiEnumType ee)
            {
                result.AppendLine(BuildEnum(ee));
                result.AppendLine();
            }
        }
        return result.ToString();
    }

    string BuildEnum(ApiEnumType enumDef)
    {
        var result = new StringBuilder();
        if (enumDef.IsFlag)
        {
            result.AppendLine("[Flags]");
        }
        result.AppendLine($"public enum {enumDef.Name} : uint {{");
        result.AppendLine(string.Join("," + Environment.NewLine, enumDef.Values.Select(ev => $"{ev.Name} = {enumDef.NativeName}.{ev.NativeName}")));
        result.AppendLine($"}}");
        return result.ToString();
    }
}

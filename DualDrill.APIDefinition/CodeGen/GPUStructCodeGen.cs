using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Value;
using System.Globalization;
using System.Text.Json;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUStructCodeGen(
    ModuleDeclaration Module
)
{
    static readonly IReadOnlyDictionary<(string Struct, string Property), string> PropertyDefaults =
        new Dictionary<(string Struct, string Property), string>
        {
            [("GPUSamplerDescriptor", "AddressModeU")] = "GPUAddressMode.ClampToEdge",
            [("GPUSamplerDescriptor", "AddressModeV")] = "GPUAddressMode.ClampToEdge",
            [("GPUSamplerDescriptor", "AddressModeW")] = "GPUAddressMode.ClampToEdge",
            [("GPUSamplerDescriptor", "LodMaxClamp")] = "32",
            [("GPUSamplerDescriptor", "LodMinClamp")] = "0",
            [("GPUSamplerDescriptor", "MagFilter")] = "GPUFilterMode.Nearest",
            [("GPUSamplerDescriptor", "MaxAnisotropy")] = "1",
            [("GPUSamplerDescriptor", "MinFilter")] = "GPUFilterMode.Nearest",
            [("GPUSamplerDescriptor", "MipmapFilter")] = "GPUMipmapFilterMode.Nearest",
        };

    public void EmitStruct(TextWriter tw, StructDeclaration decl)
    {
        tw.Write("public partial struct ");
        tw.Write(decl.Name);
        tw.WriteLine("()");
        tw.WriteLine("{");

        var fieldTypeNameOption = CSharpTypeNameVisitor.Default.Option with
        {
            Usage = CSharpTypeNameVisitorOption.TypeUsage.PropertyType
        };
        foreach (var f in decl.Properties)
        {
            var isHandle = f.Type is OpaqueTypeReference { Name: var name }
                && Module.Handles.Any(h => name == h.Name);
            var typeName = isHandle
                ? $"I{((OpaqueTypeReference)f.Type).Name}"
                : f.Type is SequenceTypeReference { Type: OpaqueTypeReference { Name: var element } }
                    && Module.Handles.Any(h => element == h.Name)
                    ? $"IReadOnlyList<I{element}>"
                : f.Type.GetCSharpTypeName(fieldTypeNameOption);
            var isOptional = !f.IsRequired && f.DefaultValue is null
                && !PropertyDefaults.ContainsKey((decl.Name, f.Name));
            tw.Write("public ");
            if (f.IsRequired)
                tw.Write("required ");
            tw.Write(typeName);
            if (isOptional && f.Type is not NullableTypeReference)
                tw.Write('?');
            tw.Write(' ');
            tw.Write(f.Name);
            tw.Write(" { get; set; }");
            var defaultValue = f.DefaultValue switch
            {
                BooleanValue boolean => boolean.Value ? "true" : "false",
                NumberValue number => NumericDefault(number.Value, f.Type, typeName),
                StringValue text when f.Type is OpaqueTypeReference =>
                    EnumDefault(typeName, text.Value),
                StringValue { Value: "" } when f.Type is StringTypeReference => "string.Empty",
                StringValue text when f.Type is StringTypeReference => JsonSerializer.Serialize(text.Value),
                EmptySequenceValue when f.Type is SequenceTypeReference => null,
                EmptyDictionaryValue when f.Type is RecordTypeReference => "new()",
                EmptyDictionaryValue when f.Type is OpaqueTypeReference
                    && Module.Structs.Any(s => s.Name == typeName) => "new()",
                null when PropertyDefaults.TryGetValue((decl.Name, f.Name), out var fallback) => fallback,
                null => null,
                _ => throw new NotSupportedException($"Unsupported WebIDL default for {decl.Name}.{f.Name}")
            };
            if (defaultValue is not null)
            {
                tw.Write(" = ");
                tw.Write(defaultValue);
                tw.Write(';');
            }
            tw.WriteLine();
        }

        if (decl.Name is "GPUComputePipelineDescriptor" or "GPURenderPipelineDescriptor"
            && !decl.Properties.Any(property => property.Name == "Layout"))
        {
            tw.WriteLine("public IGPUPipelineLayout? Layout { get; set; }");
        }

        tw.WriteLine("}");
        tw.WriteLine();
    }

    static string NumericDefault(string value, ITypeReference type, string typeName)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            _ = ulong.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        else
            _ = decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return type is OpaqueTypeReference ? $"({typeName}){value}" : value;
    }

    static string EnumDefault(string name, string value)
    {
        var type = typeof(DualDrill.Graphics.GPUBackendType).Assembly
            .GetType($"DualDrill.Graphics.{name}");
        if (type is not { IsEnum: true })
            throw new NotSupportedException($"WebIDL default for {name} is not an enum value.");
        var member = Enum.Parse(type, AlimerWebGPUApi.GetManagedEnumMemberName(name, value), ignoreCase: true);
        return $"{name}.{member}";
    }
}

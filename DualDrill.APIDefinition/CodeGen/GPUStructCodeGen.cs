using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

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
            tw.Write("public ");
            if (f.Type is OpaqueTypeReference { Name: var name } && Module.Handles.Any(h => name == h.Name))
            {
                tw.Write("I");
                tw.Write(name);
            }
            else
            {
                tw.Write(f.Type.GetCSharpTypeName(fieldTypeNameOption));
            }
            tw.Write(' ');
            tw.Write(f.Name);
            tw.Write(" { get; set; }");
            if (PropertyDefaults.TryGetValue((decl.Name, f.Name), out var defaultValue))
            {
                tw.Write(" = ");
                tw.Write(defaultValue);
                tw.Write(';');
            }
            tw.WriteLine();
        }

        if (decl.Name == "GPUComputePipelineDescriptor"
            && !decl.Properties.Any(property => property.Name == "Layout"))
        {
            tw.WriteLine("public IGPUPipelineLayout? Layout { get; set; }");
        }

        tw.WriteLine("}");
        tw.WriteLine();
    }
}

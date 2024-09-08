using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUStructCodeGen(
    ModuleDeclaration Module
)
{
    public void EmitStruct(TextWriter tw, StructDeclaration decl)
    {
        tw.Write("public partial struct ");
        tw.Write(decl.Name);
        tw.WriteLine("()");
        tw.WriteLine("{");

        var fieldTypeNameOption = CSharpTypeNameVisitor.Default.Option with
        {
            Usage = CSharpTypeNameVisitor.VisitorOption.TypeUsage.PropertyType
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
            tw.WriteLine(" { get; set; }");
        }

        tw.WriteLine("}");
        tw.WriteLine();
    }
}

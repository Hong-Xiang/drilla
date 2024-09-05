using DualDrill.ApiGen.DrillLang;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.CodeGen;

public static class CodeGenExtension
{
    public static string GetCSharpTypeName(this ITypeReference typeRef,
        CSharpTypeNameVisitor.VisitorOption? option = default,
        INameTransform? transform = default)
        => typeRef.AcceptVisitor(new CSharpTypeNameVisitor(
                option ?? CSharpTypeNameVisitor.Default.Option,
                transform ?? CSharpTypeNameVisitor.Default.Transform
        ));

    public static ModuleDeclaration CodeGenAdHocTransform(this ModuleDeclaration module)
    {
        return module.Transform(new GpuApiCodeGenTransform());
    }
}

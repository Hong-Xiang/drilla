using DualDrill.ApiGen.DrillLang;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.CodeGen;

public static class CodeGenExtension
{
    public static string GetCSharpTypeName(this ITypeReference typeRef,
        CSharpTypeNameVisitorOption? option = default,
        INameTransform? transform = default)
        => typeRef.AcceptVisitor(new CSharpTypeNameVisitor(
                option ?? CSharpTypeNameVisitor.Default.Option,
                transform ?? CSharpTypeNameVisitor.Default.Transform
        ));

    public static ModuleDeclaration CodeGenAdHocTransform(this ModuleDeclaration module, ModuleDeclaration evergineModule)
    {
        var result = module.Transform(new GpuApiCodeGenTransform(evergineModule));

        // add async method support
        result = result with
        {
            Handles = [..result.Handles.Select(h => h with {
                Methods = [..h.Methods.Select(m => {
                    if(m.ReturnType is FutureTypeReference){
                        return m with {
                            Name = m.Name + "Async",
                            Parameters = [..m.Parameters, new ParameterDeclaration(
                                "cancellation",
                                new OpaqueTypeReference("CancellationToken"), null )]
                        };
                    }else{
                        return m;
                    }
                })]
            })]
        };

        return result;
    }
}

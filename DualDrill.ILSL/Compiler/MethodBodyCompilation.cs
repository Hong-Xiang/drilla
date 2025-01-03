using DualDrill.CLSL.Language.Declaration;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Compiler;

public sealed record class MethodBodyCompilation(
    ShaderModuleCompilation ShaderModuleCompilation,
    MethodBase Method,
    MethodBody MethodBody,
    ImmutableArray<Instruction> Instructions
)
{
    public static MethodBodyCompilation Create(
        ShaderModuleCompilation shaderModuleCompilation,
        MethodBase method
    )
    {
        var body = method.GetMethodBody()
            ?? throw new NullReferenceException("Failed to get method body");
        return new(shaderModuleCompilation, method, body, [.. method.GetInstructions()]);
    }
}

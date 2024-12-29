using DualDrill.CLSL.Language.Declaration;
using Lokad.ILPack.IL;
using System.Reflection;

namespace DualDrill.ILSL.Compiler;

public sealed record class MethodBodyCompilation(
    ISharpShader ShaderModule,
    MethodBase Method,
    MethodBody MethodBody,
    IReadOnlyList<Instruction> Instructions
)
{
    public static MethodBodyCompilation Create()
    {
    }
}

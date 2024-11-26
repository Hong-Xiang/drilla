using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Mathematics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class ParserContext(
    Dictionary<MethodBase, FunctionDeclaration> Funcs,
    Dictionary<Type, IShaderType> Types,
    Dictionary<MemberInfo, VariableDeclaration> Vars)
{
    private static Dictionary<MethodBase, FunctionDeclaration> GetRuntimeMethods(
        IReadOnlyDictionary<Type, StructureDeclaration> types
    )
    {
        var dmath = typeof(DMath);
        throw new NotSupportedException();
    }

    public static ParserContext Create()
    {
        var funcs = new Dictionary<MethodBase, FunctionDeclaration>();
        var types = new Dictionary<Type, IShaderType>();
        return new ParserContext(funcs, types, new Dictionary<MemberInfo, VariableDeclaration>());
    }
}

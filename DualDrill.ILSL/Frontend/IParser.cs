using DualDrill.CLSL.Language.IR.Declaration;
using Lokad.ILPack.IL;
using System.Reflection;
using System.Reflection.Emit;

namespace DualDrill.ILSL.Frontend;

public interface IParser
{
    ParserContext Context { get; }
    public FunctionDeclaration ParseMethod(MethodBase method, Dictionary<string, IDeclaration>? symbols = default);
}

public sealed record class ParserContext(
    Dictionary<MethodBase, FunctionDeclaration> FunctionDeclarations,
    Dictionary<Type, StructureDeclaration> StructDeclarations,
    Dictionary<MemberInfo, VariableDeclaration> VariableDeclarations)
{
    public static ParserContext Create() => new([], [], []);
}

public interface IMetadataParser
{
    public IEnumerable<MethodBase> ParseCallee(MethodBase method);
}

public sealed class ILPackMetadataParser : IMetadataParser
{
    public IEnumerable<MethodBase> ParseCallee(MethodBase method)
    {
        var instructions = method.GetInstructions();
        return instructions.Where(inst => inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
                    .Select(inst => (MethodBase)inst.Operand);
    }
}

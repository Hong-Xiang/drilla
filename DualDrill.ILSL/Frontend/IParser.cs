using DualDrill.ILSL.IR.Declaration;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public interface IParser
{
    ParserContext Context { get; }
    public FunctionDeclaration ParseMethod(MethodBase method);
}

public sealed record class ParserContext(
    Dictionary<MethodBase, FunctionDeclaration> FunctionDeclarations,
    Dictionary<Type, StructureDeclaration> StructDeclarations,
    Dictionary<MemberInfo, VariableDeclaration> VariableDeclarations)
{
    public static ParserContext Create() => new([], [], []);
}


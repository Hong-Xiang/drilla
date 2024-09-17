using DualDrill.ILSL.IR.Declaration;
using System.Collections.Concurrent;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public interface IParser
{
    ParserContext Context { get; }
    public FunctionDeclaration ParseMethod(MethodBase method);
}

public sealed record class ParserContext(ConcurrentDictionary<MethodBase, FunctionDeclaration> FunctionDeclarations)
{
    public static ParserContext Create() => new(new ConcurrentDictionary<MethodBase, FunctionDeclaration>());
}


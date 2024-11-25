using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Statement;

namespace DualDrill.ILSL.Frontend;

public interface IMethodParser
{
    public CompoundStatement ParseMethodBody(ImmutableDictionary<string, IDeclaration> env, MethodBase method);
}

public static class MethodParserExtension
{
    public static CompoundStatement ParseMethodBody(this IMethodParser parser, ImmutableDictionary<string, IDeclaration> env, Action f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
    public static CompoundStatement ParseMethodBody<T>(this IMethodParser parser, ImmutableDictionary<string, IDeclaration> env, Func<T> f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
    public static CompoundStatement ParseMethodBody<TA, TB>(this IMethodParser parser, ImmutableDictionary<string, IDeclaration> env, Func<TA, TB> f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
}

/// <summary>
/// A Method Parser always returns empty body, used for testing metadata parsers
/// </summary>
public sealed class EmptyBodyMethodParser()
{
}

﻿using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Types;
using ICSharpCode.Decompiler.CSharp.Syntax;

namespace DualDrill.ILSL.Frontend;

using CLSLParameterDeclaration = CLSL.Language.AbstractSyntaxTree.Declaration.ParameterDeclaration;

public interface IMethodParser
{
    public CompoundStatement ParseMethodBody(MethodParseContext env, MethodBase method);
}

public sealed record class MethodParseContext(
    ImmutableArray<CLSLParameterDeclaration> Parameters,
    Dictionary<string, VariableDeclaration> LocalVariables,
    ImmutableDictionary<MethodBase, FunctionDeclaration> Methods,
    ImmutableDictionary<Type, IShaderType> Types,
    ImmutableDictionary<Type, FunctionDeclaration> ZeroValueConstructors
)
{
    public static MethodParseContext Empty => new MethodParseContext(
        [],
        [],
        ImmutableDictionary<MethodBase, FunctionDeclaration>.Empty,
        ImmutableDictionary<Type, IShaderType>.Empty,
        ImmutableDictionary<Type, FunctionDeclaration>.Empty
    );

    public VariableDeclaration? this[string name] => LocalVariables[name];
    public CLSLParameterDeclaration GetParameter(int position) => Parameters[position];
}

public static class MethodParserExtension
{
    public static CompoundStatement ParseMethodBody(this IMethodParser parser, MethodParseContext env, Action f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
    public static CompoundStatement ParseMethodBody<T>(this IMethodParser parser, MethodParseContext env, Func<T> f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
    public static CompoundStatement ParseMethodBody<TA, TB>(this IMethodParser parser, MethodParseContext env, Func<TA, TB> f)
    {
        return parser.ParseMethodBody(env, f.Method);
    }
}

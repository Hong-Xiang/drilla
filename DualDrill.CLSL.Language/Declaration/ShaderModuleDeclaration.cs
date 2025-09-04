using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace DualDrill.CLSL.Language.Declaration;

public interface IShaderModuleDeclaration
{
    ImmutableArray<IDeclaration> Declarations { get; init; }
}

public sealed record class ShaderModuleDeclaration<TBody>(
    ImmutableArray<IDeclaration> Declarations,
    ImmutableDictionary<FunctionDeclaration, TBody> FunctionDefinitions)
    : IDeclaration
    , IShaderModuleDeclaration
    where TBody : IFunctionBody
{
    public string Name => nameof(ShaderModuleDeclaration<TBody>);
    public ImmutableHashSet<IShaderAttribute> Attributes => [];

    public IEnumerable<FunctionDeclaration> DefinedFunctions => FunctionDefinitions.Keys;

    public TBody GetBody(FunctionDeclaration func)
    {
        return FunctionDefinitions[func];
    }

    public bool TryGetBody(FunctionDeclaration func, [NotNullWhen(true)] out TBody body)
    {
        return FunctionDefinitions.TryGetValue(func, out body);
    }

    public static ShaderModuleDeclaration<TBody> Empty
        => new([], ImmutableDictionary<FunctionDeclaration, TBody>.Empty);

    public ShaderModuleDeclaration<TResult> MapBody<TResult>(
        Func<ShaderModuleDeclaration<TBody>, FunctionDeclaration, TBody, TResult> f)
        where TResult : IFunctionBody
    {
        var result = FunctionDefinitions.Select(kv => KeyValuePair.Create(kv.Key, f(this, kv.Key, kv.Value)))
                                        .ToImmutableDictionary();
        return new ShaderModuleDeclaration<TResult>(Declarations, result);
    }


    public TResult Accept<TResult>(IDeclarationVisitor<TBody, TResult> visitor)
        => visitor.VisitModule(this);

    public T Evaluate<T>(IDeclarationSemantic<T> semantic)
    {
        if (this is ShaderModuleDeclaration<FunctionBody4> m)
        {
            return semantic.VisitModule(m);
        }
        throw new NotSupportedException();
    }


    public ShaderModuleDeclaration<FunctionBody4> RunPass(IShaderModuleSimplePass pass)
    {
        var decls = Declarations.Select(d => d.AcceptVisitor(pass))
                                .OfType<IDeclaration>()
                                .ToList();
        var funcs = decls.OfType<FunctionDeclaration>().ToFrozenSet();
        Dictionary<FunctionDeclaration, FunctionBody4> funcDefs = [];
        foreach (var kv in FunctionDefinitions)
        {
            if (kv.Value is FunctionBody4 body)
            {
                var fr = pass.VisitFunctionBody(body);
                if (funcs.Contains(fr.Declaration))
                {
                    funcDefs.Add(fr.Declaration, fr);
                }
            }
        }
        var moduleVariables = funcDefs.Values
                                      .SelectMany(d => d.UsedValues())
                                      .OfType<VariablePointerValue>()
                                      .Where(v => v.Declaration.AddressSpace is not FunctionAddressSpace)
                                      .Select(v => v.Declaration);


        // TODO: collect used non-function scope declrations
        return new ShaderModuleDeclaration<FunctionBody4>(
            [.. decls.Concat(moduleVariables).Distinct()],
            funcDefs.ToImmutableDictionary()
        );
    }

}

public static class ShaderModule
{
    public static IStatementSemantic<IShaderValue, IExpressionTree<IShaderValue>, IShaderValue, FunctionDeclaration,
                                    ShaderStmt> StatementFactory
    { get; } = Statement.Statement.Factory<IShaderValue, IExpressionTree<IShaderValue>, IShaderValue, FunctionDeclaration>();
}

using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
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
    where TBody : IFunctionBodyData
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

    public ShaderModuleDeclaration<TResult> MapBody<TResult>(Func<ShaderModuleDeclaration<TBody>, FunctionDeclaration, TBody, TResult> f)
        where TResult : IFunctionBodyData
    {
        var result = FunctionDefinitions.Select(kv => KeyValuePair.Create(kv.Key, f(this, kv.Key, kv.Value))).ToImmutableDictionary();
        return new ShaderModuleDeclaration<TResult>(Declarations, result);
    }



    public TResult Accept<TResult>(IDeclarationVisitor<TBody, TResult> visitor)
        => visitor.VisitModule(this);
}

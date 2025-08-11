using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Types;
using ValueDeclaration = DualDrill.CLSL.Language.Symbol.ValueDeclaration;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class ShaderRegionBody(
    ImmutableArray<ValueDeclaration> Parameters,
    Seq<IStatement<ShaderValue, ExpressionTree<ShaderValue>, ILoadStoreTargetSymbol, FunctionDeclaration>,
            ITerminator<RegionExpression<Label, ShaderRegionBody>, ExpressionTree<ShaderValue>>>
        Body
)
{
}

public interface IParameterBinding
{
    ShaderValue Value { get; }
    IShaderType Type { get; }
}

public sealed record class ParameterValueBinding(
    ShaderValue Value,
    ParameterDeclaration Parameter
) : IParameterBinding
{
    public IShaderType Type => Parameter.Type;
}

public sealed record class ParameterPointerBinding(
    ShaderValue Value,
    ParameterDeclaration Parameter
) : IParameterBinding
{
    public IShaderType Type => Parameter.Type.GetPtrType();
}

public sealed class FunctionBody4(
    ImmutableArray<IParameterBinding> Parameters,
    ImmutableArray<ValueDeclaration> LocalVariables,
    RegionTree<RegionExpression<Label, ShaderRegionBody>, ShaderRegionBody> Data
)
{
}
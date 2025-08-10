using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;


public sealed class ShaderRegionBody(
    ImmutableArray<RegionParameter> Parameters,
    Seq<IStatement<ShaderValue, ExpressionTree<ShaderValue>, ILoadStoreTargetSymbol, FunctionDeclaration>,
        ITerminator<RegionExpression<Label, ShaderRegionBody>, ExpressionTree<ShaderValue>>>
    Body
)
{
}

public sealed class ValueIRFunctionBody(
    RegionTree<RegionExpression<Label, ShaderRegionBody>, ShaderRegionBody> Data
)
{
}

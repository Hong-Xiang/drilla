using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class UnstructuredStackFunctionBody(
    Label Entry,
    IReadOnlyDictionary<Label, Seq<
        IStatement<
            Unit,
            IExpression<Unit>,
            IShaderSymbol<IMemoryLocationDeclaration>,
            IShaderSymbol<FunctionDeclaration>>,
        ITerminator<Label, Unit>>> BasicBlocks)
{
}

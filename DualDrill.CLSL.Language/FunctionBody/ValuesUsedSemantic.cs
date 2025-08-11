using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using System.Reactive;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class ValuesUsedSemantic
    : ISeqSemantic<Unit, ShaderStmt, ITerminator<RegionJump, ShaderValue>,
        IEnumerable<ShaderValue>,
        IEnumerable<ShaderValue>
      >
    , ITerminatorSemantic<Unit, RegionJump, ShaderValue, IEnumerable<ShaderValue>>
    , IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, IEnumerable<ShaderValue>>
    , IExpressionTreeFoldSemantic<Unit, ShaderValue, IEnumerable<ShaderValue>>
{
    public IEnumerable<ShaderValue> AddressOfChain(Unit ctx, IAccessChainOperation operation, IEnumerable<ShaderValue> e)
        => [.. e];

    public IEnumerable<ShaderValue> AddressOfIndex(Unit ctx, IAccessChainOperation operation, IEnumerable<ShaderValue> e, IEnumerable<ShaderValue> index)
        => [.. e, .. index];

    public IEnumerable<ShaderValue> AddressOfSymbol(Unit ctx, IAddressOfSymbolOperation operation)
        => [];

    public IEnumerable<ShaderValue> Br(Unit context, RegionJump target)
        => [.. target.Arguments];

    public IEnumerable<ShaderValue> BrIf(Unit context, ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        => [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];

    public IEnumerable<ShaderValue> Call(Unit context, ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
        => [result, .. arguments.SelectMany(e => e.Fold(this, context))];

    public IEnumerable<ShaderValue> Dup(Unit context, ShaderValue result, ShaderValue source)
        => [result, source];

    public IEnumerable<ShaderValue> Get(Unit context, ShaderValue result, ShaderValue source)
        => [result, source];

    public IEnumerable<ShaderValue> Let(Unit context, ShaderValue result, ShaderExpr expr)
        => [result, .. expr.Fold(this, context)];

    public IEnumerable<ShaderValue> Literal<TLiteral>(Unit ctx, TLiteral literal) where TLiteral : ILiteral
        => [];

    public IEnumerable<ShaderValue> Mov(Unit context, ShaderValue target, ShaderValue source)
        => [target, source];

    public IEnumerable<ShaderValue> Nested(Unit context, ShaderStmt head, IEnumerable<ShaderValue> next)
        => [.. head.Evaluate(this, context), .. next];

    public IEnumerable<ShaderValue> Nop(Unit context)
        => [];

    public IEnumerable<ShaderValue> Operation1(Unit ctx, IUnaryExpressionOperation operation, IEnumerable<ShaderValue> e)
        => [.. e];

    public IEnumerable<ShaderValue> Operation2(Unit ctx, IBinaryExpressionOperation operation, IEnumerable<ShaderValue> l, IEnumerable<ShaderValue> r)
        => [.. l, .. r];

    public IEnumerable<ShaderValue> Pop(Unit context, ShaderValue target)
        => [target];
    public IEnumerable<ShaderValue> ReturnExpr(Unit context, ShaderValue expr)
        => [expr];

    public IEnumerable<ShaderValue> ReturnVoid(Unit context)
        => [];

    public IEnumerable<ShaderValue> Set(Unit context, ShaderValue target, ShaderValue source)
        => [target, source];

    public IEnumerable<ShaderValue> SetVecSwizzle(Unit context, IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
        => [target, value];
    public IEnumerable<ShaderValue> Single(Unit context, ITerminator<RegionJump, ShaderValue> value)
        => value.Evaluate(this, context);

    public IEnumerable<ShaderValue> Value(Unit context, ShaderValue value)
        => [value];
}

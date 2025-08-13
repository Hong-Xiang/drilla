using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class ValuesUsedSemantic
    : ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>,
        IEnumerable<ShaderValue>,
        IEnumerable<ShaderValue>
      >
    , ITerminatorSemantic<RegionJump, ShaderValue, IEnumerable<ShaderValue>>
    , IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, IEnumerable<ShaderValue>>
    , IExpressionTreeFoldSemantic<ShaderValue, IEnumerable<ShaderValue>>
{
    public IEnumerable<ShaderValue> AddressOfChain(IAccessChainOperation operation, IEnumerable<ShaderValue> e)
        => [.. e];

    public IEnumerable<ShaderValue> AddressOfIndex(IAccessChainOperation operation, IEnumerable<ShaderValue> e, IEnumerable<ShaderValue> index)
        => [.. e, .. index];

    public IEnumerable<ShaderValue> AddressOfSymbol(IAddressOfSymbolOperation operation)
        => [];

    public IEnumerable<ShaderValue> Br(RegionJump target)
        => [.. target.Arguments];

    public IEnumerable<ShaderValue> BrIf(ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        => [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];

    public IEnumerable<ShaderValue> Call(ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
        => [result, .. arguments.SelectMany(e => e.Fold(this))];

    public IEnumerable<ShaderValue> Dup(ShaderValue result, ShaderValue source)
        => [result, source];

    public IEnumerable<ShaderValue> Get(ShaderValue result, ShaderValue source)
        => [result, source];

    public IEnumerable<ShaderValue> Let(ShaderValue result, ShaderExpr expr)
        => [result, .. expr.Fold(this)];

    public IEnumerable<ShaderValue> Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral
        => [];

    public IEnumerable<ShaderValue> Mov(ShaderValue target, ShaderValue source)
        => [target, source];

    public IEnumerable<ShaderValue> Nested(ShaderStmt head, IEnumerable<ShaderValue> next)
        => [.. head.Evaluate(this), .. next];

    public IEnumerable<ShaderValue> Nop()
        => [];

    public IEnumerable<ShaderValue> Operation1(IUnaryExpressionOperation operation, IEnumerable<ShaderValue> e)
        => [.. e];

    public IEnumerable<ShaderValue> Operation2(IBinaryExpressionOperation operation, IEnumerable<ShaderValue> l, IEnumerable<ShaderValue> r)
        => [.. l, .. r];

    public IEnumerable<ShaderValue> Pop(ShaderValue target)
        => [target];
    public IEnumerable<ShaderValue> ReturnExpr(ShaderValue expr)
        => [expr];

    public IEnumerable<ShaderValue> ReturnVoid()
        => [];

    public IEnumerable<ShaderValue> Set(ShaderValue target, ShaderValue source)
        => [target, source];

    public IEnumerable<ShaderValue> SetVecSwizzle(IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
        => [target, value];
    public IEnumerable<ShaderValue> Single(ITerminator<RegionJump, ShaderValue> value)
        => value.Evaluate(this);

    public IEnumerable<ShaderValue> Value(ShaderValue value)
        => [value];
}

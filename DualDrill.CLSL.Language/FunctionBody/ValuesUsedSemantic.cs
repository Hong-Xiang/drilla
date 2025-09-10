using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

sealed class ValuesUsedSemantic
    : ISeqSemantic<ShaderStmt, ITerminator<RegionJump, IShaderValue>,
        IEnumerable<IShaderValue>,
        IEnumerable<IShaderValue>
      >
    , ITerminatorSemantic<RegionJump, IShaderValue, IEnumerable<IShaderValue>>
    , IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, IEnumerable<IShaderValue>>
    , IExpressionTreeFoldSemantic<IShaderValue, IEnumerable<IShaderValue>>
{
    public IEnumerable<IShaderValue> AddressOfChain(IAccessChainOperation operation, IEnumerable<IShaderValue> e)
        => [.. e];

    public IEnumerable<IShaderValue> AddressOfIndex(IAccessChainOperation operation, IEnumerable<IShaderValue> e, IEnumerable<IShaderValue> index)
        => [.. e, .. index];

    public IEnumerable<IShaderValue> Br(RegionJump target)
        => [.. target.Arguments];

    public IEnumerable<IShaderValue> BrIf(IShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
        => [condition, .. trueTarget.Arguments, .. falseTarget.Arguments];

    public IEnumerable<IShaderValue> Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
        => [result, .. arguments];

    public IEnumerable<IShaderValue> Dup(IShaderValue result, IShaderValue source)
        => [result, source];

    public IEnumerable<IShaderValue> Get(IShaderValue result, IShaderValue source)
        => [result, source];

    public IEnumerable<IShaderValue> Let(IShaderValue result, ShaderExpr expr)
        => [result, .. expr.Fold(this)];

    public IEnumerable<IShaderValue> Literal<TLiteral>(TLiteral literal) where TLiteral : ILiteral
        => [];

    public IEnumerable<IShaderValue> Mov(IShaderValue target, IShaderValue source)
        => [target, source];

    public IEnumerable<IShaderValue> Nested(ShaderStmt head, IEnumerable<IShaderValue> next)
        => [.. head.Evaluate(this), .. next];

    public IEnumerable<IShaderValue> Nop()
        => [];

    public IEnumerable<IShaderValue> Operation1(IUnaryExpressionOperation operation, IEnumerable<IShaderValue> e)
        => [.. e];

    public IEnumerable<IShaderValue> Operation2(IBinaryExpressionOperation operation, IEnumerable<IShaderValue> l, IEnumerable<IShaderValue> r)
        => [.. l, .. r];

    public IEnumerable<IShaderValue> Pop(IShaderValue target)
        => [target];
    public IEnumerable<IShaderValue> ReturnExpr(IShaderValue expr)
        => [expr];

    public IEnumerable<IShaderValue> ReturnVoid()
        => [];

    public IEnumerable<IShaderValue> Set(IShaderValue target, IShaderValue source)
        => [target, source];

    public IEnumerable<IShaderValue> SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
        => [target, value];
    public IEnumerable<IShaderValue> Single(ITerminator<RegionJump, IShaderValue> value)
        => value.Evaluate(this);

    public IEnumerable<IShaderValue> Value(IShaderValue value)
        => [value];

    public IEnumerable<IShaderValue> VectorCompositeConstruction(VectorCompositeConstructionOperation operation, IEnumerable<IEnumerable<IShaderValue>> arguments)
        => arguments.SelectMany(a => a);
}

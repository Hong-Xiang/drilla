using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Transform;

sealed class OperationFunctionNotMatchException : Exception
{
    public OperationFunctionNotMatchException(FunctionDeclaration function, IOperation operation)
        : base($"function {function} does not match operation {operation}")
    {
    }
}


public sealed class FunctionToOperationPass
    : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl)
        => decl;

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        return new FunctionBody4(
            body.Declaration,
            body.Body.Select(
                l => l,
                rbody => rbody.MapStatement(TransformStatement)
            )
        );
    }

    sealed record class StatementTransformSemantic(ShaderStmt Stmt)
        : IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, IEnumerable<ShaderStmt>>
    {
        private IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, ShaderStmt> S { get; }
            = Statement.Statement.Factory<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration>();
        private IExpressionSemantic<IExpressionTree<IShaderValue>, IExpression<ShaderExpr>> E { get; } =
            Language.Expression.Expression.Factory<IExpressionTree<IShaderValue>>();


        public IEnumerable<ShaderStmt> Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
        {
            if (f.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } opAttr)
            {
                switch (opAttr.Operation)
                {
                    case IBinaryExpressionOperation be:
                        {
                            var r = arguments[1];
                            var l = arguments[0];
                            if (!l.Type.Equals(be.LeftType) || !r.Type.Equals(be.RightType))
                            {
                                throw new OperationFunctionNotMatchException(f, be);
                            }
                            return [S.Let(result, E.Operation2(be, ExpressionTree.Value(l), ExpressionTree.Value(r)).AsNode())];
                        }
                    case IBinaryStatementOperation bs:
                        {
                            var r = arguments[1];
                            var l = arguments[0];
                            if (!l.Type.Equals(bs.LeftType) || !r.Type.Equals(bs.RightType))
                            {
                                if (l.Type is IPtrType lp && bs.LeftType is IPtrType bp && lp.BaseType.Equals(bp.BaseType))
                                {
                                    // TODO: correct handling of address space equality
                                }
                                else
                                {
                                    throw new OperationFunctionNotMatchException(f, bs);
                                }
                            }

                            if (bs is IVectorComponentSetOperation vcs)
                            {
                                var p = ShaderValue.Create(vcs.ElementType.GetPtrType(FunctionAddressSpace.Instance));
                                return [
                                    S.Let(p, E.AddressOfChain(new AddressOfVecComponentOperation(vcs.VecType, vcs.Component), ExpressionTree.Value(l)).AsNode()),
                                    S.Set(p, r)
                                ];
                            }

                            if (bs is IVectorSwizzleSetOperation vss)
                            {
                                return [S.SetVecSwizzle(vss, l, r)];
                            }

                            throw new NotSupportedException($"binary statement {bs.Name}");
                        }
                    case IUnaryExpressionOperation ue:
                        {
                            var s = arguments[0];
                            if (!s.Type.Equals(ue.SourceType))
                            {
                                if (s.Type is IPtrType ps && ue.SourceType is IPtrType pu && ps.BaseType.Equals(pu.BaseType))
                                {
                                    // TODO: modify operation to support precise control on address space 
                                }
                                else
                                {
                                    throw new OperationFunctionNotMatchException(f, ue);
                                }
                            }

                            return [
                                S.Let(result, E.Operation1(ue, ExpressionTree.Value(s)).AsNode())
                            ];
                        }
                }
            }
            if (f.Attributes.OfType<VectorCompositeConstructorMethodAttribute>().SingleOrDefault() is { } vcc)
            {
                var op = (VectorCompositeConstructionOperation)vcc.GetOperation(f.Return.Type, f.Parameters.Select(p => p.Type));
                return [
                    S.Let(result, E.VectorCompositeConstruction(op, arguments.Select(ExpressionTree.Value)).AsNode())
                ];
            }
            return [Stmt];
        }

        public IEnumerable<ShaderStmt> Dup(IShaderValue result, IShaderValue source)
            => [Stmt];

        public IEnumerable<ShaderStmt> Get(IShaderValue result, IShaderValue source)
            => [Stmt];

        public IEnumerable<ShaderStmt> Let(IShaderValue result, ShaderExpr expr)
            => [Stmt];

        public IEnumerable<ShaderStmt> Mov(IShaderValue target, IShaderValue source)
            => [Stmt];

        public IEnumerable<ShaderStmt> Nop()
            => [Stmt];

        public IEnumerable<ShaderStmt> Pop(IShaderValue target)
            => [Stmt];

        public IEnumerable<ShaderStmt> Set(IShaderValue target, IShaderValue source)
            => [Stmt];

        public IEnumerable<ShaderStmt> SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
            => [Stmt];
    }

    IEnumerable<ShaderStmt> TransformStatement(ShaderStmt stmt)
        => stmt.Evaluate(new StatementTransformSemantic(stmt));

    public IDeclaration? VisitMember(MemberDeclaration decl)
        => decl;

    public IDeclaration? VisitParameter(ParameterDeclaration decl)
        => decl;

    public IDeclaration? VisitStructure(StructureDeclaration decl)
        => decl;

    public IDeclaration? VisitValue(ValueDeclaration decl)
        => decl;

    public IDeclaration? VisitVariable(VariableDeclaration decl)
        => decl;
}

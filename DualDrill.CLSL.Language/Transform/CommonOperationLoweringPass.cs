using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Transform;

public sealed class CommonOperationLoweringPass
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
        : IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, ShaderStmt>
    {
        private IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, ShaderStmt> S { get; }
            = Statement.Statement.Factory<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration>();
        private IExpressionSemantic<IExpressionTree<IShaderValue>, IExpression<ShaderExpr>> E { get; } =
            Language.Expression.Expression.Factory<IExpressionTree<IShaderValue>>();

        public ShaderStmt Call(IShaderValue result, FunctionDeclaration f, IReadOnlyList<IShaderValue> arguments)
            => Stmt;

        public ShaderStmt Dup(IShaderValue result, IShaderValue source)
            => Stmt;

        public ShaderStmt Get(IShaderValue result, IShaderValue source)
            => Stmt;

        public ShaderStmt Let(IShaderValue result, ShaderExpr expr)
        {
            throw new NotImplementedException();
        }

        public ShaderStmt Mov(IShaderValue target, IShaderValue source)
            => Stmt;

        public ShaderStmt Nop()
            => Stmt;

        public ShaderStmt Pop(IShaderValue target)
            => Stmt;

        public ShaderStmt Set(IShaderValue target, IShaderValue source)
            => Stmt;

        public ShaderStmt SetVecSwizzle(IVectorSwizzleSetOperation operation, IShaderValue target, IShaderValue value)
            => Stmt;
    }


    IEnumerable<ShaderStmt> TransformStatement(ShaderStmt stmt)
        => [stmt.Evaluate(new StatementTransformSemantic(stmt))];



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

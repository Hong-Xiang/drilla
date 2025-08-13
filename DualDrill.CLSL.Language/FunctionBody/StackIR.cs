using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.FunctionBody;

using StackInstruction = IStatement<Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration>;

public static class StackIR
{
    public static class Terminator
    {
        static ITerminatorSemantic<Label, Unit, ITerminator<Label, Unit>> Factory
            = Language.Terminator.Factory<Label, Unit>();

        public static ITerminator<Label, Unit> ReturnVoid()
            => Factory.ReturnVoid();

        public static ITerminator<Label, Unit> ReturnExpr()
            => Factory.ReturnExpr(default);

        public static ITerminator<Label, Unit> Br(Label label)
            => Factory.Br(label);

        public static ITerminator<Label, Unit> BrIf(Label t, Label f)
            => Factory.BrIf(default, t, f);
    }

    public static class Instruction
    {
        static IStatementSemantic<Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration, StackInstruction> Stmt
        { get; } = Statement.Statement
                                                       .Factory<Unit, IExpression<Unit>, IExpression<Unit>,
                                                           FunctionDeclaration>();


        static IExpressionSemantic<Unit, IExpression<Unit>> Expr = Expression.Expression.Factory<Unit>();
        static PointerOperationFactory Pointer = new();

        // Statement factory methods
        public static StackIRInstruction Nop()
            => new(Stmt.Nop());

        public static StackIRInstruction Let(IExpression<Unit> expr)
            => new(Stmt.Let(default, expr));

        public static StackIRInstruction GetParameter(ParameterDeclaration target)
            => new(Stmt.Get(default, Expr.AddressOfSymbol(Pointer.Parameter(target))));

        public static StackIRInstruction GetLocal(VariableDeclaration target)
            => new(Stmt.Get(default, Expr.AddressOfSymbol(Pointer.LocalVariable(target))));

        public static StackIRInstruction GetParameterAddress(ParameterDeclaration target)
            => new(Stmt.Let(default, Expr.AddressOfSymbol(Pointer.Parameter(target))));

        public static StackIRInstruction GetLocalAddress(VariableDeclaration target)
            => new(Stmt.Let(default, Expr.AddressOfSymbol(Pointer.LocalVariable(target))));


        public static StackIRInstruction SetVecComponent(IVectorComponentSetOperation operation)
            => new(Stmt.Set(Expr.AddressOfChain(Pointer.VecComponent(operation.VecType, operation.Component), default),
                default));

        public static StackIRInstruction SetVecSwizzle(IVectorSwizzleSetOperation operation)
            => new(Stmt.SetVecSwizzle(operation, default, default));

        public static StackIRInstruction SetParameter(ParameterDeclaration target)
            => new(Stmt.Set(Expr.AddressOfSymbol(Pointer.Parameter(target)), default));

        public static StackIRInstruction SetLocal(VariableDeclaration target)
            => new(Stmt.Set(Expr.AddressOfSymbol(Pointer.LocalVariable(target)), default));

        public static StackIRInstruction GetMember(MemberDeclaration target)
            => new(Stmt.Get(default, Expr.Operation1(Pointer.Member(target), default)));

        public static StackIRInstruction GetMemberAddress(MemberDeclaration target)
            => new(Stmt.Let(default, Expr.Operation1(Pointer.Member(target), default)));

        public static StackIRInstruction SetMember(MemberDeclaration target)
            => new(Stmt.Set(Expr.Operation1(Pointer.Member(target), default), default));


        public static StackIRInstruction Mov(ILoadStoreTargetSymbol target, ILoadStoreTargetSymbol source)
            => throw new NotImplementedException();

        public static StackIRInstruction Dup()
            => new(Stmt.Dup(default, default));

        public static StackIRInstruction Pop()
            => new(Stmt.Pop(default));

        public static StackIRInstruction Call(FunctionDeclaration function)
            => new(Stmt.Call(default, function, []));

        // Expression factory methods that return Let statements
        public static StackIRInstruction Literal<TLiteral>(TLiteral literal)
            where TLiteral : ILiteral
            => new(Stmt.Let(default, Expr.Literal(literal)));


        public static StackIRInstruction Expr1(IUnaryExpressionOperation operation)
            => new(Stmt.Let(default, Expr.Operation1(operation, default)));
    }
}
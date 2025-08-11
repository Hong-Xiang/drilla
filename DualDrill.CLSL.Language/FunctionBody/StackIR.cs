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
        static ITerminatorSemantic<Unit, Label, Unit, ITerminator<Label, Unit>> Factory
            = Language.Terminator.Factory<Label, Unit>();

        public static ITerminator<Label, Unit> ReturnVoid()
            => Factory.ReturnVoid(default);

        public static ITerminator<Label, Unit> ReturnExpr()
            => Factory.ReturnExpr(default, default);

        public static ITerminator<Label, Unit> Br(Label label)
            => Factory.Br(default, label);
        public static ITerminator<Label, Unit> BrIf(Label t, Label f)
            => Factory.BrIf(default, default, t, f);
    }

    public static class Instruction
    {
        static IStatementSemantic<Unit, Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration, StackInstruction> Stmt { get; } = Statement.Statement.Factory<Unit, IExpression<Unit>, IExpression<Unit>, FunctionDeclaration>();
        static IExpressionSemantic<Unit, Unit, IExpression<Unit>> Expr = Expression.Expression.Factory<Unit>();
        static PointerOperationFactory Pointer = new();

        // Statement factory methods
        public static StackIRInstruction Nop()
            => new(Stmt.Nop(default));

        public static StackIRInstruction Let(IExpression<Unit> expr)
            => new(Stmt.Let(default, default, expr));

        public static StackIRInstruction GetParameter(ParameterDeclaration target)
            => new(Stmt.Get(default, default, Expr.AddressOfSymbol(default, Pointer.Parameter(target))));
        public static StackIRInstruction GetLocal(VariableDeclaration target)
            => new(Stmt.Get(default, default, Expr.AddressOfSymbol(default, Pointer.LocalVariable(target))));
        public static StackIRInstruction GetParameterAddress(ParameterDeclaration target)
            => new(Stmt.Let(default, default, Expr.AddressOfSymbol(default, Pointer.Parameter(target))));
        public static StackIRInstruction GetLocalAddress(VariableDeclaration target)
            => new(Stmt.Let(default, default, Expr.AddressOfSymbol(default, Pointer.LocalVariable(target))));


        public static StackIRInstruction SetVecComponent(IVectorComponentSetOperation operation)
            => new(Stmt.Set(default, Expr.AddressOfChain(default, Pointer.VecComponent(operation.VecType, operation.Component), default), default));
        public static StackIRInstruction SetVecSwizzle(IVectorSwizzleSetOperation operation)
            => new(Stmt.SetVecSwizzle(default, operation, default, default));
        public static StackIRInstruction SetParameter(ParameterDeclaration target)
            => new(Stmt.Set(default, Expr.AddressOfSymbol(default, Pointer.Parameter(target)), default));
        public static StackIRInstruction SetLocal(VariableDeclaration target)
            => new(Stmt.Set(default, Expr.AddressOfSymbol(default, Pointer.LocalVariable(target)), default));

        public static StackIRInstruction GetMember(MemberDeclaration target)
            => new(Stmt.Get(default, default, Expr.Operation1(default, Pointer.Member(target), default)));
        public static StackIRInstruction GetMemberAddress(MemberDeclaration target)
            => new(Stmt.Let(default, default, Expr.Operation1(default, Pointer.Member(target), default)));
        public static StackIRInstruction SetMember(MemberDeclaration target)
            => new(Stmt.Set(default, Expr.Operation1(default, Pointer.Member(target), default), default));




        public static StackIRInstruction Mov(ILoadStoreTargetSymbol target, ILoadStoreTargetSymbol source)
            => throw new NotImplementedException();

        public static StackIRInstruction Dup()
            => new(Stmt.Dup(default, default, default));

        public static StackIRInstruction Pop()
            => new(Stmt.Pop(default, default));

        public static StackIRInstruction Call(FunctionDeclaration function)
            => new(Stmt.Call(default, default, function, []));

        // Expression factory methods that return Let statements
        public static StackIRInstruction Literal<TLiteral>(TLiteral literal)
            where TLiteral : ILiteral
            => new(Stmt.Let(default, default, Expr.Literal(default, literal)));

        public static StackIRInstruction Expr2(IBinaryExpressionOperation operation)
            => new(Stmt.Let(default, default, Expr.Operation2(default, operation, default, default)));

        public static StackIRInstruction Expr1(IUnaryExpressionOperation operation)
            => new(Stmt.Let(default, default, Expr.Operation1(default, operation, default)));
    }
}

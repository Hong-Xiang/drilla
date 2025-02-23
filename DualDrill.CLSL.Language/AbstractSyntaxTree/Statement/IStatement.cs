using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

[JsonDerivedType(typeof(ReturnStatement), nameof(ReturnStatement))]
[JsonDerivedType(typeof(VariableOrValueStatement), nameof(VariableOrValueStatement))]
[JsonDerivedType(typeof(CompoundStatement), nameof(CompoundStatement))]
[JsonDerivedType(typeof(IfStatement), nameof(IfStatement))]
[JsonDerivedType(typeof(SimpleAssignmentStatement), nameof(SimpleAssignmentStatement))]
[JsonDerivedType(typeof(PhonyAssignmentStatement), nameof(PhonyAssignmentStatement))]
[JsonDerivedType(typeof(WhileStatement), nameof(WhileStatement))]
[JsonDerivedType(typeof(BreakStatement), nameof(BreakStatement))]
[JsonDerivedType(typeof(ContinueStatement), nameof(ContinueStatement))]
[JsonDerivedType(typeof(ForStatement), nameof(ForStatement))]
[JsonDerivedType(typeof(IncrementStatement), nameof(IncrementStatement))]
[JsonDerivedType(typeof(DecrementStatement), nameof(DecrementStatement))]
[JsonDerivedType(typeof(SwitchStatement), nameof(SwitchStatement))]
[JsonDerivedType(typeof(LoopStatement), nameof(LoopStatement))]
public interface IStatement
    : IShaderAstNode
{
    T Accept<T>(IStatementVisitor<T> visitor);
}

public interface IStackStatement : IStructuredControlFlowElement, IBasicBlockElement
{
    IEnumerable<IStackInstruction> ToInstructions();
}

public interface ICommonStatement : IStatement, IStackStatement
{
}

public interface IStatementVisitor<T>
{
    T VisitReturn(ReturnStatement stmt);
    T VisitVariableOrValue(VariableOrValueStatement stmt);
    T VisitCompound(CompoundStatement stmt);
    T VisitIf(IfStatement stmt);
    T VisitWhile(WhileStatement stmt);
    T VisitBreak(BreakStatement stmt);
    T VisitFor(ForStatement stmt);
    T VisitSimpleAssignment(SimpleAssignmentStatement stmt);
    T VisitPhonyAssignment(PhonyAssignmentStatement stmt);
    T VisitIncrement(IncrementStatement stmt);
    T VisitDecrement(DecrementStatement stmt);

    T VisitLoop(LoopStatement stmt);
    T VisitSwitch(SwitchStatement stmt);
    T VisitContinue(ContinueStatement stmt);

    T AppendSemicolon(T t);

    T VisitVectorSwizzleSet<TRank, TElement, TPattern>(VectorSwizzleSetStatement<TRank, TElement, TPattern> stmt)
        where TRank : IRank<TRank>
        where TPattern : Swizzle.ISizedPattern<TRank, TPattern>
        where TElement : IScalarType<TElement>;

    T VisitVectorComponentSet<TRank, TElement, TComponent>(VectorComponentSetStatement<TRank, TElement, TComponent> stmt)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>;

}

public static class StatementExtension
{
    public static T AcceptVisitor<T>(this IStatement stmt, IStatementVisitor<T> visitor)
    {
        return stmt switch
        {
            ReturnStatement s => visitor.AppendSemicolon(visitor.VisitReturn(s)),
            VariableOrValueStatement s => visitor.AppendSemicolon(visitor.VisitVariableOrValue(s)),
            CompoundStatement s => visitor.VisitCompound(s),
            IfStatement s => visitor.VisitIf(s),
            WhileStatement s => visitor.VisitWhile(s),
            BreakStatement s => visitor.AppendSemicolon(visitor.VisitBreak(s)),
            ForStatement s => visitor.VisitFor(s),
            SimpleAssignmentStatement s => visitor.AppendSemicolon(visitor.VisitSimpleAssignment(s)),
            PhonyAssignmentStatement s => visitor.AppendSemicolon(visitor.VisitPhonyAssignment(s)),
            IncrementStatement s => visitor.AppendSemicolon(visitor.VisitIncrement(s)),
            DecrementStatement s => visitor.AppendSemicolon(visitor.VisitDecrement(s)),
            LoopStatement s => visitor.VisitLoop(s),
            SwitchStatement s => visitor.VisitSwitch(s),
            ContinueStatement s => visitor.VisitContinue(s),
            _ => throw new NotSupportedException($"visit {nameof(IStatement)} does not support {stmt}")
        };
    }
}
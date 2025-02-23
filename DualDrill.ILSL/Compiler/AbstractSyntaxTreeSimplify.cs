using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Compiler;

internal sealed class AbstractSyntaxTreeSimplify
    : IStatementVisitor<IStatement>
{
    public IStatement AppendSemicolon(IStatement t)
    {
        return t;
    }

    public IStatement
        VisitVectorSwizzleSet<TRank, TElement, TPattern>(VectorSwizzleSetStatement<TRank, TElement, TPattern> stmt)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>
        where TPattern : Swizzle.ISizedPattern<TRank, TPattern>
        => stmt;

    public IStatement
        VisitVectorComponentSet<TRank, TElement, TComponent>(
            VectorComponentSetStatement<TRank, TElement, TComponent> stmt) where TRank : IRank<TRank>
                                                                           where TElement : IScalarType<TElement>
                                                                           where TComponent : Swizzle.ISizedComponent<
                                                                               TRank, TComponent>
        => stmt;

    public IStatement VisitBreak(BreakStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitCompound(CompoundStatement stmt)
    {
        var results = stmt.Statements.Select(s => s.Accept(this))
                          .ToImmutableArray();
        List<IStatement> statements = [];
        foreach (var r in results)
        {
            switch (r)
            {
                case CompoundStatement cs:
                    if (cs.Statements.Length > 0)
                    {
                        statements.AddRange(cs.Statements);
                    }

                    break;
                default:
                    statements.Add(r);
                    break;
            }
        }

        return new CompoundStatement([.. statements]);
    }

    public IStatement VisitContinue(ContinueStatement stmt)
    {
        return stmt;
    }


    public IStatement VisitDecrement(DecrementStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitFor(ForStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitIf(IfStatement stmt)
    {
        return new IfStatement(
            stmt.Expr,
            (CompoundStatement)stmt.TrueBody.Accept(this),
            (CompoundStatement)stmt.FalseBody.Accept(this),
            stmt.Attributes
        );
    }

    public IStatement VisitIncrement(IncrementStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitLoop(LoopStatement stmt)
    {
        return stmt with
        {
            Body = (CompoundStatement)stmt.Body.Accept(this),
        };
    }

    public IStatement VisitPhonyAssignment(PhonyAssignmentStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitReturn(ReturnStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitSimpleAssignment(SimpleAssignmentStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitSwitch(SwitchStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitVariableOrValue(VariableOrValueStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitWhile(WhileStatement stmt)
    {
        return stmt;
    }
}
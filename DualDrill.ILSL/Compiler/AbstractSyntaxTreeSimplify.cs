using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Compiler;

internal sealed class AbstractSyntaxTreeSimplify
    : IStatementVisitor<IStatement>
{
    public IStatement AppendSemicolon(IStatement t)
    {
        return t;
    }

    public IStatement VisitBreak(BreakStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitCompound(CompoundStatement stmt)
    {
        var results = stmt.Statements.Select(s => s.AcceptVisitor(this))
                          .ToImmutableArray();
        List<IStatement> statements = [];
        foreach (var r in results)
        {
            switch (r)
            {
                case CompoundStatement cs:
                    statements.AddRange(cs.Statements);
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
        return stmt;
    }

    public IStatement VisitIncrement(IncrementStatement stmt)
    {
        return stmt;
    }

    public IStatement VisitLoop(LoopStatement stmt)
    {
        return stmt;
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

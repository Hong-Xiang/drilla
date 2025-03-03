using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Compiler;

internal sealed class AbstractSyntaxTreeSimplify
    : IStatementVisitor<IStatement>
    , IExpressionVisitor<IExpression>
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
        var tb = (CompoundStatement)stmt.TrueBody.Accept(this);
        var fb = (CompoundStatement)stmt.FalseBody.Accept(this);
        if (stmt.Expr is IUnaryExpression
            {
                Operation: LogicalNotOperation,
                Source: var expr
            })
        {
            return new IfStatement(
                expr,
                fb,
                tb,
                stmt.Attributes
            );
        }
        else
        {
            return new IfStatement(
                stmt.Expr,
                tb,
                fb,
                stmt.Attributes
            );
        }
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

    public IExpression VisitLiteralValueExpression(LiteralValueExpression expr)
        => expr;

    public IExpression VisitVariableIdentifierExpression(VariableIdentifierExpression expr)
        => expr;

    public IExpression VisitFunctionCallExpression(FunctionCallExpression expr)
        => expr;


    public IExpression VisitIndirectionExpression(IndirectionExpression expr)
        => expr.Expr switch
        {
            AddressOfExpression { Base: var e } => e,
            _ => expr
        };


    public IExpression VisitAddressOfExpression(AddressOfExpression expr)
        => expr.Base switch
        {
            IndirectionExpression { Expr: var e } => e,
            _ => expr
        };


    public IExpression VisitBinaryExpression<TOperation>(BinaryOperationExpression<TOperation> expr)
        where TOperation : IBinaryExpressionOperation<TOperation>
        => expr;


    public IExpression
        VisitUnaryExpression<TOperation, TSourceType, TResultType, TOp>(UnaryOperationExpression<TOperation> expr)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>
        => expr;


    public IExpression VisitConversionExpression<TTarget>(IUnaryExpression expr)
        where TTarget : ISingletonShaderType<TTarget>
        => expr;


    public IExpression VisitVectorSwizzleGetExpression<TPattern, TElement>(IUnaryExpression expr)
        where TPattern : Swizzle.IPattern<TPattern> where TElement : IScalarType<TElement>
        => expr;


    public IExpression VisitVectorComponentGetExpression<TRank, TVector, TComponent>(IUnaryExpression expr)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
        => expr;


    public IExpression VisitFormalParameterExpression(FormalParameterExpression expr)
        => expr;


    public IExpression VisitNamedComponentExpression(NamedComponentExpression expr)
        => expr;
}
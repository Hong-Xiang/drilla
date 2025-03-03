using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.CLSL.Compiler;

using IScfElement = IStructuredControlFlowElement;
using ScfElements = StructuredControlFlowElementSequence;
using IScfLabelRegion = ILabeledStructuredControlFlowRegion;

public sealed class StructuredInstructionToAbstractSyntaxTreeBuilder
    : IStructuredStackInstructionVisitor<Unit>
{
    public StructuredInstructionToAbstractSyntaxTreeBuilder(
        ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> shaderModule,
        FunctionDeclaration function)
    {
        ShaderModule = shaderModule;
        Function = function;
        var functionBody = ShaderModule.GetBody(function);
        RootRegion = functionBody.Root;
        // TODO: fix local variable contains module variable problem
        LocalVariables = functionBody.FunctionBodyDataLocalVariables
                                     .Where(v => v.DeclarationScope == DeclarationScope.Function)
                                     .Distinct().ToFrozenSet();
    }

    public ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> ShaderModule { get; }
    public FunctionDeclaration Function { get; }
    public IStructuredControlFlowRegion RootRegion { get; }

    private bool SupportNestedBreak = true;

    Stack<IExpression> Expressions = [];
    List<IStatement> Statements = [];

    readonly FrozenSet<VariableDeclaration> LocalVariables;
    readonly Stack<(Label, IScfLabelRegion)> LabelTargetsContext = [];

    VariableDeclaration BrDepth = new(DeclarationScope.Function, "br_depth",
        ShaderType.I32,
        []
    );

    FrozenSet<Label> UsedBrLabels = [];

    bool UsedNestedBreak = false;
    int MaxNestedBreakDepth = 0;

    public CompoundStatement Build()
    {
        UsedNestedBreak = false;

        UsedBrLabels = GetUsedBrLabels(RootRegion).ToFrozenSet();
        ControlFlowRegion(RootRegion);
        if (UsedNestedBreak)
        {
            var varsDeclare = LocalVariables.Append(BrDepth)
                                            .Select(v => new VariableOrValueStatement(v));
            return new CompoundStatement([
                .. varsDeclare,
                SyntaxFactory.AssignStatement(SyntaxFactory.VarIdentifier(BrDepth), SyntaxFactory.Literal(-1)),
                .. Statements
            ]);
        }
        else
        {
            var varsDeclare = LocalVariables
                .Select(v => new VariableOrValueStatement(v));
            return new CompoundStatement([
                .. varsDeclare,
                .. Statements
            ]);
        }
    }

    static IEnumerable<Label> GetUsedBrLabels(ScfElements elements)
    {
        foreach (var e in elements.Elements)
        {
            switch (e)
            {
                case BrInstruction br:
                    yield return br.Target;
                    break;
                case BrIfInstruction brIf:
                    yield return brIf.Target;
                    break;
                case IStructuredControlFlowRegion region:
                    foreach (var l in GetUsedBrLabels(region))
                    {
                        yield return l;
                    }

                    break;
                default:
                    break;
            }
        }
    }

    static IEnumerable<Label> GetUsedBrLabels(IStructuredControlFlowRegion region)
    {
        return region switch
        {
            Block b => GetUsedBrLabels(b.Body),
            Loop l => GetUsedBrLabels(l.Body),
            IfThenElse i => GetUsedBrLabels(i.TrueBody).Concat(GetUsedBrLabels(i.FalseBody)),
            _ => throw new NotSupportedException()
        };
    }

    public void ControlFlowRegion(IStructuredControlFlowRegion region)
    {
        switch (region)
        {
            case Block block:
            {
                Block(block);
                return;
            }
            case Loop loop:
            {
                VisitLoop(loop);
                return;
            }
            case IfThenElse ifThenElse:
                VisitIfThenElse(ifThenElse);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    public void ControlFlowElement(IScfElement element)
    {
        switch (element)
        {
            case IInstruction instruction:
                instruction.Accept<StructuredInstructionToAbstractSyntaxTreeBuilder, Unit>(this);
                return;
            case IStructuredControlFlowRegion region:
                ControlFlowRegion(region);
                return;
            default:
                throw new NotSupportedException();
        }
    }

    public CompoundStatement ControlFlowElementSequence(ScfElements sequence)
    {
        var stms = Statements;
        Statements = [];
        foreach (var e in sequence.Elements)
        {
            ControlFlowElement(e);
        }

        var result = new CompoundStatement([.. Statements]);
        Statements = stms;
        return result;
    }

    IStatement HandleBrDepth(IScfLabelRegion region)
    {
        if (MaxNestedBreakDepth > 0)
        {
            IBinaryExpressionOperation eq = NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>
                .Instance;
            IBinaryExpressionOperation sub = NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Sub>
                .Instance;
            return SyntaxFactory.If(
                eq.CreateExpression(
                    SyntaxFactory.VarIdentifier(BrDepth),
                    SyntaxFactory.Literal(0)
                ),
                SyntaxFactory.CompoundStatement(
                    SyntaxFactory.AssignStatement(SyntaxFactory.VarIdentifier(BrDepth), SyntaxFactory.Literal(-1)),
                    region.BrCurrentStatement()),
                SyntaxFactory.CompoundStatement(
                    SyntaxFactory.AssignStatement(
                        SyntaxFactory.VarIdentifier(BrDepth),
                        sub.CreateExpression(
                            SyntaxFactory.VarIdentifier(BrDepth),
                            SyntaxFactory.Literal(1)
                        )
                    ),
                    SyntaxFactory.Break()
                )
            );
        }
        else
        {
            return SyntaxFactory.CompoundStatement();
        }
    }


    public Unit Block(Block block)
    {
        var isBrTarget = UsedBrLabels.Contains(block.Label);
        if (isBrTarget)
        {
            LabelTargetsContext.Push((block.Label, block));
            var body = ControlFlowElementSequence(block.Body);
            var lp = new LoopStatement(new CompoundStatement(
                [
                    ..body.Statements,
                    HandleBrDepth(block),
                    SyntaxFactory.Break()
                ]
            ));
            Statements.Add(lp);
            LabelTargetsContext.Pop();
            MaxNestedBreakDepth--;
        }
        else
        {
            var body = ControlFlowElementSequence(block.Body);
            Statements.Add(body);
        }

        return default;
    }


    public Unit VisitLoop(Loop loop)
    {
        LabelTargetsContext.Push((loop.Label, loop));
        var body = ControlFlowElementSequence(loop.Body);
        var l = new LoopStatement(new CompoundStatement([
            body,
            HandleBrDepth(loop)
        ]));
        Statements.Add(l);
        LabelTargetsContext.Pop();
        MaxNestedBreakDepth--;
        return default;
    }

    public Unit VisitIfThenElse(IfThenElse ifThenElse)
    {
        var e = Expressions.Pop();
        var t = ControlFlowElementSequence(ifThenElse.TrueBody);
        var f = ControlFlowElementSequence(ifThenElse.FalseBody);
        Statements.Add(SyntaxFactory.If(
            e,
            t,
            f
        ));
        return default;
    }

    public Unit Visit(BrInstruction inst)
    {
        var target = LabelTargetsContext.Index().Single(x => x.Item.Item1.Equals(inst.Target));
        var (index, (_, region)) = target;
        if (index == 0)
        {
            Statements.Add(region.BrCurrentStatement());
        }
        else
        {
            if (!SupportNestedBreak)
            {
                throw new NotSupportedException();
            }

            UsedNestedBreak = true;
            MaxNestedBreakDepth = Math.Max(MaxNestedBreakDepth, index + 1);
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.VarIdentifier(BrDepth),
                SyntaxFactory.Literal(index - 1)));
            Statements.Add(SyntaxFactory.Break());
        }

        return default;
    }

    public Unit Visit(BrIfInstruction inst)
    {
        var target = LabelTargetsContext.Index().Single(x => x.Item.Item1.Equals(inst.Target));
        var (index, (_, region)) = target;
        if (index == 0)
        {
            Statements.Add(SyntaxFactory.If(
                Expressions.Pop(),
                SyntaxFactory.CompoundStatement(region.BrCurrentStatement()),
                CompoundStatement.Empty));
            return default;
        }

        if (!SupportNestedBreak)
        {
            throw new NotSupportedException();
        }

        UsedNestedBreak = true;

        var jump = new CompoundStatement([
            SyntaxFactory.AssignStatement(
                SyntaxFactory.VarIdentifier(BrDepth),
                SyntaxFactory.Literal(index)),
            SyntaxFactory.Break()
        ]);
        var e = Expressions.Pop();
        var s = SyntaxFactory.If(e, jump, CompoundStatement.Empty);
        Statements.Add(s);
        return default;
    }

    public Unit Visit(ReturnInstruction inst)
    {
        if (Expressions.Count == 0)
        {
            Statements.Add(SyntaxFactory.Return(null));
        }
        else
        {
            Statements.Add(SyntaxFactory.Return(Expressions.Pop()));
        }

        return default;
    }

    public Unit Visit(NopInstruction inst)
    {
        return default;
    }

    public Unit Visit<TLiteral>(ConstInstruction<TLiteral> inst) where TLiteral : ILiteral
    {
        Expressions.Push(SyntaxFactory.Literal(inst.Literal));
        return default;
    }

    public Unit Visit(CallInstruction inst)
    {
        List<IExpression> arguments = new(inst.Callee.Parameters.Length);
        for (var i = 0; i < inst.Callee.Parameters.Length; i++)
        {
            arguments.Add(Expressions.Pop());
        }

        arguments.Reverse();
        Expressions.Push(SyntaxFactory.Call(inst.Callee, [.. arguments]));
        return default;
    }

    public Unit VisitAddressOf<TInst>(TInst inst) where TInst : IAddressOfInstruction
    {
        var e = Expressions.Pop();
        Debug.Assert(e.Type.Equals(inst.TargetType));
        Expressions.Push(SyntaxFactory.AddressOf(e));
        return default;
    }

    public Unit VisitIndirection<TInst>(TInst inst) where TInst : IIndirectionInstruction
    {
        var e = Expressions.Pop();
        Debug.Assert(e.Type is IPtrType { BaseType: var bt } && bt.Equals(inst.TargetType));
        Expressions.Push(SyntaxFactory.AddressOf(e));
        return default;
    }

    public Unit Visit<TTarget>(LoadSymbolValueInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol
    {
        switch (inst.Target)
        {
            case IVariableIdentifierSymbol v:
                Expressions.Push(new VariableIdentifierExpression(v));
                break;
            case MemberDeclaration m:
            {
                var o = Expressions.Pop();
                Expressions.Push(new NamedComponentExpression(o, m));
                break;
            }
            default:
                throw new NotImplementedException();
        }

        return default;
    }

    public Unit Visit<TTarget>(LoadSymbolAddressInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol
    {
        switch (inst.Target)
        {
            case IVariableIdentifierSymbol v:
                Expressions.Push(new VariableIdentifierExpression(v));
                break;
            case MemberDeclaration m:
            {
                var o = Expressions.Pop();
                Expressions.Push(new NamedComponentExpression(o, m));
                break;
            }
            default:
                throw new NotImplementedException();
        }

        return default;
    }

    public Unit Visit<TTarget>(StoreSymbolInstruction<TTarget> inst)
        where TTarget : ILoadStoreTargetSymbol
    {
        {
            if (inst.Target is IVariableIdentifierSymbol v)
            {
                Statements.Add(SyntaxFactory.AssignStatement(
                    new VariableIdentifierExpression(v),
                    Expressions.Pop()
                ));
                return default;
            }
        }
        {
            if (inst.Target is MemberDeclaration m)
            {
                var v = Expressions.Pop();
                var o = Expressions.Pop();
                Statements.Add(SyntaxFactory.AssignStatement(
                    o,
                    v
                ));
                return default;
            }
        }
        throw new NotImplementedException();
    }

    public Unit Visit<TOperation>(BinaryExpressionOperationInstruction<TOperation> inst)
        where TOperation : ISingleton<TOperation>, IBinaryExpressionOperation<TOperation>
    {
        var r = Expressions.Pop();
        var l = Expressions.Pop();
        Expressions.Push(TOperation.Instance.CreateExpression(l, r));
        return default;
    }

    public Unit VisitBinaryStatement<TOperation>(BinaryStatementOperationInstruction<TOperation> inst)
        where TOperation : IBinaryStatementOperation<TOperation>
    {
        var r = Expressions.Pop();
        var l = Expressions.Pop();
        Statements.Add(TOperation.Instance.CreateStatement(l, r));
        return default;
    }

    public Unit Visit(LogicalNotInstruction inst)
    {
        var e = Expressions.Pop();
        var r = new UnaryLogicalExpression(e, UnaryLogicalOp.Not);
        Expressions.Push(r);
        return default;
    }

    public Unit VisitUnaryOperation<TOperation>(UnaryExpressionOperationInstruction<TOperation> inst)
        where TOperation : IUnaryOperation<TOperation>
    {
        var e = Expressions.Pop();
        var r = TOperation.Instance.CreateExpression(e);
        Expressions.Push(r);
        return default;
    }

    public Unit VisitVectorComponentGet<TRank, TVector, TComponent>()
        where TRank : Common.Nat.IRank<TRank>
        where TVector : Language.Types.ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
    {
        var value = Expressions.Pop();
        var expr = VectorComponentGetOperation<TRank, TVector, TComponent>.Instance.CreateExpression(value);
        Expressions.Push(expr);
        return default;
    }

    public Unit VisitVectorComponentSet<TRank, TVector, TComponent>()
        where TRank : Common.Nat.IRank<TRank>
        where TVector : Language.Types.ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
    {
        var value = Expressions.Pop();
        var vec = Expressions.Pop();
        var stmt = VectorComponentSetOperation<TRank, TVector, TComponent>.Instance.CreateStatement(vec, value);
        Statements.Add(stmt);
        return default;
    }

    public Unit VisitVectorSwizzleGet<TPattern, TElement>()
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : Language.Types.IScalarType<TElement>
    {
        var value = Expressions.Pop();
        var expr = VectorSwizzleGetOperation<TPattern, TElement>.Instance.CreateExpression(value);
        Expressions.Push(expr);
        return default;
    }

    public Unit VisitVectorSwizzleSet<TPattern, TElement>()
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : Language.Types.IScalarType<TElement>
    {
        var value = Expressions.Pop();
        var target = Expressions.Pop();
        var stmt = VectorSwizzleSetOperation<TPattern, TElement>.Instance.CreateStatement(target, value);
        Statements.Add(stmt);
        return default;
    }

    public Unit Visit(DupInstruction inst)
    {
        if (Expressions.Count == 0)
            throw new InvalidOperationException("Cannot dup when expression stack is empty");

        var top = Expressions.Peek();
        Expressions.Push(top);
        return default;
    }

    public Unit Visit(DropInstruction inst)
    {
        if (Expressions.Count == 0)
            throw new InvalidOperationException("Cannot pop when expression stack is empty");

        var expr = Expressions.Pop();
        Statements.Add(new PhonyAssignmentStatement(expr));
        return default;
    }
}
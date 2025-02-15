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
using DualDrill.Common;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using ICSharpCode.Decompiler.IL;
using CallInstruction = DualDrill.CLSL.Language.LinearInstruction.CallInstruction;

namespace DualDrill.CLSL.Compiler;

public sealed class StructuredInstructionToAbstractSyntaxTreeBuilder
    : IStructuredControlFlowRegion<IStructuredStackInstruction>.IRegionVisitor<Unit>
        , IStructuredStackInstructionVisitor<Unit>
{
    public StructuredInstructionToAbstractSyntaxTreeBuilder(
        ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> shaderModule,
        FunctionDeclaration function)
    {
        ShaderModule = shaderModule;
        Function = function;
        var functionBody = ShaderModule.GetBody(function);
        RootRegion = functionBody.Root;
        LocalVariables = functionBody.LocalVariables.Distinct().ToFrozenSet();
    }

    public ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> ShaderModule { get; }
    public FunctionDeclaration Function { get; }
    public IStructuredControlFlowRegion<IStructuredStackInstruction> RootRegion { get; }

    Stack<IExpression> Expressions = [];
    List<IStatement> Statements = [];
    Stack<CompoundStatement> Blocks = [];

    ImmutableDictionary<Label, ILabeledStructuredControlFlowRegion<IStructuredStackInstruction>> LabelContext =
        ImmutableDictionary<Label, ILabeledStructuredControlFlowRegion<IStructuredStackInstruction>>.Empty;

    FrozenSet<VariableDeclaration> LocalVariables;

    public CompoundStatement Build()
    {
        var varsDeclare = LocalVariables.Select(v => new VariableOrValueStatement(v));
        OnRegion(RootRegion);
        return new CompoundStatement([.. varsDeclare, .. Statements]);
    }

    public void OnRegion(IStructuredControlFlowRegion<IStructuredStackInstruction> region)
    {
        switch (region)
        {
            case Block<IStructuredStackInstruction> block:
                VisitBlock(block);
                break;
            case Loop<IStructuredStackInstruction> loop:
                VisitLoop(loop);
                break;
            case IfThenElse<IStructuredStackInstruction> ifThenElse:
                VisitIfThenElse(ifThenElse);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    public Unit OnElement(IStructuredControlFlowElement<IStructuredStackInstruction> element)
    {
        switch (element)
        {
            case IStructuredStackInstruction instruction:
                return instruction.Accept<StructuredInstructionToAbstractSyntaxTreeBuilder, Unit>(this);
            case IStructuredControlFlowRegion<IStructuredStackInstruction> region:
                OnRegion(region);
                return default;
            default:
                throw new NotSupportedException();
        }
    }

    public CompoundStatement OnSequence(StructuredControlFlowElementSequence<IStructuredStackInstruction> sequence)
    {
        var stms = Statements;
        Statements = [];
        foreach (var e in sequence.Elements)
        {
            _ = OnElement(e);
        }

        var result = new CompoundStatement([.. Statements]);
        Statements = stms;
        return result;
    }

    public Unit VisitBlock(Block<IStructuredStackInstruction> block)
    {
        Statements.Add(OnSequence(block.Body));
        return default;
    }

    public Unit VisitLoop(Loop<IStructuredStackInstruction> loop)
    {
        var l = new LoopStatement(OnSequence(loop.Body));
        Statements.Add(l);
        return default;
    }

    public Unit VisitIfThenElse(IfThenElse<IStructuredStackInstruction> ifThenElse)
    {
        var f = Expressions.Pop();
        var tBlock = OnSequence(ifThenElse.TrueBody);
        var fBlock = OnSequence(ifThenElse.TrueBody);
        Statements.Add(new IfStatement(
            f,
            tBlock,
            fBlock,
            []
        ));
        return default;
    }

    public Unit Visit(BrInstruction inst)
    {
        // TODO: proper handling of nested exit
        Statements.Add(new BreakStatement());
        return default;
    }

    public Unit Visit(BrIfInstruction inst)
    {
        // TODO: proper handling of nested exit
        Statements.Add(new BreakStatement());
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

    public Unit Visit<TTarget>(LoadSymbolInstruction<TTarget> inst)
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
                Statements.Add(new SimpleAssignmentStatement(
                    new VariableIdentifierExpression(v),
                    Expressions.Pop(),
                    AssignmentOp.Assign
                ));
                return default;
            }
        }
        {
            if (inst.Target is MemberDeclaration m)
            {
                var v = Expressions.Pop();
                var o = Expressions.Pop();
                Statements.Add(new SimpleAssignmentStatement(
                    o,
                    v,
                    AssignmentOp.Assign
                ));
                return default;
            }
        }
        throw new NotImplementedException();
    }

    public Unit Visit<TOperation>(BinaryOperationInstruction<TOperation> inst)
        where TOperation : ISingleton<TOperation>, IBinaryOperation<TOperation>
    {
        var r = Expressions.Pop();
        var l = Expressions.Pop();
        Expressions.Push(TOperation.Instance.CreateExpression(l, r));
        return default;
    }

    public Unit Visit(LogicalNotInstruction inst)
    {
        var e = Expressions.Pop();
        var r = new UnaryLogicalExpression(e, UnaryLogicalOp.Not);
        Expressions.Push(r);
        return default;
    }

    public Unit VisitUnaryOperation<TOperation>(UnaryOperationInstruction<TOperation> inst)
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
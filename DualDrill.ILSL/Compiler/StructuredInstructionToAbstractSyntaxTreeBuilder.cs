using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;
using System.Diagnostics;

namespace DualDrill.CLSL.Compiler;

public sealed class StructuredInstructionToAbstractSyntaxTreeBuilder
    : IStructuredControlFlowRegion<IStructuredStackInstruction>.IRegionVisitor<Unit>
    , Block<IStructuredStackInstruction>.IElement.IElementVisitor<Unit>
    , IStructuredStackInstructionVisitor<Unit>
{
    public StructuredInstructionToAbstractSyntaxTreeBuilder(
        ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> shaderModule,
        FunctionDeclaration function,
        IStructuredControlFlowRegion<IStructuredStackInstruction> stackIR
    )
    {
        ShaderModule = shaderModule;
        Function = function;
        StackIR = stackIR;
    }

    public ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> ShaderModule { get; }
    public FunctionDeclaration Function { get; }
    public IStructuredControlFlowRegion<IStructuredStackInstruction> StackIR { get; }

    Stack<IExpression> Expressions = [];
    List<IStatement> Statements = [];
    Stack<CompoundStatement> Blocks = [];

    public CompoundStatement Build()
    {
        StackIR.AcceptRegionVisitor(this);
        Debug.Assert(Blocks.Count == 1);
        return Blocks.Pop();
    }

    public Unit VisitBlock(Block<IStructuredStackInstruction> block)
    {
        List<CompoundStatement> blocks = [];
        foreach (var e in block.Body)
        {
            e.AcceptElementVisitor(this);
            blocks.Add(Blocks.Pop());
        }
        Blocks.Push(new CompoundStatement([.. blocks]));
        return default;
    }

    public Unit VisitLoop(Loop<IStructuredStackInstruction> loop)
    {
        throw new NotImplementedException();
    }

    public Unit VisitIfThenElse(IfThenElse<IStructuredStackInstruction> ifThenElse)
    {
        throw new NotImplementedException();
    }

    public Unit VisitBasicBlock(BasicBlock<IStructuredStackInstruction> basicBlock)
    {
        foreach (var inst in basicBlock.Instructions.Span)
        {
            inst.Accept<StructuredInstructionToAbstractSyntaxTreeBuilder, Unit>(this);
        }
        Blocks.Push(new CompoundStatement([.. Statements]));
        Statements.Clear();
        return default;
    }

    public Unit Visit(BrInstruction inst)
    {
        return default;
    }

    public Unit Visit(BrIfInstruction inst)
    {
        throw new NotImplementedException();
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

    public Unit Visit<TTarget>(LoadSymbolInstruction<TTarget> inst) where TTarget : IVariableIdentifierResolveResult
    {
        Expressions.Push(new VariableIdentifierExpression(inst.Target));
        return default;
    }

    public Unit Visit<TTarget>(LoadSymbolAddressInstruction<TTarget> inst) where TTarget : IVariableIdentifierResolveResult
    {
        throw new NotImplementedException();
    }

    public Unit Visit<TTarget>(StoreSymbolInstruction<TTarget> inst) where TTarget : IVariableIdentifierResolveResult
    {
        Statements.Add(new SimpleAssignmentStatement(
            new VariableIdentifierExpression(inst.Target),
            Expressions.Pop(),
            AssignmentOp.Assign
        ));
        return default;
    }

    public Unit Visit<TOperation>(BinaryOperationInstruction<TOperation> inst) where TOperation : ISingleton<TOperation>, IBinaryOperation<TOperation>
    {
        var r = Expressions.Pop();
        var l = Expressions.Pop();
        Expressions.Push(new BinaryExpression<TOperation>(l, r));
        return default;
    }

    public Unit Visit(LogicalNotInstruction inst)
    {
        throw new NotImplementedException();
    }

    public Unit Visit<TOperation>(UnaryScalarInstruction<TOperation> inst) where TOperation : IUnaryScalarOperation<TOperation>
    {
        return default;
    }
}

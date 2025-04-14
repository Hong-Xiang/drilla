using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Value;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IBasicBlockTransform<TBasicBlock>
{
}

public interface IBasicBlockTraverser<TBasicBlock>
{
    record struct Context(
        LocalDeclarationContext DeclarationContext,
        ImmutableStack<IStructuredControlFlowRegion> Regions
    )
    {
    }

    void Visit(Context context, TBasicBlock basicBlock);
}

public interface IStackInstructionTraverser
{
    record struct Context(
        LocalDeclarationContext DeclarationContext,
        ImmutableStack<IStructuredControlFlowRegion> Regions,
        StackInstructionBasicBlock BasicBlock,
        int Index
    )
    {
    }

    void Visit(Context context);
}

public interface IFunctionBody2 : IFunctionBody
{
    IFunctionBody2 ApplyTransform<TBasicBlock>(IBasicBlockTransform<TBasicBlock> transform);
}

public sealed class FunctionBody<TBodyData> : IFunctionBody, ILocalDeclarationContext
    where TBodyData : ILocalDeclarationReferencingElement
{
    public TBodyData Body { get; }

    public ImmutableArray<VariableDeclaration> LocalVariables => LocalDeclarationContext.LocalVariables;
    public ImmutableArray<Label> Labels => LocalDeclarationContext.Labels;
    public ImmutableArray<IValue> Values { get; }

    public int ValueIndex(IValue value)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
        => LocalDeclarationContext.VariableIndex(variable);

    public int LabelIndex(Label label)
        => LocalDeclarationContext.LabelIndex(label);

    public ILocalDeclarationContext LocalDeclarationContext { get; }

    public FunctionBody(TBodyData body)
    {
        Body = body;
        LocalDeclarationContext = new LocalDeclarationContext([body]);
    }

    public void Dump(IndentedTextWriter writer)
    {
        Body.Dump(LocalDeclarationContext, writer);
    }

    public ILocalDeclarationContext LocalContext => this;
}
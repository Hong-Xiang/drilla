using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock>
    where TSourceBasicBlock : IBasicBlock2
    where TResultBasicBlock : IBasicBlock2
{
    TResultBasicBlock Apply(TSourceBasicBlock basicBlock);
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

public sealed class FunctionBody<TBodyData> : IFunctionBody, ILocalDeclarationContext
    where TBodyData : IDeclarationUser
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

    public ILocalDeclarationContext DeclarationContext => this;
}
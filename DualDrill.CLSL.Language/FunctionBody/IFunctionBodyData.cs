using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock>
    where TSourceBasicBlock : IBasicBlock2
    where TResultBasicBlock : IBasicBlock2
{
    TResultBasicBlock Apply(TSourceBasicBlock basicBlock);
}

public sealed class FunctionBody<TBodyData> : IFunctionBody, ILocalDeclarationContext
    where TBodyData : IDeclarationUser
{
    public TBodyData Body { get; }

    public ImmutableArray<VariableDeclaration> LocalVariables => LocalDeclarationContext.LocalVariables;
    public ImmutableArray<Label> Labels => LocalDeclarationContext.Labels;
    public ImmutableArray<IShaderValue> Values { get; }

    public int ValueIndex(IShaderValue value)
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
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class RegionJump(Label Label, ImmutableArray<ShaderValue> Arguments)
{
}

public sealed record class ShaderRegionBody(
    Label Label,
    ImmutableArray<Symbol.ShaderValueDeclaration> Parameters,
    Seq<ShaderStmt, ITerminator<RegionJump, ShaderValue>> Body
) : IBasicBlock2
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => throw new NotSupportedException();
    public IEnumerable<IShaderValue> ReferencedValues => [
        ..Parameters.Select(p => p.Value)
    ];
    public ISuccessor Successor => Body.Last.ToSuccessor();
}

public interface IParameterBinding
{
    ShaderValue Value { get; }
    IShaderType Type { get; }
}

public sealed record class ParameterValueBinding(
    ShaderValue Value,
    ParameterDeclaration Parameter
) : IParameterBinding
{
    public IShaderType Type => Parameter.Type;
}

public sealed record class ParameterPointerBinding(
    ShaderValue Value,
    ParameterDeclaration Parameter
) : IParameterBinding
{
    public IShaderType Type => Parameter.Type.GetPtrType();
}

public sealed record class FunctionBody4(
    ImmutableArray<IParameterBinding> Parameters,
    ImmutableArray<Symbol.ShaderValueDeclaration> LocalVariableValues,
    RegionTree<Label, ShaderRegionBody> Body
) : IFunctionBody, ILocalDeclarationContext, IUnifiedFunctionBody<ShaderRegionBody>
{
    public void Dump(IndentedTextWriter writer)
    {
        new FunctionBodyFormatter(writer, this).Dump();
    }

    public ILocalDeclarationContext DeclarationContext => this;

    public int LabelIndex(Label label)
    {
        throw new NotImplementedException();
    }

    public int ValueIndex(IShaderValue value)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<VariableDeclaration> LocalVariables => [];
    public ImmutableArray<Label> Labels { get; } = GetLabels(Body);

    static ImmutableArray<Label> GetLabels(RegionTree<Label, ShaderRegionBody> body)
    {
        var labels = ImmutableArray.CreateBuilder<Label>();
        body.Traverse((l, b) =>
        {
            labels.Add(l);
            return false;
        });
        return labels.ToImmutable();
    }

    public ImmutableArray<IShaderValue> Values { get; }

    public IUnifiedFunctionBody<TResultBasicBlock> ApplyTransform<TResultBasicBlock>(
        IBasicBlockTransform<ShaderRegionBody, TResultBasicBlock> transform) where TResultBasicBlock : IBasicBlock2
    {
        throw new NotImplementedException();
    }

    public TResult Traverse<TElementResult, TResult>(
        IControlFlowElementSequenceTraverser<ShaderRegionBody, TElementResult, TResult> traverser)
    {
        throw new NotImplementedException();
    }

    public Label Entry => Body.Label;

    public ShaderRegionBody this[Label label]
    {
        get
        {
            ShaderRegionBody? found = null;
            Body.Traverse((l, b) =>
            {
                if (l == label)
                {
                    found = b;
                    return true;
                }
                return false;
            });
            if (found is null)
            {
                throw new KeyNotFoundException($"region with label {label} not found");
            }
            return found;
        }
    }


    public ISuccessor Successor(Label label)
        => this[label].Successor;
}
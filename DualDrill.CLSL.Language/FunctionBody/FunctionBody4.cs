﻿using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class FunctionBody4(FunctionDeclaration Declaration, RegionTree<Label, ShaderRegionBody> Body)
    : IFunctionBody, ILocalDeclarationContext, IUnifiedFunctionBody<ShaderRegionBody>
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
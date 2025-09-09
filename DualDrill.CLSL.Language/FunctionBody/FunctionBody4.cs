using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class FunctionBody4
    : IFunctionBody, ILocalDeclarationContext, IUnifiedFunctionBody<ShaderRegionBody>
{
    public FunctionBody4(FunctionDeclaration declaration, RegionTree<Label, ShaderRegionBody> body)
    {
        Declaration = declaration;
        Body = body;
        {
            var labels = ImmutableArray.CreateBuilder<Label>();
            var enqueued = new HashSet<Label>();
            var toVisit = new Queue<Label>();
            void TryEnqueue(Label label)
            {
                if (enqueued.Contains(label))
                {
                    return;
                }
                enqueued.Add(label);
                toVisit.Enqueue(label);
            }
            TryEnqueue(Entry);
            while (toVisit.Count > 0)
            {
                var label = toVisit.Dequeue();
                labels.Add(label);
                foreach (var t in this[label].Body.Last.ToSuccessor().AllTargets())
                {
                    TryEnqueue(t);
                }
            }
            Labels = labels.ToImmutable();
        }
        {
            LocalVariables = body.Fold(new ValueUseAnalysis())
                                 .OfType<VariablePointerValue>()
                                 .Select(v => v.Declaration)
                                 .Where(v => v.AddressSpace is FunctionAddressSpace)
                                 .Distinct()
                                 .ToImmutableArray();
        }
    }

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

    public FunctionBody4 UpdateValue(Func<IShaderValue, IShaderValue> update)
    {
        throw new NotImplementedException();
    }

    public FunctionBody4 UpdateStatement(Func<ShaderStmt, IEnumerable<ShaderStmt>> update)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }

    public ImmutableArray<Label> Labels { get; }

    public IEnumerable<IShaderValue> UsedValues()
    {
        return Body.Fold(new ValueUseAnalysis());
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
    public FunctionDeclaration Declaration { get; }
    public RegionTree<Label, ShaderRegionBody> Body { get; }

    public ShaderRegionBody this[Label label]
    {
        get
        {
            ShaderRegionBody? found = null;
            Body.Traverse((_, l, b) =>
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

    public FunctionBody4 MapValueUse(Func<IShaderValue, IShaderValue> mapValue)
    {
        return new FunctionBody4(
                    Declaration,
                    Body.Select(
                        static l => l,
                        body => new ShaderRegionBody(
                            body.Label,
                            [.. body.Parameters.Select(mapValue)],
                            body.Body.Select(
                                stmt => stmt.Select(
                                    mapValue,
                                    e => e.Select(mapValue),
                                    mapValue,
                                    f =>
                                    {
                                        var fr = mapValue(f);
                                        return (FunctionDeclaration)fr;
                                    }
                                ),
                                term => term.Select(
                                    j => new RegionJump(j.Label, [.. j.Arguments.Select(mapValue)]),
                                    mapValue)
                            ),
                            body.ImmediatePostDominator
                        )
                    )
                );
    }

    public FunctionBody4 MapRegionBody(Func<ShaderRegionBody, ShaderRegionBody> mapRegionBody)
    {
        return new FunctionBody4(
                    Declaration,
                    Body.Select(
                        static l => l,
                        body => mapRegionBody(body)
                    )
                );
    }

    public ISuccessor Successor(Label label)
        => this[label].Successor;
}
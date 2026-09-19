using System.Collections.Immutable;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;

namespace DualDrill.CLSL.Test;

// Independent test interpreter: transfers resolve through captured lexical environments,
// never through FunctionBody4's label indexer or checked control metadata.
internal static class ScopedContinuationOracle
{
    private sealed class Continuation(RegionTree<Label, ShaderRegionBody> region)
    {
        internal RegionTree<Label, ShaderRegionBody> Region { get; } = region;
        internal ImmutableDictionary<Label, Continuation> Environment { get; set; } = null!;
    }

    internal static Execution RunScoped(
        FunctionBody4 body,
        ImmutableArray<Value> arguments,
        int stepLimit = 10000)
    {
        var root = Compile(body.Body, ImmutableDictionary<Label, Continuation>.Empty);
        var machine = new Machine(body, arguments, "scoped", stepLimit);
        var current = root;
        var incoming = ImmutableArray<Value>.Empty;

        while (true)
        {
            machine.Bind(current.Region.Body, incoming);
            switch (machine.Execute(current.Region.Label, current.Region.Body))
            {
                case Control.Returned returned:
                    return machine.Complete(returned.Value);
                case Control.Transfer transfer:
                    incoming = machine.ReadArguments(transfer.Jump);
                    if (!current.Environment.TryGetValue(transfer.Jump.Label, out var target))
                        throw new InvalidOperationException(
                            $"Scoped {body.Declaration.Name}: {current.Region.Label} cannot resolve " +
                            $"{transfer.Jump.Label} in its definition-site environment.");
                    current = target;
                    break;
            }
        }
    }

    private static Continuation Compile(
        RegionTree<Label, ShaderRegionBody> region,
        ImmutableDictionary<Label, Continuation> outer)
    {
        var continuation = new Continuation(region);
        var environment = outer;
        if (region.Definition.Kind is RegionKind.Loop)
            environment = environment.Add(region.Label, continuation);

        foreach (var child in region.Bindings)
        {
            var childContinuation = Compile(child, environment);
            environment = environment.Add(child.Label, childContinuation);
        }

        continuation.Environment = environment;
        return continuation;
    }
}

using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Region;

public enum ScopedContinuationKind
{
    Forward,
    Repeat
}

public sealed record ScopedContinuation<TLabel>(
    TLabel Target,
    TLabel Owner,
    ScopedContinuationKind Kind);

public sealed record ScopedTransfer<TLabel>(
    TLabel Source,
    int Arm,
    TLabel Target,
    TLabel Owner,
    ScopedContinuationKind Kind);

public sealed class ScopedControlIndex<TLabel> where TLabel : notnull
{
    private readonly ImmutableDictionary<(TLabel Source, int Arm), ScopedTransfer<TLabel>> transfersBySource;
    private readonly ImmutableDictionary<TLabel, ImmutableArray<ScopedContinuation<TLabel>>> scopes;

    private ScopedControlIndex(
        ImmutableArray<TLabel> labels,
        ImmutableArray<ScopedTransfer<TLabel>> transfers,
        ImmutableDictionary<TLabel, ImmutableArray<ScopedContinuation<TLabel>>> scopes)
    {
        Labels = labels;
        Transfers = transfers;
        this.scopes = scopes;
        transfersBySource = transfers.ToImmutableDictionary(transfer => (transfer.Source, transfer.Arm));
    }

    public ImmutableArray<TLabel> Labels { get; }
    public ImmutableArray<ScopedTransfer<TLabel>> Transfers { get; }

    public ScopedTransfer<TLabel> Resolve(TLabel source, int arm) =>
        transfersBySource.TryGetValue((source, arm), out var transfer)
            ? transfer
            : throw new KeyNotFoundException($"No scoped transfer is defined for source '{source}', arm {arm}.");

    public ImmutableArray<ScopedContinuation<TLabel>> VisibleFrom(TLabel source) =>
        scopes.TryGetValue(source, out var scope)
            ? scope
            : throw new KeyNotFoundException($"No scoped region is defined for label '{source}'.");

    public void Dump(IndentedTextWriter writer, Func<TLabel, string> formatLabel)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(formatLabel);

        foreach (var label in Labels)
        {
            writer.Write("scope ");
            writer.Write(formatLabel(label));
            writer.Write(": [");
            writer.Write(string.Join(", ", VisibleFrom(label).Select(binding =>
                $"{formatLabel(binding.Target)} {binding.Kind.ToString().ToLowerInvariant()} owner={formatLabel(binding.Owner)}")));
            writer.WriteLine("]");
        }

        foreach (var transfer in Transfers)
        {
            writer.Write("transfer ");
            writer.Write(formatLabel(transfer.Source));
            writer.Write('#');
            writer.Write(transfer.Arm);
            writer.Write(" -> ");
            writer.Write(formatLabel(transfer.Target));
            writer.Write(' ');
            writer.Write(transfer.Kind.ToString().ToLowerInvariant());
            writer.Write(" owner=");
            writer.WriteLine(formatLabel(transfer.Owner));
        }
    }

    internal static ScopedControlIndex<TLabel> Create<TBody>(
        RegionTree<TLabel, TBody> tree,
        Func<TLabel, TBody, IReadOnlyList<TLabel>> targets,
        string context)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(targets);

        var definitions = ImmutableArray.CreateBuilder<RegionTree<TLabel, TBody>>();
        var byLabel = new Dictionary<TLabel, RegionTree<TLabel, TBody>>();

        void Collect(RegionTree<TLabel, TBody> region)
        {
            if (!byLabel.TryAdd(region.Label, region))
                throw Invalid($"duplicate defined label '{region.Label}'");
            definitions.Add(region);
            foreach (var child in region.Bindings)
                Collect(child);
        }

        Collect(tree);
        var scopes = ImmutableDictionary.CreateBuilder<TLabel, ImmutableArray<ScopedContinuation<TLabel>>>();
        var edges = byLabel.Keys.ToDictionary(label => label, static _ => new List<TLabel>());
        var transfersBySource = byLabel.Keys.ToDictionary(
            label => label,
            static _ => new List<ScopedTransfer<TLabel>>());

        void Check(RegionTree<TLabel, TBody> region, ImmutableArray<ScopedContinuation<TLabel>> outer)
        {
            var scope = outer;
            if (region.Definition.Kind is RegionKind.Loop)
                scope = scope.Add(new(region.Label, region.Label, ScopedContinuationKind.Repeat));

            foreach (var child in region.Bindings)
            {
                Check(child, scope);
                scope = scope.Add(new(child.Label, region.Label, ScopedContinuationKind.Forward));
            }

            scopes.Add(region.Label, scope);
            foreach (var (arm, target) in targets(region.Label, region.Body).Index())
            {
                if (!byLabel.ContainsKey(target))
                    throw Invalid($"transfer from '{region.Label}', arm {arm}, targets unknown label '{target}'");

                var binding = scope.LastOrDefault(item =>
                    EqualityComparer<TLabel>.Default.Equals(item.Target, target));
                if (binding is null)
                {
                    var reason = region.Definition.Kind is RegionKind.Block &&
                                 EqualityComparer<TLabel>.Default.Equals(region.Label, target)
                        ? "block self-reference"
                        : "target is not visible in the source's lexical scope";
                    throw Invalid(
                        $"transfer from '{region.Label}', arm {arm}, to '{target}' is illegal: {reason}");
                }

                var transfer = new ScopedTransfer<TLabel>(
                    region.Label,
                    arm,
                    binding.Target,
                    binding.Owner,
                    binding.Kind);
                transfersBySource[region.Label].Add(transfer);
                edges[region.Label].Add(target);
            }
        }

        Check(tree, []);

        var reachable = new HashSet<TLabel>();
        var pending = new Stack<TLabel>();
        pending.Push(tree.Label);
        while (pending.TryPop(out var label))
        {
            if (!reachable.Add(label))
                continue;
            foreach (var target in edges[label])
                pending.Push(target);
        }

        if (reachable.Count != byLabel.Count)
        {
            var unreachable = definitions
                .Select(region => region.Label)
                .Where(label => !reachable.Contains(label));
            throw Invalid($"unreachable definitions: {string.Join(", ", unreachable.Select(label => $"'{label}'"))}");
        }

        var orderedTransfers = definitions
            .SelectMany(region => transfersBySource[region.Label])
            .ToImmutableArray();
        return new(
            [.. definitions.Select(region => region.Label)],
            orderedTransfers,
            scopes.ToImmutable());

        ArgumentException Invalid(string message) => new($"{context}: {message}.");
    }
}

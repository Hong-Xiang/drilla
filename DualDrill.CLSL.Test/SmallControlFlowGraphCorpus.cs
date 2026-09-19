using System.Collections.Immutable;

namespace DualDrill.CLSL.Test;

internal static class SmallControlFlowGraphCorpus
{
    public const int ExpectedCount = 993;

    public static IEnumerable<GraphCase> Cases()
    {
        for (var nodeCount = 1; nodeCount <= 3; nodeCount++)
        {
            var optionCount = 1 + nodeCount + nodeCount * nodeCount;
            var combinationCount = IntPow(optionCount, nodeCount);
            for (var code = 0; code < combinationCount; code++)
            {
                var successors = DecodeSuccessors(nodeCount, optionCount, code);
                if (AllReachable(successors))
                    yield return new GraphCase(code, successors);
            }
        }
    }

    private static bool AllReachable(ImmutableArray<ImmutableArray<int>> successors)
    {
        HashSet<int> visited = [0];
        Queue<int> pending = new([0]);
        while (pending.TryDequeue(out var source))
            foreach (var successor in successors[source])
                if (visited.Add(successor))
                    pending.Enqueue(successor);

        return visited.Count == successors.Length;
    }

    private static ImmutableArray<ImmutableArray<int>> DecodeSuccessors(
        int nodeCount,
        int optionCount,
        int code)
    {
        var successors = ImmutableArray.CreateBuilder<ImmutableArray<int>>(nodeCount);
        for (var source = 0; source < nodeCount; source++)
        {
            var option = code % optionCount;
            code /= optionCount;
            if (option == 0)
                successors.Add([]);
            else if (option <= nodeCount)
                successors.Add([option - 1]);
            else
            {
                var targets = option - nodeCount - 1;
                successors.Add([targets / nodeCount, targets % nodeCount]);
            }
        }

        return successors.ToImmutable();
    }

    private static int IntPow(int value, int exponent)
    {
        var result = 1;
        for (var i = 0; i < exponent; i++)
            result *= value;
        return result;
    }

    internal sealed record GraphCase(
        int Code,
        ImmutableArray<ImmutableArray<int>> Successors);
}

using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.FunctionBody;

internal static class ScopedRegionControl
{
    public static ScopedControlIndex<Label> Create(
        FunctionDeclaration declaration,
        RegionTree<Label, ShaderRegionBody> tree)
    {
        var context = $"Function '{declaration.Name}'";
        var bodies = new Dictionary<Label, ShaderRegionBody>();

        void Collect(RegionTree<Label, ShaderRegionBody> region, bool root)
        {
            if (!ReferenceEquals(region.Label, region.Body.Label))
                throw Invalid(
                    $"region label '{region.Label}' does not match body label '{region.Body.Label}'");
            if (region.Body.Parameters.IsDefault)
                throw Invalid($"region '{region.Label}' has a default parameter array");
            if (root && !region.Body.Parameters.IsEmpty)
                throw Invalid($"entry region '{region.Label}' must not declare parameters");
            if (!bodies.TryAdd(region.Label, region.Body))
                throw Invalid($"duplicate defined label '{region.Label}'");
            foreach (var (arm, jump) in Jumps(region.Body).Index())
                if (jump.Arguments.IsDefault)
                    throw Invalid(
                        $"transfer from '{region.Label}', arm {arm}, to '{jump.Label}' has a default argument array");
            foreach (var child in region.Bindings)
                Collect(child, false);
        }

        Collect(tree, true);
        var control = ScopedControlIndex<Label>.Create(
            tree,
            static (_, body) => [.. Jumps(body).Select(jump => jump.Label)],
            context);

        foreach (var source in control.Labels)
        foreach (var (arm, jump) in Jumps(bodies[source]).Index())
        {
            var target = bodies[jump.Label];
            if (jump.Arguments.Length != target.Parameters.Length)
                throw Invalid(
                    $"transfer from '{source}', arm {arm}, to '{jump.Label}' has incorrect argument count " +
                    $"{jump.Arguments.Length}; " +
                    $"expected {target.Parameters.Length}");
            foreach (var (index, pair) in target.Parameters.Zip(jump.Arguments).Index())
            {
                if (!SameType(pair.First.Type, pair.Second.Type))
                    throw Invalid(
                        $"transfer from '{source}', arm {arm}, to '{jump.Label}' has incorrect argument type at " +
                        $"index {index}: " +
                        $"'{pair.Second.Type.Name}'; expected '{pair.First.Type.Name}'");
            }
        }

        return control;

        ArgumentException Invalid(string message) => new($"{context}: {message}.");
    }

    private static ImmutableArray<RegionJump<IShaderValue>> Jumps(ShaderRegionBody body) =>
        body.Body.Last switch
        {
            Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Target],
            Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                [branch.TrueTarget, branch.FalseTarget],
            Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> => [],
            Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
            _ => throw new NotSupportedException($"Unsupported region terminator '{body.Body.Last}'.")
        };

    private static bool SameType(IShaderType expected, IShaderType actual) =>
        (expected, actual) switch
        {
            (IPtrType expectedPointer, IPtrType actualPointer) =>
                Equals(expectedPointer.BaseType, actualPointer.BaseType) &&
                Equals(expectedPointer.AddressSpace, actualPointer.AddressSpace),
            _ => Equals(expected, actual)
        };
}

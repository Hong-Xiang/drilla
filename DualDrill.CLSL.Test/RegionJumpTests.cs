using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Test;

public class RegionJumpTests
{
    [Fact]
    public void SelectSatisfiesBoundedIdentityAndCompositionLaws()
    {
        var label = Label.Create("target");
        int[][] samples = [[], [1], [1, 1], [3, 1, 2]];

        foreach (var arguments in samples)
        {
            var jump = new RegionJump<int>(label, [.. arguments]);
            var identity = jump.Select(static value => value);
            var sequential = jump.Select(static value => value + 1).Select(static value => value.ToString());
            var composed = jump.Select(static value => (value + 1).ToString());

            Assert.Same(label, identity.Label);
            Assert.True(arguments.SequenceEqual(identity.Arguments));
            Assert.Same(label, sequential.Label);
            Assert.Same(label, composed.Label);
            Assert.True(sequential.Arguments.SequenceEqual(composed.Arguments));
        }
    }

    [Fact]
    public void SelectChangesPayloadTypeAndPreservesDuplicatesAndOrder()
    {
        var label = Label.Create("target");
        var strings = new RegionJump<int>(label, [2, 1, 2]).Select(static value => $"v{value}");
        var lengths = new RegionJump<string>(label, ["aa", "b"]).Select(static value => value.Length);

        Assert.Same(label, strings.Label);
        Assert.True(strings.Arguments.SequenceEqual(["v2", "v1", "v2"]));
        Assert.True(lengths.Arguments.SequenceEqual([2, 1]));
    }

    [Fact]
    public void SelectInvokesMapperOncePerArgumentInOrder()
    {
        var visited = new List<int>();
        var emptyCalls = 0;

        var mapped = new RegionJump<int>(Label.Create("target"), [3, 1, 3]).Select(value =>
        {
            visited.Add(value);
            return value * 2;
        });
        var empty = new RegionJump<int>(Label.Create("empty"), []).Select(value =>
        {
            emptyCalls++;
            return value;
        });

        Assert.Equal([3, 1, 3], visited);
        Assert.True(mapped.Arguments.SequenceEqual([6, 2, 6]));
        Assert.Empty(empty.Arguments);
        Assert.Equal(0, emptyCalls);
    }

    [Fact]
    public void SelectPropagatesMapperExceptionAndStops()
    {
        var error = new InvalidOperationException("stop");
        var visited = new List<int>();
        var jump = new RegionJump<int>(Label.Create("target"), [1, 2, 3]);

        var thrown = Assert.Throws<InvalidOperationException>(() => jump.Select(value =>
        {
            visited.Add(value);
            if (value == 2) throw error;
            return value;
        }));

        Assert.Same(error, thrown);
        Assert.Equal([1, 2], visited);
    }

    [Fact]
    public void GenericRegionJumpToSuccessorPreservesControlShapeAndOrderedTargets()
    {
        var left = Label.Create("left");
        var right = Label.Create("right");
        ITerminator<RegionJump<int>, string> distinct =
            Terminator.B.BrIf<RegionJump<int>, string>("condition", new(left, [1]), new(right, [2]));
        ITerminator<RegionJump<string>, int> parallel =
            Terminator.B.BrIf<RegionJump<string>, int>(
                1, new(left, ["first", "left"]), new(left, ["second", "right"]));

        Assert.IsType<TerminateSuccessor>(
            Terminator.B.ReturnVoid<RegionJump<int>, string>().ToSuccessor());
        Assert.IsType<TerminateSuccessor>(
            Terminator.B.ReturnExpr<RegionJump<int>, string>("value").ToSuccessor());
        Assert.Same(left, Assert.IsType<UnconditionalSuccessor>(
            Terminator.B.Br<RegionJump<int>, string>(new(left, [1])).ToSuccessor()).Target);

        var distinctSuccessor = Assert.IsType<ConditionalSuccessor>(distinct.ToSuccessor());
        var parallelSuccessor = Assert.IsType<ConditionalSuccessor>(parallel.ToSuccessor());
        var distinctTargets = distinctSuccessor.AllTargets().ToArray();
        var parallelTargets = parallelSuccessor.AllTargets().ToArray();

        Assert.Equal(2, distinctTargets.Length);
        Assert.Same(left, distinctSuccessor.TrueTarget);
        Assert.Same(right, distinctSuccessor.FalseTarget);
        Assert.Same(left, distinctTargets[0]);
        Assert.Same(right, distinctTargets[1]);
        Assert.Equal(2, parallelTargets.Length);
        Assert.Same(left, parallelSuccessor.TrueTarget);
        Assert.Same(left, parallelSuccessor.FalseTarget);
        Assert.Same(left, parallelTargets[0]);
        Assert.Same(left, parallelTargets[1]);
    }

    [Fact]
    public void FunctionBodyMapValueUsePreservesControlLabelsAndMapsJumpArguments()
    {
        var entry = Label.Create("entry");
        var target = Label.Create("target");
        var source = ShaderValue.Literal(new I32Literal(1));
        var replacement = ShaderValue.Literal(new I32Literal(2));
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();
        var entryBody = ShaderRegionBody.Create(entry, [], [],
            terms.Br(new RegionJump<IShaderValue>(target, [source])),
            new ExitPostDominance.Block(target, false));
        var targetBody = ShaderRegionBody.Create(
            target,
            [parameter],
            [],
            terms.ReturnVoid(),
            new ExitPostDominance.FunctionExit(false));
        var declaration = new FunctionDeclaration("Map", [], new FunctionReturn(UnitType.Instance, []), []);
        var body = new FunctionBody4(declaration, RegionTree<Label, ShaderRegionBody>.Block(entry,
            [RegionTree<Label, ShaderRegionBody>.Block(target, [], targetBody, null)], entryBody, target));

        var mapped = body.MapValueUse(value => ReferenceEquals(value, source) ? replacement : value);
        var jump = Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(
            mapped[entry].Body.Last).Target;

        Assert.Same(entry, mapped.Entry);
        Assert.Same(entry, mapped.Body.Label);
        Assert.Equal(2, mapped.Labels.Length);
        Assert.Same(entry, mapped.Labels[0]);
        Assert.Same(target, mapped.Labels[1]);
        Assert.Same(target, mapped[target].Label);
        Assert.Same(target, jump.Label);
        Assert.Same(replacement, Assert.Single(jump.Arguments));
        Assert.Same(parameter, Assert.Single(mapped[target].Parameters));
        Assert.Same(source, Assert.Single(
            Assert.IsType<Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue>>(body[entry].Body.Last)
                .Target.Arguments));
    }
}

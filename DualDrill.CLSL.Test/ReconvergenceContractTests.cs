using System.Collections.Immutable;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class ReconvergenceContractTests(ITestOutputHelper output)
{
    private static readonly Scope SelectionS = new(ScopeKind.Selection, "S");
    private static readonly Scope HelperSelectionT = new(ScopeKind.Selection, "T");
    private static readonly Scope LoopL = new(ScopeKind.Loop, "L");
    private static readonly Scope CallK = new(ScopeKind.Call, "K");
    private static readonly Scope CallJ = new(ScopeKind.Call, "J");
    private static readonly Scope SwitchW = new(ScopeKind.Switch, "W");
    private static readonly Scope Entry = new(ScopeKind.Entry, "entry");

    [Fact]
    public void DiamondsAndEntryReturnUseTheDeclaredSelectionInstance()
    {
        AssertRun(
            "diamond/full",
            Diamond([0, 1, 2, 3]),
            [
                "A/S#0/arm-even={0,2}",
                "B/S#0/arm-odd={1,3}",
                "M/S#0/merge={0,1,2,3}"
            ]);
        AssertRun(
            "diamond/partial",
            Diamond([0, 1, 2]),
            [
                "A/S#0/arm-even={0,2}",
                "B/S#0/arm-odd={1}",
                "M/S#0/merge={0,1,2}"
            ]);
        AssertRun(
            "observation/multiset",
            [
                Walk(0, Observe("Q"), Observe("Q")),
                Walk(1, Observe("Q"), Observe("Q"))
            ],
            ["Q/root={0,1}", "Q~1/root={0,1}"]);

        var walks = ImmutableArray.Create(
            Walk(0, Enter(SelectionS, "return"), Leave(Entry)),
            Walk(1, Enter(SelectionS, "survive"), Leave(SelectionS), Observe("M")),
            Walk(2, Enter(SelectionS, "survive"), Leave(SelectionS), Observe("M")),
            Walk(3, Enter(SelectionS, "survive"), Leave(SelectionS), Observe("M")));

        AssertRun("entry-return", walks, ["M/S#0/merge={1,2,3}"]);
    }

    [Fact]
    public void LoopIterationContinueAndExitContextsStayDistinct()
    {
        var staggered = Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                [
                    Enter(LoopL),
                    .. Enumerable.Range(0, lane).Select(_ => Next(LoopL, "C")),
                    Observe("break"),
                    Leave(LoopL),
                    Observe("X")
                ]))
            .ToImmutableArray();

        AssertRun(
            "loop/staggered-exit",
            staggered,
            [
                "C/L#0/i0={1,2,3}",
                "C/L#0/i1={2,3}",
                "C/L#0/i2={3}",
                "X/L#0/exit={0,1,2,3}",
                "break/L#0/i0={0}",
                "break/L#0/i1={1}",
                "break/L#0/i2={2}",
                "break/L#0/i3={3}"
            ]);

        var continueAndTail = ImmutableArray.Create(
            Walk(0, Enter(LoopL), Observe("break"), Leave(LoopL), Observe("X")),
            Walk(
                1,
                Enter(LoopL),
                Next(LoopL, "C"),
                Observe("break"),
                Leave(LoopL),
                Observe("X")),
            Walk(
                2,
                Enter(LoopL),
                Observe("tail"),
                Next(LoopL, "C"),
                Observe("break"),
                Leave(LoopL),
                Observe("X")),
            Walk(
                3,
                Enter(LoopL),
                Observe("tail"),
                Next(LoopL, "C"),
                Observe("break"),
                Leave(LoopL),
                Observe("X")));

        AssertRun(
            "loop/continue-tail",
            continueAndTail,
            [
                "C/L#0/i0={1,2,3}",
                "X/L#0/exit={0,1,2,3}",
                "break/L#0/i0={0}",
                "break/L#0/i1={1,2,3}",
                "tail/L#0/i0={2,3}"
            ]);

        var nestedEscapes = ImmutableArray.Create(
            Walk(
                0,
                Enter(LoopL),
                Enter(SelectionS, "break"),
                Leave(LoopL),
                Observe("X")),
            Walk(
                1,
                Enter(LoopL),
                Enter(SelectionS, "continue"),
                Next(LoopL, "C"),
                Leave(LoopL),
                Observe("X")));
        AssertRun(
            "loop/nested-escapes",
            nestedEscapes,
            ["C/L#0/i0={1}", "X/L#0/exit={0,1}"]);
    }

    [Fact]
    public void CallsUseIncomingInstanceCallPathAndLoopIteration()
    {
        AssertRun(
            "call/early-return",
            CallSequence(CallK, 1),
            [
                "H/K#0>T#0/arm-body={1,3}",
                "after-call/K#0/return={0,1,2,3}"
            ]);
        AssertRun(
            "call/repeated",
            CallSequence(CallK, 2),
            [
                "H/K#0>T#0/arm-body={1,3}",
                "H/K#1>T#0/arm-body={1,3}",
                "after-call/K#0/return={0,1,2,3}",
                "after-call/K#1/return={0,1,2,3}"
            ]);

        var differentCallers = Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                [
                    .. HelperCall(CallK, lane),
                    .. HelperCall(CallJ, lane)
                ]))
            .ToImmutableArray();
        AssertRun(
            "call/distinct-call-paths",
            differentCallers,
            [
                "H/J#0>T#0/arm-body={1,3}",
                "H/K#0>T#0/arm-body={1,3}",
                "after-call/J#0/return={0,1,2,3}",
                "after-call/K#0/return={0,1,2,3}"
            ]);

        var callerContext = Enumerable.Range(0, 3)
            .Select(lane => Walk(
                lane,
                [
                    Enter(SelectionS, lane % 2 == 0 ? "even" : "odd"),
                    .. HelperCall(CallK, lane),
                    Leave(SelectionS),
                    Observe("after-caller")
                ]))
            .ToImmutableArray();
        AssertRun(
            "call/partial-divergent-caller",
            callerContext,
            [
                "H/S#0/arm-odd>K#0>T#0/arm-body={1}",
                "after-call/S#0/arm-even>K#0/return={0,2}",
                "after-call/S#0/arm-odd>K#0/return={1}",
                "after-caller/S#0/merge={0,1,2}"
            ]);

        var loopCalls = Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                [
                    Enter(LoopL),
                    .. HelperCall(CallK, lane),
                    Next(LoopL, "C"),
                    .. HelperCall(CallK, lane),
                    Leave(LoopL),
                    Observe("X")
                ]))
            .ToImmutableArray();
        AssertRun(
            "call/loop-iterations",
            loopCalls,
            [
                "C/L#0/i0={0,1,2,3}",
                "H/L#0/i0>K#0>T#0/arm-body={1,3}",
                "H/L#0/i1>K#0>T#0/arm-body={1,3}",
                "X/L#0/exit={0,1,2,3}",
                "after-call/L#0/i0>K#0/return={0,1,2,3}",
                "after-call/L#0/i1>K#0/return={0,1,2,3}"
            ]);
    }

    [Fact]
    public void SwitchPolicyIsFixedPerCompilationAndKeepsArmIdentitySeparate()
    {
        var walks = SwitchWalks([0, 0, 1, 9], [("case#0", "A"), ("case#1", "A"), ("default", "D")], 2);
        var split = SwitchPolicy.Create(
            ("W", "A", 0, "t0"),
            ("W", "A", 1, "t1"),
            ("W", "D", 9, "t2"));
        var union = SwitchPolicy.Create(
            ("W", "A", 0, "t0"),
            ("W", "A", 1, "t0"),
            ("W", "D", 9, "t1"));
        ImmutableArray<ImmutableArray<string>> allowed =
        [
            [
                "A/W#0/t0={0,1}",
                "A/W#0/t1={2}",
                "A/W#1/t0={0,1}",
                "A/W#1/t1={2}",
                "D/W#0/t2={3}",
                "D/W#1/t2={3}",
                "M/W#0/merge={0,1,2,3}",
                "M/W#1/merge={0,1,2,3}"
            ],
            [
                "A/W#0/t0={0,1,2}",
                "A/W#1/t0={0,1,2}",
                "D/W#0/t1={3}",
                "D/W#1/t1={3}",
                "M/W#0/merge={0,1,2,3}",
                "M/W#1/merge={0,1,2,3}"
            ]
        ];

        var splitResult = Run(walks, split);
        var unionResult = Run(walks, union);
        AssertAllowed("switch/shared-target/split", splitResult, allowed);
        AssertAllowed("switch/shared-target/union", unionResult, allowed);
        AssertSequence(allowed[0], splitResult.Observations);
        AssertSequence(allowed[1], unionResult.Observations);
        AssertSequence(
            [
                "lane0 W#0 selector=0 arm=case#0 target=A tangle=t0",
                "lane0 W#1 selector=0 arm=case#0 target=A tangle=t0",
                "lane1 W#0 selector=0 arm=case#0 target=A tangle=t0",
                "lane1 W#1 selector=0 arm=case#0 target=A tangle=t0",
                "lane2 W#0 selector=1 arm=case#1 target=A tangle=t1",
                "lane2 W#1 selector=1 arm=case#1 target=A tangle=t1",
                "lane3 W#0 selector=9 arm=default target=D tangle=t2",
                "lane3 W#1 selector=9 arm=default target=D tangle=t2"
            ],
            splitResult.Choices);
    }

    [Fact]
    public void DefaultAliasAllowsExactlyFiveSelectorGroupPartitions()
    {
        var walks = SwitchWalks([0, 0, 2, 9], [("case#0", "A"), ("default", "A"), ("default", "A")]);
        ImmutableArray<SwitchChoice> choices =
        [
            new(0, "case#0", "A"),
            new(0, "case#0", "A"),
            new(2, "default", "A"),
            new(9, "default", "A")
        ];
        ImmutableArray<ImmutableArray<ImmutableArray<int>>> partitions =
        [
            [[0, 1, 2, 3]],
            [[0, 1, 2], [3]],
            [[0, 1, 3], [2]],
            [[0, 1], [2, 3]],
            [[0, 1], [2], [3]]
        ];
        ImmutableArray<ImmutableArray<string>> allowed =
        [
            ["A/W#0/t0={0,1,2,3}", "M/W#0/merge={0,1,2,3}"],
            ["A/W#0/t0={0,1,2}", "A/W#0/t1={3}", "M/W#0/merge={0,1,2,3}"],
            ["A/W#0/t0={0,1,3}", "A/W#0/t1={2}", "M/W#0/merge={0,1,2,3}"],
            ["A/W#0/t0={0,1}", "A/W#0/t1={2,3}", "M/W#0/merge={0,1,2,3}"],
            ["A/W#0/t0={0,1}", "A/W#0/t1={2}", "A/W#0/t2={3}", "M/W#0/merge={0,1,2,3}"]
        ];

        foreach (var (partition, expected) in partitions.Zip(allowed))
        {
            var policy = SwitchPolicy.FromPartition("W", choices, partition);
            var result = Run(walks, policy);
            AssertAllowed("switch/default-alias", result, allowed);
            AssertSequence(expected, result.Observations);
        }

        Assert.Throws<ArgumentException>(() =>
            SwitchPolicy.FromPartition("W", choices, [[0, 2], [3]]));
        Assert.Throws<ArgumentException>(() =>
            SwitchPolicy.FromPartition("W", choices, [[0, 1, 2], [2, 3]]));
        Assert.Throws<ArgumentException>(() =>
            SwitchPolicy.FromPartition("W", choices, [[0], [1, 2, 3]]));
        Assert.Throws<ArgumentException>(() =>
            SwitchPolicy.FromPartition("W", choices, [[], [0, 1, 2, 3]]));
        Assert.Throws<ArgumentException>(() =>
            SwitchPolicy.FromPartition(
                "W",
                choices.SetItem(3, new SwitchChoice(9, "default", "D")),
                [[0, 1, 2, 3]]));
    }

    [Fact]
    public void OrdinaryBranchIntoCaseTargetIsNotPartOfSwitchActivation()
    {
        var walks = ImmutableArray.Create(
            Walk(
                0,
                Enter(SwitchW, new SwitchChoice(0, "case#0", "A")),
                Observe("A"),
                Leave(SwitchW),
                Observe("M")),
            Walk(
                1,
                Enter(SwitchW, new SwitchChoice(0, "case#0", "A")),
                Observe("A"),
                Leave(SwitchW),
                Observe("M")),
            Walk(2, Observe("A")),
            Walk(
                3,
                Enter(SwitchW, new SwitchChoice(9, "default", "D")),
                Observe("D"),
                Leave(SwitchW),
                Observe("M")));
        var policy = SwitchPolicy.Create(("W", "A", 0, "t0"), ("W", "D", 9, "t1"));

        AssertRun(
            "switch/ordinary-branch",
            walks,
            [
                "A/W#0/t0={0,1}",
                "A/root={2}",
                "D/W#0/t1={3}",
                "M/W#0/merge={0,1,3}"
            ],
            policy);
    }

    [Fact]
    public void ScalarProjectionCannotDetermineParticipationAndMutantsAreRejected()
    {
        var beforeExit = Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                [
                    new Event.Visit("entry"),
                    Enter(LoopL),
                    new Event.Visit("head"),
                    .. Enumerable.Range(0, lane).SelectMany(_ => new Event[]
                    {
                        Next(LoopL), new Event.Visit("head")
                    }),
                    new Event.Visit("P"),
                    Observe("P", "7"),
                    Leave(LoopL),
                    new Event.Visit("X")
                ]))
            .ToImmutableArray();
        var afterExit = Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                [
                    new Event.Visit("entry"),
                    Enter(LoopL),
                    new Event.Visit("head"),
                    .. Enumerable.Range(0, lane).SelectMany(_ => new Event[]
                    {
                        Next(LoopL), new Event.Visit("head")
                    }),
                    Leave(LoopL),
                    new Event.Visit("P"),
                    Observe("P", "7"),
                    new Event.Visit("X")
                ]))
            .ToImmutableArray();

        var beforeProjection = ProjectOperations(beforeExit);
        var afterProjection = ProjectOperations(afterExit);
        Assert.All(beforeProjection, projection => AssertSequence(["P=7"], projection));
        Assert.True(beforeProjection
            .Zip(afterProjection)
            .All(pair => pair.First.SequenceEqual(pair.Second)));
        ImmutableArray<string> scalarTraces =
        [
            "lane0 entry -> head -> P -> P=7 -> X",
            "lane1 entry -> head -> head -> P -> P=7 -> X",
            "lane2 entry -> head -> head -> head -> P -> P=7 -> X",
            "lane3 entry -> head -> head -> head -> head -> P -> P=7 -> X"
        ];
        AssertSequence(scalarTraces, ProjectControlAndOperations(beforeExit));
        AssertSequence(scalarTraces, ProjectControlAndOperations(afterExit));
        var beforeExpected = ImmutableArray.Create(
            "P/L#0/i0={0}",
            "P/L#0/i1={1}",
            "P/L#0/i2={2}",
            "P/L#0/i3={3}");
        var afterExpected = ImmutableArray.Create("P/L#0/exit={0,1,2,3}");
        AssertRun("scalar/before-exit", beforeExit, beforeExpected);
        AssertRun("scalar/after-exit", afterExit, afterExpected);

        var wrongEarly = ReduceByStaticSite(beforeExit);
        AssertSequence(["P={0,1,2,3}"], wrongEarly);
        Assert.False(beforeExpected.SequenceEqual(wrongEarly));

        var heterogeneous = ImmutableArray.Create(
            Walk(0, Observe("P", "3")),
            Walk(1, Observe("P", "5")),
            Walk(2, Observe("P", "7")),
            Walk(3, Observe("P", "11")));
        AssertRun("scalar/heterogeneous-result", heterogeneous, ["P/root={0,1,2,3}"]);
        AssertSequence(
            ["lane0 P=3", "lane1 P=5", "lane2 P=7", "lane3 P=11"],
            ProjectValues(heterogeneous));

        var diamond = Diamond([0, 1, 2, 3]);
        var wrongLate = Run(diamond, keepEscapedPartition: true).Observations;
        AssertSequence(
            [
                "A/S#0/arm-even={0,2}",
                "B/S#0/arm-odd={1,3}",
                "M/S#0/arm-even/merge={0,2}",
                "M/S#0/arm-odd/merge={1,3}"
            ],
            wrongLate);
        Assert.False(new[]
        {
            "A/S#0/arm-even={0,2}",
            "B/S#0/arm-odd={1,3}",
            "M/S#0/merge={0,1,2,3}"
        }.SequenceEqual(wrongLate));

        Write("mutant/early", wrongEarly);
        Write("mutant/late", wrongLate);
    }

    [Fact]
    public void ReducerRejectsMalformedEventShapes()
    {
        Assert.Throws<InvalidOperationException>(() => Run([Walk(0, new Event.Invalid())]));
        Assert.Throws<InvalidOperationException>(() =>
            Run([Walk(0, new Event.Enter(SelectionS, new Choice.Invalid()))]));
        Assert.Throws<InvalidOperationException>(() => Run([Walk(0, Enter(SwitchW))]));
        Assert.Throws<InvalidOperationException>(() => Run([Walk(0, Next(CallK))]));
    }

    private void AssertRun(
        string name,
        ImmutableArray<LaneWalk> walks,
        ImmutableArray<string> expected,
        SwitchPolicy? switchPolicy = null)
    {
        var actual = Run(walks, switchPolicy).Observations;
        Write(name, actual);
        AssertSequence(expected, actual);
    }

    private void AssertAllowed(
        string name,
        ReductionResult result,
        ImmutableArray<ImmutableArray<string>> allowed)
    {
        Write(name, result.Observations);
        Assert.True(IsAllowed(result.Observations, allowed));
    }

    private void Write(string name, ImmutableArray<string> observations)
    {
        output.WriteLine($"ACTUAL MODEL {name}");
        foreach (var observation in observations)
        {
            output.WriteLine(observation);
        }
    }

    private static bool IsAllowed(
        ImmutableArray<string> candidate,
        ImmutableArray<ImmutableArray<string>> allowed) =>
        allowed.Any(expected => expected.SequenceEqual(candidate));

    private static void AssertSequence(
        IEnumerable<string> expected,
        ImmutableArray<string> actual) =>
        Assert.True(
            expected.SequenceEqual(actual),
            $"Expected: [{string.Join(", ", expected)}]{Environment.NewLine}" +
            $"Actual:   [{string.Join(", ", actual)}]");

    private static ImmutableArray<LaneWalk> Diamond(ImmutableArray<int> lanes) =>
        lanes.Select(lane => Walk(
                lane,
                Enter(SelectionS, lane % 2 == 0 ? "even" : "odd"),
                Observe(lane % 2 == 0 ? "A" : "B"),
                Leave(SelectionS),
                Observe("M")))
            .ToImmutableArray();

    private static ImmutableArray<LaneWalk> CallSequence(Scope call, int count) =>
        Enumerable.Range(0, 4)
            .Select(lane => Walk(
                lane,
                Enumerable.Range(0, count)
                    .SelectMany(_ => HelperCall(call, lane))
                    .ToArray()))
            .ToImmutableArray();

    private static Event[] HelperCall(Scope call, int lane) =>
    [
        Enter(call),
        Enter(HelperSelectionT, lane % 2 == 0 ? "return" : "body"),
        .. OddObservation(lane, "H"),
        Leave(call),
        Observe("after-call")
    ];

    private static Event[] OddObservation(int lane, string site) =>
        lane % 2 == 0 ? [] : [Observe(site)];

    private static ImmutableArray<LaneWalk> SwitchWalks(
        ImmutableArray<int> selectors,
        ImmutableArray<(string Arm, string Target)> choices,
        int activations = 1) =>
        selectors.Select((selector, lane) =>
        {
            var choice = selector switch
            {
                0 => choices[0],
                1 or 2 => choices[1],
                _ => choices[2]
            };
            return Walk(
                lane,
                Enumerable.Range(0, activations)
                    .SelectMany(_ => (Event[])
                    [
                        Enter(SwitchW, new SwitchChoice(selector, choice.Arm, choice.Target)),
                        Observe(choice.Target),
                        Leave(SwitchW),
                        Observe("M")
                    ])
                    .ToArray());
        }).ToImmutableArray();

    private static ImmutableArray<ImmutableArray<string>> ProjectOperations(
        ImmutableArray<LaneWalk> walks) =>
        walks.OrderBy(walk => walk.Lane)
            .Select(walk => walk.Events
                .OfType<Event.Observe>()
                .Select(observation => $"{observation.Site}={observation.Result}")
                .ToImmutableArray())
            .ToImmutableArray();

    private static ImmutableArray<string> ProjectValues(ImmutableArray<LaneWalk> walks) =>
        walks.OrderBy(walk => walk.Lane)
            .SelectMany(walk => walk.Events
                .OfType<Event.Observe>()
                .Select(observation => $"lane{walk.Lane} {observation.Display}"))
            .ToImmutableArray();

    private static ImmutableArray<string> ProjectControlAndOperations(ImmutableArray<LaneWalk> walks) =>
        walks.OrderBy(walk => walk.Lane)
            .Select(walk => $"lane{walk.Lane} " + string.Join(" -> ", walk.Events
                .Where(@event => @event is Event.Visit or Event.Observe)
                .Select(@event => @event switch
                {
                    Event.Visit visit => visit.Label,
                    Event.Observe observe => observe.Display,
                    _ => throw new InvalidOperationException("Not a scalar trace event.")
                })))
            .ToImmutableArray();

    private static ImmutableArray<string> ReduceByStaticSite(ImmutableArray<LaneWalk> walks) =>
        walks.SelectMany(walk => walk.Events
                .OfType<Event.Observe>()
                .Select(observation => (walk.Lane, Key: observation.Site)))
            .GroupBy(item => item.Key)
            .Select(group => $"{group.Key}={{{string.Join(",", group.Select(item => item.Lane).Order())}}}")
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

    private static ReductionResult Run(
        ImmutableArray<LaneWalk> walks,
        SwitchPolicy? switchPolicy = null,
        bool keepEscapedPartition = false)
    {
        var observations = new Dictionary<ObservationKey, List<int>>();
        var choices = new List<string>();

        foreach (var walk in walks.OrderBy(walk => walk.Lane))
        {
            var stack = new List<Frame>();
            var activations = new Dictionary<string, int>();
            var occurrences = new Dictionary<string, int>();
            // A Leave models only its immediately following merge/return observation.
            Frame? pending = null;
            var terminated = false;

            foreach (var @event in walk.Events)
            {
                if (terminated)
                {
                    throw new InvalidOperationException($"Lane {walk.Lane} has events after entry return.");
                }

                switch (@event)
                {
                    case Event.Enter enter:
                        {
                            if (pending is not null)
                            {
                                throw new InvalidOperationException("A declared merge observation must follow Leave.");
                            }

                            var parent = Context(stack, null);
                            var counterKey = $"{parent}|{enter.Scope.Kind}:{enter.Scope.Id}";
                            var activation = activations.GetValueOrDefault(counterKey);
                            activations[counterKey] = activation + 1;
                            var frame = new Frame(
                                enter.Scope,
                                activation,
                                enter.Scope.Kind switch
                                {
                                    ScopeKind.Loop => 0,
                                    ScopeKind.Selection or ScopeKind.Call or ScopeKind.Switch => null,
                                    ScopeKind.Entry => throw new InvalidOperationException(
                                        "Entry is exited directly, not entered as a nested scope."),
                                    _ => throw new InvalidOperationException(
                                        $"Unexpected scope kind {enter.Scope.Kind}.")
                                });

                            switch (enter.Choice)
                            {
                                case null when enter.Scope.Kind is ScopeKind.Selection or ScopeKind.Switch:
                                    throw new InvalidOperationException(
                                        $"{enter.Scope.Kind} scope {enter.Scope.Id} requires a choice.");
                                case null:
                                    break;
                                case Choice.Branch branch when enter.Scope.Kind == ScopeKind.Selection:
                                    frame = frame with { Partition = $"arm-{branch.Arm}" };
                                    break;
                                case Choice.Switch { Value: var choice }
                                    when enter.Scope.Kind == ScopeKind.Switch:
                                    {
                                        var tangle = (switchPolicy ??
                                                      throw new InvalidOperationException(
                                                          "Switch policy is required."))
                                            .Partition(
                                                enter.Scope.Id,
                                                choice.Target,
                                                choice.Selector);
                                        frame = frame with { Partition = tangle };
                                        choices.Add(
                                            $"lane{walk.Lane} {enter.Scope.Id}#{activation} " +
                                            $"selector={choice.Selector} arm={choice.Arm} " +
                                            $"target={choice.Target} tangle={tangle}");
                                        break;
                                    }
                                case Choice.Branch or Choice.Switch:
                                    throw new InvalidOperationException(
                                        $"Choice does not match {enter.Scope.Kind} scope {enter.Scope.Id}.");
                                default:
                                    throw new InvalidOperationException(
                                        $"Unexpected choice event {enter.Choice.GetType().Name}.");
                            }

                            stack.Add(frame);
                            break;
                        }
                    case Event.Next next:
                        {
                            if (next.Loop.Kind != ScopeKind.Loop)
                            {
                                throw new InvalidOperationException(
                                    $"Next requires a loop scope, not {next.Loop.Kind}.");
                            }

                            var index = FindFrame(stack, next.Loop);
                            stack.RemoveRange(index + 1, stack.Count - index - 1);
                            if (next.Site is not null)
                            {
                                AddObservation(
                                    observations,
                                    occurrences,
                                    walk.Lane,
                                    new Event.Observe(next.Site, string.Empty),
                                    stack,
                                    pending);
                            }

                            stack[index] = stack[index] with { Iteration = stack[index].Iteration + 1 };
                            pending = null;
                            break;
                        }
                    case Event.Leave leave when leave.Scope.Kind == ScopeKind.Entry:
                        stack.Clear();
                        pending = null;
                        terminated = true;
                        break;
                    case Event.Leave leave:
                        {
                            var index = FindFrame(stack, leave.Scope);
                            var frame = stack[index];
                            stack.RemoveRange(index, stack.Count - index);
                            pending = leave.Scope.Kind switch
                            {
                                ScopeKind.Loop => frame with { Iteration = null, Partition = "exit" },
                                ScopeKind.Call => frame with { Partition = "return" },
                                ScopeKind.Selection or ScopeKind.Switch when keepEscapedPartition =>
                                    frame with
                                    {
                                        Partition = $"{frame.Partition}/merge"
                                    },
                                ScopeKind.Selection or ScopeKind.Switch =>
                                    frame with { Partition = "merge" },
                                ScopeKind.Entry => throw new InvalidOperationException(
                                    "Entry leave must use the direct return path."),
                                _ => throw new InvalidOperationException(
                                    $"Unexpected scope kind {leave.Scope.Kind}.")
                            };
                            break;
                        }
                    case Event.Observe observe:
                        AddObservation(observations, occurrences, walk.Lane, observe, stack, pending);
                        pending = null;
                        break;
                    case Event.Visit:
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unexpected event {@event.GetType().Name}.");
                }
            }
        }

        return new(
            observations.Select(pair =>
                    $"{pair.Key.Display}={{{string.Join(",", pair.Value.Order())}}}")
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            choices.Order(StringComparer.Ordinal).ToImmutableArray());
    }

    private static void AddObservation(
        Dictionary<ObservationKey, List<int>> observations,
        Dictionary<string, int> occurrences,
        int lane,
        Event.Observe observe,
        List<Frame> stack,
        Frame? pending)
    {
        var context = Context(stack, pending);
        var occurrenceKey = $"{observe.Site}/{context}";
        var occurrence = occurrences.GetValueOrDefault(occurrenceKey);
        occurrences[occurrenceKey] = occurrence + 1;
        var key = new ObservationKey(observe.Site, context, occurrence);
        if (!observations.TryGetValue(key, out var participants))
        {
            participants = [];
            observations.Add(key, participants);
        }

        participants.Add(lane);
    }

    private static int FindFrame(List<Frame> stack, Scope scope)
    {
        var index = stack.FindLastIndex(frame => frame.Scope == scope);
        return index >= 0
            ? index
            : throw new InvalidOperationException($"Scope {scope.Id} is not active.");
    }

    private static string Context(List<Frame> stack, Frame? pending)
    {
        IEnumerable<Frame> frames = pending is null ? stack : stack.Append(pending);
        var context = string.Join(">", frames.Select(FrameName));
        return context.Length == 0 ? "root" : context;
    }

    private static string FrameName(Frame frame)
    {
        var name = $"{frame.Scope.Id}#{frame.Activation}";
        if (frame.Iteration is { } iteration)
        {
            name += $"/i{iteration}";
        }

        return frame.Partition is { } partition ? $"{name}/{partition}" : name;
    }

    private static LaneWalk Walk(int lane, params Event[] events) => new(lane, [.. events]);

    private static Event Enter(Scope scope) => new Event.Enter(scope, null);

    private static Event Enter(Scope scope, string arm) =>
        new Event.Enter(scope, new Choice.Branch(arm));

    private static Event Enter(Scope scope, SwitchChoice choice) =>
        new Event.Enter(scope, new Choice.Switch(choice));

    private static Event Next(Scope loop, string? site = null) => new Event.Next(loop, site);

    private static Event Leave(Scope scope) => new Event.Leave(scope);

    private static Event Observe(string site, string result = "") => new Event.Observe(site, result);

    private enum ScopeKind
    {
        Entry,
        Selection,
        Loop,
        Call,
        Switch
    }

    private sealed record Scope(ScopeKind Kind, string Id);

    private sealed record SwitchChoice(int Selector, string Arm, string Target);

    private sealed record LaneWalk(int Lane, ImmutableArray<Event> Events);

    private abstract record Choice
    {
        public sealed record Branch(string Arm) : Choice;

        public sealed record Switch(SwitchChoice Value) : Choice;

        public sealed record Invalid : Choice;
    }

    private abstract record Event
    {
        public sealed record Enter(Scope Scope, Choice? Choice) : Event;

        public sealed record Next(Scope Loop, string? Site) : Event;

        public sealed record Leave(Scope Scope) : Event;

        public sealed record Observe(string Site, string Result) : Event
        {
            public string Display => Result.Length == 0 ? Site : $"{Site}={Result}";
        }

        public sealed record Visit(string Label) : Event;

        public sealed record Invalid : Event;
    }

    private sealed record Frame(
        Scope Scope,
        int Activation,
        int? Iteration = null,
        string? Partition = null);

    private sealed record ObservationKey(string Site, string Context, int Occurrence)
    {
        public string Display => Occurrence == 0 ? $"{Site}/{Context}" : $"{Site}~{Occurrence}/{Context}";
    }

    private sealed record ReductionResult(
        ImmutableArray<string> Observations,
        ImmutableArray<string> Choices);

    private sealed class SwitchPolicy
    {
        private readonly ImmutableDictionary<(string Scope, string Target, int Selector), string> partitions;

        private SwitchPolicy(
            ImmutableDictionary<(string Scope, string Target, int Selector), string> partitions)
        {
            this.partitions = partitions;
        }

        public static SwitchPolicy Create(
            params (string Scope, string Target, int Selector, string Tangle)[] entries)
        {
            var crossTarget = entries.GroupBy(entry => (entry.Scope, entry.Tangle))
                .FirstOrDefault(group => group.Select(entry => entry.Target).Distinct().Skip(1).Any());
            if (crossTarget is not null)
            {
                throw new ArgumentException(
                    $"Tangle {crossTarget.Key.Tangle} crosses switch targets.",
                    nameof(entries));
            }

            return new(entries.ToImmutableDictionary(
                entry => (entry.Scope, entry.Target, entry.Selector),
                entry => entry.Tangle));
        }

        public static SwitchPolicy FromPartition(
            string scope,
            ImmutableArray<SwitchChoice> choices,
            ImmutableArray<ImmutableArray<int>> groups)
        {
            if (groups.Any(group => group.IsEmpty) ||
                !groups.SelectMany(group => group).Order().SequenceEqual(Enumerable.Range(0, choices.Length)))
            {
                throw new ArgumentException("Partition must contain each incoming lane exactly once.", nameof(groups));
            }

            var entries = groups.SelectMany((group, index) => group.Select(lane =>
                (Scope: scope, choices[lane].Target, choices[lane].Selector, Tangle: $"t{index}")))
                .Distinct()
                .ToArray();
            if (entries.GroupBy(entry => entry.Selector).Any(group => group.Count() != 1))
            {
                throw new ArgumentException("Equal switch selectors must remain together.", nameof(groups));
            }

            return Create(entries);
        }

        public string Partition(string scope, string target, int selector) =>
            partitions.TryGetValue((scope, target, selector), out var partition)
                ? partition
                : throw new InvalidOperationException(
                    $"No switch partition for {scope}, target {target}, selector {selector}.");
    }
}

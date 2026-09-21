using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using Xunit.Abstractions;
using static DualDrill.CLSL.Test.ScalarControlFlowOracle;
using static DualDrill.CLSL.Test.RegionFixture;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class SlangEmitterLoopOwnershipTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OrdinaryLoopExitKeepsRealExitAndMayDivergeFact()
    {
        var method = ((Func<int, int>)OrdinaryLoopExit).Method;
        var facts = CompilerTestPipeline.ControlFacts(method);
        var source = Emit(method);
        await new SlangService().ValidateAsync(source);

        Assert.Contains(
            facts.Graph.Labels(),
            label => facts.Graph[label].Annotation.PostDominance is ExitPostDominance.Block
            {
                MayDiverge: true
            });
        Assert.DoesNotContain(
            facts.Graph.Labels(),
            label => facts.Graph[label].Annotation.PostDominance is ExitPostDominance.NoExitPath);
        Assert.Contains("while(true)", source);
        Assert.Contains("return ", source);
        WriteActualCompilerOutput(nameof(OrdinaryLoopExit), facts, source);
    }

    [Fact]
    public async Task AllDivergingLoopHasNoExitPathAndEmitsNoReturn()
    {
        var method = ((Func<bool, int>)AllDiverging).Method;
        var facts = CompilerTestPipeline.ControlFacts(method);
        var source = Emit(method);
        await new SlangService().ValidateAsync(source);

        Assert.All(
            facts.Graph.Labels(),
            label => Assert.Same(
                ExitPostDominance.NoExitPath.Instance,
                facts.Graph[label].Annotation.PostDominance));
        Assert.Contains("while(true)", source);
        Assert.DoesNotContain("return ", source);
        WriteActualCompilerOutput(nameof(AllDiverging), facts, source);
    }

    [Fact]
    public async Task MixedReturnAndDivergenceEmitsLoopArmThenRealReturn()
    {
        var method = ((Func<bool, int>)MixedReturnDivergence).Method;
        var facts = CompilerTestPipeline.ControlFacts(method);
        var source = Emit(method);
        await new SlangService().ValidateAsync(source);

        Assert.Contains(
            facts.Graph.Labels(),
            label => facts.Graph[label].Annotation.PostDominance is ExitPostDominance.NoExitPath);
        Assert.Contains(
            facts.Graph.Labels(),
            label => facts.Graph[label].Annotation.PostDominance is ExitPostDominance.FunctionExit
            {
                MayDiverge: false
            });
        Assert.Contains(
            facts.Graph.Labels(),
            label => facts.Graph[label].Annotation.PostDominance is ExitPostDominance.Block
            {
                MayDiverge: true
            });
        Assert.Contains("if(", source);
        Assert.Contains("while(true)", source);
        Assert.Contains("return ", source);
        WriteActualCompilerOutput(nameof(MixedReturnDivergence), facts, source);
    }

    [Fact]
    public void MixedReturnAndDivergencePreservesReturnTraceAndDivergingBudgets()
    {
        var method = ((Func<bool, int>)MixedReturnDivergence).Method;
        var original = CompilerTestPipeline.CompileBody(method);
        var lowered = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(original));
        var source = Emit(lowered);
        var returning = ImmutableArray.Create<Value>(new Value.Boolean(true));
        var originalResult = RunCfg(original, returning);
        var loweredResult = RunCfg(lowered, returning);
        var emittedResult = new EmittedScalarProgram(lowered, source).Run(returning);

        Assert.Equal(new Value.Integer(7), originalResult.Result);
        Assert.True(originalResult.Trace.SequenceEqual(loweredResult.Trace));
        Assert.True(originalResult.Trace.SequenceEqual(emittedResult.Trace));
        Assert.Equal(originalResult.Result, loweredResult.Result);
        Assert.Equal(originalResult.Result, emittedResult.Result);

        var diverging = ImmutableArray.Create<Value>(new Value.Boolean(false));
        var originalFailure = Assert.Throws<InvalidOperationException>(
            () => RunCfg(original, diverging, 100));
        var loweredFailure = Assert.Throws<InvalidOperationException>(
            () => RunCfg(lowered, diverging, 100));
        var emittedFailure = Assert.Throws<InvalidOperationException>(
            () => new EmittedScalarProgram(lowered, source).Run(diverging, 100));
        Assert.Contains("step budget", originalFailure.Message);
        Assert.Contains("step budget", loweredFailure.Message);
        Assert.Contains("step budget", emittedFailure.Message);

        output.WriteLine($"return trace: {string.Join(" -> ", originalResult.Trace)}");
        output.WriteLine(originalFailure.Message);
        output.WriteLine(loweredFailure.Message);
        output.WriteLine(emittedFailure.Message);
    }

    [Fact]
    public void OrdinaryLoopExitCompilesThroughPublicWgslApi()
    {
        var shader = new OrdinaryLoopShader();
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);

        output.WriteLine("=== ordinary loop exit: actual Slang ===");
        output.WriteLine(slang);
        output.WriteLine("=== ordinary loop exit: actual public WGSL ===");
        output.WriteLine(wgsl);
        Assert.Contains("while(true)", slang);
        Assert.Contains("return ", slang);
        Assert.Contains("fn Ordinary", wgsl);
        Assert.Contains("return", wgsl);
    }

    [Fact]
    public void PublicWgslRejectsNoExitPathsWhileIrAndSlangRemainAvailable()
    {
        ISharpShader[] shaders = [new MixedDivergenceShader(), new AllDivergingShader()];
        foreach (var shader in shaders)
        {
            var ir = new CLSLCompiler(new(CLSLCompileTarget.IR)).Emit(shader);
            var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
            var error = Assert.Throws<NotSupportedException>(() =>
                new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader));

            Assert.Contains("no-exit-path", ir);
            Assert.Contains("while(true)", slang);
            Assert.Contains("block ", error.Message);
            Assert.Contains("WGSL output does not support", error.Message);
            Assert.Contains("no finite exit path", error.Message);
            output.WriteLine($"=== {shader.GetType().Name}: actual public WGSL diagnostic ===");
            output.WriteLine(error.Message);
        }
    }

    [Fact]
    public async Task NestedBreakAndContinueRetestTheOuterHeader()
    {
        var source = Emit(((Func<int, int, int>)NestedLoopControl).Method);

        AssertLexicalUnwind(source, 2);
        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public async Task ThreeNestedLoopsUnwindOneOwnerAtATime()
    {
        var source = Emit(((Func<int, int>)ThreeNestedLoops).Method);

        AssertLexicalUnwind(source, 3);
        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public async Task EarlyReturnDoesNotEraseNormalOuterContinuation()
    {
        var source = Emit(((Func<int, int, int, int>)NestedEarlyReturn).Method);

        Assert.Contains("return ", source);
        AssertLexicalUnwind(source, 2);
        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public async Task WgslReturnCarrierPreservesMultipleSitesValuesEffectsAndTrace()
    {
        var method = ((Func<int, int, int, int>)MultiSiteReturnControl).Method;
        var original = CompilerTestPipeline.CompileBody(method);
        var lowered = new StablePointerRegionParameterPass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(original));
        var native = Emit(lowered);
        var target = Target(lowered, SlangControlFlowPolicy.WgslCompatible);
        var compatible = new SlangEmitter(target).Emit();

        var nativeHasNestedReturn = ReturnIndices(native).Any(returned =>
            LoopScopes(native).Any(scope => scope.Start < returned && returned < scope.End));
        Assert.All(ReturnIndices(compatible), returned =>
            Assert.DoesNotContain(
                LoopScopes(compatible),
                scope => scope.Start < returned && returned < scope.End));
        Assert.DoesNotContain("_return_value", native);
        Assert.Equal(nativeHasNestedReturn, compatible.Contains("_return_value", StringComparison.Ordinal));
        await new SlangService().ValidateAsync(compatible);

        ImmutableArray<ImmutableArray<Value>> cases =
        [
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(0)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(1)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(2)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(3)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(4)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(5)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(6)],
            [new Value.Integer(3), new Value.Integer(3), new Value.Integer(7)]
        ];
        foreach (var arguments in cases)
        {
            var expected = RunCfg(original, arguments);
            var normalized = RunCfg(lowered, arguments);
            var emittedNative = new EmittedScalarProgram(lowered, native).Run(arguments);
            var emitted = new EmittedScalarProgram(lowered, compatible).Run(arguments);

            Assert.Equal(expected.Result, normalized.Result);
            Assert.Equal(expected.Result, emittedNative.Result);
            Assert.Equal(expected.Result, emitted.Result);
            Assert.True(expected.Trace.SequenceEqual(normalized.Trace));
            Assert.True(expected.Trace.SequenceEqual(emittedNative.Trace));
            Assert.True(expected.Trace.SequenceEqual(emitted.Trace));
        }
    }

    [Fact]
    public async Task WgslReturnCarrierHandlesExplicitLoopNestedReturn()
    {
        var loop = Label.Create("loop");
        var declaration = new FunctionDeclaration(
            "ExplicitLoopReturn",
            [],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var body = CreateFunctionBody(
            declaration,
            RegionTree.Loop(
                loop,
                [],
                Body(
                    loop,
                    [],
                    [],
                    Terminator.B.ReturnExpr<RegionJump<IShaderValue>, IShaderValue>(
                        ShaderValue.Literal(new I32Literal(7)))),
                null,
                null));
        var native = Emit(body);
        var compatible = Emit(body, SlangControlFlowPolicy.WgslCompatible);

        Assert.Contains(ReturnIndices(native), returned =>
            LoopScopes(native).Any(scope => scope.Start < returned && returned < scope.End));
        Assert.All(ReturnIndices(compatible), returned =>
            Assert.DoesNotContain(
                LoopScopes(compatible),
                scope => scope.Start < returned && returned < scope.End));
        Assert.Contains("_return_value", compatible);
        await new SlangService().ValidateAsync(compatible);

        var expected = RunCfg(body, []);
        var emitted = new EmittedScalarProgram(body, compatible).Run([]);
        Assert.Equal(expected.Result, emitted.Result);
        Assert.True(expected.Trace.SequenceEqual(emitted.Trace));
    }

    [Fact]
    public void MultipleSourcesMayShareOneNormalTransfer()
    {
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = new FunctionDeclaration("SharedNormalTransfer", [],
            new FunctionReturn(ShaderType.Unit, []), []);
        var outerBody = Body(
            outer, [], [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(inner, [])));
        var innerBody = Body(inner, [], [],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition, new RegionJump<IShaderValue>(left, []), new RegionJump<IShaderValue>(right, [])));
        var leftBody = Body(
            left, [], [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(outer, [])));
        var rightBody = Body(
            right, [], [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(outer, [])));
        var body = CreateFunctionBody(declaration,
            RegionTree.Loop(outer,
            [
                RegionTree.Loop(inner,
                [
                    RegionTree.Block(left, [], leftBody, null),
                    RegionTree.Block(right, [], rightBody, null)
                ], innerBody, null, null)
            ], outerBody, null, null));

        var source = Emit(body);

        Assert.True(source.Split("break;").Length - 1 >= 2);
        Assert.Contains("continue;", source);
    }

    [Fact]
    public async Task MultipleScopedExitTargetsLowerWithoutAOneExitLimit()
    {
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var exit = Label.Create("exit");
        var terminal = Label.Create("terminal");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = new FunctionDeclaration("MultipleNormalTargets", [],
            new FunctionReturn(ShaderType.Unit, []), []);
        var outerBody = Body(
            outer, [], [],
            Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(inner, [])));
        var innerBody = Body(inner, [], [],
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition, new RegionJump<IShaderValue>(outer, []), new RegionJump<IShaderValue>(exit, [])));
        var exitBody =
            Body(
                exit, [], [],
                Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(terminal, [])));
        var terminalBody = Body(
            terminal, [], [],
            Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>());
        var body = CreateFunctionBody(declaration,
            RegionTree.Loop(outer,
            [
                RegionTree.Block(terminal, [], terminalBody, null),
                RegionTree.Block(exit, [], exitBody, null),
                RegionTree.Loop(inner, [], innerBody, null, null)
            ], outerBody, null, null));

        var source = Emit(body);

        Assert.Contains("return;", source);
        Assert.Contains("continue;", source);
        Assert.Contains("break;", source);
        await new SlangService().ValidateAsync(source);
    }

    private static string Emit(MethodInfo method)
    {
        var body = CompilerTestPipeline.CompileBody(method);
        body = new FunctionToOperationPass().VisitFunctionBody(body);
        body = new StablePointerRegionParameterPass().VisitFunctionBody(body);
        return Emit(body);
    }

    private static string Emit(
        RegionFunctionBody body,
        SlangControlFlowPolicy controlFlowPolicy = SlangControlFlowPolicy.Native) =>
        new SlangEmitter(Target(body, controlFlowPolicy))
        .Emit();

    private static ShaderModuleDeclaration<SlangFunctionBody> Target(
        RegionFunctionBody body,
        SlangControlFlowPolicy controlFlowPolicy) =>
        new SlangTargetLowering().Lower(
            new ShaderModuleDeclaration<RegionFunctionBody>(
                [body.Declaration],
                ImmutableDictionary<FunctionDeclaration, RegionFunctionBody>.Empty.Add(
                    body.Declaration,
                    body)),
            controlFlowPolicy);

    private void WriteActualCompilerOutput(
        string name,
        CilValueControlFactsBody facts,
        string slang)
    {
        output.WriteLine($"=== {name}: actual CIL ===");
        output.WriteLine(facts.Source.Source.Source.Source.RawCode.PrettyPrint());
        output.WriteLine($"=== {name}: actual control facts ===");
        output.WriteLine(facts.Graph.PrettyPrint());
        output.WriteLine($"=== {name}: actual Slang ===");
        output.WriteLine(slang);
    }

    private static void AssertLexicalUnwind(string source, int depth)
    {
        var loops = LoopScopes(source);
        Assert.True(loops.Length >= depth);
        Assert.DoesNotContain("Invalid duplicate label", source);
        Assert.Contains("break;", source);
        Assert.Contains("continue;", source);
    }

    private static ImmutableArray<TextScope> LoopScopes(string source)
    {
        const string loop = "while(true)";
        var scopes = ImmutableArray.CreateBuilder<TextScope>();
        for (var start = source.IndexOf(loop, StringComparison.Ordinal);
             start >= 0;
             start = source.IndexOf(loop, start + loop.Length, StringComparison.Ordinal))
        {
            var open = source.IndexOf('{', start + loop.Length);
            scopes.Add(new(start, MatchingBrace(source, open)));
        }

        return scopes.ToImmutable();
    }

    private static IEnumerable<int> ReturnIndices(string source)
    {
        const string returned = "return ";
        for (var index = source.IndexOf(returned, StringComparison.Ordinal);
             index >= 0;
             index = source.IndexOf(returned, index + returned.Length, StringComparison.Ordinal))
            yield return index;
    }

    private static IEnumerable<TextScope> BraceScopes(string source)
    {
        var openings = new Stack<int>();
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '{')
                openings.Push(i);
            else if (source[i] == '}')
                yield return new(openings.Pop(), i);
        }
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return i;
        }

        throw new InvalidOperationException("Unmatched loop scope.");
    }

    private static int NestedLoopControl(int outer, int inner)
    {
        var result = 1;
        for (var i = 0; i < outer; i++)
        {
            for (var j = 0; j < inner; j++)
            {
                if (j == 1)
                    continue;
                if (j == 4)
                    break;
                result = result * 3 + i + j;
            }

            result = result * 5 + i;
        }

        return result + 17;
    }

    private static int ThreeNestedLoops(int count)
    {
        var result = 0;
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                for (var k = 0; k < count; k++)
                    result += i + j + k;
                result += 5;
            }

            result += 7;
        }

        return result + 11;
    }

    private static int NestedEarlyReturn(int outer, int inner, int stop)
    {
        var result = 1;
        for (var i = 0; i < outer; i++)
        {
            for (var j = 0; j < inner; j++)
            {
                if (j == stop)
                    return result + 1000;
                result = result * 3 + i + j;
            }

            result = result * 5 + i;
        }

        return result + 17;
    }

    private static int MultiSiteReturnControl(int outer, int inner, int path)
    {
        if (path == 0)
            return 10;

        var result = 1;
        for (var i = 0; i < outer; i++)
        {
            if (path == 1 && i == 1)
                return result + 100;

            for (var j = 0; j < inner; j++)
            {
                if (path == 2 && j == 1)
                    return result + 200;

                switch (path)
                {
                    case 3:
                        if (i == 1 && j == 0)
                            return result + 300;
                        break;
                    case 4:
                        if (j == 0)
                            continue;
                        break;
                }

                if (path == 5 && j == 1)
                    break;
                result = result * 3 + i + j;
            }

            if (path == 6 && i == 1)
                break;
            result = result * 5 + i;
        }

        return result + 400;
    }

    private static int OrdinaryLoopExit(int count)
    {
        while (count > 0)
            count--;
        return count;
    }

    private static int AllDiverging(bool choose)
    {
        while (true)
            choose = !choose;
    }

    private static int MixedReturnDivergence(bool shouldReturn)
    {
        if (shouldReturn)
            return 7;
        while (true)
            shouldReturn = !shouldReturn;
    }

    private sealed class OrdinaryLoopShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int Ordinary([Location(0)] int count) => OrdinaryLoopExit(count);
    }

    private sealed class MixedDivergenceShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int Mixed([Location(0)] int value) =>
            MixedReturnDivergence(value > 0);
    }

    private sealed class AllDivergingShader : ISharpShader
    {
        [Fragment]
        [return: Location(0)]
        public static int AllDivergingEntry([Location(0)] int value) =>
            AllDiverging(value > 0);
    }

    private readonly record struct TextScope(int Start, int End);
}

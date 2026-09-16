using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class SlangEmitterLoopOwnershipTests
{
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
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var earlyReturn = Label.Create("earlyReturn");
        var normal = Label.Create("normal");
        var terminal = Label.Create("terminal");
        var condition = new ParameterDeclaration("condition", ShaderType.Bool, []);
        var declaration = new FunctionDeclaration("NestedEarlyReturn", [condition],
            new FunctionReturn(ShaderType.I32, []), []);
        var outerBody = Body(outer, Terminator.B.Br<RegionJump, IShaderValue>(new(inner, [])), null);
        var innerBody = Body(inner,
            Terminator.B.BrIf<RegionJump, IShaderValue>(
                condition.Value, new RegionJump(earlyReturn, []), new RegionJump(normal, [])), null);
        var earlyReturnBody = Body(
            earlyReturn, Terminator.B.Br<RegionJump, IShaderValue>(new(terminal, [])), terminal);
        var normalBody = Body(normal, Terminator.B.Br<RegionJump, IShaderValue>(new(outer, [])), outer);
        var terminalBody = Body(terminal,
            Terminator.B.ReturnExpr<RegionJump, IShaderValue>(ShaderValue.Literal(new I32Literal(0))), null);
        var body = new FunctionBody4(declaration,
            RegionTree.Loop(outer,
            [
                RegionTree.Loop(inner,
                [
                    RegionTree.Block(earlyReturn, [], earlyReturnBody, null),
                    RegionTree.Block(normal, [], normalBody, null)
                ], innerBody, null, null),
                RegionTree.Block(terminal, [], terminalBody, null)
            ], outerBody, null, null));

        var source = Emit(body);
        var innerLoop = LoopScopes(source)[1];
        var innerSource = source[innerLoop.Start..(innerLoop.End + 1)];

        Assert.Contains("return 0;", innerSource);
        AssertLexicalUnwind(source, 2);
        await new SlangService().ValidateAsync(source);
    }

    [Fact]
    public void MultipleSourcesMayShareOneNormalTransfer()
    {
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var left = Label.Create("left");
        var right = Label.Create("right");
        var condition = new ParameterDeclaration("condition", ShaderType.Bool, []);
        var declaration = new FunctionDeclaration("SharedNormalTransfer", [condition],
            new FunctionReturn(ShaderType.Unit, []), []);
        var outerBody = Body(outer, Terminator.B.Br<RegionJump, IShaderValue>(new(inner, [])), null);
        var innerBody = Body(inner,
            Terminator.B.BrIf<RegionJump, IShaderValue>(
                condition.Value, new RegionJump(left, []), new RegionJump(right, [])), null);
        var leftBody = Body(left, Terminator.B.Br<RegionJump, IShaderValue>(new(outer, [])), outer);
        var rightBody = Body(right, Terminator.B.Br<RegionJump, IShaderValue>(new(outer, [])), outer);
        var body = new FunctionBody4(declaration,
            RegionTree.Loop(outer,
            [
                RegionTree.Loop(inner,
                [
                    RegionTree.Block(left, [], leftBody, null),
                    RegionTree.Block(right, [], rightBody, null)
                ], innerBody, null, null)
            ], outerBody, null, null));

        var source = Emit(body);

        Assert.Equal(2, source.Split("break;").Length - 1);
        Assert.Contains("continue;", source);
    }

    [Fact]
    public void MultipleNormalTargetsReportFunctionHeaderSourcesAndTargets()
    {
        var outer = Label.Create("outer");
        var inner = Label.Create("inner");
        var exit = Label.Create("exit");
        var terminal = Label.Create("terminal");
        var condition = new ParameterDeclaration("condition", ShaderType.Bool, []);
        var declaration = new FunctionDeclaration("MultipleNormalTargets", [condition],
            new FunctionReturn(ShaderType.Unit, []), []);
        var outerBody = Body(outer, Terminator.B.Br<RegionJump, IShaderValue>(new(inner, [])), null);
        var innerBody = Body(inner,
            Terminator.B.BrIf<RegionJump, IShaderValue>(
                condition.Value, new RegionJump(outer, []), new RegionJump(exit, [])), null);
        var exitBody = Body(exit, Terminator.B.Br<RegionJump, IShaderValue>(new(terminal, [])), terminal);
        var terminalBody = Body(terminal, Terminator.B.ReturnVoid<RegionJump, IShaderValue>(), null);
        var body = new FunctionBody4(declaration,
            RegionTree.Loop(outer,
            [
                RegionTree.Loop(inner, [], innerBody, null, null),
                RegionTree.Block(exit, [], exitBody, null),
                RegionTree.Block(terminal, [], terminalBody, null)
            ], outerBody, null, null));

        var error = Assert.Throws<NotSupportedException>(() => Emit(body));

        Assert.Contains("MultipleNormalTargets", error.Message);
        Assert.Contains(inner.ToString(), error.Message);
        Assert.Contains(outer.ToString(), error.Message);
        Assert.Contains(exit.ToString(), error.Message);
    }

    private static ShaderRegionBody Body(
        Label label,
        ITerminator<RegionJump, IShaderValue> terminator,
        Label? immediatePostDominator) =>
        ShaderRegionBody.Create(label, [], [], terminator, immediatePostDominator);

    private static string Emit(MethodInfo method)
    {
        var parser = new RuntimeReflectionParser();
        var declaration = parser.ParseMethod(method);
        var body = parser.MethodBodies[declaration];
        body = new FunctionToOperationPass().VisitFunctionBody(body);
        body = new RegionParameterToLocalVariablePass().VisitFunctionBody(body);
        return Emit(body);
    }

    private static string Emit(FunctionBody4 body) =>
        new SlangEmitter(new ShaderModuleDeclaration<FunctionBody4>(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body)))
        .Emit();

    private static void AssertLexicalUnwind(string source, int depth)
    {
        var loops = LoopScopes(source);
        Assert.Equal(depth, loops.Length);
        Assert.DoesNotContain("Invalid duplicate label", source);

        for (var i = 1; i < loops.Length; i++)
        {
            var outer = loops[i - 1];
            var inner = loops[i];
            Assert.InRange(inner.Start, outer.Start + 1, outer.End - 1);
            Assert.Contains("break;", source[inner.Start..(inner.End + 1)]);

            var parent = BraceScopes(source)
                .Where(scope => scope.Start < inner.Start && scope.End > inner.End)
                .MaxBy(scope => scope.Start);
            Assert.Contains("continue;", source[(inner.End + 1)..parent.End]);
        }
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

    private readonly record struct TextScope(int Start, int End);
}

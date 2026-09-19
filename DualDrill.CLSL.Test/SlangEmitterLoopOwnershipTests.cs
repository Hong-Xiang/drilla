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
        var source = Emit(((Func<int, int, int, int>)NestedEarlyReturn).Method);

        Assert.Contains("return ", source);
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
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = new FunctionDeclaration("SharedNormalTransfer", [],
            new FunctionReturn(ShaderType.Unit, []), []);
        var outerBody = Body(outer, Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(inner, [])), null);
        var innerBody = Body(inner,
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition, new RegionJump<IShaderValue>(left, []), new RegionJump<IShaderValue>(right, [])), null);
        var leftBody = Body(left, Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(outer, [])), outer);
        var rightBody = Body(right, Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(outer, [])), outer);
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
        var outerBody = Body(outer, Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(inner, [])), null);
        var innerBody = Body(inner,
            Terminator.B.BrIf<RegionJump<IShaderValue>, IShaderValue>(
                condition, new RegionJump<IShaderValue>(outer, []), new RegionJump<IShaderValue>(exit, [])), null);
        var exitBody =
            Body(exit, Terminator.B.Br<RegionJump<IShaderValue>, IShaderValue>(new(terminal, [])), terminal);
        var terminalBody = Body(terminal, Terminator.B.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>(), null);
        var body = new FunctionBody4(declaration,
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

    private static ShaderRegionBody Body(
        Label label,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator,
        Label? immediatePostDominator) =>
        ShaderRegionBody.Create(label, [], [], terminator, immediatePostDominator);

    private static string Emit(MethodInfo method)
    {
        var body = CompilerTestPipeline.CompileBody(method);
        body = new FunctionToOperationPass().VisitFunctionBody(body);
        body = new StablePointerRegionParameterPass().VisitFunctionBody(body);
        return Emit(body);
    }

    private static string Emit(FunctionBody4 body) =>
        new SlangEmitter(new SlangTargetLowering().Lower(new ShaderModuleDeclaration<FunctionBody4>(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body))))
        .Emit();

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

    private readonly record struct TextScope(int Start, int End);
}

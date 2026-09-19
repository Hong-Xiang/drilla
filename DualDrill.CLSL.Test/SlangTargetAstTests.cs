using System.Collections.Immutable;
using System.Globalization;
using DualDrill.CLSL.Backend;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Transform;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;

public sealed class SlangTargetAstTests(ITestOutputHelper output)
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Fact]
    public void SharedEffectfulTailHasOneAstPlacement()
    {
        var source = Lower(((Func<int, int>)ScalarControlFlowFixtures.SharedTail).Method);
        var target = source.Target;
        var multiplyBindings = Statements(target.Body)
            .OfType<SlangBind>()
            .Count(binding =>
                binding.Instruction.Operation is IBinaryExpressionOperation
                {
                    BinaryOp: BinaryArithmetic.Mul
                });

        Assert.Equal(1, multiplyBindings);
        Assert.Equal(source.Slang, new SlangEmitter(source.Module).Emit());
        var firstDump = target.PrettyPrint();
        Assert.Equal(firstDump, target.PrettyPrint());
        Capture("shared-tail", source.Region, target.PrettyPrint(), source.Slang);
    }

    [Fact]
    public void LoweringMakesDeclarationValueEffectAndAssignmentOrderExplicit()
    {
        var entry = Label.Create("entry");
        var declaration = Function("Ordered", ShaderType.Unit);
        var variable = new VariableDeclaration(FunctionAddressSpace.Instance, "r", ShaderType.I32, []);
        var loaded = ShaderValue.Intermediate(ShaderType.I32);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.Store(default, new StoreOperation(), variable.Value, Int(1)),
                    Instruction.Factory.Load(default, new LoadOperation(), loaded, variable.Value),
                    Instruction.Factory.Nop(default, NopOperation.Instance)
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);

        Assert.Collection(
            target.Body.Statements,
            statement => Assert.Same(variable, Assert.IsType<SlangDeclare>(statement).Variable),
            statement =>
            {
                var scope = Assert.IsType<SlangScope>(statement);
                Assert.Collection(
                    scope.Body.Statements,
                    child => Assert.IsType<SlangAssign>(child),
                    child => Assert.IsType<SlangBind>(child),
                    child => Assert.IsType<SlangEffect>(child),
                    child => Assert.IsType<SlangReturnVoid>(child));
            });
    }

    [Fact]
    public void NestedLoopsContainExplicitNearestOwnerTransfers()
    {
        var target = Lower(((Func<int, int, int>)ScalarControlFlowFixtures.NestedLoopControl).Method).Target;
        var loops = Statements(target.Body).OfType<SlangLoop>().ToArray();

        Assert.Equal(2, loops.Length);
        Assert.Contains(Statements(loops[1].Body), statement => statement is SlangBreak);
        Assert.Contains(Statements(loops[0].Body), statement => statement is SlangContinue);
    }

    [Fact]
    public void SharedTerminalDefinitionExpandsOncePerSelectedArm()
    {
        var entry = Label.Create("entry");
        var terminal = Label.Create("terminal");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = Function("TerminalClone", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [],
            Terms.BrIf(condition, new(terminal, []), new(terminal, [])),
            null);
        var terminalBody = ShaderRegionBody.Create(
            terminal,
            [],
            [],
            Terms.ReturnExpr(Int(7)),
            null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(terminal, [], terminalBody, null)], entryBody, null));

        var target = Lower(body);
        var conditional = Assert.Single(Statements(target.Body).OfType<SlangIf>());

        Assert.Single(Statements(conditional.WhenTrue).OfType<SlangReturnValue>());
        Assert.Single(Statements(conditional.WhenFalse).OfType<SlangReturnValue>());
        Assert.Equal(2, Statements(target.Body).OfType<SlangReturnValue>().Count());
    }

    [Fact]
    public void RepeatedEffectfulNonterminalDefinitionIsRejectedInsteadOfDuplicated()
    {
        var entry = Label.Create("entry");
        var shared = Label.Create("shared");
        var terminal = Label.Create("terminal");
        var condition = ShaderValue.Literal(new BoolLiteral(true));
        var declaration = Function("RepeatedNonterminal", ShaderType.Unit);
        var entryBody = ShaderRegionBody.Create(
            entry,
            [],
            [],
            Terms.BrIf(condition, new(shared, []), new(shared, [])),
            null);
        var sharedBody = ShaderRegionBody.Create(
            shared,
            [],
            [Instruction.Factory.Nop(default, NopOperation.Instance)],
            Terms.Br(new(terminal, [])),
            null);
        var terminalBody = ShaderRegionBody.Create(terminal, [], [], Terms.ReturnVoid(), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry,
            [
                RegionTree.Block(shared, [], sharedBody, null),
                RegionTree.Block(terminal, [], terminalBody, null)
            ], entryBody, null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("nonterminal target already has an executable AST placement", error.Message);
    }

    [Fact]
    public void ResidualRegionParametersAreRejected()
    {
        var entry = Label.Create("entry");
        var parameter = ShaderValue.Intermediate(ShaderType.I32);
        var declaration = Function("ResidualParameter", ShaderType.I32);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry, [parameter], [], Terms.ReturnExpr(parameter), null), null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("residual region parameters", error.Message);
    }

    [Fact]
    public void ResidualJumpArgumentsAreRejected()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var declaration = Function("ResidualArgument", ShaderType.I32);
        var entryBody = ShaderRegionBody.Create(
            entry, [], [], Terms.Br(new(exit, [Int(1)])), exit);
        var exitBody = ShaderRegionBody.Create(exit, [], [], Terms.ReturnExpr(Int(1)), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [RegionTree.Block(exit, [], exitBody, null)], entryBody, exit));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("residual arguments", error.Message);
    }

    [Fact]
    public void MissingLabelsAreRejectedBeforeLayout()
    {
        var entry = Label.Create("entry");
        var unreachable = Label.Create("unreachable");
        var missing = Label.Create("missing");
        var declaration = Function("MissingLabel", ShaderType.Unit);
        var entryBody = ShaderRegionBody.Create(entry, [], [], Terms.ReturnVoid(), null);
        var unreachableBody = ShaderRegionBody.Create(
            unreachable, [], [], Terms.Br(new(missing, [])), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(
                entry,
                [RegionTree.Block(unreachable, [], unreachableBody, null)],
                entryBody,
                null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("references missing label", error.Message);
    }

    [Fact]
    public void UnownedExpansionCyclesAreRejected()
    {
        var left = Label.Create("left");
        var right = Label.Create("right");
        var declaration = Function("Cycle", ShaderType.Unit);
        var leftBody = ShaderRegionBody.Create(left, [], [], Terms.Br(new(right, [])), null);
        var rightBody = ShaderRegionBody.Create(right, [], [], Terms.Br(new(left, [])), null);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(
                left,
                [RegionTree.Block(right, [], rightBody, null)],
                leftBody,
                null));

        var error = Assert.Throws<NotSupportedException>(() => Lower(body));

        Assert.Contains("unowned expansion cycle", error.Message);
    }

    [Fact]
    public void MissingFunctionBodiesAreRejected()
    {
        var declaration = Function("MissingBody", ShaderType.Unit);
        var module = new ShaderModuleDeclaration<FunctionBody4>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty);

        var error = Assert.Throws<NotSupportedException>(
            () => new SlangTargetLowering().Lower(module));

        Assert.Contains("requires bodies for functions: MissingBody", error.Message);
    }

    [Fact]
    public void AddressProjectionBecomesTypedPlaceSyntax()
    {
        var entry = Label.Create("entry");
        var vectorType = VecType<N2, FloatType<N32>>.Instance;
        var declaration = Function("Projection", ShaderType.Unit);
        var vector = new VariableDeclaration(FunctionAddressSpace.Instance, "value", vectorType, []);
        var component = ShaderValue.Intermediate(ShaderType.F32.GetPtrType());
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.AddressOfChain(
                        default,
                        new AddressOfVecComponentOperation(vectorType, Swizzle.X.Instance),
                        component,
                        vector.Value),
                    Instruction.Factory.Store(
                        default,
                        new StoreOperation(),
                        component,
                        ShaderValue.Literal(new F32Literal(2.5f)))
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);
        var assignment = Assert.Single(Statements(target.Body).OfType<SlangAssign>());
        var place = Assert.IsType<SlangComponentPlace>(assignment.Target);
        Assert.Equal("x", place.Component);
        Assert.Equal(ShaderType.F32, place.Type);
        Assert.DoesNotContain(
            Statements(target.Body),
            statement => statement is SlangBind
            {
                Instruction.Operation: IAddressOfOperation
            });
        Assert.Contains(".x = 2.5;", Emit(target));
    }

    [Fact]
    public void MemberAddressProjectionBecomesTypedPlaceSyntax()
    {
        var entry = Label.Create("entry");
        var member = new MemberDeclaration("field", ShaderType.F32, []);
        var structure = new StructureDeclaration { Name = "Holder", Members = [member] };
        var holder = new VariableDeclaration(
            FunctionAddressSpace.Instance,
            "holder",
            new StructureType(structure),
            []);
        var address = ShaderValue.Intermediate(ShaderType.F32.GetPtrType());
        var declaration = Function("MemberProjection", ShaderType.Unit);
        var body = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry,
                [],
                [
                    Instruction.Factory.AddressOfChain(
                        default,
                        new AddressOfMemberOperation(member),
                        address,
                        holder.Value),
                    Instruction.Factory.Store(
                        default,
                        new StoreOperation(),
                        address,
                        ShaderValue.Literal(new F32Literal(1.5f)))
                ],
                Terms.ReturnVoid(),
                null), null));

        var target = Lower(body);
        var place = Assert.IsType<SlangMemberPlace>(
            Assert.Single(Statements(target.Body).OfType<SlangAssign>()).Target);

        Assert.Same(member, place.Member);
        Assert.Equal(ShaderType.F32, place.Type);
        Assert.Contains(".field = 1.5;", Emit(target));
    }

    [Fact]
    public void LoweringStateIsIsolatedPerFunction()
    {
        var sharedLabel = Label.Create("shared-label-object");
        var left = Function("Left", ShaderType.I32);
        var right = Function("Right", ShaderType.I32);
        FunctionBody4 Body(FunctionDeclaration declaration, int value) =>
            new(
                declaration,
                RegionTree.Block(
                    sharedLabel,
                    [],
                    ShaderRegionBody.Create(
                        sharedLabel, [], [], Terms.ReturnExpr(Int(value)), null),
                    null));
        var source = new ShaderModuleDeclaration<FunctionBody4>(
            [left, right],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty
                .Add(left, Body(left, 1))
                .Add(right, Body(right, 2)));

        var target = new SlangTargetLowering().Lower(source);

        Assert.Equal(2, target.FunctionDefinitions.Count);
        Assert.Single(target.GetBody(left).Labels);
        Assert.Single(target.GetBody(right).Labels);
    }

    [Fact]
    public void AstOnlyEmitterIsDeterministicAndHasNoRegionInput()
    {
        var declaration = Function("AstOnly", ShaderType.I32);
        var variable = new VariableDeclaration(FunctionAddressSpace.Instance, "answer", ShaderType.I32, []);
        var target = new SlangFunctionBody(
            declaration,
            new SlangBlock(
            [
                new SlangDeclare(variable),
                new SlangAssign(new SlangVariablePlace(variable), new SlangValueOperand(Int(42))),
                new SlangReturnValue(new SlangPlaceOperand(new SlangVariablePlace(variable)))
            ]));
        var module = new ShaderModuleDeclaration<SlangFunctionBody>(
            [declaration],
            ImmutableDictionary<FunctionDeclaration, SlangFunctionBody>.Empty.Add(declaration, target));
        var emitter = new SlangEmitter(module);

        var first = emitter.Emit();
        var second = emitter.Emit();

        Assert.Equal(first, second);
        Assert.Equal(
        [
            "slang-target AstOnly",
            "declare %0(answer) : i32",
            "assign %0(answer) <- 42_i32",
            "return read %0(answer)"
        ], target.PrettyPrint().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("var v_0_answer : i32;", first);
        Assert.Contains("v_0_answer = 42;", first);
        Assert.Contains("return v_0_answer;", first);
    }

    [Fact]
    public void LiteralSyntaxUsesLowercaseBooleansAndInvariantNumbers()
    {
        var declaration = Function("Literals", ShaderType.Unit);
        var trueResult = ShaderValue.Intermediate(ShaderType.Bool);
        var falseResult = ShaderValue.Intermediate(ShaderType.Bool);
        var floatResult = ShaderValue.Intermediate(ShaderType.F32);
        SlangBind Bind(IShaderValue result, ILiteral literal) =>
            new(Instruction<SlangOperand, IShaderValue>.Create(
                new LiteralOperation(),
                result,
                [new SlangValueOperand(ShaderValue.Literal(literal))]));
        var trueBinding = Bind(trueResult, new BoolLiteral(true));
        var target = new SlangFunctionBody(
            declaration,
            new SlangBlock(
            [
                trueBinding,
                Bind(falseResult, new BoolLiteral(false)),
                Bind(floatResult, new F32Literal(1.5f)),
                new SlangReturnVoid()
            ]));
        var literal = Assert.IsType<LiteralValue>(
            Assert.IsType<SlangValueOperand>(Assert.Single(trueBinding.Instruction.Operands)).Value);
        Assert.True(Assert.IsType<BoolLiteral>(literal.Value).Value);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var source = Emit(target);

            Assert.Contains(" = true;", source);
            Assert.Contains(" = false;", source);
            Assert.Contains(" = 1.5;", source);
            Assert.DoesNotContain("True", source);
            Assert.DoesNotContain("False", source);
            Assert.DoesNotContain("1,5", source);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void UnsupportedAccessChainsAndScalarZeroConstructionFailInLowering()
    {
        var entry = Label.Create("entry");
        var declaration = Function("UnsupportedOperations", ShaderType.Unit);
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "value", ShaderType.I32, []);
        var accessResult = ShaderValue.Intermediate(ShaderType.I32.GetPtrType());
        var access = Instruction<IShaderValue, IShaderValue>.Create(
            new AccessChainOperation(), accessResult, [local.Value, Int(0)]);
        var accessBody = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry, [], [access], Terms.ReturnVoid(), null), null));
        Assert.Contains("access chains are not supported",
            Assert.Throws<NotSupportedException>(() => Lower(accessBody)).Message);

        var zeroResult = ShaderValue.Intermediate(ShaderType.I32);
        var zero = Instruction.Factory.ZeroConstructorOperation(
            default, new ZeroConstructorOperation(ShaderType.I32), zeroResult);
        var zeroBody = new FunctionBody4(
            declaration,
            RegionTree.Block(entry, [], ShaderRegionBody.Create(
                entry, [], [zero], Terms.ReturnVoid(), null), null));
        Assert.Contains("zero construction of i32",
            Assert.Throws<NotSupportedException>(() => Lower(zeroBody)).Message);
    }

    private LoweredFixture Lower(System.Reflection.MethodInfo method)
    {
        var region = new RegionParameterToLocalVariablePass().VisitFunctionBody(
            new FunctionToOperationPass().VisitFunctionBody(CompilerTestPipeline.CompileBody(method)));
        var target = Lower(region);
        var module = Module(target);
        var slang = new SlangEmitter(module).Emit();
        output.WriteLine(region.Dump());
        output.WriteLine(target.PrettyPrint());
        output.WriteLine(slang);
        return new LoweredFixture(region, target, module, slang);
    }

    private static SlangFunctionBody Lower(FunctionBody4 body) =>
        new SlangTargetLowering().Lower(Module(body)).GetBody(body.Declaration);

    private static ShaderModuleDeclaration<FunctionBody4> Module(FunctionBody4 body) =>
        new(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, FunctionBody4>.Empty.Add(body.Declaration, body));

    private static ShaderModuleDeclaration<SlangFunctionBody> Module(SlangFunctionBody body) =>
        new(
            [body.Declaration],
            ImmutableDictionary<FunctionDeclaration, SlangFunctionBody>.Empty.Add(body.Declaration, body));

    private static string Emit(SlangFunctionBody body) => new SlangEmitter(Module(body)).Emit();

    private static FunctionDeclaration Function(string name, IShaderType returnType) =>
        new(name, [], new FunctionReturn(returnType, []), []);

    private static IShaderValue Int(int value) => ShaderValue.Literal(new I32Literal(value));

    private static IEnumerable<SlangStatement> Statements(SlangBlock block)
    {
        foreach (var statement in block.Statements)
        {
            yield return statement;
            switch (statement)
            {
                case SlangScope scope:
                    foreach (var nested in Statements(scope.Body)) yield return nested;
                    break;
                case SlangIf conditional:
                    foreach (var nested in Statements(conditional.WhenTrue)) yield return nested;
                    foreach (var nested in Statements(conditional.WhenFalse)) yield return nested;
                    break;
                case SlangLoop loop:
                    foreach (var nested in Statements(loop.Body)) yield return nested;
                    break;
            }
        }
    }

    private static void Capture(string name, FunctionBody4 region, string ast, string slang)
    {
        var directory = Environment.GetEnvironmentVariable("DRILLA_E1_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.region.txt"), region.Dump());
        File.WriteAllText(Path.Combine(directory, $"{name}.ast.txt"), ast);
        File.WriteAllText(Path.Combine(directory, $"{name}.slang"), slang);
    }

    private sealed record LoweredFixture(
        FunctionBody4 Region,
        SlangFunctionBody Target,
        ShaderModuleDeclaration<SlangFunctionBody> Module,
        string Slang);
}

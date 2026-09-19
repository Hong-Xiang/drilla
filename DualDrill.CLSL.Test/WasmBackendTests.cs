using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using DualDrill.CLSL.Backend.Wasm;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;
using Xunit.Sdk;

namespace DualDrill.CLSL.Test;

public sealed class WasmBackendTests
{
    private static readonly ITerminatorSemantic<RegionJump<IShaderValue>, IShaderValue,
            ITerminator<RegionJump<IShaderValue>, IShaderValue>>
        Terms = Terminator.Factory<RegionJump<IShaderValue>, IShaderValue>();

    [Fact]
    public void ConstructorFixturesAssembleValidateAndExecuteInNode()
    {
        Assert.Contains("1.0.36", WasmProgram.ToolVersion("wat2wasm"));

        var add = WasmProgram.Compile(AddBody(), "wasm-add");
        var addResults = new[]
        {
            $"{int.MaxValue} + 1 = {add.Execute(int.MaxValue, 1)}",
            $"{int.MinValue} + -1 = {add.Execute(int.MinValue, -1)}",
            $"-1 + 1 = {add.Execute(-1, 1)}"
        };
        Assert.Equal(int.MinValue, add.Execute(int.MaxValue, 1));
        Assert.Equal(int.MaxValue, add.Execute(int.MinValue, -1));
        Assert.Equal(0, add.Execute(-1, 1));

        var choose = WasmProgram.Compile(ChooseBody(), "wasm-choose");
        var chooseResults = new[]
        {
            $"choose(0, 7, 9) = {choose.Execute(0, 7, 9)}",
            $"choose(-1, 7, 9) = {choose.Execute(-1, 7, 9)}",
            $"choose(42, -3, 5) = {choose.Execute(42, -3, 5)}"
        };
        Assert.Equal(9, choose.Execute(0, 7, 9));
        Assert.Equal(7, choose.Execute(-1, 7, 9));
        Assert.Equal(-3, choose.Execute(42, -3, 5));

        var sumBody = SumBody();
        var sum = WasmProgram.Compile(sumBody, "wasm-sum");
        var sumResults = new[]
        {
            $"sum(-1) = {sum.Execute(-1)}",
            $"sum(0) = {sum.Execute(0)}",
            $"sum(5) = {sum.Execute(5)}",
            $"sum(10000) = {sum.Execute(10000)}"
        };
        Assert.Equal(0, sum.Execute(-1));
        Assert.Equal(0, sum.Execute(0));
        Assert.Equal(10, sum.Execute(5));
        Assert.Equal(49_995_000, sum.Execute(10_000));
        foreach (var n in new[] { -1, 0, 5, 10 })
        {
            var expected = ScalarControlFlowOracle.RunCfg(
                sumBody,
                [new ScalarControlFlowOracle.Value.Integer(n)],
                1_000).Result.Int;
            Assert.Equal(expected, sum.Execute(n));
        }

        AssertGolden("wasm-add", AddBody(), addResults);
        AssertGolden("wasm-choose", ChooseBody(), chooseResults);
        AssertGolden("wasm-sum", sumBody, sumResults);
    }

    [Fact]
    public void SelfEdgeParallelCopyAndLocalStorageExecuteCorrectly()
    {
        var swap = WasmProgram.Compile(SwapBody(), "wasm-swap");
        Assert.Equal(21, swap.Execute());

        var local = WasmProgram.Compile(LocalBody(), "wasm-local");
        Assert.Equal(42, local.Execute());
    }

    [Fact]
    public void SignedComparisonsAndBooleanConversionsAreExact()
    {
        var cases = new (IBinaryOp Op, int Left, int Right, int Expected)[]
        {
            (BinaryRelational.Eq.Instance, -1, -1, 1),
            (BinaryRelational.Ne.Instance, -1, 1, 1),
            (BinaryRelational.Lt.Instance, int.MinValue, 0, 1),
            (BinaryRelational.Le.Instance, 7, 7, 1),
            (BinaryRelational.Gt.Instance, 0, -1, 1),
            (BinaryRelational.Ge.Instance, -1, -1, 1)
        };
        foreach (var (op, left, right, expected) in cases)
        {
            var program = WasmProgram.Compile(ComparisonBody(op), $"wasm-{op.GetType().Name}");
            Assert.Equal(expected, program.Execute(left, right));
        }

        var normalize = WasmProgram.Compile(NormalizeBody(), "wasm-normalize");
        Assert.Equal(0, normalize.Execute(0));
        Assert.Equal(1, normalize.Execute(-37));
        Assert.Equal(1, normalize.Execute(int.MinValue));
    }

    [Fact]
    public void OrdinaryCilAddUsesTheSamePublicBackendEntry()
    {
        var method = typeof(DevelopTestShaderModule).GetMethod(
            nameof(DevelopTestShaderModule.Add),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Add fixture was not found.");
        var body = CompilerTestPipeline.CompileBody(method);
        var wat = WatEmitter.Emit(WasmLowering.Lower(body));

        Assert.Contains("i32.add", wat);
        Assert.Equal(
            int.MinValue,
            WasmProgram.Compile(body, "wasm-cil-add").Execute(int.MaxValue, 1));
    }

    [Fact]
    public void LoweringIsDeterministicAndPrinterContainsNoSemanticShortcuts()
    {
        var body = ChooseBody();
        var first = WatEmitter.Emit(WasmLowering.Lower(body));
        var second = WatEmitter.Emit(WasmLowering.Lower(body));

        Assert.Equal(first, second);
        Assert.Contains("(export \"run\")", first);
        Assert.Contains("loop", first);
        Assert.Contains("br 2", first);
        Assert.DoesNotContain(body.Declaration.Name, first);
    }

    [Fact]
    public void UnsupportedAndMalformedInputsAreRejectedContextually()
    {
        AssertRejected(
            WithSignature(AddBody(), ShaderType.Bool, [ShaderType.I32]),
            "public signature");
        AssertRejected(
            WithAttributes(AddBody()),
            "shader semantic attributes");
        AssertRejected(
            UnsupportedBinaryBody(),
            "unsupported operation");
        AssertRejected(
            UninitializedLocalBody(),
            "read before");
        AssertRejected(
            CrossBlockUseBody(),
            "earlier result");
        AssertRejected(
            WrongEdgeTypeBody(),
            "edge argument");
        AssertRejected(
            WrongEdgeArityBody(),
            "edge arguments");
        AssertRejected(
            DuplicateDefinitionBody(),
            "duplicate value definition");
        AssertRejected(
            DuplicateBlockBody(),
            "duplicate block definition");
        AssertRejected(
            UnreachableBlockBody(),
            "unreachable");
        AssertRejected(
            EntryParameterBody(),
            "entry block parameters");
        AssertRejected(
            NonFunctionStorageBody(),
            "non-function storage");
        AssertRejected(
            SpoofedResultTypeBody(),
            "binary result");
        AssertRejected(
            MalformedOperandBody(),
            "malformed operand storage");
        AssertRejected(
            MissingResultBody(),
            "invalid result presence");
        AssertRejected(
            TreeLabelMismatchBody(),
            "does not match");
    }

    private static void AssertRejected(FunctionBody4 body, string message)
    {
        var exception = Assert.Throws<WasmLoweringException>(() => WasmLowering.Lower(body));
        Assert.Contains($"WASM function {body.Declaration.Name}", exception.Message);
        Assert.Contains(message, exception.Message);
    }

    private static FunctionBody4 AddBody()
    {
        var entry = Label.Create("entry");
        var a = Parameter("a");
        var b = Parameter("b");
        var left = Value(ShaderType.I32, "left");
        var right = Value(ShaderType.I32, "right");
        var result = Value(ShaderType.I32, "result");
        return Body("add", [a, b],
            Block(entry, [], [
                Load(left, a.Value),
                Load(right, b.Value),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, left, right)
            ], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 ChooseBody()
    {
        var entry = Label.Create("entry");
        var join = Label.Create("join");
        var conditionParameter = Parameter("condition");
        var whenTrueParameter = Parameter("whenTrue");
        var whenFalseParameter = Parameter("whenFalse");
        var rawCondition = Value(ShaderType.I32, "rawCondition");
        var condition = Value(ShaderType.Bool, "condition");
        var whenTrue = Value(ShaderType.I32, "whenTrue");
        var whenFalse = Value(ShaderType.I32, "whenFalse");
        var selected = Value(ShaderType.I32, "selected");
        return Body("choose", [conditionParameter, whenTrueParameter, whenFalseParameter],
            Block(entry, [], [
                Load(rawCondition, conditionParameter.Value),
                Unary(ScalarConversionOperation<IntType<N32>, BoolType>.Instance, condition, rawCondition),
                Load(whenTrue, whenTrueParameter.Value),
                Load(whenFalse, whenFalseParameter.Value)
            ], Terms.BrIf(condition, Jump(join, whenTrue), Jump(join, whenFalse))),
            Block(join, [selected], [], Terms.ReturnExpr(selected)));
    }

    private static FunctionBody4 SumBody()
    {
        var entry = Label.Create("entry");
        var header = Label.Create("header");
        var step = Label.Create("step");
        var exit = Label.Create("exit");
        var input = Parameter("n");
        var n0 = Value(ShaderType.I32, "n.entry");
        var sum = Value(ShaderType.I32, "sum");
        var i = Value(ShaderType.I32, "i");
        var n = Value(ShaderType.I32, "n");
        var condition = Value(ShaderType.Bool, "condition");
        var stepSum = Value(ShaderType.I32, "step.sum");
        var stepI = Value(ShaderType.I32, "step.i");
        var stepN = Value(ShaderType.I32, "step.n");
        var nextSum = Value(ShaderType.I32, "next.sum");
        var nextI = Value(ShaderType.I32, "next.i");
        var answer = Value(ShaderType.I32, "answer");
        return Body("sum", [input],
            Block(entry, [], [Load(n0, input.Value)],
                Terms.Br(Jump(header, Int(0), Int(0), n0))),
            Block(header, [sum, i, n], [
                Binary(NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>.Instance,
                    condition, i, n)
            ], Terms.BrIf(condition, Jump(step, sum, i, n), Jump(exit, sum))),
            Block(step, [stepSum, stepI, stepN], [
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    nextSum, stepSum, stepI),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    nextI, stepI, Int(1))
            ], Terms.Br(Jump(header, nextSum, nextI, stepN))),
            Block(exit, [answer], [], Terms.ReturnExpr(answer)));
    }

    private static FunctionBody4 SwapBody()
    {
        var entry = Label.Create("entry");
        var loop = Label.Create("loop");
        var exit = Label.Create("exit");
        var a = Value(ShaderType.I32, "a");
        var b = Value(ShaderType.I32, "b");
        var again = Value(ShaderType.Bool, "again");
        var twice = Value(ShaderType.I32, "twice");
        var four = Value(ShaderType.I32, "four");
        var eight = Value(ShaderType.I32, "eight");
        var tens = Value(ShaderType.I32, "tens");
        var result = Value(ShaderType.I32, "result");
        var answer = Value(ShaderType.I32, "answer");
        return Body("swap", [],
            Block(entry, [], [],
                Terms.Br(Jump(loop, Int(1), Int(2), Bool(true)))),
            Block(loop, [a, b, again], [
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    twice, a, a),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    four, twice, twice),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    eight, four, four),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    tens, eight, twice),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, tens, b)
            ], Terms.BrIf(again, Jump(loop, b, a, Bool(false)), Jump(exit, result))),
            Block(exit, [answer], [], Terms.ReturnExpr(answer)));
    }

    private static FunctionBody4 LocalBody()
    {
        var entry = Label.Create("entry");
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "local", ShaderType.I32, []);
        var loaded = Value(ShaderType.I32, "loaded");
        var result = Value(ShaderType.I32, "result");
        var reloaded = Value(ShaderType.I32, "reloaded");
        return Body("local", [],
            Block(entry, [], [
                Store(local.Value, Int(41)),
                Load(loaded, local.Value),
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, loaded, Int(1)),
                Instruction.Factory.Nop(default, NopOperation.Instance),
                Store(local.Value, result),
                Load(reloaded, local.Value)
            ], Terms.ReturnExpr(reloaded)));
    }

    private static FunctionBody4 ComparisonBody(IBinaryOp op)
    {
        var entry = Label.Create("entry");
        var leftParameter = Parameter("left");
        var rightParameter = Parameter("right");
        var left = Value(ShaderType.I32, "left");
        var right = Value(ShaderType.I32, "right");
        var comparison = Value(ShaderType.Bool, "comparison");
        var result = Value(ShaderType.I32, "result");
        IBinaryExpressionOperation operation = op switch
        {
            BinaryRelational.Eq => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>.Instance,
            BinaryRelational.Ne => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ne>.Instance,
            BinaryRelational.Lt => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>.Instance,
            BinaryRelational.Le => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Le>.Instance,
            BinaryRelational.Gt => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>.Instance,
            BinaryRelational.Ge => NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ge>.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
        return Body("compare", [leftParameter, rightParameter],
            Block(entry, [], [
                Load(left, leftParameter.Value),
                Load(right, rightParameter.Value),
                Binary(operation, comparison, left, right),
                Unary(ScalarConversionOperation<BoolType, IntType<N32>>.Instance, result, comparison)
            ], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 NormalizeBody()
    {
        var entry = Label.Create("entry");
        var input = Parameter("input");
        var loaded = Value(ShaderType.I32, "loaded");
        var normalized = Value(ShaderType.Bool, "normalized");
        var result = Value(ShaderType.I32, "result");
        return Body("normalize", [input],
            Block(entry, [], [
                Load(loaded, input.Value),
                Unary(ScalarConversionOperation<IntType<N32>, BoolType>.Instance, normalized, loaded),
                Unary(ScalarConversionOperation<BoolType, IntType<N32>>.Instance, result, normalized)
            ], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 UnsupportedBinaryBody()
    {
        var entry = Label.Create("entry");
        var result = Value(ShaderType.I32, "result");
        return Body("unsupported", [],
            Block(entry, [], [
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Div>.Instance,
                    result, Int(4), Int(2))
            ], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 UninitializedLocalBody()
    {
        var entry = Label.Create("entry");
        var local = new VariableDeclaration(FunctionAddressSpace.Instance, "local", ShaderType.I32, []);
        var loaded = Value(ShaderType.I32, "loaded");
        return Body("uninitialized", [],
            Block(entry, [], [Load(loaded, local.Value)], Terms.ReturnExpr(loaded)));
    }

    private static FunctionBody4 CrossBlockUseBody()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var value = Value(ShaderType.I32, "value");
        return Body("cross-block", [],
            Block(entry, [], [
                Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(1))
            ], Terms.Br(Jump(exit))),
            Block(exit, [], [], Terms.ReturnExpr(value)));
    }

    private static FunctionBody4 WrongEdgeTypeBody()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var parameter = Value(ShaderType.Bool, "parameter");
        var result = Value(ShaderType.I32, "result");
        return Body("wrong-edge-type", [],
            Block(entry, [], [], Terms.Br(Jump(exit, Int(1)))),
            Block(exit, [parameter], [
                Unary(ScalarConversionOperation<BoolType, IntType<N32>>.Instance, result, parameter)
            ], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 WrongEdgeArityBody()
    {
        var entry = Label.Create("entry");
        var exit = Label.Create("exit");
        var parameter = Value(ShaderType.I32, "parameter");
        return Body("wrong-edge-arity", [],
            Block(entry, [], [], Terms.Br(Jump(exit))),
            Block(exit, [parameter], [], Terms.ReturnExpr(parameter)));
    }

    private static FunctionBody4 DuplicateDefinitionBody()
    {
        var entry = Label.Create("entry");
        var value = Value(ShaderType.I32, "value");
        return Body("duplicate-definition", [],
            Block(entry, [], [
                Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(1)),
                Instruction.Factory.Literal(default, new LiteralOperation(), value, Int(2))
            ], Terms.ReturnExpr(value)));
    }

    private static FunctionBody4 DuplicateBlockBody()
    {
        var entry = Label.Create("entry");
        var duplicate = Label.Create("duplicate");
        var entryBody = Block(entry, [], [], Terms.ReturnExpr(Int(0)));
        var first = Block(duplicate, [], [], Terms.ReturnExpr(Int(1)));
        var second = Block(duplicate, [], [], Terms.ReturnExpr(Int(2)));
        return new FunctionBody4(
            Declaration("duplicate-block", []),
            RegionTree<Label, ShaderRegionBody>.Block(entry, [
                RegionTree<Label, ShaderRegionBody>.Block(duplicate, [], first, null),
                RegionTree<Label, ShaderRegionBody>.Block(duplicate, [], second, null)
            ], entryBody, null));
    }

    private static FunctionBody4 UnreachableBlockBody()
    {
        var entry = Label.Create("entry");
        var dead = Label.Create("dead");
        return Body("unreachable-block", [],
            Block(entry, [], [], Terms.ReturnExpr(Int(0))),
            Block(dead, [], [], Terms.ReturnExpr(Int(1))));
    }

    private static FunctionBody4 EntryParameterBody()
    {
        var entry = Label.Create("entry");
        var value = Value(ShaderType.I32, "value");
        return Body("entry-parameter", [],
            Block(entry, [value], [], Terms.ReturnExpr(value)));
    }

    private static FunctionBody4 NonFunctionStorageBody()
    {
        var entry = Label.Create("entry");
        var local = new VariableDeclaration(UniformAddressSpace.Instance, "uniform", ShaderType.I32, []);
        var loaded = Value(ShaderType.I32, "loaded");
        return Body("non-function-storage", [],
            Block(entry, [], [
                Store(local.Value, Int(1)),
                Load(loaded, local.Value)
            ], Terms.ReturnExpr(loaded)));
    }

    private static FunctionBody4 SpoofedResultTypeBody()
    {
        var entry = Label.Create("entry");
        var result = Value(ShaderType.Bool, "result");
        var converted = Value(ShaderType.I32, "converted");
        return Body("spoofed-result", [],
            Block(entry, [], [
                Binary(NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>.Instance,
                    result, Int(1), Int(2)),
                Unary(ScalarConversionOperation<BoolType, IntType<N32>>.Instance, converted, result)
            ], Terms.ReturnExpr(converted)));
    }

    private static FunctionBody4 MalformedOperandBody()
    {
        var entry = Label.Create("entry");
        var result = Value(ShaderType.I32, "result");
        var malformed = new Instruction<IShaderValue, IShaderValue>(
            new LiteralOperation(), 1, result, null, null, [], null);
        return Body("malformed-operands", [],
            Block(entry, [], [malformed], Terms.ReturnExpr(result)));
    }

    private static FunctionBody4 MissingResultBody()
    {
        var entry = Label.Create("entry");
        var parameter = Parameter("parameter");
        var missing = Instruction<IShaderValue, IShaderValue>.Create(
            new LoadOperation(), null, [parameter.Value]);
        return Body("missing-result", [parameter],
            Block(entry, [], [missing], Terms.ReturnExpr(Int(0))));
    }

    private static FunctionBody4 TreeLabelMismatchBody()
    {
        var treeLabel = Label.Create("tree");
        var bodyLabel = Label.Create("body");
        var region = Block(bodyLabel, [], [], Terms.ReturnExpr(Int(0)));
        return new FunctionBody4(
            Declaration("tree-label-mismatch", []),
            RegionTree<Label, ShaderRegionBody>.Block(treeLabel, [], region, null));
    }

    private static FunctionBody4 WithSignature(
        FunctionBody4 source,
        IShaderType returnType,
        ImmutableArray<IShaderType> parameterTypes)
    {
        var parameters = parameterTypes.Select((type, index) => Parameter($"p{index}", type)).ToImmutableArray();
        return new FunctionBody4(
            new FunctionDeclaration(source.Declaration.Name, parameters, new FunctionReturn(returnType, []), []),
            source.Body);
    }

    private static FunctionBody4 WithAttributes(FunctionBody4 source) =>
        new(
            new FunctionDeclaration(
                source.Declaration.Name,
                source.Declaration.Parameters,
                source.Declaration.Return,
                [new VertexAttribute()]),
            source.Body);

    private static void AssertGolden(string name, FunctionBody4 body, IEnumerable<string> results)
    {
        var directory = Path.Combine(RepositoryRoot(), "examples-H");
        var values = new Dictionary<string, string>
        {
            [$"{name}.ir"] = NormalizeIr(body.Dump()),
            [$"{name}.wat"] = WatEmitter.Emit(WasmLowering.Lower(body)),
            [$"{name}.results"] = string.Join('\n', results) + "\n"
        };
        if (Environment.GetEnvironmentVariable("DRILLA_UPDATE_WASM_GOLDENS") == "1")
        {
            Directory.CreateDirectory(directory);
            foreach (var (file, value) in values)
                File.WriteAllText(Path.Combine(directory, file), value);
        }
        foreach (var (file, value) in values)
            Assert.Equal(
                File.ReadAllText(Path.Combine(directory, file)).ReplaceLineEndings("\n"),
                value.ReplaceLineEndings("\n"));
    }

    private static string NormalizeIr(string value) =>
        string.Join('\n', value.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "DualDrill.CLSL.Test")))
                return directory.FullName;
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static FunctionBody4 Body(
        string name,
        ImmutableArray<ParameterDeclaration> parameters,
        params ShaderRegionBody[] blocks)
    {
        var entry = blocks[0];
        var children = blocks.Skip(1)
            .Select(block => RegionTree<Label, ShaderRegionBody>.Block(block.Label, [], block, null))
            .ToArray();
        return new FunctionBody4(
            Declaration(name, parameters),
            RegionTree<Label, ShaderRegionBody>.Block(entry.Label, children, entry, null));
    }

    private static FunctionDeclaration Declaration(
        string name,
        ImmutableArray<ParameterDeclaration> parameters) =>
        new(name, parameters, new FunctionReturn(ShaderType.I32, []), []);

    private static ShaderRegionBody Block(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<Instruction<IShaderValue, IShaderValue>> instructions,
        ITerminator<RegionJump<IShaderValue>, IShaderValue> terminator) =>
        ShaderRegionBody.Create(label, parameters, instructions, terminator, null);

    private static ParameterDeclaration Parameter(string name, IShaderType? type = null) =>
        new(name, type ?? ShaderType.I32, []);

    private static IntermediateValue Value(IShaderType type, string name) =>
        Assert.IsType<IntermediateValue>(ShaderValue.Intermediate(type, name));

    private static LiteralValue Int(int value) =>
        Assert.IsType<LiteralValue>(ShaderValue.Literal(new I32Literal(value)));

    private static LiteralValue Bool(bool value) =>
        Assert.IsType<LiteralValue>(ShaderValue.Literal(new BoolLiteral(value)));

    private static RegionJump<IShaderValue> Jump(Label label, params IShaderValue[] arguments) =>
        new(label, [.. arguments]);

    private static Instruction<IShaderValue, IShaderValue> Load(IShaderValue result, IShaderValue storage) =>
        Instruction.Factory.Load(default, new LoadOperation(), result, storage);

    private static Instruction<IShaderValue, IShaderValue> Store(IShaderValue storage, IShaderValue value) =>
        Instruction.Factory.Store(default, new StoreOperation(), storage, value);

    private static Instruction<IShaderValue, IShaderValue> Binary(
        IBinaryExpressionOperation operation,
        IShaderValue result,
        IShaderValue left,
        IShaderValue right) =>
        Instruction.Factory.Operation2(default, operation, result, left, right);

    private static Instruction<IShaderValue, IShaderValue> Unary(
        IUnaryExpressionOperation operation,
        IShaderValue result,
        IShaderValue operand) =>
        Instruction.Factory.Operation1(default, operation, result, operand);

    private sealed class WasmProgram
    {
        private const int TimeoutMilliseconds = 10_000;
        private const string NodeProgram = """
            const fs = require("fs");
            const [path, ...args] = process.argv.slice(1);
            WebAssembly.instantiate(fs.readFileSync(path))
              .then(({ instance }) => console.log(instance.exports.run(...args.map(Number))))
              .catch(error => { console.error(error); process.exit(1); });
            """;

        private readonly string wasmPath;

        private WasmProgram(string wasmPath)
        {
            this.wasmPath = wasmPath;
        }

        internal static WasmProgram Compile(FunctionBody4 body, string name)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "wasm-execution");
            Directory.CreateDirectory(directory);
            var wat = Path.Combine(directory, $"{name}.wat");
            var wasm = Path.Combine(directory, $"{name}.wasm");
            File.WriteAllText(wat, WatEmitter.Emit(WasmLowering.Lower(body)));
            Run("wat2wasm", wat, "-o", wasm);
            Run("wasm-validate", wasm);
            return new WasmProgram(wasm);
        }

        internal int Execute(params int[] arguments)
        {
            var output = Run(
                "node",
                ["--eval", NodeProgram, "--", wasmPath,
                    .. arguments.Select(value => value.ToString(CultureInfo.InvariantCulture))]);
            return int.Parse(output.Trim(), CultureInfo.InvariantCulture);
        }

        internal static string ToolVersion(string executable) => Run(executable, "--version");

        private static string Run(string executable, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            Process process;
            try
            {
                process = Process.Start(startInfo) ??
                    throw new XunitException($"Failed to start required tool '{executable}'.");
            }
            catch (Win32Exception exception)
            {
                throw new XunitException(
                    $"Required tool '{executable}' was not found; run tests in the pinned Nix shell. {exception.Message}");
            }
            using (process)
            {
                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                    throw new XunitException(
                        $"Required tool '{executable}' exceeded the {TimeoutMilliseconds} ms timeout.");
                }
                Task.WaitAll(standardOutput, standardError);
                if (process.ExitCode != 0)
                    throw new XunitException(
                        $"Required tool '{executable}' exited {process.ExitCode}.\n" +
                        $"stdout:\n{standardOutput.Result}\nstderr:\n{standardError.Result}");
                return standardOutput.Result;
            }
        }
    }
}

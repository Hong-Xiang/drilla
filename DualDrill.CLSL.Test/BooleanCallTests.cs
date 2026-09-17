using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Test;

[Collection(SlangProcessTestCollection.Name)]
public sealed class BooleanCallTests
{
    [Fact]
    public void BoolCallOperandsMatchSignaturesAndPreserveArgumentOrder()
    {
        var parser = new RuntimeReflectionParser();
        var entry = parser.ParseMethod(((Func<int, int>)BooleanCallShader.BooleanFragment).Method);
        var calls = 0;
        foreach (var body in parser.MethodBodies.Values)
        {
            var instructions = body.Labels.SelectMany(label => body[label].Body.Elements).ToArray();
            foreach (var call in instructions.Where(instruction => instruction.Operation is CallOperation))
            {
                calls++;
                var function = Assert.IsType<FunctionDeclaration>(call.Operand0);
                var arguments = call.Operands.Skip(1).ToArray();
                Assert.Equal(function.Parameters.Length, arguments.Length);
                foreach (var (parameter, argument) in function.Parameters.Zip(arguments))
                {
                    Assert.Equal(parameter.Type, argument.Type);
                    if (parameter.Type is not BoolType)
                        continue;
                    var conversion = Assert.Single(instructions, instruction => instruction.Result == argument);
                    var operation = Assert.IsAssignableFrom<IConversionOperation>(conversion.Operation);
                    Assert.Equal(ShaderType.I32, operation.SourceType);
                    Assert.Equal(ShaderType.I32, conversion.Operand0.Type);
                    Assert.Equal(ShaderType.Bool, operation.ResultType);
                }
                if (function.ReturnType is BoolType)
                {
                    Assert.Contains(instructions, instruction =>
                        instruction.Operation is IConversionOperation conversion &&
                        conversion.SourceType.Equals(ShaderType.Bool) &&
                        conversion.ResultType.Equals(ShaderType.I32) &&
                        instruction.Operand0 == call.Result);
                }
            }
        }
        Assert.Equal(6, calls);

        var entryBody = parser.MethodBodies[entry];
        var elements = entryBody.Labels.SelectMany(label => entryBody[label].Body.Elements).ToArray();
        var selections = elements.Where(instruction => instruction.Operation is CallOperation &&
            instruction.Operand0 is FunctionDeclaration { Name: nameof(BooleanCallShader.Select) }).ToArray();
        Assert.Equal(3, selections.Length);
        foreach (var (call, pair) in selections.Zip(new[] { (31, 47), (53, 71), (79, 97) }))
        {
            var arguments = call.Operands.Skip(1).ToArray();
            var left = Assert.Single(elements, instruction => instruction.Result == arguments[0]);
            var right = Assert.Single(elements, instruction => instruction.Result == arguments[2]);
            Assert.Equal(new I32Literal(pair.Item1), Assert.IsType<LiteralValue>(left.Operand0).Value);
            Assert.Equal(new I32Literal(pair.Item2), Assert.IsType<LiteralValue>(right.Operand0).Value);
        }
    }

    [Fact]
    public void OtherScalarMismatchesRemainRejected()
    {
        var parser = new RuntimeReflectionParser();
        var unsigned = Assert.Throws<ValidationException>(() =>
            parser.ParseMethod(((Func<uint, uint>)BooleanCallShader.ForwardUnsigned).Method));
        Assert.Contains("parameter arg(value: u32) not match", unsigned.Message);

        // Inject a malformed caller type: ordinary C# cannot pass a float to a bool parameter.
        parser = new RuntimeReflectionParser();
        var forwarded = ((Func<bool, int>)BooleanCallShader.Forwarded).Method;
        parser.Context.AddParameter(Symbol.Parameter(Assert.Single(forwarded.GetParameters())),
            new ParameterDeclaration("choose", ShaderType.F32, []));
        var floating = Assert.Throws<ValidationException>(() => parser.ParseMethod(forwarded));
        Assert.Contains("parameter arg(choose: bool) not match", floating.Message);
    }

    [Fact]
    public void BooleanCallsCompileThroughPublicSlangAndWgslApis()
    {
        Assert.Equal(210, BooleanCallShader.BooleanFragment(-1));
        Assert.Equal(210, BooleanCallShader.BooleanFragment(0));
        Assert.Equal(208, BooleanCallShader.BooleanFragment(1));
        var shader = new BooleanCallShader();
        var slang = new CLSLCompiler(new(CLSLCompileTarget.SLang)).Emit(shader);
        Assert.Contains("bool(", slang);
        var wgsl = new CLSLCompiler(new(CLSLCompileTarget.WGSL)).Emit(shader);
        Assert.Contains("fn BooleanFragment", wgsl);
        Assert.Contains("@fragment", wgsl);
    }
}

internal sealed class BooleanCallShader : ISharpShader
{
    [Fragment]
    [return: Location(0)]
    public static int BooleanFragment([Location(0)] int x) =>
        Select(31, Invert(x > 0), 47) + Forwarded(x > 0) + Select(53, true, 71) + Select(79, false, 97);

    public static bool Invert(bool value) => !value;
    public static int Select(int left, bool choose, int right)
    {
        if (choose)
            return left;
        return right;
    }
    public static int Forwarded(bool choose) => Select(11, choose, 29);
    public static uint Unsigned(uint value) => value;
    public static uint ForwardUnsigned(uint value) => Unsigned(value);
}

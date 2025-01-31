using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common.Nat;
using DualDrill.ILSL;
using DualDrill.ILSL.Compiler;
using DualDrill.ILSL.Frontend;
using DualDrill.Mathematics;
using System.Numerics;
using System.Reflection;
using FluentAssertions;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Test;


public class ParseBodyTest
{
    IExpression? ParseExpressionBodyMethod(MethodBase m)
    {
        var stmts = ParseStatementsMethod(m);
        Assert.Single(stmts);
        Assert.IsType<ReturnStatement>(stmts[0]);
        return ((ReturnStatement)stmts[0]).Expr;
    }

    IReadOnlyList<IStatement> ParseStatementsMethod(MethodBase m)
    {
        //var compiler = new MethodBodyCompiler();
        //var parser = new ShaderModuleMetadataParser();
        //parser.ParseMethodMetadata(m);
        //var methodContext = parser.Context.GetMethodContext(m);
        //var body = methodParser.ParseMethodBody(methodContext, m);
        //return body.Statements;
        throw new NotImplementedException();
    }

    UnstructuredControlFlowInstructionFunctionBody ParseMethod(FunctionDeclaration f, MethodBase m)
    {
        var context = CompilationContext.Create();
        context.AddFunctionDefinition(Symbol.Function(m), f);
        var parser = new RuntimeReflectionMethodBodyParser(context);
        return parser.Parse(f);
    }

    [Fact]
    public void ParseBasicLiteralExpressionBodyShouldWork()
    {
        var f = new FunctionDeclaration("return42", [], new FunctionReturn(ShaderType.I32, []), []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static () => 42));
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(42),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void MinimumLoadArgumentShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(BasicLoadArgumentShouldWork),
            [a],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (int a) => a));
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Should().Be(a),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void BasicLoadArgumentShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(BasicLoadArgumentShouldWork),
            [a],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (int a) => a + 1));
        result.Instructions.Should().HaveCount(7);
        var label = Assert.IsType<LabelInstruction>(result.Instructions[4]).Label;
        var v0 = Assert.IsType<Store<VariableDeclaration>>(result.Instructions[2]).Target;
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<NopInstruction>(),
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().And.Be(a),
            x => x.Should().BeOfType<Store<VariableDeclaration>>().Which.Target.Should().Be(v0),
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(label),
            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(label),
            x => x.Should().BeOfType<Load<VariableDeclaration>>().Which.Target.Should().Be(v0),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }


    [Fact]
    public void SimpleVec4ConstructionShouldWork()
    {
        var f = new FunctionDeclaration(
            nameof(SimpleVec4ConstructionShouldWork),
            [],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []
        );
        var result = ParseMethod(f, MethodHelper.GetMethod(() => new Vector4(0.5f, 0.0f, 1.0f, 1.0f)));
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<NopInstruction>(),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(0.5f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(0.0f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(1.0f),
            x => x.Should().BeOfType<ConstInstruction<F32Literal>>().Which.Literal.Value.Should().Be(1.0f),
            x => x.Should().Satisfy<CallInstruction>(c =>
            {
                c.Callee.Parameters.Should().HaveCount(4).And.AllSatisfy(p =>
                {
                    p.Type.Should().Be(ShaderType.F32);
                });
                c.Callee.Return.Type.Should().Be(ShaderType.Vec4F32);
            }),
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            x => x.Should().BeOfType<BrInstruction>(),
            x => x.Should().BeOfType<LabelInstruction>(),
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }


    [Fact]
    public void BasicMethodInvocationParseShouldWork()
    {


        static int Add(int a, int b) => a + b;
        var context = CompilationContext.Create();
        var fAdd = new FunctionDeclaration(
            nameof(Add),
            [new ParameterDeclaration("a", ShaderType.I32, []),
             new ParameterDeclaration("b", ShaderType.I32, [])],
            new FunctionReturn(ShaderType.I32, []),
            []);
        context.AddFunctionDeclaration(Symbol.Function(MethodHelper.GetMethod<int, int, int>(Add)), fAdd);
        var fCall = new FunctionDeclaration(nameof(BasicMethodInvocationParseShouldWork), [], new FunctionReturn(ShaderType.I32, []), []);
        var method = MethodHelper.GetMethod(() => Add(1, 2));
        context.AddFunctionDefinition(Symbol.Function(method), fCall);
        var parser = new RuntimeReflectionMethodBodyParser(context);
        var result = parser.Parse(fCall);
        result.Instructions.Should().SatisfyRespectively(
            x => x.Should().BeOfType<NopInstruction>(),
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(1),
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(2),
            x => x.Should().BeOfType<CallInstruction>().Which.Callee.Should().Be(fAdd),
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            x => x.Should().BeOfType<BrInstruction>(),
            x => x.Should().BeOfType<LabelInstruction>(),
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void BasicIfThenElseParseShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(BasicIfThenElseParseShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.I32, []),
            []
        );
        var result = ParseMethod(f, MethodHelper.GetMethod(static (int a, int b) =>
        {
            if (a >= b)
            {
                return a;
            }
            else
            {
                return b;
            }
        }));
        //  IL_0000: nop
        //  IL_0001: ldarg.0
        //  IL_0002: ldarg.1
        //  IL_0003: clt
        //  IL_0005: ldc.i4.0
        //  IL_0006: ceq
        //  IL_0008: stloc.0
        //  IL_0009: ldloc.0
        //  IL_000a: brfalse.s IL_0011

        //  IL_000c: nop
        //  IL_000d: ldarg.0
        //  IL_000e: stloc.1
        //  IL_000f: br.s IL_0016

        //  IL_0011: nop
        //  IL_0012: ldarg.1
        //  IL_0013: stloc.1
        //  IL_0014: br.s IL_0016

        //  IL_0016: ldloc.1
        //  IL_0017: ret

        ImmutableArray<LabelInstruction> labels = [.. result.Instructions.OfType<LabelInstruction>()];

        labels.Should().HaveCount(2).And.OnlyHaveUniqueItems();

        var l11 = labels[0];
        var l16 = labels[1];

        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarg.0
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0002: ldarg.1
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(b),
            // IL_0003: clt
            x => x.Should().BeOfType<NumericInstruction<NumericSignedIntegerOp<N32, BinaryRelation.Lt, Signedness.S>>>(),
            // IL_0005: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
            // IL_0006: ceq
            x => x.Should().BeOfType<NumericInstruction<NumericIntegerOp<N32, BinaryRelation.Eq>, Signedness.S>>(),
            // IL_0008: stloc.0
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            // IL_0009: ldloc.0
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            // IL_000a: brfalse.s IL_0011
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l11),

            // IL_000c: nop
            x => x.Should().BeOfType<NopInstruction>(),
            // IL_000d: ldarg.0
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(a),
            // IL_000e: stloc.1
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            // IL_000f: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l11),
            // IL_0011: nop
            x => x.Should().BeOfType<NopInstruction>(),
            // IL_0012: ldarg.1
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(b),
            // IL_0013: stloc.1
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            // IL_0014: br.s IL_0016
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l16),

            x => x.Should().BeOfType<LabelInstruction>().Which.Label.Should().Be(l16),
            // IL_0016: ldloc.1
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            // IL_0017: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void VectorSwizzleGetterShouldWork()
    {
        //  IL_0000: nop
        //  IL_0001: ldarga.s a
        //  IL_0003: swizzle.get.vec4f32.xyx [ref<vec4f32>] -> [vec3f32]
        //  IL_0008: stloc.0
        //  IL_0009: br.s IL_000b

        //  IL_000b: ldloc.0
        //  IL_000c: ret

        var v = new ParameterDeclaration("v", ShaderType.Vec4F32, []);
        var f = new FunctionDeclaration(
                nameof(VectorSwizzleGetterShouldWork),
                [],
                new FunctionReturn(ShaderType.Vec2F32, []),
                []
            );
        var result = ParseMethod(f, MethodHelper.GetMethod(static (vec4f32 v) => v.xyx));

        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarga.s a
            x => x.Should().BeOfType<LoadAddress<ParameterDeclaration>>().Which.Target.Should().Be(v),
            //  IL_0003: swizzle.vec4f32.xyx [ref<vec4f32>] -> [vec3f32]
            x => x.Should().BeOfType<VectorSwizzleGetInstruction<VecType<N4, FloatType<N32>>, Swizzle.Pattern<Swizzle.X, Swizzle.Y, Swizzle.X>>>(),
            //  IL_0008: stloc.0
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            //  IL_0009: br.s IL_000b
            x => x.Should().BeOfType<BrInstruction>(),

            x => x.Should().BeOfType<LabelInstruction>(),
            //  IL_000b: ldloc.0
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            //  IL_000c: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void SimpleVectorSwizzleSetShouldWork()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec2F32, []);
        var f = new FunctionDeclaration(
            nameof(SimpleVectorSwizzleSetShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.Vec4F32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (vec4f32 a, vec2f32 b) =>
        {
            a.xy = b;
            return a;
        }));
        //  IL_0000: nop
        //  IL_0001: ldarga @a
        //  IL_0003: ldarg @b
        //  IL_0005: swizzle.set.vec4f32.xy
        //  IL_000f: nop
        //  IL_0010: ldarg.0
        //  IL_0011: stloc.0
        //  IL_0012: br.s IL_0014

        //  IL_0014: ldloc.0
        //  IL_0015: ret


        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0001: ldarga @a
            x => x.Should().BeOfType<LoadAddress<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0003: ldarg @b
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(b),
            //  IL_0005: swizzle.set.vec4f32.xy
            x => x.Should().BeOfType<VectorSwizzleSetInstruction<VecType<N4, FloatType<N32>>, Swizzle.Pattern<Swizzle.X, Swizzle.Y>>>(),
            //  IL_000f: nop
            x => x.Should().BeOfType<NopInstruction>(),
            //  IL_0010: ldarg.0
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0011: stloc.0
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
            //  IL_0012: br IL_0014
            x => x.Should().BeOfType<BrInstruction>(),

            x => x.Should().BeOfType<LabelInstruction>(),
            //  IL_0014: ldloc.0
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
            //  IL_0015: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }

    [Fact]
    public void Vec4DotTest()
    {
        var a = new ParameterDeclaration("a", ShaderType.Vec4F32, []);
        var b = new ParameterDeclaration("b", ShaderType.Vec4F32, []);
        var f = new FunctionDeclaration(
            nameof(SimpleVectorSwizzleSetShouldWork),
            [a, b],
            new FunctionReturn(ShaderType.F32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (Vector4 a, Vector4 b) => Vector4.Dot(a, b)));
        //  IL_0000: ldarg.0
        //  IL_0001: ldarg.1
        //  IL_0002: call dot.vec4f32
        //  IL_0007: ret
        result.Instructions.Should().SatisfyRespectively(
            //  IL_0000: ldarg.0
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(a),
            //  IL_0001: ldarg.1
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(b),
            //  IL_0002: call dot.vec4f32
            x => x.Should().BeOfType<CallInstruction>(),
            //  IL_0007: ret
            x => x.Should().BeOfType<ReturnInstruction>()
         );
    }

    [Fact]
    public void CompileTaneryConditionalExpressionShouldWork()
    {
        //  IL_0000: nop
        //  IL_0001: ldarg.1
        //  IL_0002: ldc.i4.0
        //  IL_0003: ble.s IL_0008

        //  IL_0005: ldarg.3
        //  IL_0006: br.s IL_0009

        //  IL_0008: ldarg.2

        //  IL_0009: stloc.0
        //  IL_000a: br.s IL_000c

        //  IL_000c: ldloc.0
        //  IL_000d: ret
        var a = new ParameterDeclaration("a", ShaderType.I32, []);
        var b = new ParameterDeclaration("b", ShaderType.I32, []);
        var c = new ParameterDeclaration("c", ShaderType.I32, []);
        var f = new FunctionDeclaration(
            nameof(CompileTaneryConditionalExpressionShouldWork),
            [a, b, c],
            new FunctionReturn(ShaderType.I32, []),
            []);
        var result = ParseMethod(f, MethodHelper.GetMethod(static (int a, int b, int c) => a <= 0 ? b : c));

        var labels = result.Instructions.OfType<LabelInstruction>().ToArray();
        labels.Should().HaveCount(3).And.OnlyHaveUniqueItems();

        var l8 = labels[0];
        var l9 = labels[1];
        var lc = labels[2];

        result.Instructions.Should().SatisfyRespectively(
        //  IL_0000: nop
            x => x.Should().BeOfType<NopInstruction>(),
        //  IL_0001: ldarg.1
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(a),
        //  IL_0002: ldc.i4.0
            x => x.Should().BeOfType<ConstInstruction<I32Literal>>().Which.Literal.Value.Should().Be(0),
        //  IL_0003: ble.s IL_0008
            x => x.Should().BeOfType<NumericInstruction<NumericSignedIntegerOp<N32, BinaryRelation.Le, Signedness.S>>>(),
            x => x.Should().BeOfType<BrIfInstruction>().Which.Target.Should().Be(l8),

        //  IL_0005: ldarg.3
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(c),
        //  IL_0006: br.s IL_0009
            x => x.Should().BeOfType<BrInstruction>().Which.Target.Should().Be(l9),

            x => x.Should().BeOfType<LabelInstruction>().Should().Be(l8),
        //  IL_0008: ldarg.2
            x => x.Should().BeOfType<Load<ParameterDeclaration>>().Which.Target.Should().Be(b),

            x => x.Should().BeOfType<LabelInstruction>().Should().Be(l9),
        //  IL_0009: stloc.0
            x => x.Should().BeOfType<Store<VariableDeclaration>>(),
        //  IL_000a: br.s IL_000c
            x => x.Should().BeOfType<BrInstruction>().Should().Be(lc),

            x => x.Should().BeOfType<LabelInstruction>().Should().Be(lc),
        //  IL_000c: ldloc.0
            x => x.Should().BeOfType<Load<VariableDeclaration>>(),
        //  IL_000d: ret
            x => x.Should().BeOfType<ReturnInstruction>()
        );
    }
}

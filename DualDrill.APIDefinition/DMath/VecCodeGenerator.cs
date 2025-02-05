using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using IOperation = DualDrill.CLSL.Language.Operation.IOperation;

namespace DualDrill.ApiGen.DMath;

interface IVectorDetailCodeGenerator
{
    void GenerateMemberDeclaration();
    void GeneratePrimaryFactoryMethodBody();
    string CreateVectorExpression(Func<string[], IEnumerable<string>> componentExpressions);
    void GenerateArithmeticOperatorBody(IVectorBinaryNumericOperation op);
    void GenerateArithmeticOperatorBody(UnaryArithmeticOperatorDefinition op);
}

internal sealed record class VectorComponentCodeGenerator(
    IVecType VecType,
    CSharpProjectionConfiguration Config,
    IndentedTextWriter Writer) : IVectorDetailCodeGenerator
{
    public void GenerateMemberDeclaration()
    {
        foreach (var m in VecType.Size.SwizzleComponents())
        {
            Writer.Write($"public {Config.GetCSharpTypeName(VecType.ElementType)} {m.Name} ");
            Writer.WriteLine("{");
            using (Writer.IndentedScope())
            {
                Writer.WriteAggressiveInlining();
                Writer.WriteLine($"[{new RuntimeVectorSwizzleGetMethodAttribute([Enum.Parse<SwizzleComponent>(m.Name)]).GetCSharpUsageCode()}]");
                Writer.WriteLine($"[{VecType.ComponentGetOperation(m).GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                Writer.WriteLine("get;");
                Writer.WriteAggressiveInlining();
                Writer.WriteLine($"[{new RuntimeVectorSwizzleSetMethodAttribute([Enum.Parse<SwizzleComponent>(m.Name)]).GetCSharpUsageCode()}]");
                Writer.WriteLine($"[{VecType.ComponentSetOperation(m).GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                Writer.WriteLine("set;");
            }
            Writer.WriteLine();
            Writer.WriteLine("}");
        }
    }

    public void GeneratePrimaryFactoryMethodBody()
    {
        Writer.Write($"return new {Config.GetCSharpTypeName(VecType)} () ");
        Writer.WriteLine('{');
        using (Writer.IndentedScope())
        {
            Writer.WriteSeparatedList(TextCodeSeparator.CommaNewLine, [.. VecType.Size.Components().Select(m => $"{m} = {m}")]);
        }
        Writer.WriteLine();
        Writer.WriteLine("};");
    }

    public string CreateVectorExpression(Func<string[], IEnumerable<string>> componentExpressions)
    {
        var components = VecType.Size.Components().ToArray();
        var expressions = componentExpressions(components);
        var assignments = components.Zip(expressions, (c, expr) => $"{c} = {expr}");
        return $"new {Config.GetCSharpTypeName(VecType)}() {{ {string.Join(", ", assignments)} }}";
    }

    public void GenerateArithmeticOperatorBody(UnaryArithmeticOperatorDefinition op)
    {
        Writer.Write($"return vec{VecType.Size.Value}(");
        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => $"({Config.GetCSharpTypeName(VecType.ElementType)})({Config.OpName(op.Op)}v.{m})")]);
        Writer.WriteLine(");");
    }

    public void GenerateArithmeticOperatorBody(IVectorBinaryNumericOperation op)
    {
        Writer.Write($"return vec{VecType.Size.Value}(");
        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => {
            var l = op.LeftType is IScalarType ?  "left" : $"left.{m}";
            var r = op.RightType is IScalarType ?  "right" : $"right.{m}";
            return $"({Config.GetCSharpTypeName(VecType.ElementType)})({l} {Config.OpName(op.Op)} {r})"; })]);
        Writer.WriteLine(");");
    }
}

internal sealed record class VectorSimdCodeGenerator(
    IVecType VecType,
    CSharpProjectionConfiguration Config,
    IndentedTextWriter Writer) : IVectorDetailCodeGenerator
{
    int SimdDataBitWidth => (VecType.Size.Value == 3 ? 4 : VecType.Size.Value) * VecType.ElementType.BitWidth.Value;
    string SimdStaticDataTypeName => $"Vector{SimdDataBitWidth}";
    string SimdDataTypeName => $"Vector{SimdDataBitWidth}<{Config.GetCSharpTypeName(VecType.ElementType)}>";

    public void GenerateMemberDeclaration()
    {
        Writer.WriteLine($"internal {SimdDataTypeName} Data;");
        Writer.WriteLine();

        foreach (var (m, i) in VecType.Size.SwizzleComponents().Select((x, i) => (x, i)))
        {
            Writer.Write($"public {Config.GetCSharpTypeName(VecType.ElementType)} {m.Name}");
            Writer.WriteLine(" {");
            Writer.Indent++;

            //getter
            Writer.WriteAggressiveInlining();
            Writer.WriteLine($"[{new RuntimeVectorSwizzleGetMethodAttribute([Enum.Parse<SwizzleComponent>(m.Name)]).GetCSharpUsageCode()}]");
            Writer.WriteLine($"[{VecType.ComponentGetOperation(m).GetOperationMethodAttribute().GetCSharpUsageCode()}]");
            Writer.WriteLine($"get => Data[{i}];");
            Writer.WriteLine();

            Writer.WriteAggressiveInlining();
            Writer.WriteLine($"[{new RuntimeVectorSwizzleSetMethodAttribute([Enum.Parse<SwizzleComponent>(m.Name)]).GetCSharpUsageCode()}]");
            Writer.WriteLine($"[{VecType.ComponentSetOperation(m).GetOperationMethodAttribute().GetCSharpUsageCode()}]");
            Writer.WriteLine("set {");
            Writer.Indent++;
            Writer.Write($"Data = Vector{SimdDataBitWidth}.Create(");

            var argumentCount = VecType.Size.Value == 3 ? 4 : VecType.Size.Value;
            for (var ia = 0; ia < argumentCount; ia++)
            {
                if (ia == i)
                {
                    Writer.Write("value");
                }
                else
                {
                    Writer.Write($"Data[{ia}]");
                }
                if (ia < argumentCount - 1)
                {
                    Writer.Write(", ");
                }
            }

            Writer.WriteLine(");");

            Writer.Indent--;
            Writer.WriteLine("}");
            Writer.WriteLine();

            Writer.Indent--;
            Writer.WriteLine("}");
            Writer.WriteLine();
        }
    }

    public void GeneratePrimaryFactoryMethodBody()
    {
        Writer.Write($"return new {Config.GetCSharpTypeName(VecType)}() ");
        Writer.Write("{ Data = ");
        Writer.Write($"{SimdStaticDataTypeName}.Create(");
        List<string> args = [.. VecType.Size.Components()];
        if (VecType.Size.Value == 3)
        {
            args.Add("default");
        }
        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. args]);
        Writer.WriteLine(") };");
    }

    public string CreateVectorExpression(Func<string[], IEnumerable<string>> componentExpressions)
    {
        var components = VecType.Size.Components().ToArray();
        var expressions = componentExpressions(components);
        var argumentList = string.Join(", ", expressions);

        return $"{SimdStaticDataTypeName}.Create({argumentList})";
    }
    public void GenerateArithmeticOperatorBody(UnaryArithmeticOperatorDefinition op)
    {
        Writer.WriteLine($"return new() {{ Data = {Config.OpName(op.Op)} v.Data }};");
    }

    public void GenerateArithmeticOperatorBody(IVectorBinaryNumericOperation op)
    {
        Writer.WriteLine("throw new NotImplementedException();");
        return;
        if (op.Op is BinaryArithmetic.Rem)
        {
            var l = op.LeftType is IScalarType ? $"{SimdStaticDataTypeName}.Create(left)" : "left.Data";
            var r = op.RightType is IScalarType ? $"{SimdStaticDataTypeName}.Create(right)" : "right.Data";
            Writer.WriteLine($"return new() {{ Data = {l} {Config.OpName(op.Op)} {r} }};");
        }
        else
        {
            Writer.Write($"return vec{VecType.Size.Value}(");
            List<string> args = [.. VecType.Size.Components().Select(c => {
                var l = op.LeftType is IScalarType ? "left" : $"left.{c}";
                var r = op.RightType is IScalarType ? "right" : $"right.{c}";
                return $"({Config.GetCSharpTypeName(VecType.ElementType)})({l} % {r})";
            })];
            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. args]);
            Writer.WriteLine(");");
        }
    }
}


public sealed record class VecCodeGenerator<TRank, TElement>
    where TRank : IRank<TRank>
    where TElement : IScalarType<TElement>
{
    VecType<TRank, TElement> VecType { get; }
    public IndentedTextWriter Writer { get; }
    CSharpProjectionConfiguration Config { get; }
    IVectorDetailCodeGenerator DetailCodeGenerator { get; }

    static bool IsSimdSupported(IVecType vecType)
    {
        // for numeric vectors with data larger than 64 bits (except System.Half, which is not supported in VectorXX<Half>),
        // we use .NET builtin SIMD optimization
        // for vec3, we use vec4's data for optimizing memory access and SIMD optimization
        return vecType switch
        {
            { ElementType: BoolType } => false,
            { ElementType: IFloatType { BitWidth: N16 } } => false,
            { ElementType: var e, Size: N3 } when e.BitWidth.Value * 4 >= 64 => true,
            { ElementType: var e, Size: var s } when e.BitWidth.Value * s.Value >= 64 => true,
            _ => false
        };
    }

    public VecCodeGenerator(VecType<TRank, TElement> vecType, IndentedTextWriter writer, CSharpProjectionConfiguration config)
    {
        VecType = vecType;
        Writer = writer;
        Config = config;
        DetailCodeGenerator = IsSimdSupported(VecType)
            ? new VectorSimdCodeGenerator(VecType, Config, Writer)
            : new VectorComponentCodeGenerator(VecType, Config, Writer);
    }

    //public void GenerateStaticMethods()
    //{
    //    Writer.Write($"public static partial class {Config.StaticMathTypeName}");
    //    using (Writer.IndentedScopeWithBracket())
    //    {
    //        var constructors = from f in ShaderFunction.Instance.Functions
    //                           where f.Return.Type.Equals(VecType)
    //                           where f.Attributes.Any(x => x is IVecConstructorMethodAttribute)
    //                           select f;

    //        Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}(");

    //        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => $"{Config.GetCSharpTypeName(VecType.ElementType)} {m}")]);
    //        Writer.Write(')');

    //        using (Writer.IndentedScopeWithBracket())
    //        {
    //            DetailCodeGenerator.GeneratePrimaryFactoryMethodBody();
    //        }


    //        Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}({Config.GetCSharpTypeName(VecType.ElementType)} e) => vec{VecType.Size.Value}(");
    //        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(_ => "e")]);
    //        Writer.WriteLine(");");

    //        {
    //            var v2t = ShaderType.GetVecType(N2.Instance, VecType.ElementType);
    //            var v3t = ShaderType.GetVecType(N3.Instance, VecType.ElementType);

    //            int[][] patterns = VecType.Size switch
    //            {
    //                N3 => [[1, 2], [2, 1]],
    //                N4 => [[1, 3], [3, 1], [1, 1, 2], [1, 2, 1], [2, 1, 1], [2, 2]],
    //                _ => []
    //            };
    //            string[] components = [.. VecType.Size.Components()];
    //            foreach (var ptn in patterns)
    //            {
    //                Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}(");
    //                var ps = ptn.Select((d, i) => d switch
    //                {
    //                    1 => Config.GetCSharpTypeName(VecType.ElementType),
    //                    2 => Config.GetCSharpTypeName(v2t),
    //                    3 => Config.GetCSharpTypeName(v3t),
    //                    _ => throw new NotImplementedException()
    //                } + $" e{i}");
    //                Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. ps]);
    //                Writer.Write($") => vec{VecType.Size.Value}(");
    //                List<string> args = [];
    //                foreach (var (p, i) in ptn.Select((p, i) => (p, i)))
    //                {
    //                    var name = $"e{i}";
    //                    switch (p)
    //                    {
    //                        case 1:
    //                            args.Add(name);
    //                            break;
    //                        case 2:
    //                            args.Add($"{name}.x");
    //                            args.Add($"{name}.y");
    //                            break;
    //                        case 3:
    //                            args.Add($"{name}.x");
    //                            args.Add($"{name}.y");
    //                            args.Add($"{name}.z");
    //                            break;
    //                        default:
    //                            break;
    //                    }
    //                }
    //                Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. args]);
    //                Writer.WriteLine(");");
    //            }
    //        }
    //    }
    //}


    public void GenerateDeclaration()
    {
        Writer.WriteStructLayout();
        var csharpName = Config.GetCSharpTypeName(VecType);
        Writer.Write("public partial struct ");
        Writer.Write(csharpName);
        using (Writer.IndentedScopeWithBracket())
        {
            DetailCodeGenerator.GenerateMemberDeclaration();
            foreach (var op in ShaderOperator.UnaryArithmeticOperatorDefinitions.Where(o => o.Source.Equals(VecType)))
            {
                Writer.WriteAggressiveInlining();
                if (op.Op == UnaryArithmeticOp.Minus && TElement.Instance is INumericType nt)
                {
                    IOperation operation = nt.GetVectorUnaryNumericOperation<TRank, UnaryArithmetic.Neg>();
                    Writer.WriteLine($"[{operation.GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                }
                Writer.WriteLine($"public static {Config.GetCSharpTypeName(op.Result)} operator {Config.OpName(op.Op)}({Config.GetCSharpTypeName(op.Source)} v)");
                using (Writer.IndentedScopeWithBracket())
                {
                    DetailCodeGenerator.GenerateArithmeticOperatorBody(op);
                }
            }

            if (VecType.ElementType is not BoolType)
            {
                foreach (var op in VecType.GetBinaryNumericOperations())
                {
                    Writer.WriteAggressiveInlining();
                    Writer.WriteLine($"[{op.GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                    Writer.WriteLine($"public static {Config.GetCSharpTypeName(VecType)} operator {Config.OpName(op.Op)}({Config.GetCSharpTypeName(op.LeftType)} left, {Config.GetCSharpTypeName(op.RightType)} right)");
                    using (Writer.IndentedScopeWithBracket())
                    {
                        DetailCodeGenerator.GenerateArithmeticOperatorBody(op);
                    }
                }
            }


            GenerateSwizzles();
        }
    }


    public void GenerateSwizzles()
    {
        //static IEnumerable<ImmutableArray<string>> MakePermutations(int count, IReadOnlyList<string> components)
        //{
        //    if (count <= 0)
        //    {
        //        yield return [];
        //        yield break;
        //    }
        //    var result = new string[count];
        //    if (count == 1)
        //    {
        //        foreach (var c in components)
        //        {
        //            result[0] = c;
        //            yield return [.. result];
        //        }
        //        yield break;
        //    }
        //    var q = from n in MakePermutations(count - 1, components)
        //            from c in components
        //            select (string[])[c, .. n];
        //    foreach (var r in q)
        //    {
        //        yield return [.. r];
        //    }
        //}

        //var sizedComponents = VecType.Size.SwizzleComponents()
        //                                  .OfType<Swizzle.ISizedComponent<TRank>>()
        //                                  .ToImmutableArray();
        //Debug.Assert(sizedComponents.Length == VecType.Size.Value);

        //var p2s = from c0 in sizedComponents
        //          from c1 in sizedComponents
        //          select c0.PatternAfterComponent(c1);

        //string[] components = [.. VecType.Size.Components()];
        //foreach (var rank in (IRank[])[N2.Instance, N3.Instance, N4.Instance])
        //{
        //    var rv = ShaderType.GetVecType(rank, VecType.ElementType);
        //    foreach (var sw in MakePermutations(rank.Value, components))
        //    {

        //    }
        //}
        foreach (var p in Swizzle.SwizzlePatterns<TRank>())
        {
            _ = p.Accept(new SwizzleForPatternGenerator(Config, Writer));
        }
    }

    sealed class SwizzleForPatternGenerator(
        CSharpProjectionConfiguration Config,
        IndentedTextWriter Writer) : Swizzle.ISizedPattern<TRank>.ISizedPatternVisitor<Unit>
    {
        public Unit Visit<TPattern>() where TPattern : Swizzle.ISizedPattern<TRank, TPattern>
        {

            var pattern = TPattern.Instance;
            var rv = pattern.TargetType<TElement>();
            Writer.Write($"public {Config.GetCSharpTypeName(rv)} {TPattern.Instance.Name} ");
            //var cps = sw.Select(Enum.Parse<SwizzleComponent>).ToArray();
            using (Writer.IndentedScopeWithBracket())
            {
                Writer.WriteAggressiveInlining();
                //Writer.WriteLine($"[{new RuntimeVectorSwizzleGetMethodAttribute(cps).GetCSharpUsageCode()}]");
                CLSL.Language.Operation.IOperation getOperation = VectorSwizzleGetOperation<TPattern, TElement>.Instance;
                Writer.WriteLine($"[{getOperation.GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                Writer.Write($"get => vec{rv.Size.Value}(");
                Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. pattern.Components.Select(c => c.Name)]);
                Writer.WriteLine(");");

                if (!pattern.HasDuplicateComponent)
                {
                    Writer.WriteLine();
                    Writer.WriteAggressiveInlining();
                    //Writer.WriteLine($"[{new RuntimeVectorSwizzleSetMethodAttribute(cps).GetCSharpUsageCode()}]");
                    CLSL.Language.Operation.IOperation setOperation = VectorSwizzleSetOperation<TPattern, TElement>.Instance;
                    Writer.WriteLine($"[{setOperation.GetOperationMethodAttribute().GetCSharpUsageCode()}]");
                    Writer.WriteLine("set ");

                    using (Writer.IndentedScopeWithBracket())
                    {
                        var sourceComponents = rv.Size.SwizzleComponents().ToImmutableArray();
                        foreach (var (ic, c) in pattern.Components.Index())
                        {
                            Writer.WriteLine($"{c.Name} = value.{sourceComponents[ic].Name};");
                        }
                    };

                }
            }
            return default;
        }
    }



    public void Generate()
    {
        GenerateDeclaration();
        //GenerateStaticMethods();
    }
}


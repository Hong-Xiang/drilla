using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.DMath;

interface IVectorDetailCodeGenerator
{
    void GenerateMemberDeclaration();
    void GeneratePrimaryFactoryMethodBody();
    string CreateVectorExpression(Func<string[], IEnumerable<string>> componentExpressions);
    void GenerateArithmeticOperatorBody(BinaryArithmeticOperatorDefinition op);
    void GenerateArithmeticOperatorBody(UnaryArithmeticOperatorDefinition op);
}

internal sealed record class VectorComponentCodeGenerator(
    IVecType VecType,
    CSharpProjectionConfiguration Config,
    IndentedTextWriter Writer) : IVectorDetailCodeGenerator
{
    public void GenerateMemberDeclaration()
    {
        foreach (var m in VecType.Size.Components())
        {
            Writer.Write($"public {Config.GetCSharpTypeName(VecType.ElementType)} {m} ");
            Writer.WriteLine("{ get; set; }");
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

    public void GenerateArithmeticOperatorBody(BinaryArithmeticOperatorDefinition op)
    {
        Writer.Write($"return vec{VecType.Size.Value}(");
        Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => {
            var l = op.Left is IScalarType ?  "left" : $"left.{m}";
            var r = op.Right is IScalarType ?  "right" : $"right.{m}";
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

        foreach (var (m, i) in VecType.Size.Components().Select((x, i) => (x, i)))
        {
            Writer.Write($"public {Config.GetCSharpTypeName(VecType.ElementType)} {m}");
            Writer.WriteLine(" {");
            Writer.Indent++;

            //getter
            Writer.WriteAggressiveInlining();
            Writer.WriteLine($"get => Data[{i}];");
            Writer.WriteLine();

            Writer.WriteAggressiveInlining();
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

    public void GenerateArithmeticOperatorBody(BinaryArithmeticOperatorDefinition op)
    {
        if (op.Op != BinaryArithmeticOp.Remainder)
        {
            var l = op.Left is IScalarType ? $"{SimdStaticDataTypeName}.Create(left)" : "left.Data";
            var r = op.Right is IScalarType ? $"{SimdStaticDataTypeName}.Create(right)" : "right.Data";
            Writer.WriteLine($"return new() {{ Data = {l} {Config.OpName(op.Op)} {r} }};");
        }
        else
        {
            Writer.Write($"return vec{VecType.Size.Value}(");
            List<string> args = [.. VecType.Size.Components().Select(c => {
                var l = op.Left is IScalarType ? "left" : $"left.{c}";
                var r = op.Right is IScalarType ? "right" : $"right.{c}";
                return $"({Config.GetCSharpTypeName(VecType.ElementType)})({l} % {r})";
            })];
            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. args]);
            Writer.WriteLine(");");
        }
    }
}


public sealed record class VecCodeGenerator
{
    IVecType VecType { get; }
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
            IVecType { ElementType: BoolType } => false,
            IVecType { ElementType: FloatType { BitWidth: N16 } } => false,
            IVecType { ElementType: var e, Size: N3 } when e.BitWidth.Value * 4 >= 64 => true,
            IVecType { ElementType: var e, Size: var s } when e.BitWidth.Value * s.Value >= 64 => true,
            _ => false
        };


    }

    public VecCodeGenerator(IVecType vecType, IndentedTextWriter writer, CSharpProjectionConfiguration config)
    {
        VecType = vecType;
        Writer = writer;
        Config = config;
        DetailCodeGenerator = IsSimdSupported(VecType)
            ? new VectorSimdCodeGenerator(VecType, Config, Writer)
            : new VectorComponentCodeGenerator(VecType, Config, Writer);
    }

    public void GenerateStaticMethods()
    {
        Writer.Write($"public static partial class {Config.StaticMathTypeName}");
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}(");

            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => $"{Config.GetCSharpTypeName(VecType.ElementType)} {m}")]);
            Writer.Write(')');

            using (Writer.IndentedScopeWithBracket())
            {
                DetailCodeGenerator.GeneratePrimaryFactoryMethodBody();
            }


            Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}({Config.GetCSharpTypeName(VecType.ElementType)} e) => vec{VecType.Size.Value}(");
            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(_ => "e")]);
            Writer.WriteLine(");");

            {
                var v2t = ShaderType.GetVecType(N2.Instance, VecType.ElementType);
                var v3t = ShaderType.GetVecType(N3.Instance, VecType.ElementType);

                int[][] patterns = VecType.Size switch
                {
                    N3 => [[1, 2], [2, 1]],
                    N4 => [[1, 3], [3, 1], [1, 1, 2], [1, 2, 1], [2, 1, 1], [2, 2]],
                    _ => []
                };
                string[] components = [.. VecType.Size.Components()];
                foreach (var ptn in patterns)
                {
                    Writer.Write($"public static {Config.GetCSharpTypeName(VecType)} vec{VecType.Size.Value}(");
                    var ps = ptn.Select((d, i) => d switch
                    {
                        1 => Config.GetCSharpTypeName(VecType.ElementType),
                        2 => Config.GetCSharpTypeName(v2t),
                        3 => Config.GetCSharpTypeName(v3t),
                        _ => throw new NotImplementedException()
                    } + $" e{i}");
                    Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. ps]);
                    Writer.Write($") => vec{VecType.Size.Value}(");
                    List<string> args = [];
                    foreach (var (p, i) in ptn.Select((p, i) => (p, i)))
                    {
                        var name = $"e{i}";
                        switch (p)
                        {
                            case 1:
                                args.Add(name);
                                break;
                            case 2:
                                args.Add($"{name}.x");
                                args.Add($"{name}.y");
                                break;
                            case 3:
                                args.Add($"{name}.x");
                                args.Add($"{name}.y");
                                args.Add($"{name}.z");
                                break;
                            default:
                                break;
                        }
                    }
                    Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. args]);
                    Writer.WriteLine(");");
                }
            }
        }
    }


    public void GenerateDeclaration()
    {

        Writer.WriteStructLayout();
        var csharpName = Config.GetCSharpTypeName(VecType);
        Writer.Write($"[CLSLMathematicsType({csharpName})]");
        Writer.Write("public partial struct ");
        Writer.Write(csharpName);
        using (Writer.IndentedScopeWithBracket())
        {
            DetailCodeGenerator.GenerateMemberDeclaration();
            foreach (var op in ShaderOperator.UnaryArithmeticOperatorDefinitions.Where(o => o.Source.Equals(VecType)))
            {
                Writer.WriteAggressiveInlining();
                Writer.WriteLine($"public static {Config.GetCSharpTypeName(op.Result)} operator {Config.OpName(op.Op)}({Config.GetCSharpTypeName(op.Source)} v)");
                using (Writer.IndentedScopeWithBracket())
                {
                    DetailCodeGenerator.GenerateArithmeticOperatorBody(op);
                }
            }

            foreach (var op in ShaderOperator.BinaryArithmeticOperatorDefinitions.Where(o => o.Left.Equals(VecType) || o.Right.Equals(VecType)))
            {
                Writer.WriteAggressiveInlining();
                Writer.WriteLine($"public static {Config.GetCSharpTypeName(op.Result)} operator {Config.OpName(op.Op)}({Config.GetCSharpTypeName(op.Left)} left, {Config.GetCSharpTypeName(op.Right)} right)");
                using (Writer.IndentedScopeWithBracket())
                {
                    DetailCodeGenerator.GenerateArithmeticOperatorBody(op);
                }
            }

            GenerateSwizzles();
        }
    }

    public void GenerateSwizzles()
    {

        static IEnumerable<ImmutableArray<string>> MakePerm(int count, IReadOnlyList<string> components)
        {
            if (count <= 0)
            {
                yield return [];
                yield break;
            }
            var result = new string[count];
            if (count == 1)
            {
                foreach (var c in components)
                {
                    result[0] = c;
                    yield return [.. result];
                }
                yield break;
            }
            var q = from n in MakePerm(count - 1, components)
                    from c in components
                    select (string[])[c, .. n];
            foreach (var r in q)
            {
                yield return [.. r];
            }
        }

        string[] components = [.. VecType.Size.Components()];
        foreach (var rank in (IRank[])[N2.Instance, N3.Instance, N4.Instance])
        {
            var rv = ShaderType.GetVecType(rank, VecType.ElementType);
            foreach (var sw in MakePerm(rank.Value, components))
            {
                var name = string.Join(string.Empty, sw);
                Writer.Write($"public {Config.GetCSharpTypeName(rv)} {name} ");
                using (Writer.IndentedScopeWithBracket())
                {
                    Writer.WriteAggressiveInlining();
                    Writer.Write($"get => vec{rank.Value}(");
                    Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. sw]);
                    Writer.WriteLine(");");

                    if (sw.Distinct().Count() == sw.Length && sw.Length < VecType.Size.Value)
                    {
                        Writer.WriteLine();
                        Writer.WriteAggressiveInlining();
                        Writer.WriteLine("set ");
                        using (Writer.IndentedScopeWithBracket())
                        {
                            for (var i = 0; i < sw.Length; i++)
                            {
                                Writer.WriteLine($"{sw[i]} = value.{components[i]};");
                            }
                        };

                    }
                }
            }
        }
    }

    public void Generate()
    {
        GenerateDeclaration();
        GenerateStaticMethods();
    }
}


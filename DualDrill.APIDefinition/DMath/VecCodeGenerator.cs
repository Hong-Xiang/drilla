using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.DMath;

internal sealed class VectorCodeGeneratorConfiguration
{
    // for numeric vectors with data larger than 64 bits (except System.Half, which is not supported in VectorXX<Half>),
    // we use .NET builtin SIMD optimization
    // for vec3, we use vec4's data for optimizing memory access and SIMD optimization
    public bool IsSimdSupported { get; }
    public string ElementName { get; }
    public string ElementCSharpTypeName { get; }
    public string VecCSharpTypeName { get; }
    public VectorCodeGeneratorConfiguration(VecType vecType)
    {
        IsSimdSupported = !vecType.ElementType.Equals(ShaderType.Bool)
                && (!vecType.ElementType.Equals(ShaderType.F16))
                && ((vecType.Size.Value == 3 ? 4 : vecType.Size.Value) * vecType.ElementType.ByteSize >= 8);
        ElementName = vecType.ElementType.ElementName();
        ElementCSharpTypeName = vecType.ElementType.ScalarCSharpType().FullName;
        VecCSharpTypeName = $"vec{vecType.Size.Value}{ElementName}";
    }
}

interface IVectorDetailCodeGenerator
{
    void GenerateMemberDeclaration();
    void GeneratePrimaryFactoryMethodBody();
    string CreateVectorExpression(Func<string[], IEnumerable<string>> componentExpressions);
    void GenerateOperators(); // Add this method
}

internal sealed record class VectorComponentCodeGenerator(
    VecType VecType,
    VectorCodeGeneratorConfiguration Config,
    IndentedTextWriter Writer) : IVectorDetailCodeGenerator
{
    public void GenerateMemberDeclaration()
    {
        foreach (var m in VecType.Size.Components())
        {
            Writer.Write($"public {Config.ElementCSharpTypeName} {m} ");
            Writer.WriteLine("{ get; set; }");
        }
    }

    public void GeneratePrimaryFactoryMethodBody()
    {
        Writer.Write($"return new {Config.VecCSharpTypeName} () ");
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
        return $"new {Config.VecCSharpTypeName}() {{ {string.Join(", ", assignments)} }}";
    }

    public void GenerateOperators()
    {
        Writer.WriteLine();

        // Generate unary negation operator
        Writer.WriteAggressiveInlining();
        Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator -({Config.VecCSharpTypeName} v) => {CreateVectorExpression(components => components.Select(c => $"-v.{c}"))};");
        Writer.WriteLine();

        // Operators to generate
        string[] operators = { "+", "-", "*", "/", "&", "|", "^" };

        // Generate binary operators
        foreach (var op in operators)
        {
            Writer.WriteAggressiveInlining();
            Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator {op} ({Config.VecCSharpTypeName} left, {Config.VecCSharpTypeName} right) => {CreateVectorExpression(components => components.Select(c => $"left.{c} {op} right.{c}"))};");
            Writer.WriteLine();
        }

        // Generate modulus operator
        Writer.WriteAggressiveInlining();
        Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator % ({Config.VecCSharpTypeName} left, {Config.VecCSharpTypeName} right) => {CreateVectorExpression(components => components.Select(c => $"({Config.ElementCSharpTypeName})(left.{c} % right.{c})"))};");
    }
}

internal sealed record class VectorSimdCodeGenerator(
    VecType VecType,
    VectorCodeGeneratorConfiguration Config,
    IndentedTextWriter Writer) : IVectorDetailCodeGenerator
{
    int SimdDataBitWidth => (VecType.Size.Value == 3 ? 4 : VecType.Size.Value) * VecType.ElementType.ByteSize * 8;
    string SimdStaticDataTypeName => $"Vector{SimdDataBitWidth}";
    string SimdDataTypeName => $"Vector{SimdDataBitWidth}<{Config.ElementCSharpTypeName}>";

    public void GenerateMemberDeclaration()
    {
        Writer.WriteLine($"internal {SimdDataTypeName} Data;");
        Writer.WriteLine();

        foreach (var (m, i) in VecType.Size.Components().Select((x, i) => (x, i)))
        {
            Writer.Write($"public {Config.ElementCSharpTypeName} {m}");
            Writer.WriteLine(" {");
            Writer.Indent++;

            //getter
            Writer.WriteMethodInline();
            Writer.WriteLine($"get => Data[{i}];");
            Writer.WriteLine();

            Writer.WriteMethodInline();
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
        Writer.Write($"return new {Config.VecCSharpTypeName}() ");
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

    public void GenerateOperators()
    {
        Writer.WriteLine();

        // Generate unary negation operator
        Writer.WriteAggressiveInlining();
        Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator -({Config.VecCSharpTypeName} v) => new() {{ Data = -v.Data }};");
        Writer.WriteLine();

        // Operators to generate
        string[] operators = { "+", "-", "*", "/", "&", "|", "^" };

        // Generate binary operators
        foreach (var op in operators)
        {
            Writer.WriteAggressiveInlining();
            Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator {op} ({Config.VecCSharpTypeName} left, {Config.VecCSharpTypeName} right) => new() {{ Data = left.Data {op} right.Data }};");
            Writer.WriteLine();
        }

        // Generate modulus operator
        Writer.WriteAggressiveInlining();
        // if (SupportsVectorizedModulus(VecType.ElementType))
        // {
            Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator % ({Config.VecCSharpTypeName} left, {Config.VecCSharpTypeName} right) => new() {{ Data = left.Data % right.Data }};");
        // }
        // else
        // {
            // Element-wise modulus
            // Writer.WriteLine($"public static {Config.VecCSharpTypeName} operator % ({Config.VecCSharpTypeName} left, {Config.VecCSharpTypeName} right) => {CreateVectorExpression(components => components.Select(c => $"({Config.ElementCSharpTypeName})(left.{c} % right.{c})"))};");
        // }
    }

    // Helper method to determine if the element type supports vectorized modulus
    // private static bool SupportsVectorizedModulus(IShaderType elementType)
    // {
    //     return elementType.Equals(ShaderType.Int) || elementType.Equals(ShaderType.Uint) ||
    //            elementType.Equals(ShaderType.Long) || elementType.Equals(ShaderType.Ulong);
    // }
}


public sealed record class VecCodeGenerator : ITextCodeGenerator
{
    VecType VecType { get; }
    public IndentedTextWriter Writer { get; }
    VectorCodeGeneratorConfiguration Config { get; }
    IVectorDetailCodeGenerator DetailCodeGenerator { get; }
    public VecCodeGenerator(VecType vecType, IndentedTextWriter writer)
    {
        VecType = vecType;
        Writer = writer;
        Config = new(VecType);
        DetailCodeGenerator = Config.IsSimdSupported
            ? new VectorSimdCodeGenerator(VecType, Config, Writer)
            : new VectorComponentCodeGenerator(VecType, Config, Writer);
    }

    public void GenerateStaticMethods()
    {
        Writer.Write("public static partial class DMath");
        using (Writer.IndentedScopeWithBracket())
        {
            Writer.Write($"public static {Config.VecCSharpTypeName} vec{VecType.Size.Value}(");

            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(m => $"{Config.ElementCSharpTypeName} {m}")]);
            Writer.Write(')');

            using (Writer.IndentedScopeWithBracket())
            {
                DetailCodeGenerator.GeneratePrimaryFactoryMethodBody();
            }


            Writer.Write($"public static {Config.VecCSharpTypeName} vec{VecType.Size.Value}({Config.ElementCSharpTypeName} e) => vec{VecType.Size.Value}(");
            Writer.WriteSeparatedList(TextCodeSeparator.CommaSpace, [.. VecType.Size.Components().Select(_ => "e")]);
            Writer.WriteLine(");");

            {
                var v2t = ShaderType.GetVecType(N2.Instance, VecType.ElementType);
                var v2c = new VectorCodeGeneratorConfiguration(v2t);
                var v3t = ShaderType.GetVecType(N3.Instance, VecType.ElementType);
                var v3c = new VectorCodeGeneratorConfiguration(v3t);

                int[][] patterns = VecType.Size switch
                {
                    N3 => [[1, 2], [2, 1]],
                    N4 => [[1, 3], [3, 1], [1, 1, 2], [1, 2, 1], [2, 1, 1], [2, 2]],
                    _ => []
                };
                string[] components = [.. VecType.Size.Components()];
                foreach (var ptn in patterns)
                {
                    Writer.Write($"public static {Config.VecCSharpTypeName} vec{VecType.Size.Value}(");
                    var ps = ptn.Select((d, i) => d switch
                    {
                        1 => Config.ElementCSharpTypeName,
                        2 => v2c.VecCSharpTypeName,
                        3 => v3c.VecCSharpTypeName,
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

        Writer.Write("public partial struct ");
        Writer.Write(Config.VecCSharpTypeName);
        using (Writer.IndentedScopeWithBracket())
        {
            DetailCodeGenerator.GenerateMemberDeclaration();
            DetailCodeGenerator.GenerateOperators(); // Use polymorphism to generate operators
            //GenerateSwizzles();
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
            var rvc = new VectorCodeGeneratorConfiguration(rv);
            foreach (var sw in MakePerm(rank.Value, components))
            {
                var name = string.Join(string.Empty, sw);
                Writer.Write($"public {rvc.VecCSharpTypeName} {name} ");
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


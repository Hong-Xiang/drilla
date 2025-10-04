using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using System.Text.Json;

namespace ILSpyReflectionDecompilerTest;

internal class Program
{
    static CompoundStatement Compile<T>(Func<T> f)
    {
        return new Compiler().Compile(f.Method);
    }
    static CompoundStatement Compile<TA, TB>(Func<TA, TB> f)
    {
        return new Compiler().Compile(f.Method);
    }
    static CompoundStatement Compile<TA, TB, TC>(Func<TA, TB, TC> f)
    {
        return new Compiler().Compile(f.Method);
    }
    static void Main(string[] args)
    {
        var s = Compile(static (int a, int b) => a + b);
        var option = new JsonSerializerOptions(JsonSerializerOptions.Web);
        option.Converters.Add(new TypeJsonConverter());
        option.WriteIndented = true;
        Console.WriteLine(JsonSerializer.Serialize(s, option));
    }
}

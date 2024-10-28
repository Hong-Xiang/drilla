using System.CodeDom.Compiler;

namespace DualDrill.ApiGen;

public interface ITextCodeGenerator
{

    public IndentedTextWriter Writer { get; }



}

public static class TextSourceGeneratorExtension
{
    public static void EmptyLine(this ITextCodeGenerator generator)
    {
        generator.Writer.WriteLine();
    }

    sealed record class IndentedScopeDisposable : IDisposable
    {
        IndentedTextWriter Writer { get; }
        bool WriteBracket { get; }
        public IndentedScopeDisposable(IndentedTextWriter writer, bool writeBracket)
        {
            Writer = writer;
            if (writeBracket)
            {
                Writer.WriteLine("{");
            }
            Writer.Indent++;
        }
        public void Dispose()
        {
            Writer.Indent--;
            if (WriteBracket)
            {
                Writer.WriteLine("}");
                Writer.WriteLine();
            }
        }
    }

    public static IDisposable IndentedScope(this ITextCodeGenerator generator)
    {
        return new IndentedScopeDisposable(generator.Writer, false);
    }

    public static void WriteAggressiveInlining(this ITextCodeGenerator generator)
    {
        generator.Writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    }

    public static void WriteArguments(this ITextCodeGenerator generator, params string[] arguments)
    {
        generator.Writer.Write("(");
        for (var i = 0; i < arguments.Length; i++)
        {
            generator.Writer.Write(arguments[i]);
            if (i < arguments.Length - 1)
            {
                generator.Writer.Write(", ");
            }
        }
        generator.Writer.Write(")");
    }

    public static IDisposable IndentedScopeWithBracket(this ITextCodeGenerator generator)
    {
        return new IndentedScopeDisposable(generator.Writer, true);
    }
}


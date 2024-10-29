using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;

namespace DualDrill.ApiGen;

public interface ITextCodeGenerator
{
    public IndentedTextWriter Writer { get; }
    void Generate();
}

internal enum TextCodeSeparator
{
    CommaSpace = 0,
    CommaNewLine
}

internal static class IndentedTextWriterExtension
{
    sealed record class IndentedScopeDisposable : IDisposable
    {
        IndentedTextWriter Writer { get; }
        bool WriteBracket { get; }
        public IndentedScopeDisposable(IndentedTextWriter writer, bool writeBracket)
        {
            Writer = writer;
            WriteBracket = writeBracket;
            if (WriteBracket)
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

    public static IDisposable IndentedScope(this IndentedTextWriter writer)
    {
        return new IndentedScopeDisposable(writer, false);
    }

    public static void WriteAggressiveInlining(this IndentedTextWriter writer)
    {
        writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    }

    public static void WriteSeparatedList(this IndentedTextWriter writer, TextCodeSeparator separator, params string[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            writer.Write(arguments[i]);
            if (i < arguments.Length - 1)
            {
                switch (separator)
                {
                    case TextCodeSeparator.CommaSpace:
                        writer.Write(", ");
                        break;
                    case TextCodeSeparator.CommaNewLine:
                        writer.WriteLine(',');
                        break;
                    default:
                        writer.Write(' ');
                        break;
                }
            }
        }
    }

    public static IDisposable IndentedScopeWithBracket(this IndentedTextWriter writer)
    {
        return new IndentedScopeDisposable(writer, true);
    }
}


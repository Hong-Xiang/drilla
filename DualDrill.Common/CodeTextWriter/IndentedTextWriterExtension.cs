using System.CodeDom.Compiler;

namespace DualDrill.Common.CodeTextWriter;

public enum TextCodeSeparator
{
    CommaSpace = 0,
    CommaNewLine
}

public static class IndentedTextWriterExtension
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

    public static IDisposable IndentedScopeWithBracket(this IndentedTextWriter writer)
    {
        return new IndentedScopeDisposable(writer, true);
    }

    public static void WriteSeparatedList(this TextWriter writer, TextCodeSeparator separator, params string[] arguments)
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

    public static void WriteSeparatedList(this TextWriter writer, TextCodeSeparator separator, params Action<TextWriter>[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i](writer);
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
}

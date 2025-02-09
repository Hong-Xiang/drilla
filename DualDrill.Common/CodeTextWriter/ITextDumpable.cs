using System.CodeDom.Compiler;

namespace DualDrill.Common.CodeTextWriter;

public interface ITextDumpable
{
    public void Dump(IndentedTextWriter writer);
}

public interface ITextDumpable<TContext>
{
    public void Dump(TContext context, IndentedTextWriter writer);
}

public static class TextDumpExtension
{
    public static string Dump(this ITextDumpable target)
    {
        var sw = new StringWriter();
        var isw = new IndentedTextWriter(sw);
        target.Dump(isw);
        return sw.ToString();
    }
}

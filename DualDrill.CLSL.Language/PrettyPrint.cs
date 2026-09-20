using System.CodeDom.Compiler;
using System.Globalization;

namespace DualDrill.CLSL.Language;

public sealed record class PrettyPrintOption(
    string TabString,
    bool LiteralSuffix,
    bool ShowTypes
)
{
    public static readonly PrettyPrintOption Default = new("\t", true, true);
}

public interface IPrintable
{
    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option);
}

public static class Printable
{
    public static string PrettyPrint(
        this IPrintable printable,
        PrettyPrintOption? option = null)
    {
        ArgumentNullException.ThrowIfNull(printable);
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new IndentedTextWriter(text);
        printable.PrettyPrint(writer, option ?? PrettyPrintOption.Default);
        return text.ToString();
    }
}
using System.CodeDom.Compiler;

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
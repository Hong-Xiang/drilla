using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.Symbol;

public interface ILabeledEntity
{
    public Label Label { get; }
}

internal sealed class LabelNotFoundException(Label Label) : Exception($"Label {Label.Name} not found")
{
}

public sealed class Label : ITextDumpable<ILocalDeclarationContext>
{
    private Label(string? name)
    {
        Name = name;
    }

    public string? Name { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write('^');
        writer.Write(context.LabelIndex(this));
        if (Name is not null) writer.Write($":{Name}");
    }

    public static Label Create(string name) => new(name);

    public static Label Create(int byteOffset) => new($"0x{byteOffset:X}");

    public static Label FromIndex(int index) => new($"#{index}");

    public static Label Create() => new(null);


    public override string ToString() => $"Label({Name})";
}

public interface ILabelMap<in TS, out TR>
{
    TR MapLabel(TS label);
}

public static class LabelMap
{
    public static ILabelMap<TS, TR> Create<TS, TR>(Func<TS, TR> f) => new FuncLabelMap<TS, TR>(f);

    public static ILabelMap<Label, T> Create<T>(Func<Label, T> f) => new FuncLabelMap<Label, T>(f);

    private sealed class FuncLabelMap<TS, TR>(Func<TS, TR> f) : ILabelMap<TS, TR>
    {
        public TR MapLabel(TS label) => f(label);
    }
}
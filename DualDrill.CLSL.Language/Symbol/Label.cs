namespace DualDrill.CLSL.Language.Symbol;

public interface ILabeledEntity
{
    public Label Label { get; }
}

sealed class LabelNotFoundException(Label Label) : Exception($"Label {Label.Name} not found")
{
}

public sealed class Label
{
    Label(string? name)
    {
        Name = name;
    }

    public string? Name { get; }

    public static Label Create(string name) => new(name);
    public static Label Create(int byteOffset) => new($"0x{byteOffset:X}");
    public static Label FromIndex(int index) => new($"#{index}");
    public static Label Create() => new(null);


    public override string ToString()
    {
        return $"Label({Name})";
    }
}

public interface ILabelMap<in TS, out TR>
{
    TR MapLabel(TS label);
}

public static class LabelMap
{
    sealed class FuncLabelMap<TS, TR>(Func<TS, TR> f) : ILabelMap<TS, TR>
    {
        public TR MapLabel(TS label) => f(label);
    }
    public static ILabelMap<TS, TR> Create<TS, TR>(Func<TS, TR> f)
    {
        return new FuncLabelMap<TS, TR>(f);
    }
    public static ILabelMap<Label, T> Create<T>(Func<Label, T> f)
    {
        return new FuncLabelMap<Label, T>(f);
    }
}
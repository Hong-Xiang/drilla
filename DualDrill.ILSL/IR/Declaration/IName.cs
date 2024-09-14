namespace DualDrill.ILSL.IR.Declaration;

public interface IName
{
    string GetName(ITargetLanguage targetLanguage);
}

internal sealed record class SimpleName(string Name) : IName
{
    public string GetName(ITargetLanguage targetLanguage) => Name;
}

public static class Name
{
    public static IName Create(string name) => new SimpleName(name);
}


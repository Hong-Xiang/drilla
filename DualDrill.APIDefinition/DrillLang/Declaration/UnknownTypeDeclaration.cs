namespace DualDrill.ApiGen.DrillLang.Declaration;

public sealed record class UnknownTypeDeclaration(object Data) : ITypeDeclaration
{
    public string Name => Data switch
    {
        Type t => t.FullName ?? t.Name,
        _ => Data.ToString() ?? ToString()
    };
}

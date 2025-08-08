namespace DualDrill.CLSL.Language.Declaration;

public enum ValueDeclarationKind
{
    Const,
    Override,
    Let,
    FormalParameter
}

public interface IConstValueSymbol { }
public interface IOverrideValueSymbol { }

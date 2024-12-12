using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree;

public interface ITargetLanguage
{
    string GetName(BoolType type);
    string GetName(IIntType type);
    string GetName(UIntType type);
    string GetName(FloatType type);
    string GetName(IVecType type);
    string GetName(MatType type);
    string GetLiteralString(ILiteral literal);
}


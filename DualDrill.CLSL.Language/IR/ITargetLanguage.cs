using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.IR;

public interface ITargetLanguage
{
    string GetName(BoolType type);
    string GetName(IntType type);
    string GetName(UIntType type);
    string GetName(FloatType type);
    string GetName(IVecType type);
    string GetName(MatType type);
    string GetLiteralString(ILiteral literal);
}


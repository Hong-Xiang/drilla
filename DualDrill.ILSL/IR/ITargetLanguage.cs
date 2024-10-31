using DualDrill.Common.Nat;
using DualDrill.ILSL.Types;

namespace DualDrill.ILSL.IR;

public interface ITargetLanguage
{
    string GetName(BoolType type);
    string GetName<TBitWidth>(IntType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TBitWidth>(UIntType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TBitWidth>(FloatType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TSize, TElement>(VecType<TSize, TElement> type)
        where TSize : IRank<TSize>
        where TElement : IScalarType<TElement>;
    string GetName(CLSL.Language.Types.MatType type);
    string GetLiteralString(ILiteral literal);
}


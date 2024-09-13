using DualDrill.ILSL.IR.Declaration;

namespace DualDrill.ILSL.IR;

public interface ITargetLanguage
{
    string GetName(BoolType type);
    string GetName<TBitWidth>(IntType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TBitWidth>(UIntType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TBitWidth>(FloatType<TBitWidth> type) where TBitWidth : IBitWidth;
    string GetName<TSize, TElement>(VecType<TSize, TElement> type)
        where TSize : IRank
        where TElement : IScalarType, new();
    string GetName<TRow, TCol, TElement>(MatType<TRow, TCol, TElement> type)
        where TRow : IRank
        where TCol : IRank
        where TElement : IScalarType, new();
    string GetLiteralString(ILiteral literal);
}


using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public interface IFloatOp<TSelf>
    where TSelf : IFloatOp<TSelf>
{ }
public interface IIntegerOp<TSelf>
    where TSelf : IIntegerOp<TSelf>
{ }

public interface ISignedIntegerOp<TSelf>
    where TSelf : ISignedIntegerOp<TSelf>
{ }

public interface IUnaryOp { }
public interface IBinaryOp<TSelf>
    where TSelf : IBinaryOp<TSelf>
{ }

public interface INamedOp<TSelf>
    where TSelf : INamedOp<TSelf>
{
    abstract static string Name { get; }
}
public interface ISymbolOp<TSelf>
    where TSelf : ISymbolOp<TSelf>
{
    abstract static string Symbol { get; }
}

public interface IOpKind<TSelf, TOpKind>
    where TOpKind : struct, Enum
    where TSelf : IOpKind<TSelf, TOpKind>
{
    abstract static TOpKind Kind { get; }
}

public interface IWASMOp { }
public interface IWGSLOp { }
public interface ISPIRVOp { }

public interface IBitWidthOp<TSelf, TBitWidth>
    where TSelf : IBitWidthOp<TSelf, TBitWidth>
    where TBitWidth : IBitWidth
{
}

public interface INumericOp<TSelf>
    where TSelf : INumericOp<TSelf>
{
}

public interface ISignedNumericOp<TSelf>
    where TSelf : ISignedNumericOp<TSelf>
{
}

public struct NumericSignedIntegerOp<TBitWidth, TOp, TSign>
    : ISignedNumericOp<NumericSignedIntegerOp<TBitWidth, TOp, TSign>>
    where TBitWidth : IBitWidth
    where TSign : ISignedness<TSign>
    where TOp : ISignedIntegerOp<TOp>
{
}

public struct NumericIntegerOp<TBitWidth, TOp>
    : INumericOp<NumericIntegerOp<TBitWidth, TOp>>
    where TBitWidth : IBitWidth
    where TOp : IIntegerOp<TOp>
{
}

public struct NumericFloatOp<TBitWidth, TOp>
    : INumericOp<NumericFloatOp<TBitWidth, TOp>>
    where TBitWidth : IBitWidth
    where TOp : IFloatOp<TOp>
{
}




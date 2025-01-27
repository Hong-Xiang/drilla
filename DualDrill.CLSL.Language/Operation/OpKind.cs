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

public interface INumericOp<TSelf, TOp>
{
}

public struct NumericIOp<TBitWidth, TOp>
     : INumericOp<NumericIOp<TBitWidth, TOp>, TOp>
     where TBitWidth : IBitWidth
     where TOp : IIntegerOp<TOp>
{
}

public struct NumericSignedIOp<TBitWidth, TOp, TSign>
    : INumericOp<NumericSignedIOp<TBitWidth, TOp, TSign>, TOp>
    where TBitWidth : IBitWidth
    where TSign : ISignedness<TSign>
    where TOp : ISignedIntegerOp<TOp>
{
}

public struct NumericFOp<TBitWidth, TOp>
    : INumericOp<NumericFOp<TBitWidth, TOp>, TOp>
    where TBitWidth : IBitWidth
    where TOp : IFloatOp<TOp>
{
}




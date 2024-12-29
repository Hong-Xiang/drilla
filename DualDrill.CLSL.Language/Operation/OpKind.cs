using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Operation;

public interface IFloatOp { }
public interface IIntegerOp { }
public interface ISignednessIntegerOp
{ }

public interface IUnaryOp { }
public interface IBinaryOp { }

public interface ISymbolOp<TOp>
{
    string Symbol { get; }
}

public interface IWASMOp { }
public interface IWGSLOp { }
public interface ISPIRVOp { }

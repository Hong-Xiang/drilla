using System.Collections.Immutable;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language;

public abstract record class Expr
{
}

public sealed record class ExprValue(IShaderValue Value) : Expr { }
public sealed record class ExprTree(Instruction.Instruction<Expr, object> Data) : Expr
{
}

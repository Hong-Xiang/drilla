using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DualDrill.ILSL.LinearIR;

public interface IInstruction
{
}

interface IBranchInstruction : IInstruction
{
    BasicBlock Target { get; }
}

interface IBranchCondition
{
}

sealed record class BranchInstruction<TCondition>(BasicBlock Target) : IBranchInstruction
   where TCondition : IBranchCondition
{

}


static class Condition
{
    public struct Unconditional : IBranchCondition { }
    public struct True : IBranchCondition { }
    public struct False : IBranchCondition { }
    public struct Eq : IBranchCondition, IConditionValueCondition
    {
        public static BinaryRelationalOp BinaryRelationalOp => BinaryRelationalOp.Equal;
    }
    public struct Ge<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Gt<TSign> : IBranchCondition, IConditionValueCondition
        where TSign : ISignedness
    {
        public static BinaryRelationalOp BinaryRelationalOp => BinaryRelationalOp.GreaterThan;
    }
    public struct Le<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Lt<TSign> : IBranchCondition, IConditionValueCondition
        where TSign : ISignedness
    {
        public static BinaryRelationalOp BinaryRelationalOp => BinaryRelationalOp.LessThan;
    }
    public struct Inst : IBranchCondition { }
    public struct Null : IBranchCondition { }
    public struct Ne : IBranchCondition { }
}

sealed record class NopInstruction : IInstruction { }
sealed record class ReturnInstruction : IInstruction { }

sealed record class CallInstruction(MethodInfo Callee) : IInstruction
{
}

sealed record class NewObjInstruction(ConstructorInfo Constructor) : IInstruction
{
}

sealed record class LoadArgumentInstruction(int Index) : IInstruction
{
}

sealed record class LoadArgumentAddressInstruction(int Index) : IInstruction { }

interface ILoadConstantInstruction : IInstruction
{
    ILiteral Literal { get; }
}
sealed record class LoadConstantInstruction<TLiteral>(TLiteral Literal) : ILoadConstantInstruction
    where TLiteral : ILiteral
{
    ILiteral ILoadConstantInstruction.Literal => Literal;
}

sealed record class LoadLocalInstruction(LocalVariableInfo Info) : IInstruction { }

sealed record class StoreLocalInstruction(LocalVariableInfo Info) : IInstruction { }

sealed record class LoadLocalAddressInstruction(LocalVariableInfo Info) : IInstruction { }


interface IBinaryArithmeticInstruction : IInstruction
{
    public IExpression CreateExpression(IExpression l, IExpression r);

}

sealed record class BinaryArithmeticInstruction<TOp> : IBinaryArithmeticInstruction
    where TOp : BinaryArithmetic.IOp
{
    public IExpression CreateExpression(IExpression l, IExpression r)
    {
        return new BinaryArithmeticExpression(l, r, TOp.Instance.Value);
    }
}

interface IConditionValueCondition
{
    static abstract BinaryRelationalOp BinaryRelationalOp { get; }
}

interface IConditionValueInstruction : IInstruction
{
    IExpression CreateExpression(IExpression l, IExpression r);
}

sealed record class BinaryBitwiseInstruction(BinaryBitwiseOp Op) : IInstruction
{
}

sealed record class ConvertInstruction(IShaderType Target) : IInstruction 
{ 
}

sealed record class ConditionValueInstruction<TCondition> : IConditionValueInstruction
    where TCondition : IConditionValueCondition
{
    bool TryConvertToBoolExpression(IExpression source, [NotNullWhen(true)] out IExpression? result)
    {
        if (source.Type is BoolType)
        {
            result = source;
            return true;
        }
        if (source is LiteralValueExpression { Literal: I32Literal { Value: 0 } })
        {
            result = new LiteralValueExpression(new BoolLiteral(false));
            return true;
        }
        if (source is LiteralValueExpression { Literal: I32Literal { Value: 1 } })
        {
            result = new LiteralValueExpression(new BoolLiteral(true));
            return true;
        }

        result = null;
        return false;
    }
    public IExpression CreateExpression(IExpression l, IExpression r)
    {
        {
            if (l.Type is BoolType && TryConvertToBoolExpression(r, out var rb))
            {
                return new BinaryRelationalExpression(l, rb, TCondition.BinaryRelationalOp);
            }
        }
        {
            if (r.Type is BoolType && TryConvertToBoolExpression(l, out var lb))
            {
                return new BinaryRelationalExpression(lb, r, TCondition.BinaryRelationalOp);
            }
        }
        return new BinaryRelationalExpression(l, r, TCondition.BinaryRelationalOp);
    }
}

sealed record class LoadStaticFieldInstruction(FieldInfo Field) : IInstruction
{
}

sealed record class LoadInstanceFieldInstruction(FieldInfo Field) : IInstruction
{
}

sealed record class LoadInstanceFieldAddressInstruction(FieldInfo Field) : IInstruction
{
}

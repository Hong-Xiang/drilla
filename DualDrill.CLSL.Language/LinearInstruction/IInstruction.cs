using DotNext.Patterns;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DualDrill.CLSL.LinearInstruction;

public interface IInstruction
{
}

public interface ILabel
{
    public int Index { get; }
}

public sealed record class Br : IInstruction { }

public sealed record class BrIfInstruction : IInstruction, ISingleton<BrIfInstruction>
{
    public static BrIfInstruction Instance { get; } = new();
}

public interface IBranchInstruction : IInstruction
{
    ILabel Target { get; }
}

public interface IBranchCondition
{
}

public sealed record class BranchInstruction<TCondition>(ILabel Target) : IBranchInstruction
   where TCondition : IBranchCondition
{
    public override string ToString()
    {
        return $"branch.{typeof(TCondition).Name}({Target})";
    }
}


public static class Condition
{
    public struct Unconditional : IBranchCondition { }
    public struct True : IBranchCondition { }
    public struct False : IBranchCondition { }
    public struct Eq : IBranchCondition, IBinaryRelationInstruction
    {
        public static BinaryRelation.Op OpKind => BinaryRelation.Op.eq;
    }
    public struct Ge<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Gt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness
    {
        public static BinaryRelation.Op OpKind => BinaryRelation.Op.gt;
    }
    public struct Le<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Lt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness
    {
        public static BinaryRelation.Op OpKind => BinaryRelation.Op.lt;
    }
    public struct Inst : IBranchCondition { }
    public struct Null : IBranchCondition { }
    public struct Ne : IBranchCondition { }
}

public sealed record class NopInstruction : ISingleton<NopInstruction>, IInstruction
{
    public static NopInstruction Instance { get; } = new();
}
public sealed record class ReturnInstruction : IInstruction { }

public sealed record class CallInstruction(MethodInfo Callee) : IInstruction
{
}

public sealed record class NewObjInstruction(ConstructorInfo Constructor) : IInstruction
{
}

public sealed record class LoadArgumentInstruction(int Index) : IInstruction
{
}

public sealed record class StoreArgumentInstruction(int Index) : IInstruction
{
}
public sealed record class LoadArgumentAddressInstruction(int Index) : IInstruction { }

public interface IConstInstruction : IInstruction
{
    ILiteral Literal { get; }
}
public sealed record class Const<TLiteral>(TLiteral Literal) : IConstInstruction
    where TLiteral : ILiteral
{
    ILiteral IConstInstruction.Literal => Literal;
}

public sealed record class LoadLocalInstruction(VariableDeclaration Source) : IInstruction { }

public sealed record class StoreLocalInstruction(VariableDeclaration Target) : IInstruction { }

public sealed record class LoadLocalAddressInstruction(VariableDeclaration Source) : IInstruction { }


public interface IBinaryArithmeticInstruction : IInstruction
{
    public IExpression CreateExpression(IExpression l, IExpression r);

}

public sealed record class BinaryArithmeticInstruction<TOp> : IBinaryArithmeticInstruction
    where TOp : class, BinaryArithmetic.IOp<TOp>
{
    public IExpression CreateExpression(IExpression l, IExpression r)
    {
        return new BinaryArithmeticExpression(l, r, TOp.Instance.Value);
    }
}

public interface IBinaryRelationInstruction : IInstruction
{
    static abstract BinaryRelation.Op OpKind { get; }
}

public sealed record class BinaryRelationInstruction<TOp> : IBinaryRelationInstruction, ISingleton<BinaryRelationInstruction<TOp>>
    where TOp : class, BinaryRelation.IOp<TOp>
{
    public static BinaryRelation.Op OpKind => TOp.Instance.Value;
    public static BinaryRelationInstruction<TOp> Instance { get; } = new();
}

public interface IConditionValueInstruction : IInstruction
{
    IExpression CreateExpression(IExpression l, IExpression r);
}

public sealed record class BinaryBitwiseInstruction(BinaryBitwiseOp Op) : IInstruction
{
}

public sealed record class ConvertInstruction(IShaderType Target) : IInstruction
{
}

public sealed record class ConditionValueInstruction<TCondition> : IConditionValueInstruction
    where TCondition : IBinaryRelationInstruction
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
                return new BinaryRelationalExpression(l, rb, TCondition.OpKind);
            }
        }
        {
            if (r.Type is BoolType && TryConvertToBoolExpression(l, out var lb))
            {
                return new BinaryRelationalExpression(lb, r, TCondition.OpKind);
            }
        }
        return new BinaryRelationalExpression(l, r, TCondition.OpKind);
    }
}

public sealed record class LoadStaticFieldInstruction(FieldInfo Field) : IInstruction
{
}

public sealed record class LoadInstanceFieldInstruction(FieldInfo Field) : IInstruction
{
}

public sealed record class LoadInstanceFieldAddressInstruction(FieldInfo Field) : IInstruction
{
}

public sealed record class NegateInstruction : IInstruction { }

public static class ShaderInstruction
{
    public static NopInstruction Nop => NopInstruction.Instance;
    public static BrIfInstruction BrIf => BrIfInstruction.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Lt> Lt => BinaryRelationInstruction<BinaryRelation.Lt>.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Gt> Gt => BinaryRelationInstruction<BinaryRelation.Gt>.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Le> Le => BinaryRelationInstruction<BinaryRelation.Le>.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Ge> Ge => BinaryRelationInstruction<BinaryRelation.Ge>.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Eq> Eq => BinaryRelationInstruction<BinaryRelation.Eq>.Instance;
    public static BinaryRelationInstruction<BinaryRelation.Ne> Ne => BinaryRelationInstruction<BinaryRelation.Ne>.Instance;
}

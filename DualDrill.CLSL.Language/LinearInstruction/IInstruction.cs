using DotNext.Patterns;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
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

public sealed record class BrInstruction(ILabel Target) : IInstruction
{
}

public sealed record class BrIfInstruction(ILabel Target) : IInstruction
{
}

public sealed record class ReturnInstruction() : IInstruction
{
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
        public static BinaryRelation.OpKind OpKind => BinaryRelation.OpKind.eq;
    }
    public struct Ge<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Gt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness
    {
        public static BinaryRelation.OpKind OpKind => BinaryRelation.OpKind.gt;
    }
    public struct Le<TSign> : IBranchCondition
        where TSign : ISignedness
    { }
    public struct Lt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness
    {
        public static BinaryRelation.OpKind OpKind => BinaryRelation.OpKind.lt;
    }
    public struct Inst : IBranchCondition { }
    public struct Null : IBranchCondition { }
    public struct Ne : IBranchCondition { }
}

public sealed record class NopInstruction : IInstruction
{
}
public sealed record class ReturnInstruction : IInstruction { }

public sealed record class Call(FunctionDeclaration FunctionDeclaration) : IInstruction
{
}

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
public sealed record class ConstInstruction<TLiteral>(TLiteral Literal) : IConstInstruction
    where TLiteral : ILiteral
{
    ILiteral IConstInstruction.Literal => Literal;
}

public sealed record class Load<TTarget>(TTarget Target) : IInstruction
    where TTarget : IVariableIdentifierResolveResult
{
}
public sealed record class LoadAddress<TTarget>(TTarget Target) : IInstruction
    where TTarget : IVariableIdentifierResolveResult
{
}
public sealed record class Store<TTarget>(TTarget Target) : IInstruction
    where TTarget : IVariableIdentifierResolveResult
{
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
}

public sealed record class NumericInstruction<TTarget, TOp>
    : IInstruction, ISingleton<NumericInstruction<TTarget, TOp>>
    where TTarget : INumericOp<TTarget, TOp>
{
    public static NumericInstruction<TTarget, TOp> Instance { get; } = new();
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
    public static IInstruction Nop() => new NopInstruction();
    public static IInstruction Br(ILabel target) => new BrInstruction(target);
    public static IInstruction BrIf(ILabel target) => new BrIfInstruction(target);
    public static IInstruction I32Eq() => NumericInstruction<NumericIOp<N32, BinaryRelation.Eq>, BinaryRelation.Eq>.Instance;

    public static IInstruction LogicalNot() => throw new NotImplementedException();

    public static Load<ParameterDeclaration> Load(ParameterDeclaration decl) => new(decl);
    public static Load<VariableDeclaration> Load(VariableDeclaration decl) => new(decl);
    public static LoadAddress<ParameterDeclaration> LoadAddress(ParameterDeclaration decl) => new(decl);
    public static LoadAddress<VariableDeclaration> LoadAddress(VariableDeclaration decl) => new(decl);
    public static Store<ParameterDeclaration> Store(ParameterDeclaration decl) => new(decl);
    public static Store<VariableDeclaration> Store(VariableDeclaration decl) => new(decl);

    public static IInstruction Call(FunctionDeclaration decl) => new Call(decl);

    public static IInstruction Return() => new ReturnInstruction();
    public static IInstruction Const<TLiteral>(TLiteral value) 
        where TLiteral : ILiteral
        => new ConstInstruction<TLiteral>(value);
}

using DotNext.Patterns;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlowGraph;
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

public interface IStackInstruction : IInstruction
{
}

public interface IStructuredStackInstruction : IStackInstruction
{
}

public interface ILabel
{
    public int Index { get; }
}


public sealed record class LabelInstruction(Label Label) : IStackInstruction, ILabeledEntity
{
}

public sealed record class BrInstruction(Label Target) : IStructuredStackInstruction
{
}

public sealed record class BrIfInstruction(Label Target) : IStructuredStackInstruction
{
}

public sealed record class ReturnInstruction() : IStructuredStackInstruction
{
}


[Obsolete]
public interface IBranchInstruction : IInstruction
{
    ILabel Target { get; }
}

[Obsolete]
public interface IBranchCondition
{
}

[Obsolete]
public sealed record class BranchInstruction<TCondition>(ILabel Target) : IBranchInstruction
   where TCondition : IBranchCondition
{
    public override string ToString()
    {
        return $"branch.{typeof(TCondition).Name}({Target})";
    }
}

[Obsolete]
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
        where TSign : ISignedness<TSign>
    { }
    public struct Gt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness<TSign>
    {
        public static BinaryRelation.OpKind OpKind => BinaryRelation.OpKind.gt;
    }
    public struct Le<TSign> : IBranchCondition
        where TSign : ISignedness<TSign>
    { }
    public struct Lt<TSign> : IBranchCondition, IBinaryRelationInstruction
        where TSign : ISignedness<TSign>
    {
        public static BinaryRelation.OpKind OpKind => BinaryRelation.OpKind.lt;
    }
    public struct Inst : IBranchCondition { }
    public struct Null : IBranchCondition { }
    public struct Ne : IBranchCondition { }
}

public sealed record class NopInstruction
    : IStructuredStackInstruction
    , ISingleton<NopInstruction>
{
    public static NopInstruction Instance { get; } = new();
}

public sealed record class CallInstruction(FunctionDeclaration Callee) : IStructuredStackInstruction
{
}

[Obsolete]
public sealed record class CallInstructionLegacy(MethodInfo Callee) : IInstruction
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

public interface IConstInstruction : IStructuredStackInstruction
{
    ILiteral Literal { get; }
}
public sealed record class ConstInstruction<TLiteral>(TLiteral Literal) : IConstInstruction
    where TLiteral : ILiteral
{
    ILiteral IConstInstruction.Literal => Literal;
}

public sealed record class Load<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
}
public sealed record class LoadAddress<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
}
public sealed record class Store<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
}

public sealed record class LoadLocalInstruction(VariableDeclaration Source) : IStructuredStackInstruction { }

public sealed record class StoreLocalInstruction(VariableDeclaration Target) : IStructuredStackInstruction { }

public sealed record class LoadLocalAddressInstruction(VariableDeclaration Source) : IStructuredStackInstruction { }


public interface IBinaryArithmeticInstruction : IInstruction
{
    public IExpression CreateExpression(IExpression l, IExpression r);

}

public sealed record class BinaryArithmeticInstruction<TOp> : IBinaryArithmeticInstruction
    where TOp : class, BinaryArithmetic.IOp<TOp>
{
    public IExpression CreateExpression(IExpression l, IExpression r)
    {
        throw new NotImplementedException();
    }
}

public interface IBinaryRelationInstruction : IInstruction
{
}

public sealed record class VectorSwizzleGetInstruction<TTarget, TPattern>
    where TPattern : Swizzle.IPattern<TPattern>
{
}

public sealed record class VectorSwizzleSetInstruction<TTarget, TPattern>
    where TPattern : Swizzle.IPattern<TPattern>
{
}

public sealed record class NumericInstruction<TOp>
    : IStructuredStackInstruction, ISingleton<NumericInstruction<TOp>>
    where TOp : ISignedNumericOp<TOp>
{
    public static NumericInstruction<TOp> Instance { get; } = new();
}

public sealed record class NumericInstruction<TOp, TSign>
    : IStructuredStackInstruction, ISingleton<NumericInstruction<TOp, TSign>>
    where TOp : INumericOp<TOp>
{
    public static NumericInstruction<TOp, TSign> Instance { get; } = new();
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
                throw new NotImplementedException();
            }
        }
        {
            if (r.Type is BoolType && TryConvertToBoolExpression(l, out var lb))
            {
                throw new NotImplementedException();
            }
        }
        throw new NotImplementedException();
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
    public static IStructuredStackInstruction Nop() => new NopInstruction();
    public static IStructuredStackInstruction Br(Label target) => new BrInstruction(target);
    public static IStructuredStackInstruction BrIf(Label target) => new BrIfInstruction(target);
    public static IStructuredStackInstruction I32Eq() => NumericInstruction<NumericIntegerOp<N32, BinaryRelation.Eq>, Signedness.S>.Instance;

    public static IInstruction LogicalNot() => throw new NotImplementedException();

    public static Load<ParameterDeclaration> Load(ParameterDeclaration decl) => new(decl);
    public static Load<VariableDeclaration> Load(VariableDeclaration decl) => new(decl);
    public static LoadAddress<ParameterDeclaration> LoadAddress(ParameterDeclaration decl) => new(decl);
    public static LoadAddress<VariableDeclaration> LoadAddress(VariableDeclaration decl) => new(decl);
    public static Store<ParameterDeclaration> Store(ParameterDeclaration decl) => new(decl);
    public static Store<VariableDeclaration> Store(VariableDeclaration decl) => new(decl);

    public static IStructuredStackInstruction Call(FunctionDeclaration decl) => new CallInstruction(decl);

    public static IStructuredStackInstruction Return() => new ReturnInstruction();
    public static IStructuredStackInstruction Const<TLiteral>(TLiteral value)
        where TLiteral : ILiteral
        => new ConstInstruction<TLiteral>(value);
}

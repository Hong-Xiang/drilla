﻿using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Reflection;

namespace DualDrill.CLSL.LinearInstruction;

public interface IInstruction
{
}

public interface IStackInstruction : IInstruction
{
}

public interface IStackPush
{
    public void Run(Stack<IShaderType> stack);
}

public interface IStackPushSingleton<T> : IStackPush
    where T : class, ISingleton<T>, IShaderType
{
    void IStackPush.Run(Stack<IShaderType> stack)
    {
        stack.Push(T.Instance);
    }
}

public interface IStackPop<T1, T2>
{
}

public interface IStructuredStackInstruction : IStackInstruction
{
    TResult Accept<TVisitor, TResult>(TVisitor visitor)
        where TVisitor : IStructuredStackInstructionVisitor<TResult>;
}

public interface ILabel
{
    public int Index { get; }
}


public sealed record class LabelInstruction(Label Label) : IStackInstruction, ILabeledEntity
{
}

public interface IJumpInstruction : IStructuredStackInstruction
{
}

public sealed record class BrInstruction(Label Target) : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);
}

public sealed record class BrIfInstruction(Label Target) : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class ReturnInstruction() : IJumpInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}


public sealed record class NopInstruction
    : IStructuredStackInstruction
    , ISingleton<NopInstruction>
{
    public static NopInstruction Instance { get; } = new();
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class ConstInstruction<TLiteral>(TLiteral Literal) : IStructuredStackInstruction
    where TLiteral : ILiteral
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class CallInstruction(FunctionDeclaration Callee) : IStructuredStackInstruction
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class LoadSymbolInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}
public sealed record class LoadSymbolAddressInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}
public sealed record class StoreSymbolInstruction<TTarget>(TTarget Target) : IStructuredStackInstruction
    where TTarget : IVariableIdentifierResolveResult
{
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class VectorSwizzleGetInstruction<TTarget, TPattern>
    where TPattern : Swizzle.IPattern<TPattern>
{
}

public sealed record class VectorSwizzleSetInstruction<TTarget, TPattern>
    where TPattern : Swizzle.IPattern<TPattern>
{
}


public sealed record class BinaryOperationInstruction<TOperation>
    : ISingleton<BinaryOperationInstruction<TOperation>>
    , IStructuredStackInstruction
    where TOperation : ISingleton<TOperation>, IBinaryOperation<TOperation>
{
    public static BinaryOperationInstruction<TOperation> Instance { get; } = new();
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed class LogicalNotInstruction
    : IStructuredStackInstruction
    , ISingleton<LogicalNotInstruction>
{
    public static LogicalNotInstruction Instance { get; } = new();
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

}

public sealed record class UnaryScalarInstruction<TOp>
    : IStructuredStackInstruction
    , ISingleton<UnaryScalarInstruction<TOp>>
    where TOp : IUnaryScalarOperation<TOp>
{
    public static UnaryScalarInstruction<TOp> Instance { get; } = new();
    public TResult Accept<TVisitor, TResult>(TVisitor visitor) where TVisitor : IStructuredStackInstructionVisitor<TResult>
         => visitor.Visit(this);

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
    public static IStructuredStackInstruction I32Eq() => BinaryOperationInstruction<NumericBinaryOperation<IntType<N32>, BinaryRelation.Eq>>.Instance;

    public static IStructuredStackInstruction LogicalNot() => new LogicalNotInstruction();

    public static LoadSymbolInstruction<ParameterDeclaration> Load(ParameterDeclaration decl) => new(decl);
    public static LoadSymbolInstruction<VariableDeclaration> Load(VariableDeclaration decl) => new(decl);
    public static LoadSymbolAddressInstruction<ParameterDeclaration> LoadAddress(ParameterDeclaration decl) => new(decl);
    public static LoadSymbolAddressInstruction<VariableDeclaration> LoadAddress(VariableDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<ParameterDeclaration> Store(ParameterDeclaration decl) => new(decl);
    public static StoreSymbolInstruction<VariableDeclaration> Store(VariableDeclaration decl) => new(decl);

    public static IStructuredStackInstruction Call(FunctionDeclaration decl) => new CallInstruction(decl);

    public static IStructuredStackInstruction Return() => new ReturnInstruction();
    public static IStructuredStackInstruction Const<TLiteral>(TLiteral value)
        where TLiteral : ILiteral
        => new ConstInstruction<TLiteral>(value);
}

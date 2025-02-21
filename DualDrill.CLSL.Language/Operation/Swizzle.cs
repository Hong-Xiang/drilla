using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Operation;

public static class Swizzle
{
    public enum ComponentKind
    {
        x,
        y,
        z,
        w
    }

    public interface IComponent
    {
        ComponentKind Kind { get; }
        string Name { get; }
        AbstractSyntaxTree.Expression.SwizzleComponent LegacySwizzleComponent { get; }
    }

    public interface IComponent<TSelf> : IComponent, ISingleton<TSelf>
        where TSelf : IComponent<TSelf>
    {
    }

    public interface ISizedComponent<TRank>
        where TRank : IRank<TRank>
    {
        IOperation ComponentGetOperation<TVector, TElement>()
            where TVector : ISizedVecType<TRank, TVector>;

        IOperation ComponentSetOperation<TVector, TElement>()
            where TVector : ISizedVecType<TRank, TVector>;

        ISizedPattern<TRank> PatternAfterComponent(ISizedComponent<TRank> c);

        ISizedPattern<TRank> WithComponent<TC>()
            where TC : ISizedComponent<TRank, TC>;

        ISizedPattern<TRank> PatternAfterPattern(ISizedPattern<TRank> pattern);
    }

    public static IEnumerable<ISizedComponent<TRank>> Components<TRank>()
        where TRank : IRank<TRank>
        => ((IEnumerable<IComponent>) [X.Instance, Y.Instance, Z.Instance, W.Instance])
            .OfType<ISizedComponent<TRank>>();

    public static IEnumerable<ISizedPattern<TRank>> SwizzlePatterns<TRank>()
        where TRank : IRank<TRank>
    {
        var p2 = from c0 in Components<TRank>()
                 from c1 in Components<TRank>()
                 select c1.PatternAfterComponent(c0);
        var p3 = from p in p2
                 from c2 in Components<TRank>()
                 select c2.PatternAfterPattern(p);
        var p4 = from p in p3
                 from c3 in Components<TRank>()
                 select c3.PatternAfterPattern(p);
        return [.. p2, .. p3, .. p4];
    }

    public interface ISizedComponent<TRank, TSelf> : IComponent<TSelf>, ISizedComponent<TRank>
        where TRank : IRank<TRank>
        where TSelf : ISizedComponent<TRank, TSelf>
    {
        IOperation ISizedComponent<TRank>.ComponentGetOperation<TVector, TElement>()
            => VectorComponentGetOperation<TRank, TVector, TSelf>.Instance;

        IOperation ISizedComponent<TRank>.ComponentSetOperation<TVector, TElement>()
            => VectorComponentSetOperation<TRank, TVector, TSelf>.Instance;

        ISizedPattern<TRank> ISizedComponent<TRank>.PatternAfterComponent(ISizedComponent<TRank> c)
            => c.WithComponent<TSelf>();

        ISizedPattern<TRank> ISizedComponent<TRank>.WithComponent<TC>()
            => Pattern<TRank, TSelf, TC>.Instance;

        ISizedPattern<TRank> ISizedComponent<TRank>.PatternAfterPattern(ISizedPattern<TRank> pattern)
            => pattern.WithComponent<TSelf>();
    }

    public sealed class X
        : ISizedComponent<N2, X>
        , ISizedComponent<N3, X>
        , ISizedComponent<N4, X>
    {
        public static X Instance { get; } = new();
        public ComponentKind Kind => ComponentKind.x;

        public SwizzleComponent LegacySwizzleComponent => SwizzleComponent.x;

        public string Name => "x";
    }

    public sealed class Y
        : ISizedComponent<N2, Y>
        , ISizedComponent<N3, Y>
        , ISizedComponent<N4, Y>
    {
        public static Y Instance { get; } = new();
        public ComponentKind Kind => ComponentKind.y;
        public SwizzleComponent LegacySwizzleComponent => SwizzleComponent.y;
        public string Name => "y";
    }

    public sealed class Z
        : ISizedComponent<N3, Z>
        , ISizedComponent<N4, Z>
    {
        public static Z Instance { get; } = new();
        public ComponentKind Kind => ComponentKind.z;
        public SwizzleComponent LegacySwizzleComponent => SwizzleComponent.z;
        public string Name => "z";
    }

    public sealed class W
        : ISizedComponent<N4, W>
    {
        public static W Instance { get; } = new();
        public ComponentKind Kind => ComponentKind.w;
        public SwizzleComponent LegacySwizzleComponent => SwizzleComponent.w;
        public string Name => "w";
    }

    public interface IPattern<TSelf> : ISingleton<TSelf>
        where TSelf : IPattern<TSelf>
    {
        IEnumerable<IComponent> Components { get; }

        string Name { get; }

        IVecType SourceType<TElement>() where TElement : IScalarType<TElement>;
        IVecType TargetType<TElement>() where TElement : IScalarType<TElement>;
        bool HasDuplicateComponent { get; }
    }

    public interface ISizedPattern<TRank>
        where TRank : IRank<TRank>
    {
        ISizedPattern<TRank> WithComponent<TY>()
            where TY : ISizedComponent<TRank, TY>;

        public interface ISizedPatternVisitor<TResult>
        {
            TResult Visit<TPattern>() where TPattern : ISizedPattern<TRank, TPattern>;
        }

        TResult Accept<TResult>(ISizedPatternVisitor<TResult> visitor);
    }

    public interface ISizedPattern<TRank, TSelf>
        : IPattern<TSelf>
        , ISizedPattern<TRank>
        where TRank : IRank<TRank>
        where TSelf : ISizedPattern<TRank, TSelf>
    {
        TResult ISizedPattern<TRank>.Accept<TResult>(ISizedPatternVisitor<TResult> visitor)
            => visitor.Visit<TSelf>();

        IVecType IPattern<TSelf>.SourceType<TElement>() => VecType<TRank, TElement>.Instance;
    }

    public sealed class Pattern<TRank, TX, TY> : ISizedPattern<TRank, Pattern<TRank, TX, TY>>
        where TRank : IRank<TRank>
        where TX : ISizedComponent<TRank, TX>
        where TY : ISizedComponent<TRank, TY>
    {
        public static Pattern<TRank, TX, TY> Instance { get; } = new();
        public IEnumerable<IComponent> Components => [TX.Instance, TY.Instance];

        public bool HasDuplicateComponent => TX.Instance.Equals(TY.Instance);

        public string Name => $"{TX.Instance.Name}{TY.Instance.Name}";

        public IVecType TargetType<TElement>()
            where TElement : IScalarType<TElement>
            => VecType<N2, TElement>.Instance;

        public ISizedPattern<TRank> WithComponent<TZ>()
            where TZ : ISizedComponent<TRank, TZ>
            => Pattern<TRank, TX, TY, TZ>.Instance;
    }

    public sealed class Pattern<TRank, TX, TY, TZ> : ISizedPattern<TRank, Pattern<TRank, TX, TY, TZ>>
        where TRank : IRank<TRank>
        where TX : ISizedComponent<TRank, TX>
        where TY : ISizedComponent<TRank, TY>
        where TZ : ISizedComponent<TRank, TZ>
    {
        public string Name => $"{TX.Instance.Name}{TY.Instance.Name}{TZ.Instance.Name}";
        public static Pattern<TRank, TX, TY, TZ> Instance { get; } = new();
        public IEnumerable<IComponent> Components => [TX.Instance, TY.Instance, TZ.Instance];

        public IVecType TargetType<TElement>()
            where TElement : IScalarType<TElement>
            => VecType<N3, TElement>.Instance;

        public ISizedPattern<TRank> WithComponent<TW>()
            where TW : ISizedComponent<TRank, TW>
            => Pattern<TRank, TX, TY, TZ, TW>.Instance;

        public bool HasDuplicateComponent =>
            TX.Instance.Equals(TY.Instance)
            || TX.Instance.Equals(TZ.Instance)
            || TY.Instance.Equals(TZ.Instance);
    }

    public sealed class Pattern<TRank, TX, TY, TZ, TW> : ISizedPattern<TRank, Pattern<TRank, TX, TY, TZ, TW>>
        where TRank : IRank<TRank>
        where TX : ISizedComponent<TRank, TX>
        where TY : ISizedComponent<TRank, TY>
        where TZ : ISizedComponent<TRank, TZ>
        where TW : ISizedComponent<TRank, TW>
    {
        public string Name => $"{TX.Instance.Name}{TY.Instance.Name}{TZ.Instance.Name}{TW.Instance.Name}";
        public static Pattern<TRank, TX, TY, TZ, TW> Instance { get; } = new();
        public IEnumerable<IComponent> Components => [TX.Instance, TY.Instance, TZ.Instance, TW.Instance];

        public IVecType TargetType<TElement>()
            where TElement : IScalarType<TElement>
            => VecType<N4, TElement>.Instance;

        public ISizedPattern<TRank> WithComponent<TY1>() where TY1 : ISizedComponent<TRank, TY1>
        {
            throw new NotSupportedException();
        }

        public bool HasDuplicateComponent =>
            Pattern<TRank, TX, TY, TZ>.Instance.HasDuplicateComponent
            || TX.Instance.Equals(TW.Instance)
            || TY.Instance.Equals(TW.Instance)
            || TZ.Instance.Equals(TW.Instance);
    }
}

public interface IVectorSizzleOperation<TSelf> : IOperation<TSelf>
    where TSelf : IVectorSizzleOperation<TSelf>
{
}

public sealed class VectorComponentGetOperation<TRank, TVector, TComponent>
    : IOperation<VectorComponentGetOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentGetOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public FunctionDeclaration Function { get; } = new FunctionDeclaration(
        $"get_{TComponent.Instance.Name}_{TVector.Instance.Name}",
        [new ParameterDeclaration("v", TVector.Instance.GetPtrType(), [])],
        new FunctionReturn(TVector.Instance.ElementType, []),
        [new OperationMethodAttribute<VectorComponentGetOperation<TRank, TVector, TComponent>>()]);

    public string Name => $"get.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    IStructuredStackInstruction IOperation.Instruction => Instruction.Instance;

    public sealed class Instruction
        : ISingleton<Instruction>
        , IStructuredStackInstruction
    {
        public static Instruction Instance { get; } = new();

        public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

        public IEnumerable<Label> ReferencedLabels => [];

        public TResult Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : IStructuredStackInstructionVisitor<TResult>
            => visitor.VisitVectorComponentGet<TRank, TVector, TComponent>();
    }

    public IExpression CreateExpression(IExpression expr)
    {
        return new VectorSwizzleAccessExpression(expr, [TComponent.Instance.LegacySwizzleComponent]);
    }
}

public sealed class VectorComponentSetOperation<TRank, TVector, TComponent>
    : IOperation<VectorComponentSetOperation<TRank, TVector, TComponent>>
    where TRank : IRank<TRank>
    where TVector : ISizedVecType<TRank, TVector>
    where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
{
    public static VectorComponentSetOperation<TRank, TVector, TComponent> Instance { get; } = new();

    public FunctionDeclaration Function { get; } = new FunctionDeclaration(
        $"set_{TComponent.Instance.Name}_{TVector.Instance.Name}",
        [
            new ParameterDeclaration("v", TVector.Instance.GetPtrType(), []),
            new ParameterDeclaration("value", TVector.Instance.ElementType, [])
        ],
        new FunctionReturn(TVector.Instance.ElementType, []),
        [new OperationMethodAttribute<VectorComponentSetOperation<TRank, TVector, TComponent>>()]);

    public string Name => $"set.{TComponent.Instance.Name}.{TVector.Instance.Name}";

    IStructuredStackInstruction IOperation.Instruction => Instruction.Instance;

    public sealed class Instruction
        : ISingleton<Instruction>
        , IStructuredStackInstruction
    {
        public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

        public IEnumerable<Label> ReferencedLabels => [];

        public static Instruction Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : IStructuredStackInstructionVisitor<TResult>
            => visitor.VisitVectorComponentSet<TRank, TVector, TComponent>();
    }

    public IStatement CreateStatement(IExpression target, IExpression value)
    {
        return new SimpleAssignmentStatement(
            new VectorSwizzleAccessExpression(target, [TComponent.Instance.LegacySwizzleComponent]),
            value,
            AssignmentOp.Assign
        );
    }
}

public interface IVectorSwizzleGetOperation
{
}

public sealed class VectorSwizzleGetOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleGetOperation<TPattern, TElement>>
    , IUnaryOperation<VectorSwizzleGetOperation<TPattern, TElement>>
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public static VectorSwizzleGetOperation<TPattern, TElement> Instance { get; } = new();
    public string Name => $"get.{TPattern.Instance.Name}.{TElement.Instance.Name}";
    public IShaderType SourceType => TPattern.Instance.SourceType<TElement>().GetPtrType();
    public IShaderType ResultType => TPattern.Instance.TargetType<TElement>();

    public IUnaryExpression CreateExpression(IExpression expr)
        => new UnaryExpression<VectorSwizzleGetOperation<TPattern, TElement>>(expr);

    IStructuredStackInstruction IOperation.Instruction => Instruction.Instance;

    public TResult EvaluateExpression<TResult>(IExpressionVisitor<TResult> visitor,
        UnaryExpression<VectorSwizzleGetOperation<TPattern, TElement>> expr)
    {
        throw new NotImplementedException();
    }


    public sealed class Instruction
        : ISingleton<Instruction>
        , IStructuredStackInstruction
    {
        public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

        public IEnumerable<Label> ReferencedLabels => [];

        public static Instruction Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : IStructuredStackInstructionVisitor<TResult>
            => visitor.VisitVectorSwizzleGet<TPattern, TElement>();
    }
}

public sealed class VectorSwizzleSetOperation<TPattern, TElement>
    : IVectorSizzleOperation<VectorSwizzleSetOperation<TPattern, TElement>>
    , IBinaryStatementOperation
    where TPattern : Swizzle.IPattern<TPattern>
    where TElement : IScalarType<TElement>
{
    public static VectorSwizzleSetOperation<TPattern, TElement> Instance { get; } = new();

    public FunctionDeclaration Function { get; } = new FunctionDeclaration(
        $"set_{TPattern.Instance.Name}_{TPattern.Instance.SourceType<TElement>().Name}",
        [
            new ParameterDeclaration("v", TPattern.Instance.SourceType<TElement>().GetPtrType(), []),
            new ParameterDeclaration("value", TPattern.Instance.TargetType<TElement>(), [])
        ],
        new FunctionReturn(UnitType.Instance, []),
        [new OperationMethodAttribute<VectorSwizzleSetOperation<TPattern, TElement>>()]);

    public string Name => $"set.{TPattern.Instance.Name}.{TElement.Instance.Name}";
    IStructuredStackInstruction IOperation.Instruction => Instruction.Instance;

    public IShaderType LeftType => TPattern.Instance.SourceType<TElement>().GetPtrType();

    public IShaderType RightType => TPattern.Instance.TargetType<TElement>();

    public sealed class Instruction
        : ISingleton<Instruction>
        , IStructuredStackInstruction
    {
        public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

        public IEnumerable<Label> ReferencedLabels => [];

        public static Instruction Instance { get; } = new();

        public TResult Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : IStructuredStackInstructionVisitor<TResult>
            => visitor.VisitVectorSwizzleSet<TPattern, TElement>();
    }

    public IStatement CreateStatement(IExpression target, IExpression value)
    {
        return new SimpleAssignmentStatement(
            new VectorSwizzleAccessExpression(target,
                [.. TPattern.Instance.Components.Select(c => c.LegacySwizzleComponent)]),
            value,
            AssignmentOp.Assign
        );
    }
}
using DualDrill.Common.Nat;
using DualDrill.ILSL.IR.Declaration;
using System.Collections.Immutable;

namespace DualDrill.ILSL.Types;
public interface IVecType : IStorableType
{
    public IRank Size { get; }
    public IScalarType ElementType { get; }
}

public sealed class VecType<TSize, TElement>() : IVecType, IBasicPrimitiveType<VecType<TSize, TElement>>
    where TSize : IRank<TSize>
    where TElement : IScalarType<TElement>
{
    public static VecType<TSize, TElement> Instance { get; } = new();
    public IRank Size => TSize.Instance;
    public IScalarType ElementType => TElement.Instance;

    static readonly ImmutableArray<string> ParameterNames = ["x", "y", "z", "w"];

    public static readonly ImmutableArray<FunctionDeclaration> Constructors =
        [
          new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []
          ),
          ..Enumerable.Range(0, Nat.GetInstance< TSize>().Value).Select(static count =>
            new FunctionDeclaration(
                new VecType<TSize, TElement>().Name,
                [.. Enumerable.Range(0, count + 1).Select(static i => new ParameterDeclaration(ParameterNames[i], TElement.Instance, []))],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
            []
        ))];

    public string Name => $"vec{Nat.GetInstance<TSize>().Value}<{TElement.Instance.Name}>";

    public static readonly FunctionDeclaration Dot =
        new FunctionDeclaration("dot",
                               [new ParameterDeclaration("e1", new VecType<TSize, TElement>(), []),
                                new ParameterDeclaration("e2", new VecType<TSize, TElement>(), [])],
                                new FunctionReturn(TElement.Instance, []),
                                []);
    public static readonly FunctionDeclaration Length =
    new FunctionDeclaration("length",
                            [],
                            new FunctionReturn(TElement.Instance, []),
                            []);
    public static readonly FunctionDeclaration Abs =
    new FunctionDeclaration("abs",
                            [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
                            new FunctionReturn(new VecType<TSize, TElement>(), []),
                            []);

    public static readonly FunctionDeclaration Normalize =
    new FunctionDeclaration("normalize",
                        [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
                        new FunctionReturn(new VecType<TSize, TElement>(), []),
                        []);
    public static readonly FunctionDeclaration Reflect =
    new FunctionDeclaration("reflect",
                    [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
                    new ParameterDeclaration("norm", new VecType<TSize, TElement>(), [])],
                    new FunctionReturn(new VecType<TSize, TElement>(), []),
                    []);
    public static readonly FunctionDeclaration Cross =
    new FunctionDeclaration("cross",
                [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
                    new ParameterDeclaration("b", new VecType<TSize, TElement>(), [])],
                new FunctionReturn(new VecType<TSize, TElement>(), []),
                []);
    public int ByteSize => Nat.GetInstance<TSize>().Value * TElement.Instance.ByteSize;
}

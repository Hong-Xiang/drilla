using DotNext.Patterns;
using DualDrill.Common.Nat;
using System.Collections.Immutable;
using System.Diagnostics;

namespace DualDrill.CLSL.Language.Types;

public interface IVecType : IShaderType
{
    public IScalarType ElementType { get; }
    public IRank Size { get; }
}

[DebuggerDisplay("{Name}")]
public sealed class VecType<TRank, TElement> : IVecType,
    ISingleton<VecType<TRank, TElement>>,
    ISingletonShaderType<VecType<TRank, TElement>>
    where TRank : class, IRank<TRank>
    where TElement : class, IScalarType<TElement>
{
    private VecType() { }
    public static VecType<TRank, TElement> Instance { get; } = new();

    //public static readonly ImmutableArray<FunctionDeclaration> Constructors =
    //    [
    //      new FunctionDeclaration(
    //            new VecType<TSize, TElement>().Name,
    //            [],
    //            new FunctionReturn(new VecType<TSize, TElement>(), []),
    //            []
    //      ),
    //      ..Enumerable.Range(0, TSize.Value).Select(static count =>
    //        new FunctionDeclaration(
    //            new VecType<TSize, TElement>().Name,
    //            [.. Enumerable.Range(0, count + 1).Select(static i => new ParameterDeclaration(ParameterNames[i], TElement.Instance, []))],
    //            new FunctionReturn(new VecType<TSize, TElement>(), []),
    //        []
    //    ))];

    public string Name => $"vec{Size.Value}{ElementType.ElementName()}";

    //public static readonly FunctionDeclaration Dot =
    //    new FunctionDeclaration("dot",
    //                           [new ParameterDeclaration("e1", new VecType<TSize, TElement>(), []),
    //                            new ParameterDeclaration("e2", new VecType<TSize, TElement>(), [])],
    //                            new FunctionReturn(TElement.Instance, []),
    //                            []);
    //public static readonly FunctionDeclaration Length =
    //new FunctionDeclaration("length",
    //                        [],
    //                        new FunctionReturn(TElement.Instance, []),
    //                        []);
    //public static readonly FunctionDeclaration Abs =
    //new FunctionDeclaration("abs",
    //                        [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
    //                        new FunctionReturn(new VecType<TSize, TElement>(), []),
    //                        []);

    //public static readonly FunctionDeclaration Normalize =
    //new FunctionDeclaration("normalize",
    //                    [new ParameterDeclaration("e", new VecType<TSize, TElement>(), [])],
    //                    new FunctionReturn(new VecType<TSize, TElement>(), []),
    //                    []);
    //public static readonly FunctionDeclaration Reflect =
    //new FunctionDeclaration("reflect",
    //                [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
    //                new ParameterDeclaration("norm", new VecType<TSize, TElement>(), [])],
    //                new FunctionReturn(new VecType<TSize, TElement>(), []),
    //                []);
    //public static readonly FunctionDeclaration Cross =
    //new FunctionDeclaration("cross",
    //            [new ParameterDeclaration("a", new VecType<TSize, TElement>(), []),
    //                new ParameterDeclaration("b", new VecType<TSize, TElement>(), [])],
    //            new FunctionReturn(new VecType<TSize, TElement>(), []),
    //            []);
    public int ByteSize => Size.Value * ElementType.ByteSize;

    public IScalarType ElementType => TElement.Instance;

    public IRank Size => TRank.Instance;

    public IRefType GetRefType()
    {
        throw new NotImplementedException();
    }

    public IPtrType GetPtrType()
    {
        throw new NotImplementedException();
    }
}

public static partial class ShaderType
{
    public static IEnumerable<IVecType> GetVecTypes() =>
        [..from r in Ranks
           from e in ScalarTypes
           select GetVecType(r, e)];
    public static IVecType GetVecType(IRank size, IScalarType elementType)
        => elementType.Accept<IVecType, VecTypeBuilder>(new(size));

    private struct VecTypeBuilder(IRank Size) : IScalarType.IGenericVisitor<IVecType>
    {
        private struct Builder<TScalarType> : IRank.IVisitor<IVecType>
                where TScalarType : class, IScalarType<TScalarType>
        {
            readonly IVecType IRank.IVisitor<IVecType>.Visit<TRank>() => VecType<TRank, TScalarType>.Instance;
        }

        readonly IVecType IScalarType.IGenericVisitor<IVecType>.Visit<TScalarType>(TScalarType scalarType)
                    => Size.Accept(new Builder<TScalarType>());
    }


}

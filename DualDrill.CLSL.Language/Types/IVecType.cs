﻿using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IVecType : IShaderType
{
    public IScalarType ElementType { get; }
    public IRank Size { get; }
}

public sealed record class VecType(
    IScalarType ElementType,
    IRank Size
) : IVecType
{
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

    public string Name { get; } = $"vec{Size.Value}{ElementType.ElementName()}";

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
}

public static partial class ShaderType
{
    static IReadOnlyDictionary<(IRank, IScalarType), IVecType> VecTypesLookup { get; }
    static ShaderType()
    {
        VecTypesLookup = (from r in Ranks
                          from e in ScalarTypes
                          select KeyValuePair.Create((r, e), (IVecType)new VecType(e, r))).ToDictionary();
    }
    public static IEnumerable<IVecType> GetVecTypes() => VecTypesLookup.Values;
    public static IVecType GetVecType(IRank size, IScalarType elementType)
    {
        return VecTypesLookup[(size, elementType)];
    }
}

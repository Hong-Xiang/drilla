using DualDrill.Common;

namespace DualDrill.CLSL.Language.Symbol;

public enum AddressSpaceKind
{
    Generic,
    Function,
    Uniform,
    Input,
    Output,
}

public interface IAddressSpace
{
    AddressSpaceKind Kind { get; }
    T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic);
    T Eval<T>(IAddressSpaceSemantic<T> semantic);
}

public interface IAddressSpaceGenericSemantic<T>
{
    T AddressSpace<TSpace>(TSpace space) where TSpace : IAddressSpace<TSpace>;
}

public interface IAddressSpaceSemantic<T>
{
    T Generic(GenericAddressSpace s);
    T Function(FunctionAddressSpace s);
    T Uniform(UniformAddressSpace s);
    T Input(InputAddressSpace s);
    T Output(OutputAddressSpace s);
}

public interface IAddressSpace<TSelf>
    : IAddressSpace
    , ISingleton<TSelf>
    where TSelf : IAddressSpace<TSelf>
{
}

public sealed class GenericAddressSpace
    : IAddressSpace<GenericAddressSpace>
    , ISingleton<GenericAddressSpace>
{
    public static GenericAddressSpace Instance { get; } = new();

    public AddressSpaceKind Kind => AddressSpaceKind.Generic;

    public T Eval<T>(IAddressSpaceSemantic<T> semantic)
        => semantic.Generic(this);

    public T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic)
        => semantic.AddressSpace(this);
}

public sealed class FunctionAddressSpace
    : IAddressSpace<FunctionAddressSpace>
    , ISingleton<FunctionAddressSpace>
{
    public static FunctionAddressSpace Instance { get; } = new();

    public AddressSpaceKind Kind => AddressSpaceKind.Function;

    public T Eval<T>(IAddressSpaceSemantic<T> semantic)
        => semantic.Function(this);


    public T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic)
        => semantic.AddressSpace(this);
}

public sealed class InputAddressSpace
    : IAddressSpace<InputAddressSpace>
    , ISingleton<InputAddressSpace>
{
    public static InputAddressSpace Instance { get; } = new();
    public AddressSpaceKind Kind => AddressSpaceKind.Input;

    public T Eval<T>(IAddressSpaceSemantic<T> semantic)
        => semantic.Input(this);

    public T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic)
        => semantic.AddressSpace(this);
}

public sealed class OutputAddressSpace
    : IAddressSpace<OutputAddressSpace>
    , ISingleton<OutputAddressSpace>
{
    public static OutputAddressSpace Instance { get; } = new();
    public AddressSpaceKind Kind => AddressSpaceKind.Output;

    public T Eval<T>(IAddressSpaceSemantic<T> semantic)
        => semantic.Output(this);

    public T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic)
        => semantic.AddressSpace(this);
}

public sealed class UniformAddressSpace
    : IAddressSpace<UniformAddressSpace>
    , ISingleton<UniformAddressSpace>
{
    public AddressSpaceKind Kind => AddressSpaceKind.Uniform;
    public static UniformAddressSpace Instance { get; } = new();

    public T Eval<T>(IAddressSpaceSemantic<T> semantic)
        => semantic.Uniform(this);

    public T EvalG<T>(IAddressSpaceGenericSemantic<T> semantic)
        => semantic.AddressSpace(this);
}

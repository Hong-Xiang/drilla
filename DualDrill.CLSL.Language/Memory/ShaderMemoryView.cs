namespace DualDrill.CLSL.Language.Memory;

public interface IMemoryViewSemantic<in TX, in TS, in TM, out TR>
{
    TR Symbol(TX context, TS symbol);
    // TODO: add access chain related ones, e.g. get address of member
}

public interface IMemoryView<TS, TM>
{
    public TR Evaluate<TX, TR>(IMemoryViewSemantic<TX, TS, TM, TR> semantic, TX context);
}

public sealed record class SymbolMemoryView<TS, TM>(TS Symbol) : IMemoryView<TS, TM>
{
    public TR Evaluate<TX, TR>(IMemoryViewSemantic<TX, TS, TM, TR> semantic, TX context)
        => semantic.Symbol(context, Symbol);
}

public readonly record struct ShaderMemoryView<TS>(IMemoryView<TS, ShaderMemoryView<TS>> Value)
    : IMemoryView<TS, ShaderMemoryView<TS>>
{
    public TR Evaluate<TX, TR>(IMemoryViewSemantic<TX, TS, ShaderMemoryView<TS>, TR> semantic, TX context)
        => Value.Evaluate(semantic, context);
}

public static class MemoryViewExtension
{
    public static ShaderMemoryView<TS> AsMemoryView<TS>(this IMemoryView<TS, ShaderMemoryView<TS>> memoryView)
        => new(memoryView);
}

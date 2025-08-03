using DualDrill.Common;

namespace DualDrill.CLSL.Language;

public interface ISeqSemantic<in TX, in TH, in TL, in TI, out TO>
    where TH : allows ref struct
    where TL : allows ref struct
    where TX : allows ref struct
    where TI : allows ref struct
    where TO : allows ref struct
{
    TO Single(TX context, TL value);
    TO Nested(TX context, TH head, TI next);
}

public interface ISeq<TH, TL, TS>
    where TH : allows ref struct
    where TL : allows ref struct
    where TS : allows ref struct
{
    public TR Evaluate<TX, TR>(ISeqSemantic<TX, TH, TL, TS, TR> semantic, TX context);
    public ISeq<THR, TLR, TSR> Select<THR, TLR, TSR>(Func<TH, THR> fh, Func<TL, TLR> fl, Func<TS, TSR> fs);
}

sealed record class SingleSeq<TH, TL, TS>(TL Last) : ISeq<TH, TL, TS>
{
    public TR Evaluate<TX, TR>(ISeqSemantic<TX, TH, TL, TS, TR> semantic, TX ctx)
        => semantic.Single(ctx, Last);

    public ISeq<THR, TLR, TSR> Select<THR, TLR, TSR>(Func<TH, THR> fh, Func<TL, TLR> fl, Func<TS, TSR> fs)
        => new SingleSeq<THR, TLR, TSR>(fl(Last));
}

sealed record class NestedSeq<TH, TL, TS>(TH Head, TS Next) : ISeq<TH, TL, TS>
{
    public TR Evaluate<TX, TR>(ISeqSemantic<TX, TH, TL, TS, TR> semantic, TX ctx)
        => semantic.Nested(ctx, Head, Next);

    public ISeq<THR, TLR, TSR> Select<THR, TLR, TSR>(Func<TH, THR> fh, Func<TL, TLR> fl, Func<TS, TSR> fs)
        => new NestedSeq<THR, TLR, TSR>(fh(Head), fs(Next));
}


/// <summary>
/// Abstraction of sequence of TH*, TL, e.g. [tl] | [th, tl] | [th, th, ... th, tl]
/// </summary>
/// <typeparam name="TH">non-last element type</typeparam>
/// <typeparam name="TL">last element type</typeparam>
public readonly record struct Seq<TH, TL>(ISeq<TH, TL, Seq<TH, TL>> Value)
{
    public static Seq<TH, TL> Single(TL last) => new(new SingleSeq<TH, TL, Seq<TH, TL>>(last));
    public static Seq<TH, TL> Nested(TH head, Seq<TH, TL> next) => new(new NestedSeq<TH, TL, Seq<TH, TL>>(head, next));

    public int Count => Fold(Seq.Semantic<TH, TL, int, int>(x => 0, (_, n) => n + 1), default);

    public TL Last => Fold(new LastSemantic<TH, TL>(), default);
    public IEnumerable<TH> Elements => Fold(Seq.Semantic<TH, TL, IEnumerable<TH>, IEnumerable<TH>>(x => [], (h, s) => [h, .. s]), default);
    public Seq<THR, TLR> Select<THR, TLR>(Func<TH, THR> f, Func<TL, TLR> g)
        => new(Value.Select(f, g, s => s.Select(f, g)));
    public Seq<TH, TLR> Select<TLR>(Func<TL, TLR> f)
         => new(Value.Select(x => x, f, s => s.Select(x => x, f)));


    public T Fold<TX, T>(ISeqSemantic<TX, TH, TL, T, T> semantic, TX ctx)
        => Value.Evaluate(new FoldSemantic<TX, TH, TL, T>(semantic), ctx);
    public T Fold<TX, T>(ISeqSemantic<TX, TH, TL, Func<TX, T>, T> semantic, TX ctx)
        => Value.Evaluate(new StatefulFoldSemantic<TX, TH, TL, T>(semantic), ctx);


    public delegate TR UnfoldStep<TA, TR>(ISeqSemantic<Unit, TH, TL, TA, TR> builder, TA value)
        where TA : allows ref struct;

    sealed class UnfolderSemantic<TA>(UnfoldStep<TA, Seq<TH, TL>> step) : ISeqSemantic<Unit, TH, TL, TA, Seq<TH, TL>>
        where TA : allows ref struct
    {
        public Seq<TH, TL> Single(Unit _, TL value)
            => new(new SingleSeq<TH, TL, Seq<TH, TL>>(value));

        public Seq<TH, TL> Nested(Unit _, TH head, TA next)
            => new(new NestedSeq<TH, TL, Seq<TH, TL>>(head, step(this, next)));
    }

    public static Seq<TH, TL> Unfold<TA>(TA value, UnfoldStep<TA, Seq<TH, TL>> unfolder)
        where TA : allows ref struct
            => unfolder(new UnfolderSemantic<TA>(unfolder), value);

    public override string ToString()
        => Fold(new FormatSemantic<TH, TL>(), true);

    public Seq<TH, TR> SelectMany<TR>(Func<TL, Seq<TH, TR>> f)
        => Fold(new SelectManySemantic<TH, TL, TR>(f), default);
}

sealed class SelectManySemantic<TH, TL, TR>(Func<TL, Seq<TH, TR>> f) : ISeqSemantic<Unit, TH, TL, Seq<TH, TR>, Seq<TH, TR>>
{
    public Seq<TH, TR> Nested(Unit context, TH head, Seq<TH, TR> next)
        => new(new NestedSeq<TH, TR, Seq<TH, TR>>(head, next));

    public Seq<TH, TR> Single(Unit context, TL value)
        => f(value);
}

sealed class FoldSemantic<TX, TH, TL, T>(ISeqSemantic<TX, TH, TL, T, T> semantic) : ISeqSemantic<TX, TH, TL, Seq<TH, TL>, T>
{
    public T Nested(TX context, TH head, Seq<TH, TL> next)
        => semantic.Nested(context, head, next.Value.Evaluate(this, context));

    public T Single(TX context, TL value)
        => semantic.Single(context, value);
}

sealed class StatefulFoldSemantic<TX, TH, TL, T>(ISeqSemantic<TX, TH, TL, Func<TX, T>, T> semantic) : ISeqSemantic<TX, TH, TL, Seq<TH, TL>, T>
{
    public T Nested(TX context, TH head, Seq<TH, TL> next)
        => semantic.Nested(context, head, ctx => next.Value.Evaluate(this, ctx));

    public T Single(TX context, TL value)
        => semantic.Single(context, value);
}


sealed class LastSemantic<TH, TL> : ISeqSemantic<Unit, TH, TL, TL, TL>
{
    public TL Nested(Unit _, TH head, TL next)
        => next;

    public TL Single(Unit _, TL value)
        => value;
}

sealed class FormatSemantic<TH, TL> : ISeqSemantic<bool, TH, TL, Func<bool, string>, string>
{
    public string Nested(bool isFirst, TH head, Func<bool, string> next)
        => isFirst ? $"[{head}, {next(false)}]" : $"{head}, {next(false)}";

    public string Single(bool isFirst, TL value)
        => isFirst ? $"[; {value}]" : $"; {value}";
}

public static class Seq
{
    public static Seq<TH, TT> Create<TH, TT>(ReadOnlySpan<TH> seq, TT last) =>
        Seq<TH, TT>.Unfold(seq, (b, s) => s.IsEmpty ? b.Single(default, last) : b.Nested(default, s[0], s[1..]));

    public static Seq<TH, TL> Single<TH, TL>(TL last) => Seq<TH, TL>.Single(last);
    public static Seq<TH, TL> Nested<TH, TL>(TH head, Seq<TH, TL> next) => Seq<TH, TL>.Nested(head, next);

    sealed class SeqSemantic<TH, TL, TS, TO>(Func<TL, TO> single, Func<TH, TS, TO> concat) : ISeqSemantic<Unit, TH, TL, TS, TO>
    {
        public TO Nested(Unit _, TH head, TS seq)
            => concat(head, seq);

        public TO Single(Unit _, TL value)
            => single(value);
    }

    public static ISeqSemantic<Unit, TH, TL, TS, TO> Semantic<TH, TL, TS, TO>(Func<TL, TO> single, Func<TH, TS, TO> concat)
        => new SeqSemantic<TH, TL, TS, TO>(single, concat);
}


namespace DualDrill.CLSL.Language;

public interface ISeqSemantic<in TH, in TL, in TI, out TO>
    where TH : allows ref struct
    where TL : allows ref struct
    where TI : allows ref struct
    where TO : allows ref struct
{
    TO Single(TL value);
    TO Nested(TH head, TI next);
}

public interface ISeq<TH, TL, TS>
    where TH : allows ref struct
    where TL : allows ref struct
    where TS : allows ref struct
{
    public TR Evaluate<TR>(ISeqSemantic<TH, TL, TS, TR> semantic);
    public ISeq<THR, TLR, TSR> Select<THR, TLR, TSR>(Func<TH, THR> fh, Func<TL, TLR> fl, Func<TS, TSR> fs);
}

sealed record class SingleSeq<TH, TL, TS>(TL Last) : ISeq<TH, TL, TS>
{
    public TR Evaluate<TR>(ISeqSemantic<TH, TL, TS, TR> semantic)
        => semantic.Single(Last);

    public ISeq<THR, TLR, TSR> Select<THR, TLR, TSR>(Func<TH, THR> fh, Func<TL, TLR> fl, Func<TS, TSR> fs)
        => new SingleSeq<THR, TLR, TSR>(fl(Last));
}

sealed record class NestedSeq<TH, TL, TS>(TH Head, TS Next) : ISeq<TH, TL, TS>
{
    public TR Evaluate<TR>(ISeqSemantic<TH, TL, TS, TR> semantic)
        => semantic.Nested(Head, Next);

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

    public int Count => Fold(Seq.Semantic<TH, TL, int, int>(x => 0, (_, n) => n + 1));

    public TL Last => Fold(new LastSemantic<TH, TL>());
    public IEnumerable<TH> Elements => Fold(Seq.Semantic<TH, TL, IEnumerable<TH>, IEnumerable<TH>>(x => [], (h, s) => [h, .. s]));
    public Seq<THR, TLR> Select<THR, TLR>(Func<TH, THR> f, Func<TL, TLR> g)
        => new(Value.Select(f, g, s => s.Select(f, g)));
    public Seq<TH, TLR> Select<TLR>(Func<TL, TLR> f)
         => new(Value.Select(x => x, f, s => s.Select(x => x, f)));


    public T Fold<T>(ISeqSemantic<TH, TL, T, T> semantic)
        => Value.Evaluate(new FoldSemantic<TH, TL, T>(semantic));
    public T FoldLazy<T>(ISeqSemantic<TH, TL, Func<T>, T> semantic)
        => Value.Evaluate(new StatefulFoldSemantic<TH, TL, T>(semantic));

    public delegate TR UnfoldStep<TA, TR>(ISeqSemantic<TH, TL, TA, TR> builder, TA value)
        where TA : allows ref struct;

    sealed class UnfolderSemantic<TA>(UnfoldStep<TA, Seq<TH, TL>> step) : ISeqSemantic<TH, TL, TA, Seq<TH, TL>>
        where TA : allows ref struct
    {
        public Seq<TH, TL> Single(TL value)
            => new(new SingleSeq<TH, TL, Seq<TH, TL>>(value));

        public Seq<TH, TL> Nested(TH head, TA next)
            => new(new NestedSeq<TH, TL, Seq<TH, TL>>(head, step(this, next)));
    }

    public static Seq<TH, TL> Unfold<TA>(TA value, UnfoldStep<TA, Seq<TH, TL>> unfolder)
        where TA : allows ref struct
            => unfolder(new UnfolderSemantic<TA>(unfolder), value);

    public override string ToString()
        => FoldLazy(new FormatSemantic<TH, TL>(true));

    public Seq<TH, TR> SelectMany<TR>(Func<TL, Seq<TH, TR>> f)
        => Fold(new SelectManySemantic<TH, TL, TR>(f));
}

sealed class SelectManySemantic<TH, TL, TR>(Func<TL, Seq<TH, TR>> f) : ISeqSemantic<TH, TL, Seq<TH, TR>, Seq<TH, TR>>
{
    public Seq<TH, TR> Nested(TH head, Seq<TH, TR> next)
        => new(new NestedSeq<TH, TR, Seq<TH, TR>>(head, next));

    public Seq<TH, TR> Single(TL value)
        => f(value);
}

sealed class FoldSemantic<TH, TL, T>(ISeqSemantic<TH, TL, T, T> semantic) : ISeqSemantic<TH, TL, Seq<TH, TL>, T>
{
    public T Nested(TH head, Seq<TH, TL> next)
        => semantic.Nested(head, next.Value.Evaluate(this));

    public T Single(TL value)
        => semantic.Single(value);
}

sealed class StatefulFoldSemantic<TH, TL, T>(ISeqSemantic<TH, TL, Func<T>, T> semantic)
    : ISeqSemantic<TH, TL, Seq<TH, TL>, T>
{
    public T Nested(TH head, Seq<TH, TL> next)
        => semantic.Nested(head, () => next.Value.Evaluate(this));

    public T Single(TL value)
        => semantic.Single(value);
}


sealed class LastSemantic<TH, TL> : ISeqSemantic<TH, TL, TL, TL>
{
    public TL Nested(TH head, TL next)
        => next;

    public TL Single(TL value)
        => value;
}

sealed class FormatSemantic<TH, TL>(bool isFirst) : ISeqSemantic<TH, TL, Func<string>, string>
{
    public bool IsFirst { get; private set; } = isFirst;

    public string Nested(TH head, Func<string> next)
    {
        if (IsFirst)
        {
            IsFirst = false;
            return $"[{head}, {next()}]";
        }
        else
        {
            IsFirst = false;
            return $"{head}, {next()}";
        }
    }

    public string Single(TL value)
    {
        return IsFirst ? $"[; {value}]" : $"; {value}";
    }
}

public static class Seq
{
    public static Seq<TH, TT> Create<TH, TT>(IEnumerable<TH> seq, TT last) =>
        Seq<TH, TT>.Unfold<ReadOnlySpan<TH>>([..seq], (b, s) => s.IsEmpty ? b.Single(last) : b.Nested(s[0], s[1..]));

    public static Seq<TH, TL> Single<TH, TL>(TL last) => Seq<TH, TL>.Single(last);
    public static Seq<TH, TL> Nested<TH, TL>(TH head, Seq<TH, TL> next) => Seq<TH, TL>.Nested(head, next);

    sealed class SeqSemantic<TH, TL, TS, TO>(Func<TL, TO> single, Func<TH, TS, TO> concat) : ISeqSemantic<TH, TL, TS, TO>
    {
        public TO Nested(TH head, TS seq)
            => concat(head, seq);

        public TO Single(TL value)
            => single(value);
    }

    public static ISeqSemantic<TH, TL, TS, TO> Semantic<TH, TL, TS, TO>(Func<TL, TO> single, Func<TH, TS, TO> concat)
        => new SeqSemantic<TH, TL, TS, TO>(single, concat);
}


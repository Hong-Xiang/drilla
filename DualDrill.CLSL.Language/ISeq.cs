namespace DualDrill.CLSL.Language;

public interface ISeqSemantic<in THead, in TLast, in TSeq, out TO>
{
    TO Single(TLast value);
    TO Nested(THead head, TSeq next);
}

public interface ISeq<TH, TL, TS>
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
    public TL Last => Value.Evaluate(new LastSemantic<TH, TL>());
    public Seq<THR, TLR> Select<THR, TLR>(Func<TH, THR> f, Func<TL, TLR> g) => throw new NotImplementedException();
    public T Fold<T>(ISeqSemantic<TH, TL, T, T> semantic)
        => Value.Evaluate(new FoldSemantic<TH, TL, T>(semantic));
}
sealed class FoldSemantic<TH, TL, T>(ISeqSemantic<TH, TL, T, T> semantic) : ISeqSemantic<TH, TL, Seq<TH, TL>, T>
{
    public T Nested(TH head, Seq<TH, TL> next)
        => semantic.Nested(head, next.Value.Evaluate(this));

    public T Single(TL value)
        => semantic.Single(value);
}

sealed class LastSemantic<TH, TL> : ISeqSemantic<TH, TL, Seq<TH, TL>, TL>
{
    public TL Nested(TH head, Seq<TH, TL> next)
        => next.Last;

    public TL Single(TL value)
        => value;
}



public static class Seq
{
    public static ISeq<TH, TT> Create<TH, TT>(ReadOnlySpan<TH> seq, TT last) =>
        seq.IsEmpty ? new SingleSeq<TH, TT>(last) : new ConcatSeq<TH, TT>(seq[0], Create(seq[1..], last));


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


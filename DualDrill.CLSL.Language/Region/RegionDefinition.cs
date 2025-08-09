namespace DualDrill.CLSL.Language.Region;

public enum RegionKind
{
    Block,
    Loop,
}

public interface IRegionDefinitionSemantic<in TX, in TL, in TB, out TO>
{
    TO Block(TX context, TL label, TB body);
    TO Loop(TX context, TL label, TB body);
}

public interface IRegionDefinition<out TL, out TB>
{
    TL Label { get; }
    TB Body { get; }
    RegionKind Kind { get; }
    TR Evaluate<TX, TR>(IRegionDefinitionSemantic<TX, TL, TB, TR> semantic, TX context);
    IRegionDefinition<TL, TR> Select<TR>(Func<TB, TR> f);
}


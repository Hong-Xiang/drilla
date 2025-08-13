using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;


public enum ValueDefKind
{
    Normal,
    FunctionParameter,
    RegionParameter
}

public sealed class SemanticModel
{
    int ValueCount = 0;
    Dictionary<IShaderValue, ValueDefInfoData> ValueDefInfo = [];
    int LabelCount = 0;
    Dictionary<Label, int> LabelDefIndex = [];


    readonly record struct ValueDefInfoData(
        ValueDefKind Kind,
        int Index
    )
    {
    }


    public SemanticModel(FunctionBody4 body)
    {
        Body = body;
        Analysis(Body);
    }

    public FunctionBody4 Body { get; }


    void ValueDef(IShaderValue value, ValueDefKind kind)
    {
        ValueDefInfo.Add(value, new(kind, ValueCount));
        ValueCount++;
    }

    public int ValueIndex(IShaderValue value)
        => ValueDefInfo[value].Index;

    void LabelDef(Label label)
    {
        LabelDefIndex.Add(label, LabelCount);
        LabelCount++;
    }

    public int LabelIndex(Label l) => LabelDefIndex[l];

    void Analysis(FunctionBody4 body)
    {
        foreach (var (i, p) in body.Parameters.Index())
        {
            ValueDef(p.Value, ValueDefKind.FunctionParameter);
        }

        foreach (var decl in body.LocalVariableValues)
        {
            ValueDef(decl.Value, ValueDefKind.RegionParameter);
        }
    }
}

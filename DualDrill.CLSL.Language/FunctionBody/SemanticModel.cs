﻿using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class SemanticModel
    : IRegionTreeFoldLazySemantic<Label, ShaderRegionBody, Unit, Unit>
    , ITerminatorSemantic<RegionJump, IShaderValue, Unit>

{
    int ValueCount = 0;
    Dictionary<IShaderValue, int> ValueIndices = [];
    int LabelCount = 0;
    Dictionary<Label, int> LabelDefIndex = [];
    HashSet<Label> LoopLabels = [];
    Dictionary<Label, ImmutableStack<Label>> DefinedScope = [];
    Dictionary<Label, HashSet<Label>> LabelUsage = [];


    Dictionary<IShaderValue, int> ValueUsage = [];
    Dictionary<Label, RegionTree<Label, ShaderRegionBody>> RegionTreeDefinitions = [];

    Stack<ITerminator<RegionJump, IShaderValue>> VisitingTerminator = [];
    ImmutableStack<Label> Scope = [];

    public RegionTree<Label, ShaderRegionBody> RegionTree(Label label) => RegionTreeDefinitions[label];


    public SemanticModel(FunctionBody4 body)
    {
        FunctionBody = body;
        ValueIndices = (body.GetValueDefinitions().Concat(body.GetUsedValues())).Distinct().Select((v, i) => (v, i))
            .ToDictionary(x => x.v, x => x.i);
        FunctionBody.Body.Fold(this);
        FunctionBody.Body.Traverse(r => { RegionTreeDefinitions.Add(r.Label, r); });
        foreach (var l in LabelDefIndex.Keys)
        {
            if (!LabelUsage.ContainsKey(l))
            {
                LabelUsage.Add(l, []);
            }
        }
    }

    public FunctionBody4 FunctionBody { get; }


    void ValueUse(IShaderValue value, object? user)
    {
        if (!ValueUsage.TryGetValue(value, out var count))
        {
            ValueUsage.Add(value, 0);
        }

        ValueUsage[value]++;
    }

    public int ValueIndex(IShaderValue value)
        => ValueIndices[value];
    //=> ValueIndices.TryGetValue(value, out var index) ? index : -1;

    void LabelDef(Label label)
    {
        DefinedScope.Add(label, Scope);
        LabelDefIndex.Add(label, LabelCount);
        LabelCount++;
    }

    Label? CurrentScope => Scope.IsEmpty ? null : Scope.Peek();

    void LabelUse(Label label, ITerminator<RegionJump, IShaderValue> terminator)
    {
        if (LabelUsage.TryGetValue(label, out var usages))
        {
            usages.Add(CurrentScope ?? throw new NullReferenceException());
        }
        else
        {
            LabelUsage.Add(label, [CurrentScope ?? throw new NullReferenceException()]);
        }
    }


    public int LabelIndex(Label l) => LabelDefIndex[l];

    public bool IsUsedOnce(Label l) => LabelUsage[l].Count() == 1;
    public bool IsUsedInJoin(Label l) => false;
    public bool IsLoop(Label l) => LoopLabels.Contains(l);


    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Single(ShaderRegionBody value)
    {
        foreach (var e in value.Body.Elements)
        {
            foreach (var o in e.Operands)
            {
                ValueUse(o, null);
            }
        }

        VisitingTerminator.Push(value.Body.Last);
        value.Body.Last.Evaluate(this);
        VisitingTerminator.Pop();
        return default;
    }

    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Nested(Func<Unit> head, Func<Unit> next)
    {
        head();
        next();
        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Block(Label label, Func<Unit> body, Label? next)
    {
        LabelDef(label);
        Scope = Scope.Push(label);
        body();
        Scope = Scope.Pop();
        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Loop(Label label, Func<Unit> body, Label? next,
        Label? breakNext)
    {
        LabelDef(label);
        LoopLabels.Add(label);
        Scope = Scope.Push(label);
        body();
        Scope = Scope.Pop();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnVoid()
    {
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.ReturnExpr(IShaderValue expr)
    {
        ValueUse(expr, null);
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.Br(RegionJump target)
    {
        LabelUse(target.Label, VisitingTerminator.Peek());
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, IShaderValue, Unit>.BrIf(IShaderValue condition, RegionJump trueTarget,
        RegionJump falseTarget)
    {
        LabelUse(trueTarget.Label, VisitingTerminator.Peek());
        LabelUse(falseTarget.Label, VisitingTerminator.Peek());
        return default;
    }
}
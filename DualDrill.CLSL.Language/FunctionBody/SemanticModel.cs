using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;


public enum ValueDefKind
{
    Normal,
    FunctionParameter,
    RegionParameter
}

public sealed class SemanticModel
    : IRegionTreeFoldLazySemantic<Label, ShaderRegionBody, Unit, Unit>
    , ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>, Func<Unit>, Unit>
    , ITerminatorSemantic<RegionJump, ShaderValue, Unit>
    , IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>
    , IExpressionTreeLazyFoldSemantic<ShaderValue, Unit>

{
    int ValueCount = 0;
    Dictionary<IShaderValue, ValueDefInfoData> ValueDefInfo = [];
    int LabelCount = 0;
    Dictionary<Label, int> LabelDefIndex = [];

    Dictionary<Label, ImmutableArray<ITerminator<RegionJump, ShaderValue>>> LabelUsage = [];
    Dictionary<ShaderValue, int> ValueUsage = [];
    Dictionary<Label, ShaderRegionBody> RegionBody = [];

    Stack<ITerminator<RegionJump, ShaderValue>> VisitingTerminator = [];
    Stack<ShaderRegionBody> VisitedRegionBodies = [];

    readonly record struct ValueDefInfoData(
        ValueDefKind Kind,
        int ValueIndex,
        int? InfoIndex
    )
    {
    }


    public SemanticModel(FunctionBody4 body)
    {
        Body = body;
        Analysis(Body);
        Body.Body.Fold(this);
    }

    public FunctionBody4 Body { get; }


    void ValueDef(IShaderValue value, ValueDefKind kind, int? infoIndex)
    {
        ValueDefInfo.Add(value, new(kind, ValueCount, infoIndex));
        ValueCount++;
    }
    void ValueDef(IShaderValue value)
    {
        ValueDef(value, ValueDefKind.Normal, null);
    }

    void ValueUse(ShaderValue value, object? user)
    {
        if (!ValueUsage.TryGetValue(value, out var count))
        {
            ValueUsage.Add(value, 0);
        }
        ValueUsage[value]++;
    }
    public int ValueIndex(IShaderValue value)
        => ValueDefInfo[value].ValueIndex;

    void LabelDef(Label label)
    {
        LabelDefIndex.Add(label, LabelCount);
        LabelCount++;
    }
    void LabelUse(Label label, ITerminator<RegionJump, ShaderValue> terminator)
    {
        if (!LabelUsage.TryGetValue(label, out var usages))
        {
            usages = [];
            LabelUsage.Add(label, usages);
        }
        usages.Add(terminator);
    }
    public int LabelUsageCount(Label label)
        => LabelUsage[label].Length;


    public int LabelIndex(Label l) => LabelDefIndex[l];

    void Analysis(FunctionBody4 body)
    {
        foreach (var (i, p) in body.Parameters.Index())
        {
            ValueDef(p.Value, ValueDefKind.FunctionParameter, i);
        }

        foreach (var decl in body.LocalVariableValues)
        {
            ValueDef(decl.Value, ValueDefKind.RegionParameter, null);
        }
    }

    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Single(ShaderRegionBody value)
    {
        foreach (var (i, p) in value.Parameters.Index())
        {
            ValueDef(p.Value, ValueDefKind.RegionParameter, i);
        }
        value.Body.FoldLazy<Unit>(this);
        VisitedRegionBodies.Push(value);
        return default;
    }

    Unit ISeqSemantic<Func<Unit>, ShaderRegionBody, Func<Unit>, Unit>.Nested(Func<Unit> head, Func<Unit> next)
    {
        head();
        next();
        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Block(Label label, Func<Unit> body)
    {
        LabelDef(label);
        body();
        RegionBody.Add(label, VisitedRegionBodies.Pop());
        return default;
    }

    Unit IRegionDefinitionSemantic<Label, Func<Unit>, Unit>.Loop(Label label, Func<Unit> body)
    {
        LabelDef(label);
        body();
        RegionBody.Add(label, VisitedRegionBodies.Pop());
        return default;
    }

    Unit ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>, Func<Unit>, Unit>.Single(ITerminator<RegionJump, ShaderValue> value)
    {
        VisitingTerminator.Push(value);
        value.Evaluate(this);
        VisitingTerminator.Pop();
        return default;
    }

    Unit ISeqSemantic<ShaderStmt, ITerminator<RegionJump, ShaderValue>, Func<Unit>, Unit>.Nested(ShaderStmt head, Func<Unit> next)
    {
        head.Evaluate(this);
        next();
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.ReturnVoid()
    {
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.ReturnExpr(ShaderValue expr)
    {
        ValueUse(expr, null);
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.Br(RegionJump target)
    {
        LabelUse(target.Label, VisitingTerminator.Peek());
        return default;
    }

    Unit ITerminatorSemantic<RegionJump, ShaderValue, Unit>.BrIf(ShaderValue condition, RegionJump trueTarget, RegionJump falseTarget)
    {
        LabelUse(trueTarget.Label, VisitingTerminator.Peek());
        LabelUse(falseTarget.Label, VisitingTerminator.Peek());
        return default;
    }

    Unit IExpressionTreeLazyFoldSemantic<ShaderValue, Unit>.Value(ShaderValue value)
    {
        ValueUse(value, null);
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Literal<TLiteral>(TLiteral literal)
    {
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfSymbol(IAddressOfSymbolOperation operation)
    {
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfChain(IAccessChainOperation operation, Func<Unit> e)
    {
        e();
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.AddressOfIndex(IAccessChainOperation operation, Func<Unit> e, Func<Unit> index)
    {
        e();
        index();
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation1(IUnaryExpressionOperation operation, Func<Unit> e)
    {
        e();
        return default;
    }

    Unit IExpressionSemantic<Func<Unit>, Unit>.Operation2(IBinaryExpressionOperation operation, Func<Unit> l, Func<Unit> r)
    {
        l();
        r();
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Nop()
    {
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Let(ShaderValue result, ShaderExpr expr)
    {
        ValueDef(result);
        expr.Fold(this);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Get(ShaderValue result, ShaderValue source)
    {
        ValueDef(result);
        ValueUse(source, null);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Set(ShaderValue target, ShaderValue source)
    {
        ValueUse(target, null);
        ValueUse(source, null);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Mov(ShaderValue target, ShaderValue source)
    {
        ValueDef(target);
        ValueUse(source, null);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Call(ShaderValue result, FunctionDeclaration f, IReadOnlyList<ShaderExpr> arguments)
    {
        ValueDef(result);
        foreach (var a in arguments)
        {
            a.Fold(this);
        }
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Dup(ShaderValue result, ShaderValue source)
    {
        ValueDef(result);
        ValueUse(source, null);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.Pop(ShaderValue target)
    {
        ValueUse(target, null);
        return default;
    }

    Unit IStatementSemantic<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, Unit>.SetVecSwizzle(IVectorSwizzleSetOperation operation, ShaderValue target, ShaderValue value)
    {
        ValueUse(target, null);
        ValueUse(value, null);
        return default;
    }
}

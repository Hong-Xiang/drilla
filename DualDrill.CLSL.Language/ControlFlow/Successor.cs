using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

public enum SuccessorKind
{
    Unconditional,
    Conditional,
    Switch,
    Terminate
}

public interface ISuccessorSemantic<in TX, in TI, out TO>
{
    TO Unconditional(TX context, TI target);
    TO Conditional(TX context, TI trueTarget, TI falseTarget);
    TO Switch(TX context, IReadOnlyList<TI> caseTargets, TI defaultTarget);
    TO Terminate(TX context);
}

public interface ISuccessor<T>
{
    TR Evaluate<TX, TR>(ISuccessorSemantic<TX, T, TR> semantic, TX context);
}

public interface ISuccessor
    : ITextDumpable<ILocalDeclarationContext>
    , ISuccessor<Label>
{
    void Traverse(Action<Label> action);
    IEnumerable<Label> GetReferencedLabels();
}

public sealed record class UnconditionalSuccessor(Label Target) : ISuccessor
{
    public void Traverse(Action<Label> action)
    {
        action(Target);
    }


    public IEnumerable<Label> GetReferencedLabels() => [Target];


    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"br -> {context.LabelName(Target)}");
    }

    public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, Label, TR> semantic, TX context) =>
        semantic.Unconditional(context, Target);

    public override string ToString() => $"br -> ^{Target.Name}";
}

public sealed record class ConditionalSuccessor(Label TrueTarget, Label FalseTarget) : ISuccessor
{
    public IEnumerable<Label> GetReferencedLabels() => [TrueTarget, FalseTarget];

    public void Traverse(Action<Label> action)
    {
        action(TrueTarget);
        action(FalseTarget);
    }


    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"br_if -> t: {context.LabelName(TrueTarget)} f: {context.LabelName(FalseTarget)}");
    }

    public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, Label, TR> semantic, TX context) =>
        semantic.Conditional(context, TrueTarget, FalseTarget);

    public override string ToString() => $"br_if -> t: ^{TrueTarget.Name} f: ^{FalseTarget.Name}";
}

public sealed class SwitchSuccessor : ISuccessor, IEquatable<SwitchSuccessor>
{
    public SwitchSuccessor(ImmutableArray<Label> caseTargets, Label defaultTarget)
    {
        if (caseTargets.IsDefault)
            throw new ArgumentException("Switch case targets must be initialized.", nameof(caseTargets));
        CaseTargets = caseTargets;
        DefaultTarget = defaultTarget;
    }

    public ImmutableArray<Label> CaseTargets { get; }
    public Label DefaultTarget { get; }

    public IEnumerable<Label> GetReferencedLabels() => [.. CaseTargets, DefaultTarget];

    public void Traverse(Action<Label> action)
    {
        foreach (var target in CaseTargets)
            action(target);
        action(DefaultTarget);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.Write("switch -> [");
        writer.Write(string.Join(", ", CaseTargets.Select(context.LabelName)));
        writer.Write("] default: ");
        writer.WriteLine(context.LabelName(DefaultTarget));
    }

    public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, Label, TR> semantic, TX context) =>
        semantic.Switch(context, CaseTargets, DefaultTarget);

    public bool Equals(SwitchSuccessor? other) =>
        other is not null &&
        CaseTargets.SequenceEqual(other.CaseTargets) &&
        Equals(DefaultTarget, other.DefaultTarget);

    public override bool Equals(object? obj) => obj is SwitchSuccessor other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var target in CaseTargets)
            hash.Add(target);
        hash.Add(DefaultTarget);
        return hash.ToHashCode();
    }

    public override string ToString() =>
        $"switch -> [{string.Join(", ", CaseTargets.Select(target => $"^{target.Name}"))}] " +
        $"default: ^{DefaultTarget.Name}";
}

public sealed record class TerminateSuccessor : ISuccessor
{
    public IEnumerable<Label> GetReferencedLabels() => [];

    public void Traverse(Action<Label> action)
    {
    }


    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("return");
    }

    public TR Evaluate<TX, TR>(ISuccessorSemantic<TX, Label, TR> semantic, TX context) => semantic.Terminate(context);

    public override string ToString() => "return";
}

public static class Successor
{
    public static ISuccessor Unconditional(Label target) => new UnconditionalSuccessor(target);

    public static ISuccessor Conditional(Label trueTarget, Label falseTarget) =>
        new ConditionalSuccessor(trueTarget, falseTarget);

    public static ISuccessor Switch(ImmutableArray<Label> caseTargets, Label defaultTarget) =>
        new SwitchSuccessor(caseTargets, defaultTarget);

    public static ISuccessor Terminate() => new TerminateSuccessor();

    public static IEnumerable<Label> AllTargets(this ISuccessor successor)
    {
        List<Label> targets = [];
        successor.Traverse(targets.Add);
        return targets;
    }
}
using System.CodeDom.Compiler;
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

    public static ISuccessor Switch() => throw new NotImplementedException();

    public static ISuccessor Terminate() => new TerminateSuccessor();

    public static IEnumerable<Label> AllTargets(this ISuccessor successor)
    {
        List<Label> targets = [];
        successor.Traverse(targets.Add);
        return targets;
    }
}
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.ControlFlow;

public enum SuccessorKind
{
    Unconditional,
    Conditional,
    Switch,
    Terminate
}

public interface ISuccessor
    : ITextDumpable<ILocalDeclarationContext>
{
    void Traverse(Action<Label> action);
    bool IsCompatible(ITerminatorStackInstruction terminator);
    IEnumerable<Label> GetReferencedLabels();
}

public sealed record class UnconditionalSuccessor(Label Target) : ISuccessor
{
    public void Traverse(Action<Label> action)
    {
        action(Target);
    }

    public bool IsCompatible(ITerminatorStackInstruction terminator)
        => terminator is BrInstruction { Target: var target } && target == Target;

    public IEnumerable<Label> GetReferencedLabels() => [Target];


    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"br -> {context.LabelName(Target)}");
    }
}

public sealed record class ConditionalSuccessor(Label TrueTarget, Label FalseTarget) : ISuccessor
{
    public IEnumerable<Label> GetReferencedLabels() => [TrueTarget, FalseTarget];

    public void Traverse(Action<Label> action)
    {
        action(TrueTarget);
        action(FalseTarget);
    }

    public bool IsCompatible(ITerminatorStackInstruction terminator)
        => terminator is BrIfInstruction { TrueTarget: var tt, FalseTarget: var ft } && TrueTarget == tt &&
           FalseTarget == ft;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"br_if -> t: {context.LabelName(TrueTarget)} f: {context.LabelName(FalseTarget)}");
    }
}

public sealed record class TerminateSuccessor() : ISuccessor
{
    public IEnumerable<Label> GetReferencedLabels() => [];

    public void Traverse(Action<Label> action)
    {
    }

    public bool IsCompatible(ITerminatorStackInstruction terminator)
        => terminator is ReturnResultStackInstruction;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("return");
    }
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

//public static class Successor
//{
//    sealed class VisitorAlgebra<TLabel>(Action<TLabel> Run) : ISuccessorAlgebra<TLabel, TLabel, Unit>
//    {
//        public Unit Br(TLabel target)
//        {
//            Run(target);
//            return default;
//        }

//        public Unit BrIf(TLabel trueTarget, TLabel falseTarget)
//        {
//            Run(trueTarget);
//            Run(falseTarget);
//            return default;
//        }

//        public Unit Return()
//        {
//            return default;
//        }

//        public Unit Switch(TLabel defaultLabel, IEnumerable<(ILiteral, TLabel)> cases)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public static void Match<TLabel>(this ISuccessor<TLabel, TLabel> successor, Action<TLabel> action)
//    {
//        successor.Evaluate(new VisitorAlgebra<TLabel>(action));
//    }

//    sealed class KindAlgebra<TLabel, TFalseLabel> : ISuccessorAlgebra<TLabel, TFalseLabel, SuccessorKind>
//    {
//        public SuccessorKind Br(TLabel target) => SuccessorKind.BrOrNext;

//        public SuccessorKind BrIf(TLabel trueTarget, TFalseLabel falseTarget) => SuccessorKind.BrIf;
//        public SuccessorKind Return() => SuccessorKind.ReturnOrTerminate;
//        public SuccessorKind Switch(TLabel defaultLabel, IEnumerable<(ILiteral, TLabel)> cases) => SuccessorKind.Switch;
//    }

//    public static SuccessorKind GetKind<TLabel, TFalseLabel>(this ISuccessor<TLabel, TFalseLabel> successor)
//    {
//        return successor.Evaluate(new KindAlgebra<TLabel, TFalseLabel>());
//    }
//}


//public class SuccessorFactory<TLabel> : ISingleton<SuccessorFactory<TLabel>>, ISuccessorAlgebra<TLabel, TLabel, ISuccessor<TLabel, TLabel>>
//{
//    public static SuccessorFactory<TLabel> Instance { get; } = new();

//    public ISuccessor<TLabel, TLabel> Br(TLabel target) => new BrSuccessor<TLabel, TLabel>(target);
//    public ISuccessor<TLabel, TLabel> BrIf(TLabel trueTarget, TLabel falseTarget) => new BrIfSuccessor<TLabel, TLabel>(trueTarget, falseTarget);
//    public ISuccessor<TLabel, TLabel> Return() => new ReturnOrTerminateSuccessor<TLabel, TLabel>();

//    public ISuccessor<TLabel, TLabel> Switch(TLabel defaultLabel, IEnumerable<(ILiteral, TLabel)> cases)
//        => throw new NotImplementedException();
//}
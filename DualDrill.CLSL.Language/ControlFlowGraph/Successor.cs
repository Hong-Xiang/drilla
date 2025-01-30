using DotNext.Patterns;
using DualDrill.CLSL.Language.Literal;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public enum SuccessorKind
{
    Unconditional,
    Conditional,
    Switch,
    Terminate
}

public interface ISuccessor
{
    void Traverse(Action<Label> action);
}

public sealed record class BrOrNextSuccessor(Label Target) : ISuccessor
{
    public void Traverse(Action<Label> action)
    {
        action(Target);
    }
}
public sealed record class BrIfSuccessor(Label TrueTarget, Label FalseTarget) : ISuccessor
{
    public void Traverse(Action<Label> action)
    {
        action(TrueTarget);
        action(FalseTarget);
    }
}
public sealed record class ReturnOrTerminateSuccessor() : ISuccessor
{
    public void Traverse(Action<Label> action)
    {
    }
}


public static class Successor
{
    public static ISuccessor Unconditional(Label target) => new BrOrNextSuccessor(target);
    public static ISuccessor Conditional(Label trueTarget, Label falseTarget) => new BrIfSuccessor(trueTarget, falseTarget);
    public static ISuccessor Switch() => throw new NotImplementedException();
    public static ISuccessor Terminate() => new ReturnOrTerminateSuccessor();
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


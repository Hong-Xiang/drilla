using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Value;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ValueInstruction;

public interface IValueInstruction : IDeclarationUser
{
    IEnumerable<Label> IDeclarationUser.ReferencedLabels => [];
    IEnumerable<VariableDeclaration> IDeclarationUser.ReferencedLocalVariables => [];

    void ITextDumpable<ILocalDeclarationContext>.Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(ToString());
    }
}

public interface ITerminatorValueInstruction : IValueInstruction
{
    ISuccessor ToSuccessor();
}

public interface IOperationValueInstruction : IValueInstruction
{
    IOperation Operation { get; }
}

public interface IExpressionValueInstruction : IValueInstruction
{
    IEnumerable<IValue> IDeclarationUser.ReferencedValues => [ResultValue];
    IOperationValue ResultValue { get; }
}

public interface IStatementValueInstruction : IValueInstruction
{
}

public interface IExpressionOperationValueInstruction : IExpressionValueInstruction, IOperationValueInstruction
{
}

public interface IStatementOperationValueInstruction : IStatementValueInstruction, IOperationValueInstruction
{
}
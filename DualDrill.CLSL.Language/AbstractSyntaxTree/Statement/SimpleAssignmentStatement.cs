using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentOp
{
    Assign,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulus,
    BitwiseAnd,
    BitwiseOr,
    ExclusiveOr,
    ShiftRight,
    ShiftLeft
}

public sealed record class SimpleAssignmentStatement(
    IExpression L,
    IExpression R,
    AssignmentOp Op
) : IStatement, IStackStatement, IForInit, IForUpdate
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine("assign :=");
        using (writer.IndentedScope())
        {
            writer.WriteLine("target:");
            using (writer.IndentedScope())
            {
                L.Dump(context, writer);
            }

            writer.WriteLine("value:");
            using (writer.IndentedScope())
            {
                R.Dump(context, writer);
            }
        }
    }

    public override string ToString()
    {
        return Op == AssignmentOp.Assign ? $"{L} = {R}" : $"{L} {Op} {R}";
    }

    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitSimpleAssignment(this);

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
    [
        ..L.ReferencedVariables,
        ..R.ReferencedVariables,
    ];

    public IEnumerable<IInstruction> ToInstructions()
    {
        return L switch
        {
            VariableIdentifierExpression { Variable: VariableDeclaration v } =>
                [..R.ToInstructions(), ShaderInstruction.Store(v)],
            VariableIdentifierExpression { Variable: ParameterDeclaration p } =>
                [..R.ToInstructions(), ShaderInstruction.Store(p)],
            FormalParameterExpression { Parameter: var p } =>
                [..R.ToInstructions(), ShaderInstruction.Store(p)],
            NamedComponentExpression
                {
                    Base: var e,
                    Component: var m
                } =>
                [
                    ..e.ToInstructions(),
                    ..R.ToInstructions(),
                    ShaderInstruction.Store(m)
                ],
            _ => throw new NotImplementedException(),
        };
    }
}
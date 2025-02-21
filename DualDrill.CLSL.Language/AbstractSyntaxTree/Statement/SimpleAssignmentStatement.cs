using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;

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
    public IEnumerable<Label> ReferencedLabels => [];

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables =>
    [
        ..L.ReferencedVariables,
        ..R.ReferencedVariables,
    ];

    public IEnumerable<IStackInstruction> ToInstructions()
    {
        return L switch
        {
            VariableIdentifierExpression { Variable: VariableDeclaration v } =>
                [..R.ToInstructions(), ShaderInstruction.Store(v)],
            VariableIdentifierExpression { Variable: ParameterDeclaration p } =>
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
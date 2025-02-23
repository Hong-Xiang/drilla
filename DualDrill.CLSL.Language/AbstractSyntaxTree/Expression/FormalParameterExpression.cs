using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class FormalParameterExpression(ParameterDeclaration Parameter) : IExpression
{
    public IShaderType Type => Parameter.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitFormalParameterExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        => [ShaderInstruction.Load(Parameter)];

    public IEnumerable<VariableDeclaration> ReferencedVariables => [];

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine(Parameter);
    }
}
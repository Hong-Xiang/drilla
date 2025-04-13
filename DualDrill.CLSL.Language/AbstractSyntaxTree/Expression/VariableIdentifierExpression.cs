using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public interface IVariableIdentifierSymbol : ILoadStoreTargetSymbol
{
    string Name { get; }
}

public sealed record class VariableIdentifierExpression(IVariableIdentifierSymbol Variable) : IExpression
{
    public IShaderType Type => Variable.Type;

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitVariableIdentifierExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
        => Variable switch
        {
            ParameterDeclaration d => [ShaderInstruction.Load(d)],
            VariableDeclaration d => [ShaderInstruction.Load(d)],
            _ => throw new InvalidOperationException()
        };

    public IEnumerable<VariableDeclaration> ReferencedVariables => Variable switch
    {
        VariableDeclaration d => [d],
        _ => []
    };

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        switch (Variable)
        {
            case VariableDeclaration v:
                writer.WriteLine(context.VariableName(v));
                break;
            case ParameterDeclaration p:
                writer.WriteLine(p);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}

public interface ILoadStoreTargetSymbol
{
    IShaderType Type { get; }
}
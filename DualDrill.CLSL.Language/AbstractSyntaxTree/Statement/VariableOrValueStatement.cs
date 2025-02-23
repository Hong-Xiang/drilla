using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

public sealed record class VariableOrValueStatement(VariableDeclaration Variable) : IStatement, IForInit
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitVariableOrValue(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"var {context.VariableName(Variable)} : {Variable.Type.Name}");
    }

    public IEnumerable<Label> ReferencedLabels => [];
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [Variable];
}
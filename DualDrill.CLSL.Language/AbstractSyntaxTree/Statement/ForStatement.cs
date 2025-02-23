using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;

[Obsolete]
public sealed record class ForStatement(
    ImmutableHashSet<IShaderAttribute> Attributes,
    ForHeader ForHeader,
    IStatement Statement
) : IStatement
{
    public T Accept<T>(IStatementVisitor<T> visitor)
        => visitor.VisitFor(this);

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Label> ReferencedLabels { get; }
    public IEnumerable<VariableDeclaration> ReferencedLocalVariables { get; }
}

public sealed record class ForHeader
{
    public IForInit? Init { get; set; } = null;
    public IExpression? Expr { get; set; } = null;
    public IForUpdate? Update { get; set; } = null;
}


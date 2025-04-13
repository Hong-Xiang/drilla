using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

public sealed record class AddressOfExpression(IExpression Base) : IExpression
{
    public IShaderType Type => Base.Type.GetPtrType();

    public TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        => visitor.VisitAddressOfExpression(this);

    public IEnumerable<IInstruction> ToInstructions()
    {
        return Base switch
        {
            VariableIdentifierExpression { Variable: VariableDeclaration v } =>
            [
                ShaderInstruction.Load(v),
                ShaderInstruction.AddressOf(v.Type)
            ],
            FormalParameterExpression { Parameter: var p } =>
            [
                ShaderInstruction.Load(p),
                ShaderInstruction.AddressOf(p.Type)
            ],
            NamedComponentExpression { Base: var be, Component: var m } =>
            [
                ..be.ToInstructions(), ShaderInstruction.Load(m),
                ShaderInstruction.AddressOf(m.Type)
            ],
            _ => throw new NotImplementedException()
        };
    }

    public IEnumerable<VariableDeclaration> ReferencedVariables => Base.ReferencedVariables;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"address_of : {Type.Name}");
        using (writer.IndentedScope())
        {
            Base.Dump(context, writer);
        }
    }
}
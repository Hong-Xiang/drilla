using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

using ShaderStmt = IStatement<Unit, IExpression<Unit>, ILoadStoreTargetSymbol, FunctionDeclaration>;

public sealed class StackInstrctionBasicBlock : IBasicBlock2
{
    public StackInstrctionBasicBlock(
        Label label,
        ITerminator<Label, Unit> terminator,
        ImmutableArray<IShaderType> inputs,
        ImmutableArray<IShaderType> outputs,
        Seq<ShaderStmt, ITerminator<Label, Unit>> statements
    )
    {
        Label = label;
        Terminator = terminator;
        Inputs = inputs;
        Outputs = outputs;
        Statements = statements;
    }

    public Label Label { get; }
    public ITerminator<Label, Unit> Terminator { get; }
    public ISuccessor Successor => Terminator.ToSuccessor();

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => [];

    public IEnumerable<IValue> ReferencedValues => [];

    public FrozenDictionary<ShaderValue, IShaderType> ValueTypes { get; }
    public ImmutableArray<IShaderType> Inputs { get; }
    public ImmutableArray<IShaderType> Outputs { get; }
    public Seq<ShaderStmt, ITerminator<Label, Unit>> Statements { get; }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        writer.WriteLine($"^{Label}:");
        foreach (var stmt in Statements.Elements)
        {
            writer.WriteLine(stmt.ToString());
        }
        writer.WriteLine(Statements.Last);
    }
}

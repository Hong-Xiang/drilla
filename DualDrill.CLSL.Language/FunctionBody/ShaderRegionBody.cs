using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ShaderRegionBody(
    Label Label,
    ImmutableArray<IShaderValue> Parameters,
    Seq<ShaderStmt, ITerminator<RegionJump, IShaderValue>> Body,
    Label? ImmediatePostDominator
) : IBasicBlock2
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => throw new NotSupportedException();
    public IEnumerable<IShaderValue> ReferencedValues => [
        ..Parameters
    ];
    public ISuccessor Successor => Body.Last.ToSuccessor();
}

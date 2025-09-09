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

    public static ShaderRegionBody Create(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<ShaderStmt> statements,
        ITerminator<RegionJump, IShaderValue> terminator,
        Label? immediatePostDominator
    )
        => new(label, parameters, Seq.Create([.. statements], terminator), immediatePostDominator);

    public ShaderRegionBody MapStatement(Func<ShaderStmt, IEnumerable<ShaderStmt>> f)
    {
        return new ShaderRegionBody(
            Label,
            Parameters,
            Seq.Create(Body.Elements.SelectMany(f), Body.Last),
            ImmediatePostDominator
        );
    }
}

using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ShaderRegionBody(
    Label Label,
    ImmutableArray<IShaderValue> Parameters,
    Seq<Instruction2<IShaderValue, IShaderValue>, ITerminator<RegionJump, IShaderValue>> Body,
    Label? ImmediatePostDominator
) 
{
    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<VariableDeclaration> ReferencedLocalVariables => throw new NotSupportedException();

    public IEnumerable<IShaderValue> ReferencedValues =>
    [
        ..Parameters
    ];

    public ISuccessor Successor => Body.Last.ToSuccessor();

    public static ShaderRegionBody Create(
        Label label,
        ImmutableArray<IShaderValue> parameters,
        IEnumerable<Instruction2<IShaderValue, IShaderValue>> statements,
        ITerminator<RegionJump, IShaderValue> terminator,
        Label? immediatePostDominator
    ) =>
        new(label, parameters, Seq.Create([.. statements], terminator), immediatePostDominator);

    public ShaderRegionBody MapInstruction(
        Func<Instruction2<IShaderValue, IShaderValue>, IEnumerable<Instruction2<IShaderValue, IShaderValue>>> f) =>
        new(
            Label,
            Parameters,
            Seq.Create(Body.Elements.SelectMany(f), Body.Last),
            ImmediatePostDominator
        );
}
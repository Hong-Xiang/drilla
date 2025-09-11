using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.Transform;

public sealed class CommonOperationLoweringPass
    : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl)
        => decl;

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        return new FunctionBody4(
            body.Declaration,
            body.Body.Select(
                l => l,
                rbody => rbody.MapInstruction(TransformInstruction)
            )
        );
    }

    IEnumerable<Instruction2<IShaderValue, IShaderValue>> TransformInstruction(Instruction2<IShaderValue, IShaderValue> inst)
        => [inst];



    public IDeclaration? VisitMember(MemberDeclaration decl)
        => decl;

    public IDeclaration? VisitParameter(ParameterDeclaration decl)
        => decl;

    public IDeclaration? VisitStructure(StructureDeclaration decl)
        => decl;

    public IDeclaration? VisitValue(ValueDeclaration decl)
        => decl;

    public IDeclaration? VisitVariable(VariableDeclaration decl)
        => decl;
}

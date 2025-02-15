using DualDrill.CLSL.Language.Declaration;

namespace DualDrill.CLSL.Language.ControlFlow;

public interface ILocalDeclarationContext
{
    int LabelIndex(Label label);
    int VariableIndex(VariableDeclaration variable);
}
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

/// <summary>
/// Currently support 3 kinds of function body representation:
/// * UnstructedControlFlowInstructionFunctionBody
/// * StructuredStackInstructionFunctionBody
/// * CompondStatement
/// </summary>
public interface IFunctionBody
{
    public void Dump(IndentedTextWriter writer);
}

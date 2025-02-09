using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ControlFlowGraphFunctionBody(
    ControlFlowGraph<BasicBlock<IStructuredStackInstruction>> Graph
) : IFunctionBodyData
{
    public IEnumerable<VariableDeclaration> LocalVariables
    {
        get
        {
            foreach (var l in Graph.Labels())
            {
                var bb = Graph[l];
                foreach (var instruction in bb.Instructions.ToArray())
                {
                    switch (instruction)
                    {
                        case LoadSymbolInstruction<VariableDeclaration> x:
                            yield return x.Target;
                            break;
                        case LoadSymbolAddressInstruction<VariableDeclaration> x:
                            yield return x.Target;
                            break;
                        case StoreSymbolInstruction<VariableDeclaration> x:
                            yield return x.Target;
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }


    public IEnumerable<Label> Labels => Graph.Labels();

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}

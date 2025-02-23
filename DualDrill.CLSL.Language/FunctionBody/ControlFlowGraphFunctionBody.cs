using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ControlFlowGraphFunctionBody(
    ControlFlowGraph<BasicBlock<IStructuredStackInstruction>> Graph
) : IFunctionBodyData
{
    public IEnumerable<VariableDeclaration> FunctionBodyDataLocalVariables
    {
        get
        {
            foreach (var l in Graph.Labels())
            {
                var bb = Graph[l];
                foreach (var instruction in bb.Elements.ToArray())
                {
                    switch (instruction)
                    {
                        case LoadSymbolValueInstruction<VariableDeclaration> x:
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


    public IEnumerable<Label> FunctionBodyDataLabels => Graph.Labels();

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}

public interface IStructuredControlRegionFunctionBody<TElement> {}

public interface IAbstractSyntaxTreeFunctionBody<TElement> {}
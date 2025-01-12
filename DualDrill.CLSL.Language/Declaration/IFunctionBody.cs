using System.CodeDom.Compiler;
using System.Reflection;

namespace DualDrill.CLSL.Language.Declaration;

public interface IFunctionBody
{
    public void Dump(IndentedTextWriter writer);
}

public sealed record class NotParsedFunctionBody(MethodBase Method) : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        writer.WriteLine("...");
    }
}

public sealed record class MethodMetadataRepresentation(MethodBase Method)
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
    }
}

public sealed class ControlFlowGraphRepresentation
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
    }
}

public sealed class StackMachineRepresentation
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
    }
}

public sealed class AbstractSyntaxTreeRepresentation
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
    }
}


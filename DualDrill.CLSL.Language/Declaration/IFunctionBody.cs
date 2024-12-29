using System.CodeDom.Compiler;
using System.Reflection;

namespace DualDrill.CLSL.Language.Declaration;

public interface IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer);
}

public sealed record class EmptyFunctionBody : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
        writer.WriteLine("...");
    }
}

public sealed record class MethodMetadataRepresentation(MethodBase Method)
    : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
    }
}

public sealed class ControlFlowGraphRepresentation
    : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
    }
}

public sealed class StackMachineRepresentation
    : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
    }
}

public sealed class AbstractSyntaxTreeRepresentation
    : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
    }
}


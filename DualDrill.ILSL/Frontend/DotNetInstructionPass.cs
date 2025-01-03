using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed record class DotNetInstructionRepresentation(
    MethodBase Method,
    MethodBody Body,
    ImmutableArray<Instruction> Instructions,
    ImmutableArray<int> InstructionByteSizes,
    int CodeSize
) : IFunctionBody
{
    public void EmitCode(IndentedTextWriter writer)
    {
        writer.WriteLine($"{Instructions.Length} instructions ({CodeSize} bytes)");
        foreach (var (idx, inst) in Instructions.Index())
        {
            writer.WriteLine($"0x{inst.Offset:X8}({InstructionByteSizes[idx]:d4})\t{inst.OpCode} {inst.Operand}");
        }
    }
}


public record class DotNetInstructionPass(
    CompilationContext Context,
    MethodBodyCompilation Compilation
) : IMethodBodyPass
{
    public ShaderModuleCompilation ShaderModuleCompilation => throw new NotImplementedException();

    public IFunctionBody Compile(IFunctionBody compilation)
    {
        var body = method.GetMethodBody() ?? throw new NullReferenceException("Failed to get method body");
        var codeSize = body.GetILAsByteArray()?.Length ?? 0;
        var instructionByteSizes = new int[instructions.Count];

        for (var i = 0; i < instructions.Count; i++)
        {
            var nextOffset = (i + 1) < instructions.Count ? instructions[i + 1].Offset : codeSize;
            instructionByteSizes[i] = nextOffset - instructions[i].Offset;
        }

        return new DotNetInstructionRepresentation(
            method,
            body,
            [.. instructions],
            [.. instructionByteSizes],
            codeSize
        );

        throw new NotImplementedException();
    }
}

using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language;

namespace DualDrill.CLSL.Frontend;

public sealed class LinearCode<TInstruction> : IPrintable
{
    private readonly Action<LinearCode<TInstruction>, IndentedTextWriter, PrettyPrintOption> prettyPrint;

    internal LinearCode(
        CilMethodEnvironment environment,
        ImmutableArray<TInstruction> instructions,
        Action<LinearCode<TInstruction>, IndentedTextWriter, PrettyPrintOption> prettyPrint)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(prettyPrint);
        if (instructions.IsDefault)
            throw new ArgumentException("Linear code instructions must be initialized.", nameof(instructions));

        Environment = environment;
        Instructions = instructions;
        this.prettyPrint = prettyPrint;
    }

    public CilMethodEnvironment Environment { get; }
    public ImmutableArray<TInstruction> Instructions { get; }
    public int Count => Instructions.Length;
    public TInstruction this[int index] => Instructions[index];

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option) =>
        prettyPrint(this, writer, option);
}

using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface ILocalDeclarationContext
{
    ImmutableArray<VariableDeclaration> LocalVariables { get; }
    ImmutableArray<Label> Labels { get; }
    int LabelIndex(Label label);

    int ValueIndex(IShaderValue value);
    // TODO: add type index
    // int TypeIndex(IShaderType type);
}

public static class LocalDeclarationContextExtensions
{
    public static string LabelName(this ILocalDeclarationContext context, Label label) =>
        $"^{context.LabelIndex(label)} ({label.Name})";
}
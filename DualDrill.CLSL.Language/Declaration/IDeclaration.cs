using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

[JsonDerivedType(typeof(FunctionDeclaration), nameof(FunctionDeclaration))]
[JsonDerivedType(typeof(ParameterDeclaration), nameof(ParameterDeclaration))]
[JsonDerivedType(typeof(StructureDeclaration), nameof(StructureDeclaration))]
[JsonDerivedType(typeof(MemberDeclaration), nameof(MemberDeclaration))]
[JsonDerivedType(typeof(VariableDeclaration), nameof(VariableDeclaration))]
[JsonDerivedType(typeof(ValueDeclaration), nameof(ValueDeclaration))]
[JsonDerivedType(typeof(ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>>),
    nameof(ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>>))]
[JsonDerivedType(typeof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>),
    nameof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>))]
public interface IDeclaration : IShaderAstNode
{
    string Name { get; }
    ImmutableHashSet<IShaderAttribute> Attributes { get; }
}

// module = (symbol, decl)[], (function_symbol, body)[]
// decl = module_value | module_variable | structure | function
// module_value = type_ref, attribute[], literal_expr
// module_variable = type_ref, attribute[]
// structure = (symbol, member)[], attribute[], function_ref
// member = type, attribute[]
// function = (symbol, parameter)[], return_type
// parameter = type_ref, attribute[]

// body = (symbol, local_variable)[], region
// region uses type_ref, function_ref

public interface IDeclarationSemantic<in TX, in TD, out TR>
{
    TR Value(TX ctx);
    TR Variable(TX ctx);
    TR Function(TX ctx, IReadOnlyList<TD> parameters, TD ret);
    TR Parameter(TX ctx);
    TR Return(TX ctx);
    TR Structure(TX ctx, IReadOnlyList<TD> members);
    TR Member(TX ctx);
}

public interface IDeclaration<TD>
{
    TR Evaluate<TX, TR>(IDeclarationSemantic<TX, TD, TR> semantic, TX context);
}

public sealed record class ShaderDeclaration(
    string? Name,
    IShaderType Type,
    IDeclaration<IShaderSymbol<ShaderDeclaration>> Declaration,
    IReadOnlyList<IShaderAttribute> Attributes
)
{
}

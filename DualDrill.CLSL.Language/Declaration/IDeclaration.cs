using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Declaration;

[JsonDerivedType(typeof(FunctionDeclaration), nameof(FunctionDeclaration))]
[JsonDerivedType(typeof(ParameterDeclaration), nameof(ParameterDeclaration))]
[JsonDerivedType(typeof(StructureDeclaration), nameof(StructureDeclaration))]
[JsonDerivedType(typeof(MemberDeclaration), nameof(MemberDeclaration))]
[JsonDerivedType(typeof(VariableDeclaration), nameof(VariableDeclaration))]
[JsonDerivedType(typeof(ValueDeclaration), nameof(ValueDeclaration))]
//[JsonDerivedType(typeof(ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>>),
//    nameof(ShaderModuleDeclaration<IUnifiedFunctionBody<StackInstructionBasicBlock>>))]
//[JsonDerivedType(typeof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>),
//    nameof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>))]
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
    TR Val(TX ctx);
    TR Var(TX ctx);
    TR Function(TX ctx, IReadOnlyList<TD> parameters, TD ret);
    TR Parameter(TX ctx, TD function, int index);
    TR Return(TX ctx, TD function);
    TR Struct(TX ctx, IReadOnlyList<TD> members);
    TR Member(TX ctx, TD structure, int index);
    TR Module(TX ctx, IReadOnlyList<TD> values, IReadOnlyList<TD> variables, IReadOnlyList<TD> structures, IReadOnlyList<TD> functions);
}



public interface IDeclaration<out TD>
{
    TR Evaluate<TX, TR>(IDeclarationSemantic<TX, TD, TR> semantic, TX context);
}

public sealed record class ValDecl<T>() : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Val(context);
}

public sealed record class VarDecl<T>() : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Var(context);
}

public sealed record class FunctionDecl<T>(IReadOnlyList<T> Parameters, T Return) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Function(context, Parameters, Return);
}

public sealed record class ParameterDecl<T>(T Function, int Index) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Parameter(context, Function, Index);
}
public sealed record class ReturnDecl<T>(T Function) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Return(context, Function);
}

public sealed record class StructDecl<T>(IReadOnlyList<T> Members) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Struct(context, Members);
}

public sealed record class MemberDecl<T>(T Structure, int Index) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Member(context, Structure, Index);
}

public sealed record class ModuleDecl<T>(IReadOnlyList<T> Values, IReadOnlyList<T> Variables, IReadOnlyList<T> Structures, IReadOnlyList<T> Functions) : IDeclaration<T>
{
    public TR Evaluate<TX, TR>(IDeclarationSemantic<TX, T, TR> semantic, TX context)
        => semantic.Module(context, Values, Variables, Structures, Functions);
}

public sealed record class DeclarationData<T>(
    string? Name,
    IShaderSymbol<IShaderType> Type,
    IDeclaration<T> Declaration,
    IReadOnlyList<IShaderAttribute> Attributes
)
{
}

public sealed record class ShaderDecl(
    string? Name,
    IShaderSymbol<IShaderType> Type,
    IDeclaration<IShaderSymbol> Declaration,
    IReadOnlyList<IShaderAttribute> Attributes
)
{
    public TR Evaluate<TR>(IDeclarationSemantic<ShaderDecl, IShaderSymbol, TR> semantic)
        => Declaration.Evaluate(semantic, this);
}

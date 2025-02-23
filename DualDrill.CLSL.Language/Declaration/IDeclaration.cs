using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Declaration;

[JsonDerivedType(typeof(FunctionDeclaration), nameof(FunctionDeclaration))]
[JsonDerivedType(typeof(ParameterDeclaration), nameof(ParameterDeclaration))]
[JsonDerivedType(typeof(StructureDeclaration), nameof(StructureDeclaration))]
[JsonDerivedType(typeof(MemberDeclaration), nameof(MemberDeclaration))]
[JsonDerivedType(typeof(VariableDeclaration), nameof(VariableDeclaration))]
[JsonDerivedType(typeof(ValueDeclaration), nameof(ValueDeclaration))]
[JsonDerivedType(typeof(ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>),
    nameof(ShaderModuleDeclaration<StructuredStackInstructionFunctionBody>))]
[JsonDerivedType(typeof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>),
    nameof(ShaderModuleDeclaration<FunctionBody<CompoundStatement>>))]
public interface IDeclaration : IShaderAstNode
{
    string Name { get; }
    ImmutableHashSet<IShaderAttribute> Attributes { get; }
}
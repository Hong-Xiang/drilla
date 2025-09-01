using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Compiler;

public sealed class ParameterWithSemanticBindingToModuleVariablePass
    : IDeclarationSemantic<IDeclaration>
{
    Dictionary<FunctionDeclaration, FunctionDeclaration> FunctionUpdates { get; } = [];
    Dictionary<FunctionDeclaration, FunctionBody4> TransformedBody { get; } = [];
    public IDeclaration VisitFunction(FunctionDeclaration decl, FunctionBody4? body)
    {
        var transformed = false;
        FunctionReturn ret = decl.Return;

        List<ParameterDeclaration> parameters = new(decl.Parameters.Length);
        foreach (var p in decl.Parameters)
        {
            if (IsBindingParameter(p))
            {
            }
            else
            {
                parameters.Add(p);
            }
        }
        if (decl.Return.Attributes.SingleOrDefault(a => a is LocationAttribute) is IShaderAttribute a)
        {
            transformed = true;
            var vd = new VariableDeclaration(DeclarationScope.Module,
                string.Empty,
                decl.Return.Type,
                [a]
            );
        }
        if (transformed)
        {
            return new FunctionDeclaration(
                decl.Name,
                [.. parameters],
                ret,
                decl.Attributes
            );
        }
        else
        {
            return decl;
        }
    }

    bool IsBindingParameter(ParameterDeclaration p)
    {
        return false;
    }

    FunctionBody4 TransformFunctionBody(FunctionDeclaration decl, FunctionBody4 body)
    {
        throw new NotImplementedException();
    }

    public IDeclaration VisitMember(MemberDeclaration decl)
        => decl;

    public IDeclaration VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        var decls = decl.Declarations.Select(d => d.Evaluate(this)).ToImmutableArray();
        var functionDefs = new Dictionary<FunctionDeclaration, FunctionBody4>();
        foreach (var kv in decl.FunctionDefinitions)
        {
            if (FunctionUpdates.TryGetValue(kv.Key, out var uf))
            {
                functionDefs.Add(uf, decl.FunctionDefinitions[kv.Key]);
            }
            else
            {
                functionDefs.Add(kv.Key, kv.Value);
            }
        }
        return new ShaderModuleDeclaration<FunctionBody4>(
            decls,
            functionDefs.ToImmutableDictionary()
        );
    }

    public IDeclaration VisitParameter(ParameterDeclaration decl)
        => decl;

    public IDeclaration VisitStructure(StructureDeclaration decl)
        => decl;

    public IDeclaration VisitVariable(VariableDeclaration decl)
        => decl;
}

using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Transform;

public sealed class ParameterWithSemanticBindingToModuleVariablePass
    : IShaderModuleSimplePass
{
    Dictionary<FunctionDeclaration, FunctionDeclaration> FunctionUpdates { get; } = [];
    Dictionary<FunctionDeclaration, (Dictionary<IShaderValue, IShaderValue> ValueMap, IShaderValue ResultValue)> TransformedBodyData { get; } = [];
    public IDeclaration VisitFunction(FunctionDeclaration decl)
    {
        if (!decl.Attributes.Any(a => a is IShaderStageAttribute))
        {
            return decl;
        }
        Dictionary<IShaderValue, IShaderValue> updatedValues = [];
        foreach (var p in decl.Parameters)
        {
            var v = new VariableDeclaration(
                InputAddressSpace.Instance,
                p.Name,
                p.Type,
                p.Attributes
            );
            updatedValues.Add(p.Value, v.Value);
        }
        var resultVar = new VariableDeclaration(
            OutputAddressSpace.Instance,
            $"{decl.Name}_result",
            decl.Return.Type,
            decl.Return.Attributes
        );



        var resultDecl = new FunctionDeclaration(
                decl.Name,
                [],
                new FunctionReturn(ShaderType.Unit, []),
                decl.Attributes
            );

        FunctionUpdates.Add(decl, resultDecl);
        TransformedBodyData.Add(decl, (updatedValues, resultVar.Value));
        return resultDecl;
    }

    public IDeclaration VisitMember(MemberDeclaration decl)
        => decl;

    public IDeclaration VisitParameter(ParameterDeclaration decl)
        => decl;

    public IDeclaration VisitStructure(StructureDeclaration decl)
        => decl;

    public IDeclaration VisitVariable(VariableDeclaration decl)
        => decl;

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        if (FunctionUpdates.TryGetValue(body.Declaration, out var fu))
        {
            var (valueUpdates, resultValue) = TransformedBodyData[body.Declaration];
            body = body.MapValueUse(v => valueUpdates.TryGetValue(v, out var r) ? r : v);
            body = new FunctionBody4(
                fu,
                body.Body
            );
            return body.MapRegionBody(body =>
            {
                var t = body.Body.Last;
                if (t is Terminator.D.ReturnExpr<RegionJump, IShaderValue> rv)
                {
                    var setResultStatement = ShaderModule.StatementFactory.Set(resultValue, rv.Expr);
                    return ShaderRegionBody.Create(
                        body.Label,
                        body.Parameters,
                        [.. body.Body.Elements, setResultStatement],
                        Terminator.B.ReturnVoid<RegionJump, IShaderValue>(),
                        body.ImmediatePostDominator
                    );
                }
                else
                {
                    return body;
                }
            });
        }
        else
        {
            return body;
        }
    }

    public IDeclaration? VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }
}

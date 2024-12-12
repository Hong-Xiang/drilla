using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using System.Numerics;

namespace DualDrill.ILSL;

class SimpleWGSLOutputVisitor(TextWriter Writer) : CSharpOutputVisitor(Writer, FormattingOptionsFactory.CreateMono())
{
    static Dictionary<string, string> KnownTypes = new()
    {
        [typeof(VertexAttribute).FullName] = "vertex",
        [typeof(FragmentAttribute).FullName] = "fragment",
        [typeof(LocationAttribute).FullName] = "location",
        [typeof(BuiltinAttribute).FullName] = "builtin",
        [typeof(Vector4).FullName] = "vec4<f32>",
        [typeof(uint).FullName] = "u32",
        [typeof(ShaderMethodAttribute).FullName] = "",
    };
    public override void VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        StartNode(namespaceDeclaration);

        foreach (var member in namespaceDeclaration.Members)
        {
            member.AcceptVisitor(this);
        }

        EndNode(namespaceDeclaration);
    }

    public override void VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        if (propertyDeclaration.Name.StartsWith("ILSLWGSLExpectedCode"))
        {
            return;
        }
        base.VisitPropertyDeclaration(propertyDeclaration);
    }



    public override void VisitAttribute(ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
    {
        StartNode(attribute);
        attribute.Type.AcceptVisitor(this);
        if (attribute.Arguments.Count != 0 || attribute.HasArgumentList)
        {
            Space(policy.SpaceBeforeMethodCallParentheses);
            WriteCommaSeparatedListInParenthesis(attribute.Arguments, policy.SpaceWithinMethodCallParentheses);
        }
        EndNode(attribute);
    }

    public override void VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
    {
        var variableInitializer = variableDeclarationStatement.Variables.Single();

        WriteKeyword("var");
        Space();
        StartNode(variableDeclarationStatement);
        WriteIdentifier(variableInitializer.Name);
        WriteToken(Roles.Colon);
        //WriteModifiers(variableDeclarationStatement.GetChildrenByRole(VariableDeclarationStatement.ModifierRole));
        variableDeclarationStatement.Type.AcceptVisitor(this);
        Space();
        variableInitializer.AcceptVisitor(this);
        Semicolon();
        EndNode(variableDeclarationStatement);
    }

    public override void VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        StartNode(variableInitializer);
        //WriteIdentifier(variableInitializer.NameToken);
        if (!variableInitializer.Initializer.IsNull && !(variableInitializer.Initializer is DefaultValueExpression))
        {
            Space(policy.SpaceAroundAssignment);
            WriteToken(Roles.Assign);
            Space(policy.SpaceAroundAssignment);
            variableInitializer.Initializer.AcceptVisitor(this);
        }
        EndNode(variableInitializer);
    }

    public override void VisitCastExpression(CastExpression castExpression)
    {
        StartNode(castExpression);

        castExpression.Type.AcceptVisitor(this);
        LPar();
        Space(policy.SpacesWithinCastParentheses);
        castExpression.Expression.AcceptVisitor(this);
        Space(policy.SpacesWithinCastParentheses);
        RPar();
        Space(policy.SpaceAfterTypecast);
        EndNode(castExpression);
    }

    public override void VisitTypeDeclaration(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.BaseTypes.Any(b => b.Annotation<TypeResolveResult>() is TypeResolveResult
            {
                Type: { FullName: var fn }
            } && fn == typeof(ISharpShader).FullName))
        {
            typeDeclaration.Members.AcceptVisitor(this);
        }
        else
        {
            base.VisitTypeDeclaration(typeDeclaration);
        }
    }

    public override void VisitDeclarationExpression(DeclarationExpression declarationExpression)
    {
        base.VisitDeclarationExpression(declarationExpression);
    }

    public override void VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
    {

        if (memberReferenceExpression.Target is TypeReferenceExpression te)
        {
            if (te.Type.Annotation<TypeResolveResult>() is TypeResolveResult { Type: var r })
            {
                if (r.FullName == typeof(BuiltinBinding).FullName)
                {
                    StartNode(memberReferenceExpression);
                    //memberReferenceExpression.Target.AcceptVisitor(this);
                    //bool insertedNewLine = InsertNewLineWhenInMethodCallChain(memberReferenceExpression);
                    //WriteToken(Roles.Dot);

                    WriteIdentifier(memberReferenceExpression.MemberNameToken switch
                    {
                        { Name: nameof(BuiltinBinding.position) } => "position",
                        { Name: nameof(BuiltinBinding.vertex_index) } => "vertex_index",
                        _ => memberReferenceExpression.MemberNameToken.Name,
                    });
                    //WriteTypeArguments(memberReferenceExpression.TypeArguments);
                    //if (insertedNewLine && !(memberReferenceExpression.Parent is InvocationExpression))
                    //{
                    //    writer.Unindent();
                    //}
                    EndNode(memberReferenceExpression);
                    return;
                }
            }
        }

        base.VisitMemberReferenceExpression(memberReferenceExpression);
    }



    public override void VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
    {
        base.VisitTypeReferenceExpression(typeReferenceExpression);
    }

    private bool IsReturnAttribute(AttributeSection attr)
    {
        return attr is { AttributeTarget: "return" };
    }

    public override void VisitMethodDeclaration(MethodDeclaration methodDeclaration)
    {
        StartNode(methodDeclaration);
        WriteAttributes(methodDeclaration.Attributes.Where(a => !IsReturnAttribute(a)));
        //WriteModifiers(methodDeclaration.ModifierTokens);
        WriteKeyword("fn");
        Space();
        WritePrivateImplementationType(methodDeclaration.PrivateImplementationType);
        WriteIdentifier(methodDeclaration.NameToken);
        WriteTypeParameters(methodDeclaration.TypeParameters);
        Space(policy.SpaceBeforeMethodDeclarationParentheses);
        WriteCommaSeparatedListInParenthesis(methodDeclaration.Parameters, policy.SpaceWithinMethodDeclarationParentheses);
        //foreach (Constraint constraint in methodDeclaration.Constraints)
        //{
        //    constraint.AcceptVisitor(this);
        //}
        Space();
        WriteToken(new TokenRole("->"));
        WriteAttributes(methodDeclaration.Attributes.Where(IsReturnAttribute));
        Space();
        methodDeclaration.ReturnType.AcceptVisitor(this);
        WriteMethodBody(methodDeclaration.Body, policy.MethodBraceStyle);
        EndNode(methodDeclaration);
    }

    public override void VisitParameterDeclaration(ParameterDeclaration parameterDeclaration)
    {
        StartNode(parameterDeclaration);
        WriteAttributes(parameterDeclaration.Attributes);
        if (parameterDeclaration.HasThisModifier)
        {
            WriteKeyword(ParameterDeclaration.ThisModifierRole);
            Space();
        }
        if (parameterDeclaration.IsScopedRef)
        {
            WriteKeyword(ParameterDeclaration.ScopedRefRole);
            Space();
        }
        switch (parameterDeclaration.ParameterModifier)
        {
            case ParameterModifier.Ref:
                WriteKeyword(ParameterDeclaration.RefModifierRole);
                Space();
                break;
            case ParameterModifier.Out:
                WriteKeyword(ParameterDeclaration.OutModifierRole);
                Space();
                break;
            case ParameterModifier.Params:
                WriteKeyword(ParameterDeclaration.ParamsModifierRole);
                Space();
                break;
            case ParameterModifier.In:
                WriteKeyword(ParameterDeclaration.InModifierRole);
                Space();
                break;
        }
        if (!parameterDeclaration.Type.IsNull && !string.IsNullOrEmpty(parameterDeclaration.Name))
        {
            Space();
        }
        if (!string.IsNullOrEmpty(parameterDeclaration.Name))
        {
            WriteIdentifier(parameterDeclaration.NameToken);
        }
        WriteToken(Roles.Colon);
        parameterDeclaration.Type.AcceptVisitor(this);
        if (parameterDeclaration.HasNullCheck)
        {
            WriteToken(Roles.DoubleExclamation);
        }
        if (!parameterDeclaration.DefaultExpression.IsNull)
        {
            Space(policy.SpaceAroundAssignment);
            WriteToken(Roles.Assign);
            Space(policy.SpaceAroundAssignment);
            parameterDeclaration.DefaultExpression.AcceptVisitor(this);
        }
        EndNode(parameterDeclaration);
    }

    public override void VisitPrimitiveType(PrimitiveType primitiveType)
    {
        StartNode(primitiveType);
        writer.WritePrimitiveType(primitiveType.Keyword switch
        {
            "uint" => "u32",
            "int" => "i32",
            "float" => "f32",
            _ => primitiveType.Keyword,
        });
        isAfterSpace = false;
        EndNode(primitiveType);
    }


    public override void VisitMemberType(MemberType memberType)
    {
        if (memberType.Annotation<TypeResolveResult>() is TypeResolveResult t)
        {
            if (t?.Type.FullName is string fn && KnownTypes.TryGetValue(fn, out var kn))
            {
                StartNode(memberType);
                WriteIdentifier(kn);
                EndNode(memberType);
                return;
            }
        }
        StartNode(memberType);
        memberType.Target.AcceptVisitor(this);
        if (memberType.IsDoubleColon)
        {
            WriteToken(new("__"));
        }
        else
        {
            WriteToken(new("_"));
        }
        WriteIdentifier(memberType.MemberNameToken);
        WriteTypeArguments(memberType.TypeArguments);
        EndNode(memberType);
    }

    public override void VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
    {
        StartNode(objectCreateExpression);
        objectCreateExpression.Type.AcceptVisitor(this);
        bool useParenthesis = objectCreateExpression.Arguments.Any() || objectCreateExpression.Initializer.IsNull;
        // also use parenthesis if there is an '(' token
        if (!objectCreateExpression.LParToken.IsNull)
        {
            useParenthesis = true;
        }
        if (useParenthesis)
        {
            Space(policy.SpaceBeforeMethodCallParentheses);
            WriteCommaSeparatedListInParenthesis(objectCreateExpression.Arguments, policy.SpaceWithinMethodCallParentheses);
        }
        objectCreateExpression.Initializer.AcceptVisitor(this);
        EndNode(objectCreateExpression);
    }

    public override void VisitAttributeSection(AttributeSection attributeSection)
    {
        StartNode(attributeSection);
        WriteToken(new TokenRole("@"));
        //if (!string.IsNullOrEmpty(attributeSection.AttributeTarget))
        //{
        //    WriteKeyword(attributeSection.AttributeTarget, Roles.Identifier);
        //    WriteToken(Roles.Colon);
        //    Space();
        //}
        WriteCommaSeparatedList(attributeSection.Attributes);
        switch (attributeSection.Parent)
        {
            case ParameterDeclaration _:
                if (attributeSection.NextSibling is AttributeSection)
                    Space(policy.SpaceBetweenParameterAttributeSections);
                else
                    Space();
                break;
            case TypeParameterDeclaration _:
            case ComposedType _:
            case LambdaExpression _:
                Space();
                break;
            default:
                NewLine();
                break;
        }
        EndNode(attributeSection);
    }
}

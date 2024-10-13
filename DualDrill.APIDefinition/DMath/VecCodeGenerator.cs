using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace DualDrill.ApiGen.DMath;

public sealed record class VecCodeGenerator(DMathVectorType VecType, CodeNamespace Namespace, CodeTypeDeclaration DMath)
{
    CodeAttributeDeclaration aggressiveInliningAttribute = new CodeAttributeDeclaration(
      new CodeTypeReference(typeof(MethodImplAttribute)),
      new CodeAttributeArgument(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(MethodImplOptions)), "AggressiveInlining"))
    );



    CodeMemberMethod GeneratePrimaryConstructor(DMathVectorType vecType)
    {
        var primary = new CodeMemberMethod()
        {
            Name = $"vec{(int)vecType.Size}",
            Attributes = MemberAttributes.Public | MemberAttributes.Static,
            ReturnType = new CodeTypeReference(vecType.Name),
        };
        foreach (var m in vecType.Size.Components())
        {
            primary.Parameters.Add(new CodeParameterDeclarationExpression(
                vecType.ScalarType.MappedPrimitiveCSharpType(),
                m
            ));
        }
        var resultName = "result";
        primary.Statements.Add(
            new CodeVariableDeclarationStatement(
            new CodeTypeReference(vecType.Name),
            resultName,
            new CodeObjectCreateExpression(vecType.Name)
        ));
        foreach (var m in vecType.Size.Components())
        {
            primary.Statements.Add(
                new CodeAssignStatement(
                    new CodePropertyReferenceExpression(new CodeVariableReferenceExpression(resultName), m),
                    new CodeVariableReferenceExpression(m))
            );
        }
        primary.Statements.Add(new CodeMethodReturnStatement(
            new CodeVariableReferenceExpression(resultName)
        ));
        primary.CustomAttributes.Add(aggressiveInliningAttribute);
        return primary;
    }

    CodeMemberMethod GenerateBroadcastConstructor(DMathVectorType vecType)
    {
        var methodName = $"vec{(int)vecType.Size}";
        var method = new CodeMemberMethod()
        {
            Name = methodName,
            Attributes = MemberAttributes.Public | MemberAttributes.Static,
            ReturnType = new CodeTypeReference(vecType.Name),
        };
        method.Parameters.Add(new CodeParameterDeclarationExpression(
            vecType.ScalarType.MappedPrimitiveCSharpType(),
            "e"
        ));

        method.Statements.Add(new CodeMethodReturnStatement(
          new CodeMethodInvokeExpression(
              new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(DMath.Name), methodName),
              vecType.Size.Components().Select(n => new CodeVariableReferenceExpression("e")).ToArray())
        ));
        method.CustomAttributes.Add(aggressiveInliningAttribute);
        return method;
    }


    public IEnumerable<CodeMemberMethod> GenerateConstructors()
    {
        yield return GeneratePrimaryConstructor(VecType);
        yield return GenerateBroadcastConstructor(VecType);
        if (VecType.Size == Rank._3)
        {
            yield return GenerateVecParameterConstroctor(VecType, 1, 2);
            yield return GenerateVecParameterConstroctor(VecType, 2, 1);
        }
        if (VecType.Size == Rank._4)
        {
            yield return GenerateVecParameterConstroctor(VecType, 2, 1, 1);
            yield return GenerateVecParameterConstroctor(VecType, 1, 2, 1);
            yield return GenerateVecParameterConstroctor(VecType, 1, 1, 2);
            yield return GenerateVecParameterConstroctor(VecType, 2, 2);
            yield return GenerateVecParameterConstroctor(VecType, 1, 3);
            yield return GenerateVecParameterConstroctor(VecType, 3, 1);
        }
    }

    CodeMemberMethod GenerateVecParameterConstroctor(DMathVectorType vecType, params int[] sizes)
    {
        var methodName = $"vec{(int)vecType.Size}";
        var method = new CodeMemberMethod()
        {
            Name = methodName,
            Attributes = MemberAttributes.Public | MemberAttributes.Static,
            ReturnType = new CodeTypeReference(vecType.Name),
        };

        var idx = 0;
        foreach (var s in sizes)
        {
            var pname = $"e{idx}";
            if (s == 1)
            {
                method.Parameters.Add(new CodeParameterDeclarationExpression(
                     vecType.ScalarType.MappedPrimitiveCSharpType(), pname));
            }
            else
            {
                method.Parameters.Add(new CodeParameterDeclarationExpression(
                    new DMathVectorType(vecType.ScalarType, (Rank)s).Name, pname));
            }
            idx++;
        }

        var exprs = new List<CodeExpression>();
        idx = 0;
        foreach (var s in sizes)
        {
            var pname = $"e{idx}";
            if (s == 1)
            {
                exprs.Add(new CodeVariableReferenceExpression(pname));
            }
            else
            {
                foreach (var m in ((Rank)s).Components())
                {
                    exprs.Add(new CodeFieldReferenceExpression(new CodeVariableReferenceExpression(pname), m));
                }
            }
            idx++;
        }

        method.Statements.Add(new CodeMethodReturnStatement(
          new CodeMethodInvokeExpression(
              new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(DMath.Name), methodName),
              exprs.ToArray())
        ));
        method.CustomAttributes.Add(aggressiveInliningAttribute);
        return method;

    }
    public StructDeclarationSyntax GenerateDeclaration()
    {
        var result = StructDeclaration(VecType.CSharpName()).AddModifiers(
            Token(SyntaxKind.PartialKeyword),
            Token(SyntaxKind.PublicKeyword)
        );
        foreach (var m in VecType.Components)
        {
            var member = FieldDeclaration(
                VariableDeclaration(
                    ParseTypeName(VecType.ScalarType.MappedPrimitiveCSharpType().FullName)
                ).AddVariables(VariableDeclarator(m))
            ).AddModifiers(Token(SyntaxKind.PublicKeyword));
            //{
            //    Name = m,
            //    Type = new CodeTypeReference(VecType.ScalarType.MappedPrimitiveCSharpType()),
            //    Attributes = MemberAttributes.Public,
            //};
            result = result.AddMembers(member);
        }


        result = result.WithAttributeLists(
             SingletonList(
                AttributeList(SingletonSeparatedList(DMathCodeGen.StructLayoutAttribute))));
        return result;
    }

}

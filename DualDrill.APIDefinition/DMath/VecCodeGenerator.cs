using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ApiGen.DMath;

[Flags]
public enum VecFeatures
{
    StructDeclaration = 1,
    Swizzle = 2,
    Constructor = 4,
    ImplicitOperators = 8,
    ArithmeticOperators = 16,
}

public sealed record class VecCodeGenerator(VecFeatures Features, CodeNamespace Namespace, CodeTypeDeclaration DMath)
{
    public void Generate(CodeCompileUnit compileUnit, DMathVectorType vecType)
    {
        if (Features.HasFlag(VecFeatures.StructDeclaration))
        {
            Namespace.Types.Add(GenerateVecDecl(vecType));
        }
        if (Features.HasFlag(VecFeatures.Constructor))
        {
            GenerateConstructors(vecType);
        }
    }

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


    void GenerateConstructors(DMathVectorType vecType)
    {
        DMath.Members.Add(GeneratePrimaryConstructor(vecType));
        DMath.Members.Add(GenerateBroadcastConstructor(vecType));
        if (vecType.Size == Rank._3)
        {
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 1, 2));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 2, 1));
        }
        if (vecType.Size == Rank._4)
        {
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 2, 1, 1));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 1, 2, 1));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 1, 1, 2));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 2, 2));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 1, 3));
            DMath.Members.Add(GenerateVecParameterConstroctor(vecType, 3, 1));
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
                foreach(var m in ((Rank)s).Components())
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

    CodeTypeDeclaration GenerateVecDecl(DMathVectorType t)
    {
        var result = new CodeTypeDeclaration(t.Name)
        {
            IsStruct = true,
            TypeAttributes = System.Reflection.TypeAttributes.Public,
            IsPartial = true,
        };
        result.CustomAttributes.Add(
            new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(StructLayoutAttribute)),
                new CodeAttributeArgument(
                    new CodePropertyReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(LayoutKind)),
                        nameof(LayoutKind.Sequential)))
                ));
        foreach (var m in t.Components)
        {
            var member = new CodeMemberField()
            {
                Name = m,
                Type = new CodeTypeReference(t.ScalarType.MappedPrimitiveCSharpType()),
                Attributes = MemberAttributes.Public,
            };
            result.Members.Add(member);
        }
        return result;
    }

}

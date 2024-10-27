﻿using DualDrill.CLSL.Language.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Runtime.InteropServices;
namespace DualDrill.ApiGen.DMath;


[Flags]
public enum CodeGenFeatures
{
    StructDeclaration = 1 << 0,
    Swizzle = 1 << 1,
    Constructor = 1 << 2,
    Operators = 1 << 4,
}

public sealed class DMathCodeGen
{
    public static readonly string NameSpace = "DualDrill.Mathematics";
    public static readonly string StaticMathTypeName = "DMath";

    public static readonly AttributeSyntax AggressiveInlining = SyntaxFactory.Attribute(
                                                        SyntaxFactory.IdentifierName("MethodImpl"))
                                                    .WithArgumentList(
                                                        SyntaxFactory.AttributeArgumentList(
                                                            SyntaxFactory.SingletonSeparatedList<AttributeArgumentSyntax>(
                                                                SyntaxFactory.AttributeArgument(
                                                                    SyntaxFactory.MemberAccessExpression(
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.IdentifierName("MethodImplOptions"),
                                                                        SyntaxFactory.IdentifierName("AggressiveInlining"))))));
    public static readonly AttributeSyntax StructLayoutAttribute =
         //{
         //SyntaxFactory.Attribute(
         //    SyntaxFactory.IdentifierName("StructLayout"))
         //    .WithArgumentList(
         //        SyntaxFactory.AttributeArgumentList(
         //            SyntaxFactory.SingletonSeparatedList(
         //                SyntaxFactory.AttributeArgument(
         //                    SyntaxFactory.MemberAccessExpression(
         //                                           SyntaxKind.SimpleMemberAccessExpression,
         //                                           IdentifierName("LayoutKind"),
         //                                           IdentifierName("Sequential"))))))))),
         SyntaxFactory.Attribute(
            SyntaxFactory.ParseName(typeof(StructLayoutAttribute).FullName),
            SyntaxFactory.ParseAttributeArgumentList($"({typeof(LayoutKind).FullName}.Sequential)"));
    //}

    public DMathCodeGen()
    {
    }

    CodeTypeDeclaration StaticMathClassDecl()
    {
        return new CodeTypeDeclaration(StaticMathTypeName)
        {
            IsPartial = true,
            IsClass = true,
        };
    }

    void AddGeneratedCodeComment(TextWriter writer)
    {
        writer.WriteLine("//------------------------------------------------------------------------------");
        writer.WriteLine("// <auto-generated>");
        writer.WriteLine("//     This code was generated by a tool.");
        writer.WriteLine("//");
        writer.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        writer.WriteLine("//     the code is regenerated.");
        writer.WriteLine("// </auto-generated>");
        writer.WriteLine("//------------------------------------------------------------------------------");
    }

    public string Generate()
    {
        var tw = new StringWriter();
        var itw = new IndentedTextWriter(tw);

        AddGeneratedCodeComment(itw);
        itw.WriteLine("using System.Runtime.CompilerServices");
        itw.WriteLine("using System.Runtime.Intrinsics");
        itw.WriteLine($"namespace {NameSpace}");
        itw.WriteLine();

        var math = StaticMathClassDecl();


        foreach (var t in ShaderType.GetVecTypes())
        {
            var vecGenertor = new VecCodeGenerator(t, itw);
            //ns.Types.Add(vecGenertor.GenerateDeclaration());
            //foreach (var m in vecGenertor.GenerateConstructors())
            //{
            //    math.Members.Add(m);
            //}
            //ns.Types.Add(GenerateVecDecl(t));
            vecGenertor.GenerateDeclaration();
            vecGenertor.GenerateStaticMethods();
        }
        //cu = cu.AddMembers(nsr);
        //var formattedCode = Formatter.Format(cu.NormalizeWhitespace(), new AdhocWorkspace()).ToFullString();
        return tw.ToString();
    }
}

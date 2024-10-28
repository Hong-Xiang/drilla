using DualDrill.CLSL.Language.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;
using System.Diagnostics;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace DualDrill.ApiGen.DMath;

public sealed record class VecCodeGenerator(VecType VecType, IndentedTextWriter Writer) : ITextCodeGenerator
{
    TypeSyntax VecStructTypeSyntax { get; } = ParseTypeName(VecType.CSharpStructName());
    string ElementName { get; } = VecType.ElementType.ElementName();
    string ElementTypeName { get; } = VecType.ElementType.ScalarCSharpType().FullName;
    string StructTypeName { get; } = $"vec{VecType.Size.Value}{VecType.ElementType.ElementName()}";

    public void GenerateStaticMethods()
    {

        Writer.Write("public static partial class DMath");
        using (var _ = this.IndentedScopeWithBracket())
        {
            Writer.Write($"public static {StructTypeName} vec{VecType.Size.Value}");
        }

        var dmath = ClassDeclaration("DMath")
                    .WithModifiers(TokenList([Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword)]));
        // constructors
        var constructorMethod = MethodDeclaration(
                        VecStructTypeSyntax,
                        Identifier($"vec{VecType.Size.Value}"))
                        .AddMethodInline()
                        .WithModifiers(
                            TokenList([Token(SyntaxKind.PublicKeyword),
                                      Token(SyntaxKind.StaticKeyword)]));
        {
            // primary constructor
            var method = constructorMethod.AddParameterListParameters(
                [..VecType.Size.Components()
                .Select(m => Parameter(Identifier(m)).WithType(ElementCSharpTypeSyntax))]
            );
            if (IsSIMDOptimized)
            {
                List<ArgumentSyntax> dataArguments = [];
                foreach (var m in VecType.Size.Components())
                {
                    dataArguments.Add(Argument(IdentifierName(m)));
                }
                if (VecType.Size.Value == 3)
                {
                }

                var body = ImplicitObjectCreationExpression()
                            .WithInitializer(
                                InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SingletonSeparatedList<ExpressionSyntax>(
                                        AssignmentExpression(
                                            SyntaxKind.SimpleAssignmentExpression,
                                            IdentifierName("Data"),
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("Vector64"),
                                                    IdentifierName("Create")))
                                            .WithArgumentList(
                                                                ArgumentList(
                                                                    SeparatedList<ArgumentSyntax>(
                                                                        new SyntaxNodeOrToken[]{
                                                            Argument(
                                                                IdentifierName("x")),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("y"))})))))));
                method = method.WithArrowExpressionBody(body);

            }
            else
            {
                List<AssignmentExpressionSyntax> assenments = [];
                foreach (var m in VecType.Size.Components())
                {
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(m),
                        IdentifierName(m));
                }

                var body = ImplicitObjectCreationExpression()
                                           .WithInitializer(
                                               InitializerExpression(
                                                   SyntaxKind.ObjectInitializerExpression,
                                                  SeparatedList<ExpressionSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                IdentifierName("x"),
                                                IdentifierName("x")),
                                            Token(SyntaxKind.CommaToken),
                                            AssignmentExpression(
                                                SyntaxKind.SimpleAssignmentExpression,
                                                IdentifierName("y"),
                                                IdentifierName("y"))})));
                method = method.WithArrowExpressionBody(body);

            }
            dmath = dmath.AddMembers(method);
        }

        dmath = dmath.WithMembers(
            List<MemberDeclarationSyntax>(
                [
                    MethodDeclaration(
                        VecStructTypeSyntax,
                        Identifier($"vec{VecType.Size.Value}"))
                        .AddMethodInline()
                        .WithModifiers(
                            TokenList([Token(SyntaxKind.PublicKeyword),
                                      Token(SyntaxKind.StaticKeyword)]))
                    .WithParameterList(
                        ParameterList(
                            SeparatedList<ParameterSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Parameter(
                                        Identifier("x"))
                                    .WithType(
                                        PredefinedType(
                                            Token(SyntaxKind.IntKeyword))),
                                    Token(SyntaxKind.CommaToken),
                                    Parameter(
                                        Identifier("y"))
                                    .WithType(
                                        PredefinedType(
                                            Token(SyntaxKind.IntKeyword)))})))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            ImplicitObjectCreationExpression()
                            .WithInitializer(
                                InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SingletonSeparatedList<ExpressionSyntax>(
                                        AssignmentExpression(
                                            SyntaxKind.SimpleAssignmentExpression,
                                            IdentifierName("Data"),
                                            InvocationExpression(
                                                MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    IdentifierName("Vector64"),
                                                    IdentifierName("Create")))
                                            .WithArgumentList(
                                                ArgumentList(
                                                    SeparatedList<ArgumentSyntax>(
                                                        new SyntaxNodeOrToken[]{
                                                            Argument(
                                                                IdentifierName("x")),
                                                            Token(SyntaxKind.CommaToken),
                                                            Argument(
                                                                IdentifierName("y"))})))))))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)),
                    MethodDeclaration(
                        IdentifierName("vec2i32"),
                        Identifier("vec2"))
                    .WithAttributeLists(
                        SingletonList<AttributeListSyntax>(
                            AttributeList(
                                SingletonSeparatedList<AttributeSyntax>(
                                    Attribute(
                                        IdentifierName("MethodImpl"))
                                    .WithArgumentList(
                                        AttributeArgumentList(
                                            SingletonSeparatedList<AttributeArgumentSyntax>(
                                                AttributeArgument(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("MethodImplOptions"),
                                                        IdentifierName("AggressiveInlining"))))))))))
                    .WithModifiers(
                        TokenList(
                            new []{
                                Token(SyntaxKind.PublicKeyword),
                                Token(SyntaxKind.StaticKeyword)}))
                    .WithParameterList(
                        ParameterList(
                            SingletonSeparatedList<ParameterSyntax>(
                                Parameter(
                                    Identifier("e"))
                                .WithType(
                                    PredefinedType(
                                        Token(SyntaxKind.IntKeyword))))))
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            InvocationExpression(
                                IdentifierName("vec2"))
                            .WithArgumentList(
                                ArgumentList(
                                    SeparatedList<ArgumentSyntax>(
                                        new SyntaxNodeOrToken[]{
                                            Argument(
                                                IdentifierName("e")),
                                            Token(SyntaxKind.CommaToken),
                                            Argument(
                                                IdentifierName("e"))})))))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken))]));

        return dmath;
    }

    public void GenerateDeclaration()
    {
        Writer.Write("public partial struct vec");
        Writer.Write(VecType.Size.Value);
        Writer.Write(VecType.ElementType.ElementName());
        Writer.WriteLine(" {");
        Writer.Indent++;
        DataMembersDeclaration();
        Writer.Indent--;
        Writer.WriteLine("}");
        Writer.WriteLine();
    }
    // for numeric vectors with data larger than 64 bits (except System.Half, which is not supported in VectorXX<Half>),
    // we use .NET builtin SIMD optimization
    // for vec3, we use vec4's data for optimizing memory access and SIMD optimization
    bool IsSIMDOptimized { get; } =
        !VecType.ElementType.Equals(ShaderType.Bool)
        && (!VecType.ElementType.Equals(ShaderType.F16))
        && ((VecType.Size.Value == 3 ? 4 : VecType.Size.Value) * VecType.ElementType.ByteSize >= 8);

    TypeSyntax ElementCSharpTypeSyntax { get; } = ParseTypeName(VecType.ElementType.ScalarCSharpType().FullName);

    int SIMDDataBitWidth => (VecType.Size.Value == 3 ? 4 : VecType.Size.Value) * VecType.ElementType.ByteSize * 8;

    TypeSyntax SIMDDataType()
    {
        Debug.Assert(IsSIMDOptimized);
        return ParseTypeName($"System.Runtime.Intrinsics.Vector{SIMDDataBitWidth}<{VecType.ElementType.ScalarCSharpType().FullName}>");
    }

    void DataMembersDeclaration()
    {
        if (IsSIMDOptimized)
        {
            Writer.WriteLine($"internal Vector{SIMDDataBitWidth}<{ElementTypeName}> Data;");
            Writer.WriteLine();

            foreach (var (m, i) in VecType.Size.Components().Select((x, i) => (x, i)))
            {
                Writer.Write($"public {ElementTypeName} {m}");
                Writer.WriteLine(" {");
                Writer.Indent++;

                //getter
                Writer.WriteMethodInline();
                Writer.WriteLine($"get => Data[{i}];");
                Writer.WriteLine();

                Writer.WriteMethodInline();
                Writer.WriteLine("set {");
                Writer.Indent++;
                Writer.Write($"Data = Vector{SIMDDataBitWidth}.Create(");

                var argumentCount = VecType.Size.Value == 3 ? 4 : VecType.Size.Value;
                for (var ia = 0; ia < argumentCount; ia++)
                {
                    if (ia == i)
                    {
                        Writer.Write("value");
                    }
                    else
                    {
                        Writer.Write($"Data[{ia}]");
                    }
                    if (ia < argumentCount - 1)
                    {
                        Writer.Write(", ");
                    }
                }
                Writer.WriteLine(");");


                Writer.Indent--;
                Writer.WriteLine("}");
                Writer.WriteLine();



                Writer.Indent--;
                Writer.WriteLine("}");
                Writer.WriteLine();
            }
        }
        else
        {
            foreach (var m in VecType.Size.Components())
            {
                Writer.WriteLine($"public {ElementTypeName} {m};");
            }
        }
    }
}

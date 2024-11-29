using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.IR;
using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace DualDrill.ILSL.Frontend;

public class ILSpyMethodParseException(string message) : Exception(message)
{
}

public class ILSpyMethodParseInvocationMethodNotResolvedException()
{
}

public sealed record class ILSpyMethodBodyToCLSLNodeAstVisitor(MethodParseContext Context, Assembly Assembly) : IAstVisitor<IShaderAstNode?>
{
    public IShaderAstNode? VisitAccessor(Accessor accessor)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitAnonymousTypeCreateExpression(AnonymousTypeCreateExpression anonymousTypeCreateExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitArraySpecifier(ArraySpecifier arraySpecifier)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitAsExpression(AsExpression asExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitAttribute(ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
    {
        var a = attribute.Annotation<MemberResolveResult>().Type;
        if (a.FullName == typeof(BuiltinAttribute).FullName)
        {
            return new BuiltinAttribute(Enum.Parse<BuiltinBinding>(attribute.Children.OfType<MemberReferenceExpression>().Single().MemberName));
        }
        if (a.FullName == typeof(VertexAttribute).FullName)
        {
            return new VertexAttribute();
        }
        if (a.FullName == typeof(FragmentAttribute).FullName)
        {
            return new FragmentAttribute();
        }
        if (a.FullName == typeof(LocationAttribute).FullName)
        {
            return new LocationAttribute(0);
        }
        if (a.FullName == typeof(ShaderMethodAttribute).FullName)
        {
            return new ShaderMethodAttribute();
        }
        return null;
    }

    public IShaderAstNode? VisitAttributeSection(AttributeSection attributeSection)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        var l = (IExpression)binaryOperatorExpression.Left.AcceptVisitor(this);
        var r = (IExpression)binaryOperatorExpression.Right.AcceptVisitor(this);
        // TODO: proper expression type handling
        if (l is LiteralValueExpression { Literal: IntLiteral { Value: var v, BitWidth: var bw } }
            && r is FormalParameterExpression { Parameter: { Type: UIntType { BitWidth: N32 } } })
        {
            l = new LiteralValueExpression(SyntaxFactory.Literal((uint)v));
        }
        return binaryOperatorExpression.Operator switch
        {
            BinaryOperatorType.Add => new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Addition),
            BinaryOperatorType.Subtract => new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Subtraction),
            BinaryOperatorType.Multiply => new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Multiplication),
            BinaryOperatorType.Divide => new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Division),
            BinaryOperatorType.Modulus => new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Remainder),
            BinaryOperatorType.BitwiseAnd => new BinaryBitwiseExpression(l, r, BinaryBitwiseOp.BitwiseAnd),
            BinaryOperatorType.BitwiseOr => new BinaryBitwiseExpression(l, r, BinaryBitwiseOp.BitwiseOr),
            BinaryOperatorType.ExclusiveOr => new BinaryBitwiseExpression(l, r, BinaryBitwiseOp.BitwiseExclusiveOr),
            BinaryOperatorType.LessThan => new BinaryRelationalExpression(l, r, BinaryRelationalOp.LessThan),
            BinaryOperatorType.GreaterThan => new BinaryRelationalExpression(l, r, BinaryRelationalOp.GreaterThan),
            BinaryOperatorType.LessThanOrEqual => new BinaryRelationalExpression(l, r, BinaryRelationalOp.LessThanEqual),
            BinaryOperatorType.GreaterThanOrEqual => new BinaryRelationalExpression(l, r, BinaryRelationalOp.GreaterThanEqual),
            BinaryOperatorType.Equality => new BinaryRelationalExpression(l, r, BinaryRelationalOp.Equal),
            BinaryOperatorType.InEquality => new BinaryRelationalExpression(l, r, BinaryRelationalOp.NotEqual),
            BinaryOperatorType.ConditionalAnd => new BinaryLogicalExpression(l, r, BinaryLogicalOp.And),
            BinaryOperatorType.ConditionalOr => new BinaryLogicalExpression(l, r, BinaryLogicalOp.Or),
            _ => throw new NotSupportedException($"{nameof(VisitBinaryOperatorExpression)} does not support {binaryOperatorExpression}")
        };
    }

    public IShaderAstNode? VisitBlockStatement(BlockStatement blockStatement)
    {
        var result = new CompoundStatement([.. blockStatement.Statements.Select(s => s.AcceptVisitor(this)).OfType<IStatement>()]);
        return result;
    }

    public IShaderAstNode? VisitBreakStatement(ICSharpCode.Decompiler.CSharp.Syntax.BreakStatement breakStatement)
        => SyntaxFactory.Break();

    public IShaderAstNode? VisitCaseLabel(CaseLabel caseLabel)
    {
        throw new NotImplementedException();
    }

    Type? GetCSharpType(IType t)
    {
        return Type.GetType(t.FullName);
    }

    public IShaderAstNode? VisitCastExpression(CastExpression castExpression)
    {
        throw new NotImplementedException();
        //var source = castExpression.Expression.Annotation<ResolveResult>().Type;
        //var target = castExpression.Type.Annotation<TypeResolveResult>().Type;
        //var sourceType = GetCSharpType(source);
        //var targetType = GetCSharpType(target);

        //var f = target.FullName switch
        //{
        //    "System.Single" => FloatType<N32>.Cast,
        //    { FullName: "System.Double" } => FloatType<N64>.Cast,
        //    { Type.FullName: "System.Int32" } => IntType<N32>.Cast,
        //    { Type.FullName: "System.Int64" } => IntType<N64>.Cast,
        //    //{ Type: { FullName: "System.UInt32" } } => UIntType<N32>.Cast,
        //    //{ Type: { FullName: "System.UInt64" } } => UIntType<N64>.Cast,
        //    _ => throw new NotSupportedException()
        //};
        //return new FunctionCallExpression(f, [(IExpression)castExpression.Expression.AcceptVisitor(this)]);
    }

    public IShaderAstNode? VisitCatchClause(CatchClause catchClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitCheckedExpression(CheckedExpression checkedExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitCheckedStatement(CheckedStatement checkedStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitComment(Comment comment)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitComposedType(ComposedType composedType)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitConditionalExpression(ConditionalExpression conditionalExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitConstraint(Constraint constraint)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitConstructorInitializer(ConstructorInitializer constructorInitializer)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitContinueStatement(ContinueStatement continueStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitCSharpTokenNode(CSharpTokenNode cSharpTokenNode)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDeclarationExpression(DeclarationExpression declarationExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDirectionExpression(DirectionExpression directionExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDocumentationReference(DocumentationReference documentationReference)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitDoWhileStatement(DoWhileStatement doWhileStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitEmptyStatement(EmptyStatement emptyStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitErrorNode(AstNode errorNode)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitExpressionStatement(ExpressionStatement expressionStatement)
    {
        var expr = expressionStatement.Expression;
        return expr switch
        {
            AssignmentExpression assignment => UnwrapConditionalAssignment(
                (IExpression)assignment.Left.AcceptVisitor(this)!,
                assignment.Right,
                MapAssignmentOperator(assignment.Operator)
            ),
            UnaryOperatorExpression { Operator: UnaryOperatorType.PostIncrement } unary => new IncrementStatement(
                (IExpression)unary.Expression.AcceptVisitor(this)!
            ),
            UnaryOperatorExpression { Operator: UnaryOperatorType.PostDecrement } unary => new DecrementStatement(
                (IExpression)unary.Expression.AcceptVisitor(this)!
            ),
            _ => new PhonyAssignmentStatement(
                (IExpression)expr.AcceptVisitor(this)!
            )
        };
    }

    private IStatement UnwrapConditionalAssignment(IExpression lhs, Expression expr, AssignmentOp op)
    {
        if (expr is ICSharpCode.Decompiler.CSharp.Syntax.ParenthesizedExpression { Expression: ConditionalExpression cond })
        {
            return new IfStatement(
                Attributes: [],
                new IfClause(
                    (IExpression)cond.Condition.AcceptVisitor(this)!,
                    MapCompoundStatement(UnwrapConditionalAssignment(lhs, cond.TrueExpression, op))!
                ),
                ElseIfClause: []
            )
            {
                Else = MapCompoundStatement(UnwrapConditionalAssignment(lhs, cond.FalseExpression, op))
            };
        }

        return new SimpleAssignmentStatement(lhs, (IExpression)expr.AcceptVisitor(this)!, op);
    }

    public IShaderAstNode? VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitFixedStatement(FixedStatement fixedStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitFixedVariableInitializer(FixedVariableInitializer fixedVariableInitializer)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitForeachStatement(ForeachStatement foreachStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitForStatement(ICSharpCode.Decompiler.CSharp.Syntax.ForStatement forStatement)
    {
        IForInit? init = null;
        IForUpdate? update = null;

        var initializers = forStatement.Initializers;
        if (initializers.Count() == 1)
        {
            init = (IForInit)initializers.First().AcceptVisitor(this)!;
        }
        else if (initializers.Count() > 1)
        {
            // later we can generate a sequence of assignment statements
            // followed by the for-loop with only one initializer
            throw new NotImplementedException("ForStatement only accepts 1 initializer statement");
        }

        var iterators = forStatement.Iterators;
        if (iterators.Count() == 1)
        {
            update = (IForUpdate)iterators.First().AcceptVisitor(this)!;
        }
        else if (iterators.Count() > 1)
        {
            // later we could generate a sequence of update statements before
            // the end of the loop and before every continue statement
            throw new NotImplementedException("ForStatement only accepts 1 iterator statement");
        }

        return new CLSL.Language.IR.Statement.ForStatement(
            Attributes: [],
            new ForHeader()
            {
                Init = init,
                Expr = (IExpression)forStatement.Condition.AcceptVisitor(this)!,
                Update = update
            },
            (IStatement)forStatement.EmbeddedStatement.AcceptVisitor(this)!
        );
    }

    public IShaderAstNode? VisitFunctionPointerType(FunctionPointerAstType functionPointerType)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitGotoStatement(GotoStatement gotoStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitIdentifier(Identifier identifier)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitIdentifierExpression(IdentifierExpression identifierExpression)
    {
        var v = identifierExpression.Annotation<ILVariable>();
        return v.Kind switch
        {
            VariableKind.Parameter when v.Index.HasValue => Context.Parameters[v.Index.Value],
            VariableKind.Local when v.Name is not null => Context.LocalVariables[v.Name],
            _ => throw new NotSupportedException($"{nameof(VariableIdentifierExpression)} of variable kind {Enum.GetName(v.Kind)}")
        };
    }

    public IShaderAstNode? VisitIfElseStatement(IfElseStatement ifElseStatement)
    {
        // if time, expand nested if/else into list of else if clauses
        return new IfStatement(
            Attributes: [],
            new IfClause(
                (IExpression)ifElseStatement.Condition.AcceptVisitor(this)!,
                MapCompoundStatement((IStatement?)ifElseStatement.TrueStatement.AcceptVisitor(this))!
            ),
            ElseIfClause: []
        )
        {
            Else = MapCompoundStatement((IStatement?)ifElseStatement.FalseStatement.AcceptVisitor(this))
        };
    }

    public IShaderAstNode? VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitIndexerExpression(IndexerExpression indexerExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitInterpolatedStringExpression(InterpolatedStringExpression interpolatedStringExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitInterpolatedStringText(InterpolatedStringText interpolatedStringText)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitInterpolation(Interpolation interpolation)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitInvocationExpression(InvocationExpression invocationExpression)
    {
        var callee = invocationExpression.Annotation<InvocationResolveResult>().Member;
        var entityHandle = callee.MetadataToken;
        var token = MetadataTokens.GetToken(entityHandle);
        var methodBase = Assembly.ManifestModule.ResolveMethod(token);
        if (methodBase is null)
        {
            throw new NullReferenceException($"Failed to resolve method {callee}");
        }
        ImmutableArray<IExpression> args = [.. invocationExpression.Arguments.Select(a => a.AcceptVisitor(this)).OfType<IExpression>()];
        return new FunctionCallExpression(Context.Methods[methodBase], args);
    }

    public IShaderAstNode? VisitInvocationType(InvocationAstType invocationType)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitIsExpression(IsExpression isExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitLabelStatement(LabelStatement labelStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitLambdaExpression(LambdaExpression lambdaExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitLocalFunctionDeclarationStatement(LocalFunctionDeclarationStatement localFunctionDeclarationStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitLockStatement(LockStatement lockStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
    {
        var targetType = memberReferenceExpression.Target.Annotation<ResolveResult>().Type;
        var targetTypeDefinition = targetType.GetDefinition();
        var targetMember = memberReferenceExpression.Target.Annotation<MemberResolveResult>();
        // TODO: proper handling this reference, check target type
        if (memberReferenceExpression.Target is ThisReferenceExpression)
        {
            // assume this references to IShaderModule, which is global naming space for shaders
            return new VariableIdentifierExpression((VariableDeclaration)Context[memberReferenceExpression.MemberName]);
        }
        // TODO: check if it's a vector
        var baseExpr = (IExpression)memberReferenceExpression.Target.AcceptVisitor(this);
        if (targetType.FullName == typeof(Vector2).FullName
            || targetType.FullName == typeof(Vector3).FullName
            || targetType.FullName == typeof(Vector4).FullName)
        {
            var member = (SwizzleComponent)Enum.Parse(typeof(SwizzleComponent), memberReferenceExpression.MemberName.ToLower());
            return new VectorSwizzleAccessExpression(baseExpr, [member]);
        }
        else
        {
            return new NamedComponentExpression(baseExpr, memberReferenceExpression.MemberName);
        }
    }

    public IShaderAstNode? VisitMemberType(MemberType memberType)
    {
        var t = memberType.Annotation<TypeResolveResult>();
        IShaderType tm = t.Type.FullName switch
        {
            "System.Numerics.Vector4" => ShaderType.GetVecType(N4.Instance, ShaderType.F32),
            "System.Numerics.Vector3" => ShaderType.GetVecType(N3.Instance, ShaderType.F32),
            "System.Numerics.Vector2" => ShaderType.GetVecType(N2.Instance, ShaderType.F32),
            //_ => throw new NotSupportedException($"{nameof(VisitMemberType)} not support {memberType}")
            _ => new StructureDeclaration(t.Type.Name, [], []),
        };
        return new TypeReference(tm);
    }

    public IShaderAstNode? VisitMethodDeclaration(MethodDeclaration methodDeclaration)
    {
        throw new NotSupportedException();
        static bool IsReturnAttributeSection(AttributeSection a)
        {
            return a.AttributeTarget == "return";
        }
        var returnAttributes = methodDeclaration.Attributes
                                                .Where(IsReturnAttributeSection)
                                                .SelectMany(sec => sec.Attributes)
                                                .Select(attr => attr.AcceptVisitor(this))
                                                .OfType<IShaderAttribute>();
        var methodAttributes = methodDeclaration.Attributes
                                                .Where(sec => !IsReturnAttributeSection(sec))
                                                .SelectMany(sec => sec.Attributes)
                                                .Select(attr => attr.AcceptVisitor(this))
                                                .OfType<IShaderAttribute>();
        var parameters = methodDeclaration.Parameters
                                                .Select(p => p.AcceptVisitor(this))
                                                .OfType<CLSL.Language.IR.Declaration.ParameterDeclaration>()
                                                .ToImmutableArray();
        var env = new Dictionary<string, IDeclaration>();
        //foreach (var kv in Symbols)
        //{
        //    env.Add(kv.Key, kv.Value);
        //}
        //foreach (var p in parameters)
        //{
        //    if (env.ContainsKey(p.Name))
        //    {
        //        env[p.Name] = p;
        //    }
        //    else
        //    {
        //        env.Add(p.Name, p);
        //    }
        //}
        var visitor = new ILSpyMethodBodyToCLSLNodeAstVisitor(Context, Assembly);
        var body = (CompoundStatement)methodDeclaration.Body.AcceptVisitor(visitor);
        // TODO: proper handling of return type
        var rt = methodDeclaration.ReturnType.AcceptVisitor(this);
        // TODO: remove pattern matching hack for return type
        var fReturn = new FunctionReturn(
            //rt is IR.Declaration.TypeDeclaration { Type: var t } ? t :
            rt is IShaderType it ? it : null
            , [.. returnAttributes]);
        return new FunctionDeclaration(
            methodDeclaration.Name,
            parameters,
            fReturn,
            [.. methodAttributes]
            )
        {
            Body = body
        };
    }

    public IShaderAstNode? VisitNamedArgumentExpression(NamedArgumentExpression namedArgumentExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitNamedExpression(NamedExpression namedExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        var c = namespaceDeclaration.Children.OfType<ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration>().Single();
        return c.AcceptVisitor(this);
    }

    public IShaderAstNode? VisitNullNode(AstNode nullNode)
    {
        return null;
    }

    public IShaderAstNode? VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
    {
        var t = objectCreateExpression.GetChildByRole(Roles.Type);
        var args = objectCreateExpression.GetChildrenByRole(Roles.Argument)
                                         .Select(a => a.AcceptVisitor(this))
                                         .OfType<IExpression>()
                                         .ToImmutableArray();
        return GetConstructExpression(t.Annotation<TypeResolveResult>().Type, args);
    }

    IExpression GetConstructExpression(IType type, ImmutableArray<IExpression> args)
    {
        //if (type.FullName == typeof(Vector4).FullName)
        //{
        //    return SyntaxFactory.vec4<FloatType<N32>>(args.ToArray());
        //}
        //else if (type.FullName == typeof(Vector3).FullName)
        //{
        //    return SyntaxFactory.vec3<FloatType<N32>>(args.ToArray());
        //}
        //else if (type.FullName == typeof(Vector2).FullName)
        //{
        //    return SyntaxFactory.vec2<FloatType<N32>>(args.ToArray());
        //}
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitOutVarDeclarationExpression(OutVarDeclarationExpression outVarDeclarationExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitParameterDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.ParameterDeclaration parameterDeclaration)
    {
        return new CLSL.Language.IR.Declaration.ParameterDeclaration(
            parameterDeclaration.Name,
            (IShaderType)parameterDeclaration.Type.AcceptVisitor(this) ?? ShaderType.Unit,
            [.. parameterDeclaration.Attributes.SelectMany(sec => sec.Attributes).Select(a => a.AcceptVisitor(this)).OfType<IShaderAttribute>()]
        );
    }

    public IShaderAstNode? VisitParenthesizedExpression(ICSharpCode.Decompiler.CSharp.Syntax.ParenthesizedExpression parenthesizedExpression)
    {
        return new CLSL.Language.IR.Expression.ParenthesizedExpression((IExpression)parenthesizedExpression.Expression.AcceptVisitor(this));
    }

    public IShaderAstNode? VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignation parenthesizedVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitPatternPlaceholder(AstNode placeholder, ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching.Pattern pattern)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
    {
        var value = primitiveExpression.Value;
        return value switch
        {
            float v => new LiteralValueExpression(SyntaxFactory.Literal(v)),
            double v => new LiteralValueExpression(SyntaxFactory.Literal(v)),
            int v => new LiteralValueExpression(SyntaxFactory.Literal(v)),
            uint v => new LiteralValueExpression(SyntaxFactory.Literal(v)),
            bool v => new LiteralValueExpression(SyntaxFactory.Literal(v)),
            _ => throw new NotSupportedException($"{nameof(VisitPrimitiveExpression)} does not support {primitiveExpression}")
        };
    }

    public IShaderAstNode? VisitPrimitiveType(ICSharpCode.Decompiler.CSharp.Syntax.PrimitiveType primitiveType)
    {
        IShaderType t = primitiveType.KnownTypeCode switch
        {
            KnownTypeCode.Boolean => ShaderType.Bool,
            KnownTypeCode.UInt32 => ShaderType.U32,
            KnownTypeCode.UInt64 => ShaderType.U64,
            KnownTypeCode.Int32 => ShaderType.I32,
            KnownTypeCode.Int64 => ShaderType.I64,
            KnownTypeCode.Single => ShaderType.F32,
            KnownTypeCode.Double => ShaderType.F64,
            _ => throw new NotSupportedException($"Unknown primitive type {primitiveType}")
        };
        return new TypeReference(t);
    }

    public IShaderAstNode? VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryContinuationClause(QueryContinuationClause queryContinuationClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryExpression(QueryExpression queryExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryFromClause(QueryFromClause queryFromClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryGroupClause(QueryGroupClause queryGroupClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryJoinClause(QueryJoinClause queryJoinClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryLetClause(QueryLetClause queryLetClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryOrderClause(QueryOrderClause queryOrderClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryOrdering(QueryOrdering queryOrdering)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQuerySelectClause(QuerySelectClause querySelectClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitQueryWhereClause(QueryWhereClause queryWhereClause)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitRecursivePatternExpression(RecursivePatternExpression recursivePatternExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitReturnStatement(ICSharpCode.Decompiler.CSharp.Syntax.ReturnStatement returnStatement)
    {
        if (returnStatement.HasChildren)
        {
            return SyntaxFactory.Return(
                (IExpression?)returnStatement.GetChildByRole(Roles.Expression).AcceptVisitor(this)
            );
        }
        else
        {
            return SyntaxFactory.Return(null);
        }

    }

    public IShaderAstNode? VisitSimpleType(SimpleType simpleType)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSingleVariableDesignation(SingleVariableDesignation singleVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSizeOfExpression(SizeOfExpression sizeOfExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitStackAllocExpression(StackAllocExpression stackAllocExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSwitchExpression(SwitchExpression switchExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSwitchExpressionSection(SwitchExpressionSection switchExpressionSection)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSwitchSection(ICSharpCode.Decompiler.CSharp.Syntax.SwitchSection switchSection)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSwitchStatement(SwitchStatement switchStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitSyntaxTree(SyntaxTree syntaxTree)
    {
        var c = syntaxTree.Children.Single();
        return c.AcceptVisitor(this);
    }

    public IShaderAstNode? VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression)
    {
        // TODO: check if is referencing shader module object (only this case we can simply access member as global references)
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitThrowExpression(ThrowExpression throwExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitThrowStatement(ThrowStatement throwStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTryCatchStatement(TryCatchStatement tryCatchStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTupleExpression(TupleExpression tupleExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTupleType(TupleAstType tupleType)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTupleTypeElement(TupleTypeElement tupleTypeElement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTypeDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration typeDeclaration)
    {
        var nodes = typeDeclaration.Members.Where(m => !m.Name.StartsWith("ILSLWGSL"))
                                           .Select(m => m.AcceptVisitor(this))
                                           .OfType<FunctionDeclaration>();
        return new CLSL.Language.IR.ShaderModule([.. nodes]);
    }

    public IShaderAstNode? VisitTypeOfExpression(TypeOfExpression typeOfExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTypeParameterDeclaration(TypeParameterDeclaration typeParameterDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
    {
        var expr = (IExpression)unaryOperatorExpression.Expression.AcceptVisitor(this)!;
        return unaryOperatorExpression.Operator switch
        {
            UnaryOperatorType.Not => new UnaryLogicalExpression(expr, UnaryLogicalOp.Not),
            UnaryOperatorType.Minus => new UnaryArithmeticExpression(expr, UnaryArithmeticOp.Minus),
            _ => throw new NotSupportedException($"{nameof(VisitUnaryOperatorExpression)} does not support {unaryOperatorExpression}")
        };
    }

    public IShaderAstNode? VisitUncheckedExpression(UncheckedExpression uncheckedExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUncheckedStatement(UncheckedStatement uncheckedStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUndocumentedExpression(UndocumentedExpression undocumentedExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUnsafeStatement(UnsafeStatement unsafeStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUsingAliasDeclaration(UsingAliasDeclaration usingAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUsingDeclaration(UsingDeclaration usingDeclaration)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitUsingStatement(UsingStatement usingStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
    {
        // TODO: handle multiple variable declaration 
        // TODO: proper handling of variable type
        var v = variableDeclarationStatement.Variables.Single();
        var varDecl = new VariableDeclaration(DeclarationScope.Function, v.Name, (IShaderType)variableDeclarationStatement.Type.AcceptVisitor(this), []);
        var context = Context.WithLocalVariableDeclaration(varDecl.Name, varDecl);
        var c = v.GetChildByRole(Roles.Expression);
        if (c is not null)
        {
            varDecl.Initializer = (IExpression)c.AcceptVisitor(new ILSpyMethodBodyToCLSLNodeAstVisitor(context, Assembly));
        }
        return new VariableOrValueStatement(varDecl);
    }

    public IShaderAstNode? VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitWhileStatement(ICSharpCode.Decompiler.CSharp.Syntax.WhileStatement whileStatement)
    {
        return new CLSL.Language.IR.Statement.WhileStatement(
            Attributes: [],
            (IExpression)whileStatement.Condition.AcceptVisitor(this)!,
            (IStatement)whileStatement.EmbeddedStatement.AcceptVisitor(this)!
        );
    }

    public IShaderAstNode? VisitWithInitializerExpression(WithInitializerExpression withInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement)
    {
        throw new NotImplementedException();
    }

    public IShaderAstNode? VisitYieldReturnStatement(YieldReturnStatement yieldReturnStatement)
    {
        throw new NotImplementedException();
    }

    private static CompoundStatement? MapCompoundStatement(IStatement? stmt)
    {
        return stmt switch
        {
            CompoundStatement s => s,
            not null => new CompoundStatement([stmt]),
            null => null
        };
    }

    private static AssignmentOp MapAssignmentOperator(AssignmentOperatorType op)
    {
        return op switch
        {
            AssignmentOperatorType.Assign => AssignmentOp.Assign,
            AssignmentOperatorType.Add => AssignmentOp.Add,
            AssignmentOperatorType.Subtract => AssignmentOp.Subtract,
            AssignmentOperatorType.Multiply => AssignmentOp.Multiply,
            AssignmentOperatorType.Divide => AssignmentOp.Divide,
            AssignmentOperatorType.Modulus => AssignmentOp.Modulus,
            AssignmentOperatorType.BitwiseAnd => AssignmentOp.BitwiseAnd,
            AssignmentOperatorType.BitwiseOr => AssignmentOp.BitwiseOr,
            AssignmentOperatorType.ExclusiveOr => AssignmentOp.ExclusiveOr,
            AssignmentOperatorType.ShiftLeft => AssignmentOp.ShiftLeft,
            AssignmentOperatorType.ShiftRight => AssignmentOp.ShiftRight,
            _ => throw new NotImplementedException()
        };
    }
}

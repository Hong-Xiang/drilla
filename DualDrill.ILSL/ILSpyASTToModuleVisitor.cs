using DualDrill.Common.Nat;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using DualDrill.ILSL.Types;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace DualDrill.ILSL;

public sealed class ILSpyASTToModuleVisitor(Dictionary<string, IDeclaration> Symbols, Assembly Assembly) : IAstVisitor<IAstNode?>
{
    public IAstNode? VisitAccessor(Accessor accessor)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitAnonymousTypeCreateExpression(AnonymousTypeCreateExpression anonymousTypeCreateExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitArraySpecifier(ArraySpecifier arraySpecifier)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitAsExpression(AsExpression asExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitAttribute(ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
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

    public IAstNode? VisitAttributeSection(AttributeSection attributeSection)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        var l = (IExpression)binaryOperatorExpression.Left.AcceptVisitor(this);
        var r = (IExpression)binaryOperatorExpression.Right.AcceptVisitor(this);
        // TODO: proper expression type handling
        if (l is LiteralValueExpression { Literal: IntLiteral<N32> { Value: var v } }
            && r is FormalParameterExpression { Parameter: { Type: UIntType<N32> } })
        {
            l = new LiteralValueExpression(new UIntLiteral<N32>((uint)v));
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

    public IAstNode? VisitBlockStatement(BlockStatement blockStatement)
    {
        Dictionary<string, IDeclaration> newScope = [];
        foreach (var kv in Symbols)
        {
            newScope.Add(kv.Key, kv.Value);
        }
        var blockScopeVisitor = new ILSpyASTToModuleVisitor(newScope, Assembly);
        var result = new CompoundStatement([.. blockStatement.Statements.Select(s => s.AcceptVisitor(blockScopeVisitor)).OfType<IStatement>()]);
        return result;
    }

    public IAstNode? VisitBreakStatement(ICSharpCode.Decompiler.CSharp.Syntax.BreakStatement breakStatement)
    {
        return new IR.Statement.BreakStatement();
    }

    public IAstNode? VisitCaseLabel(CaseLabel caseLabel)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitCastExpression(CastExpression castExpression)
    {
        var t = castExpression.Type.Annotation<TypeResolveResult>();
        var f = t switch
        {
            { Type.FullName: "System.Single" } => FloatType<N32>.Cast,
            { Type.FullName: "System.Double" } => FloatType<N64>.Cast,
            { Type.FullName: "System.Int32" } => IntType<N32>.Cast,
            { Type.FullName: "System.Int64" } => IntType<N64>.Cast,
            //{ Type: { FullName: "System.UInt32" } } => UIntType<N32>.Cast,
            //{ Type: { FullName: "System.UInt64" } } => UIntType<N64>.Cast,
            _ => throw new NotSupportedException()
        };
        return new FunctionCallExpression(f, [(IExpression)castExpression.Expression.AcceptVisitor(this)]);
    }

    public IAstNode? VisitCatchClause(CatchClause catchClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitCheckedExpression(CheckedExpression checkedExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitCheckedStatement(CheckedStatement checkedStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitComment(Comment comment)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitComposedType(ComposedType composedType)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitConditionalExpression(ConditionalExpression conditionalExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitConstraint(Constraint constraint)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitConstructorInitializer(ConstructorInitializer constructorInitializer)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitContinueStatement(ContinueStatement continueStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitCSharpTokenNode(CSharpTokenNode cSharpTokenNode)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDeclarationExpression(DeclarationExpression declarationExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDirectionExpression(DirectionExpression directionExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDocumentationReference(DocumentationReference documentationReference)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitDoWhileStatement(DoWhileStatement doWhileStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitEmptyStatement(EmptyStatement emptyStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitErrorNode(AstNode errorNode)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitExpressionStatement(ExpressionStatement expressionStatement)
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

    public IAstNode? VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitFixedStatement(FixedStatement fixedStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitFixedVariableInitializer(FixedVariableInitializer fixedVariableInitializer)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitForeachStatement(ForeachStatement foreachStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitForStatement(ICSharpCode.Decompiler.CSharp.Syntax.ForStatement forStatement)
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

        return new IR.Statement.ForStatement(
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

    public IAstNode? VisitFunctionPointerType(FunctionPointerAstType functionPointerType)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitGotoStatement(GotoStatement gotoStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitIdentifier(Identifier identifier)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitIdentifierExpression(IdentifierExpression identifierExpression)
    {
        var sym = Symbols[identifierExpression.GetChildByRole(Roles.Identifier).Name];
        return sym switch
        {
            VariableDeclaration v => SyntaxFactory.Identifier(v),
            IR.Declaration.ParameterDeclaration v => new FormalParameterExpression(v),
            _ => throw new NotSupportedException()
        };
    }

    public IAstNode? VisitIfElseStatement(IfElseStatement ifElseStatement)
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

    public IAstNode? VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitIndexerExpression(IndexerExpression indexerExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitInterpolatedStringExpression(InterpolatedStringExpression interpolatedStringExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitInterpolatedStringText(InterpolatedStringText interpolatedStringText)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitInterpolation(Interpolation interpolation)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitInvocationExpression(InvocationExpression invocationExpression)
    {
        Func<string, string> RemoveThisDot = (string expression) =>
        {
            if (expression.StartsWith("this."))
            {
                return expression.Substring("this.".Length);
            }
            return expression;
        };
        List<IAstNode> args = new();
        foreach (var argument in invocationExpression.Arguments)
        {
            // For example, you can add it to the 'args' list
            args.Add(argument.AcceptVisitor(this));
        }
        var immutableArgs = args.Cast<IExpression>().ToImmutableArray();
        if (invocationExpression.Target is MemberReferenceExpression memberReference)
        {
            string functionName = RemoveThisDot(memberReference.ToString());
            if (Symbols.ContainsKey(functionName))
            {
                return new FunctionCallExpression(
                    (FunctionDeclaration)Symbols[functionName],
                    immutableArgs
                );
            }
            // special case for vector dot as it's generic type
            switch (functionName)
            {
                case "global::System.Numerics.Vector2.Dot":
                    return new FunctionCallExpression(
                        VecType<N2, FloatType<N32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Dot":
                    return new FunctionCallExpression(
                        VecType<N3, FloatType<N32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Dot":
                    return new FunctionCallExpression(
                        VecType<N4, FloatType<N32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Length":
                    return new FunctionCallExpression(
                        VecType<N2, FloatType<N32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Length":
                    return new FunctionCallExpression(
                        VecType<N3, FloatType<N32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Length":
                    return new FunctionCallExpression(
                        VecType<N4, FloatType<N32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Abs":
                    return new FunctionCallExpression(
                        VecType<N2, FloatType<N32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Abs":
                    return new FunctionCallExpression(
                        VecType<N3, FloatType<N32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Abs":
                    return new FunctionCallExpression(
                        VecType<N4, FloatType<N32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Reflect":
                    return new FunctionCallExpression(
                        VecType<N2, FloatType<N32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Reflect":
                    return new FunctionCallExpression(
                        VecType<N3, FloatType<N32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Reflect":
                    return new FunctionCallExpression(
                        VecType<N4, FloatType<N32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Cross":
                    return new FunctionCallExpression(
                        VecType<N2, FloatType<N32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Cross":
                    return new FunctionCallExpression(
                        VecType<N3, FloatType<N32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Cross":
                    return new FunctionCallExpression(
                        VecType<N4, FloatType<N32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Math.Cos":
                    var res = new FunctionCallExpression(
                        FloatType<N32>.Cos,
                        immutableArgs
                    );
                    return res;
                case "global::System.Math.Sin":
                    return new FunctionCallExpression(
                        FloatType<N32>.Sin,
                        immutableArgs
                    );
                case "global::System.Math.Sqrt":
                    return new FunctionCallExpression(
                        FloatType<N32>.Sqrt,
                        immutableArgs
                    );
                case "global::System.Math.Pow":
                    return new FunctionCallExpression(
                        FloatType<N32>.Pow,
                        immutableArgs
                    );
                case "global::System.Math.Log":
                    return new FunctionCallExpression(
                        FloatType<N32>.Log,
                        immutableArgs
                    );
                case "global::System.Math.Clamp":
                    return new FunctionCallExpression(
                        FloatType<N32>.Clamp,
                        immutableArgs
                    );
                case "global::System.Math.Abs":
                    return new FunctionCallExpression(
                        FloatType<N32>.Abs,
                        immutableArgs
                    );
                case "global::System.Math.Max":
                    return new FunctionCallExpression(
                        FloatType<N32>.Max,
                        immutableArgs
                    );
                case "global::System.Math.Min":
                    return new FunctionCallExpression(
                        FloatType<N32>.Min,
                        immutableArgs
                    );
                case "global::System.Math.Floor":
                    return new FunctionCallExpression(
                        FloatType<N32>.Floor,
                        immutableArgs
                    );
                case "global::System.Math.Exp":
                    return new FunctionCallExpression(
                        FloatType<N32>.Exp,
                        immutableArgs
                    );
                case "global::System.Math.Sign":
                    return new FunctionCallExpression(
                        FloatType<N32>.Sign,
                        immutableArgs
                    );
                default:
                    throw new NotImplementedException();
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public IAstNode? VisitInvocationType(InvocationAstType invocationType)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitIsExpression(IsExpression isExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitLabelStatement(LabelStatement labelStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitLambdaExpression(LambdaExpression lambdaExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitLocalFunctionDeclarationStatement(LocalFunctionDeclarationStatement localFunctionDeclarationStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitLockStatement(LockStatement lockStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
    {
        var targetType = memberReferenceExpression.Target.Annotation<ResolveResult>().Type;
        var targetTypeDefinition = targetType.GetDefinition();
        var targetMember = memberReferenceExpression.Target.Annotation<MemberResolveResult>();
        // TODO: proper handling this reference, check target type
        if (memberReferenceExpression.Target is ThisReferenceExpression)
        {
            // assume this references to IShaderModule, which is global naming space for shaders
            return new VariableIdentifierExpression((VariableDeclaration)Symbols[memberReferenceExpression.MemberName]);
        }
        // TODO: check if it's a vector
        var baseExpr = (IExpression)(memberReferenceExpression.Target.AcceptVisitor(this));
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

    public IAstNode? VisitMemberType(MemberType memberType)
    {
        var t = memberType.Annotation<TypeResolveResult>();
        return t.Type.FullName switch
        {
            "System.Numerics.Vector4" => new VecType<N4, FloatType<N32>>(),
            "System.Numerics.Vector3" => new VecType<N3, FloatType<N32>>(),
            "System.Numerics.Vector2" => new VecType<N2, FloatType<N32>>(),
            //"System.Numerics.Vector4" => new VecType<R4, FloatType<N32>>(),
            //_ => throw new NotSupportedException($"{nameof(VisitMemberType)} not support {memberType}")
            _ => new StructureDeclaration(t.Type.Name, [], []),
        };
    }

    public IAstNode? VisitMethodDeclaration(MethodDeclaration methodDeclaration)
    {
        static bool IsReturnAttributeSection(AttributeSection a)
        {
            return a.AttributeTarget == "return";
        }
        var returnAttributes = methodDeclaration.Attributes
                                                .Where(IsReturnAttributeSection)
                                                .SelectMany(sec => sec.Attributes)
                                                .Select(attr => attr.AcceptVisitor(this))
                                                .OfType<IR.IAttribute>();
        var methodAttributes = methodDeclaration.Attributes
                                                .Where(sec => !IsReturnAttributeSection(sec))
                                                .SelectMany(sec => sec.Attributes)
                                                .Select(attr => attr.AcceptVisitor(this))
                                                .OfType<IR.IAttribute>();
        var parameters = methodDeclaration.Parameters
                                                .Select(p => p.AcceptVisitor(this))
                                                .OfType<IR.Declaration.ParameterDeclaration>()
                                                .ToImmutableArray();
        var env = new Dictionary<string, IDeclaration>();
        foreach (var kv in Symbols)
        {
            env.Add(kv.Key, kv.Value);
        }
        foreach (var p in parameters)
        {
            if (env.ContainsKey(p.Name))
            {
                env[p.Name] = p;
            }
            else
            {
                env.Add(p.Name, p);
            }
        }
        var visitor = new ILSpyASTToModuleVisitor(env, Assembly);
        var body = (CompoundStatement)methodDeclaration.Body.AcceptVisitor(visitor);
        // TODO: proper handling of return type
        var rt = methodDeclaration.ReturnType.AcceptVisitor(this);
        // TODO: remove pattern matching hack for return type
        var fReturn = new IR.Declaration.FunctionReturn(
            //rt is IR.Declaration.TypeDeclaration { Type: var t } ? t :
            rt is Types.IType it ? it : null
            , [.. returnAttributes]);
        return new IR.Declaration.FunctionDeclaration(
            methodDeclaration.Name,
            parameters,
            fReturn,
            [.. methodAttributes]
            )
        {
            Body = body
        };
    }

    public IAstNode? VisitNamedArgumentExpression(NamedArgumentExpression namedArgumentExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitNamedExpression(NamedExpression namedExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        var c = namespaceDeclaration.Children.OfType<ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration>().Single();
        return c.AcceptVisitor(this);
    }

    public IAstNode? VisitNullNode(AstNode nullNode)
    {
        return null;
    }

    public IAstNode? VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
    {
        var t = objectCreateExpression.GetChildByRole(Roles.Type);
        var args = objectCreateExpression.GetChildrenByRole(Roles.Argument)
                                         .Select(a => a.AcceptVisitor(this))
                                         .OfType<IExpression>()
                                         .ToImmutableArray();
        return GetConstructExpression(t.Annotation<TypeResolveResult>().Type, args);
    }

    IExpression GetConstructExpression(ICSharpCode.Decompiler.TypeSystem.IType type, ImmutableArray<IExpression> args)
    {
        if (type.FullName == typeof(Vector4).FullName)
        {
            return SyntaxFactory.vec4<FloatType<N32>>(args.ToArray());
        }
        else if (type.FullName == typeof(Vector3).FullName)
        {
            return SyntaxFactory.vec3<FloatType<N32>>(args.ToArray());
        }
        else if (type.FullName == typeof(Vector2).FullName)
        {
            return SyntaxFactory.vec2<FloatType<N32>>(args.ToArray());
        }
        throw new NotImplementedException();
    }

    public IAstNode? VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitOutVarDeclarationExpression(OutVarDeclarationExpression outVarDeclarationExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitParameterDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.ParameterDeclaration parameterDeclaration)
    {
        return new IR.Declaration.ParameterDeclaration(
            parameterDeclaration.Name,
            (Types.IType)parameterDeclaration.Type.AcceptVisitor(this),
            [.. parameterDeclaration.Attributes.SelectMany(sec => sec.Attributes).Select(a => a.AcceptVisitor(this)).OfType<IR.IAttribute>()]
        );
    }

    public IAstNode? VisitParenthesizedExpression(ICSharpCode.Decompiler.CSharp.Syntax.ParenthesizedExpression parenthesizedExpression)
    {
        return new IR.Expression.ParenthesizedExpression((IExpression)parenthesizedExpression.Expression.AcceptVisitor(this));
    }

    public IAstNode? VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignation parenthesizedVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitPatternPlaceholder(AstNode placeholder, ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching.Pattern pattern)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
    {
        var value = primitiveExpression.Value;
        return value switch
        {
            float v => new LiteralValueExpression(new FloatLiteral<N32>(v)),
            double v => new LiteralValueExpression(new FloatLiteral<N32>(v)),
            int v => new LiteralValueExpression(new IntLiteral<N32>(v)),
            uint v => new LiteralValueExpression(new UIntLiteral<N32>(v)),
            bool v => new LiteralValueExpression(new BoolLiteral(v)),
            _ => throw new NotSupportedException($"{nameof(VisitPrimitiveExpression)} does not support {primitiveExpression}")
        };
    }

    public IAstNode? VisitPrimitiveType(PrimitiveType primitiveType)
    {
        return primitiveType.KnownTypeCode switch
        {
            KnownTypeCode.Boolean => new BoolType(),
            KnownTypeCode.UInt32 => new UIntType<N32>(),
            KnownTypeCode.UInt64 => new UIntType<N64>(),
            KnownTypeCode.Int32 => new IntType<N32>(),
            KnownTypeCode.Int64 => new IntType<N64>(),
            KnownTypeCode.Single => new FloatType<N32>(),
            KnownTypeCode.Double => new FloatType<N64>(),
            _ => throw new NotSupportedException($"Unknown primitive type {primitiveType}")
        };
    }

    public IAstNode? VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryContinuationClause(QueryContinuationClause queryContinuationClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryExpression(QueryExpression queryExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryFromClause(QueryFromClause queryFromClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryGroupClause(QueryGroupClause queryGroupClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryJoinClause(QueryJoinClause queryJoinClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryLetClause(QueryLetClause queryLetClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryOrderClause(QueryOrderClause queryOrderClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryOrdering(QueryOrdering queryOrdering)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQuerySelectClause(QuerySelectClause querySelectClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitQueryWhereClause(QueryWhereClause queryWhereClause)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitRecursivePatternExpression(RecursivePatternExpression recursivePatternExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitReturnStatement(ICSharpCode.Decompiler.CSharp.Syntax.ReturnStatement returnStatement)
    {
        if (returnStatement.HasChildren)
        {
            return new IR.Statement.ReturnStatement(
                (IExpression)returnStatement.GetChildByRole(Roles.Expression).AcceptVisitor(this)
            );
        }
        else
        {
            return new IR.Statement.ReturnStatement(null);
        }

    }

    public IAstNode? VisitSimpleType(SimpleType simpleType)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSingleVariableDesignation(SingleVariableDesignation singleVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSizeOfExpression(SizeOfExpression sizeOfExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitStackAllocExpression(StackAllocExpression stackAllocExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSwitchExpression(SwitchExpression switchExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSwitchExpressionSection(SwitchExpressionSection switchExpressionSection)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSwitchSection(SwitchSection switchSection)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSwitchStatement(SwitchStatement switchStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitSyntaxTree(SyntaxTree syntaxTree)
    {
        var c = syntaxTree.Children.Single();
        return c.AcceptVisitor(this);
    }

    public IAstNode? VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression)
    {
        // TODO: check if is referencing shader module object (only this case we can simply access member as global references)
        throw new NotImplementedException();
    }

    public IAstNode? VisitThrowExpression(ThrowExpression throwExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitThrowStatement(ThrowStatement throwStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTryCatchStatement(TryCatchStatement tryCatchStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTupleExpression(TupleExpression tupleExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTupleType(TupleAstType tupleType)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTupleTypeElement(TupleTypeElement tupleTypeElement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTypeDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration typeDeclaration)
    {
        var nodes = typeDeclaration.Members.Where(m => !m.Name.StartsWith("ILSLWGSL"))
                                           .Select(m => m.AcceptVisitor(this))
                                           .OfType<FunctionDeclaration>();
        return new IR.Module([.. nodes]);
    }

    public IAstNode? VisitTypeOfExpression(TypeOfExpression typeOfExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTypeParameterDeclaration(TypeParameterDeclaration typeParameterDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
    {
        var expr = (IExpression)unaryOperatorExpression.Expression.AcceptVisitor(this)!;
        return unaryOperatorExpression.Operator switch
        {
            UnaryOperatorType.Not => new UnaryLogicalExpression(expr, UnaryLogicalOp.Not),
            UnaryOperatorType.Minus => new UnaryArithmeticExpression(expr, UnaryArithmeticOp.Minus),
            _ => throw new NotSupportedException($"{nameof(VisitUnaryOperatorExpression)} does not support {unaryOperatorExpression}")
        };
    }

    public IAstNode? VisitUncheckedExpression(UncheckedExpression uncheckedExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUncheckedStatement(UncheckedStatement uncheckedStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUndocumentedExpression(UndocumentedExpression undocumentedExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUnsafeStatement(UnsafeStatement unsafeStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUsingAliasDeclaration(UsingAliasDeclaration usingAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUsingDeclaration(UsingDeclaration usingDeclaration)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitUsingStatement(UsingStatement usingStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
    {
        // TODO: handle multiple variable declaration 
        // TODO: proper handling of variable type
        var v = variableDeclarationStatement.Variables.Single();
        var varDecl = new VariableDeclaration(DeclarationScope.Function, v.Name, ((Types.IType)variableDeclarationStatement.Type.AcceptVisitor(this)), []);
        Symbols.Add(varDecl.Name, varDecl);
        var c = v.GetChildByRole(Roles.Expression);
        if (c is not null)
        {
            varDecl.Initializer = (IExpression)c.AcceptVisitor(this);
        }
        return new VariableOrValueStatement(varDecl);
    }

    public IAstNode? VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitWhileStatement(ICSharpCode.Decompiler.CSharp.Syntax.WhileStatement whileStatement)
    {
        return new IR.Statement.WhileStatement(
            Attributes: [],
            (IExpression)whileStatement.Condition.AcceptVisitor(this)!,
            (IStatement)whileStatement.EmbeddedStatement.AcceptVisitor(this)!
        );
    }

    public IAstNode? VisitWithInitializerExpression(WithInitializerExpression withInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement)
    {
        throw new NotImplementedException();
    }

    public IAstNode? VisitYieldReturnStatement(YieldReturnStatement yieldReturnStatement)
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

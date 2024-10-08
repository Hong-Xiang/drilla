﻿using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace DualDrill.ILSL;

public sealed class ILSpyASTToModuleVisitor(Dictionary<string, IDeclaration> Symbols, Assembly Assembly) : IAstVisitor<INode?>
{
    public INode? VisitAccessor(Accessor accessor)
    {
        throw new NotImplementedException();
    }

    public INode? VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitAnonymousTypeCreateExpression(AnonymousTypeCreateExpression anonymousTypeCreateExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitArraySpecifier(ArraySpecifier arraySpecifier)
    {
        throw new NotImplementedException();
    }

    public INode? VisitAsExpression(AsExpression asExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitAttribute(ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
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
        if(a.FullName == typeof(ShaderMethodAttribute).FullName)
        {
            return new ShaderMethodAttribute();
        }
        return null;
    }

    public INode? VisitAttributeSection(AttributeSection attributeSection)
    {
        throw new NotImplementedException();
    }

    public INode? VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        var l = (IExpression)binaryOperatorExpression.Left.AcceptVisitor(this);
        var r = (IExpression)binaryOperatorExpression.Right.AcceptVisitor(this);
        // TODO: proper expression type handling
        if (l is LiteralValueExpression { Literal: IntLiteral<B32> { Value: var v } }
            && r is FormalParameterExpression { Parameter: { Type: UIntType<B32> } })
        {
            l = new LiteralValueExpression(new UIntLiteral<B32>((uint)v));
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

    public INode? VisitBlockStatement(BlockStatement blockStatement)
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

    public INode? VisitBreakStatement(ICSharpCode.Decompiler.CSharp.Syntax.BreakStatement breakStatement)
    {
        return new IR.Statement.BreakStatement();
    }

    public INode? VisitCaseLabel(CaseLabel caseLabel)
    {
        throw new NotImplementedException();
    }

    public INode? VisitCastExpression(CastExpression castExpression)
    {
        var t = castExpression.Type.Annotation<TypeResolveResult>();
        var f = t switch
        {
            { Type.FullName: "System.Single" } => FloatType<B32>.Cast,
            { Type.FullName: "System.Double" } => FloatType<B64>.Cast,
            { Type.FullName: "System.Int32" } => IntType<B32>.Cast,
            { Type.FullName: "System.Int64" } => IntType<B64>.Cast,
            //{ Type: { FullName: "System.UInt32" } } => UIntType<B32>.Cast,
            //{ Type: { FullName: "System.UInt64" } } => UIntType<B64>.Cast,
            _ => throw new NotSupportedException()
        };
        return new FunctionCallExpression(f, [(IExpression)castExpression.Expression.AcceptVisitor(this)]);
    }

    public INode? VisitCatchClause(CatchClause catchClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitCheckedExpression(CheckedExpression checkedExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitCheckedStatement(CheckedStatement checkedStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitComment(Comment comment)
    {
        throw new NotImplementedException();
    }

    public INode? VisitComposedType(ComposedType composedType)
    {
        throw new NotImplementedException();
    }

    public INode? VisitConditionalExpression(ConditionalExpression conditionalExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitConstraint(Constraint constraint)
    {
        throw new NotImplementedException();
    }

    public INode? VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitConstructorInitializer(ConstructorInitializer constructorInitializer)
    {
        throw new NotImplementedException();
    }

    public INode? VisitContinueStatement(ContinueStatement continueStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitCSharpTokenNode(CSharpTokenNode cSharpTokenNode)
    {
        throw new NotImplementedException();
    }

    public INode? VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDeclarationExpression(DeclarationExpression declarationExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDirectionExpression(DirectionExpression directionExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDocumentationReference(DocumentationReference documentationReference)
    {
        throw new NotImplementedException();
    }

    public INode? VisitDoWhileStatement(DoWhileStatement doWhileStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitEmptyStatement(EmptyStatement emptyStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitErrorNode(AstNode errorNode)
    {
        throw new NotImplementedException();
    }

    public INode? VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitExpressionStatement(ExpressionStatement expressionStatement)
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

    public INode? VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitFixedStatement(FixedStatement fixedStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitFixedVariableInitializer(FixedVariableInitializer fixedVariableInitializer)
    {
        throw new NotImplementedException();
    }

    public INode? VisitForeachStatement(ForeachStatement foreachStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitForStatement(ICSharpCode.Decompiler.CSharp.Syntax.ForStatement forStatement)
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

    public INode? VisitFunctionPointerType(FunctionPointerAstType functionPointerType)
    {
        throw new NotImplementedException();
    }

    public INode? VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitGotoStatement(GotoStatement gotoStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitIdentifier(Identifier identifier)
    {
        throw new NotImplementedException();
    }

    public INode? VisitIdentifierExpression(IdentifierExpression identifierExpression)
    {
        var sym = Symbols[identifierExpression.GetChildByRole(Roles.Identifier).Name];
        return sym switch
        {
            VariableDeclaration v => SyntaxFactory.Identifier(v),
            IR.Declaration.ParameterDeclaration v => new FormalParameterExpression(v),
            _ => throw new NotSupportedException()
        };
    }

    public INode? VisitIfElseStatement(IfElseStatement ifElseStatement)
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

    public INode? VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitIndexerExpression(IndexerExpression indexerExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitInterpolatedStringExpression(InterpolatedStringExpression interpolatedStringExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitInterpolatedStringText(InterpolatedStringText interpolatedStringText)
    {
        throw new NotImplementedException();
    }

    public INode? VisitInterpolation(Interpolation interpolation)
    {
        throw new NotImplementedException();
    }

    public INode? VisitInvocationExpression(InvocationExpression invocationExpression)
    {
        Func<string, string> RemoveThisDot = (string expression) => 
        {
            if (expression.StartsWith("this."))
            {
                return expression.Substring("this.".Length);
            }
            return expression;
        };
        List<INode> args = new();
        foreach (var argument in invocationExpression.Arguments)
        {
            // For example, you can add it to the 'args' list
            args.Add(argument.AcceptVisitor(this));
        }
        var immutableArgs = args.Cast<IExpression>().ToImmutableArray();
        if (invocationExpression.Target is MemberReferenceExpression memberReference)
        {
            string functionName = RemoveThisDot(memberReference.ToString());
            if(Symbols.ContainsKey(functionName))
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
                        VecType<R2, FloatType<B32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Dot":
                    return new FunctionCallExpression(
                        VecType<R3, FloatType<B32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Dot":
                    return new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Dot,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Length":
                    return new FunctionCallExpression(
                        VecType<R2, FloatType<B32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Length":
                    return new FunctionCallExpression(
                        VecType<R3, FloatType<B32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Length":
                    return new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Length,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Abs":
                    return new FunctionCallExpression(
                        VecType<R2, FloatType<B32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Abs":
                    return new FunctionCallExpression(
                        VecType<R3, FloatType<B32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Abs":
                    return new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Abs,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Reflect":
                    return new FunctionCallExpression(
                        VecType<R2, FloatType<B32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Reflect":
                    return new FunctionCallExpression(
                        VecType<R3, FloatType<B32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Reflect":
                    return new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Reflect,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector2.Cross":
                    return new FunctionCallExpression(
                        VecType<R2, FloatType<B32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector3.Cross":
                    return new FunctionCallExpression(
                        VecType<R3, FloatType<B32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Numerics.Vector4.Cross":
                    return new FunctionCallExpression(
                        VecType<R4, FloatType<B32>>.Cross,
                        immutableArgs
                    );
                case "global::System.Math.Cos":
                    var res = new FunctionCallExpression(
                        FloatType<B32>.Cos,
                        immutableArgs
                    );
                    return res;
                case "global::System.Math.Sin":
                    return new FunctionCallExpression(
                        FloatType<B32>.Sin,
                        immutableArgs
                    );
                case "global::System.Math.Sqrt":
                    return new FunctionCallExpression(
                        FloatType<B32>.Sqrt,
                        immutableArgs
                    );
                case "global::System.Math.Pow":
                    return new FunctionCallExpression(
                        FloatType<B32>.Pow,
                        immutableArgs
                    );
                case "global::System.Math.Log":
                    return new FunctionCallExpression(
                        FloatType<B32>.Log,
                        immutableArgs
                    );
                case "global::System.Math.Clamp":
                    return new FunctionCallExpression(
                        FloatType<B32>.Clamp,
                        immutableArgs
                    );
                case "global::System.Math.Abs":
                    return new FunctionCallExpression(
                        FloatType<B32>.Abs,
                        immutableArgs
                    );
                case "global::System.Math.Max":
                    return new FunctionCallExpression(
                        FloatType<B32>.Max,
                        immutableArgs
                    );
                case "global::System.Math.Min":
                    return new FunctionCallExpression(
                        FloatType<B32>.Min,
                        immutableArgs
                    );
                case "global::System.Math.Floor":
                    return new FunctionCallExpression(
                        FloatType<B32>.Floor,
                        immutableArgs
                    );
                case "global::System.Math.Exp":
                    return new FunctionCallExpression(
                        FloatType<B32>.Exp,
                        immutableArgs
                    );
                case "global::System.Math.Sign":
                    return new FunctionCallExpression(
                        FloatType<B32>.Sign,
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

    public INode? VisitInvocationType(InvocationAstType invocationType)
    {
        throw new NotImplementedException();
    }

    public INode? VisitIsExpression(IsExpression isExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitLabelStatement(LabelStatement labelStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitLambdaExpression(LambdaExpression lambdaExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitLocalFunctionDeclarationStatement(LocalFunctionDeclarationStatement localFunctionDeclarationStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitLockStatement(LockStatement lockStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
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

    public INode? VisitMemberType(MemberType memberType)
    {
        var t = memberType.Annotation<TypeResolveResult>();
        return t.Type.FullName switch
        {
            "System.Numerics.Vector4" => new VecType<R4, FloatType<B32>>(),
            "System.Numerics.Vector3" => new VecType<R3, FloatType<B32>>(),
            "System.Numerics.Vector2" => new VecType<R2, FloatType<B32>>(),
            //"System.Numerics.Vector4" => new VecType<R4, FloatType<B32>>(),
            //_ => throw new NotSupportedException($"{nameof(VisitMemberType)} not support {memberType}")
            _ => new StructureDeclaration(t.Type.Name, [], []),
        };
    }

    public INode? VisitMethodDeclaration(MethodDeclaration methodDeclaration)
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
            rt is IR.Declaration.IType it ? it : null
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

    public INode? VisitNamedArgumentExpression(NamedArgumentExpression namedArgumentExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitNamedExpression(NamedExpression namedExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        var c = namespaceDeclaration.Children.OfType<ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration>().Single();
        return c.AcceptVisitor(this);
    }

    public INode? VisitNullNode(AstNode nullNode)
    {
        return null;
    }

    public INode? VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
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
            return SyntaxFactory.vec4<FloatType<B32>>(args.ToArray());
        }
        else if (type.FullName == typeof(Vector3).FullName)
        {
            return SyntaxFactory.vec3<FloatType<B32>>(args.ToArray());
        }
        else if (type.FullName == typeof(Vector2).FullName)
        {
            return SyntaxFactory.vec2<FloatType<B32>>(args.ToArray());
        }
        throw new NotImplementedException();
    }

    public INode? VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitOutVarDeclarationExpression(OutVarDeclarationExpression outVarDeclarationExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitParameterDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.ParameterDeclaration parameterDeclaration)
    {
        return new IR.Declaration.ParameterDeclaration(
            parameterDeclaration.Name,
            (IR.Declaration.IType)parameterDeclaration.Type.AcceptVisitor(this),
            [.. parameterDeclaration.Attributes.SelectMany(sec => sec.Attributes).Select(a => a.AcceptVisitor(this)).OfType<IR.IAttribute>()]
        );
    }

    public INode? VisitParenthesizedExpression(ICSharpCode.Decompiler.CSharp.Syntax.ParenthesizedExpression parenthesizedExpression)
    {
        return new IR.Expression.ParenthesizedExpression((IExpression)parenthesizedExpression.Expression.AcceptVisitor(this));
    }

    public INode? VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignation parenthesizedVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public INode? VisitPatternPlaceholder(AstNode placeholder, ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching.Pattern pattern)
    {
        throw new NotImplementedException();
    }

    public INode? VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
    {
        throw new NotImplementedException();
    }

    public INode? VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
    {
        var value = primitiveExpression.Value;
        return value switch
        {
            float v => new LiteralValueExpression(new FloatLiteral<B32>(v)),
            double v => new LiteralValueExpression(new FloatLiteral<B32>(v)),
            int v => new LiteralValueExpression(new IntLiteral<B32>(v)),
            uint v => new LiteralValueExpression(new UIntLiteral<B32>(v)),
            bool v => new LiteralValueExpression(new BoolLiteral(v)),
            _ => throw new NotSupportedException($"{nameof(VisitPrimitiveExpression)} does not support {primitiveExpression}")
        };
    }

    public INode? VisitPrimitiveType(PrimitiveType primitiveType)
    {
        return primitiveType.KnownTypeCode switch
        {
            KnownTypeCode.Boolean => new BoolType(),
            KnownTypeCode.UInt32 => new UIntType<B32>(),
            KnownTypeCode.UInt64 => new UIntType<B64>(),
            KnownTypeCode.Int32 => new IntType<B32>(),
            KnownTypeCode.Int64 => new IntType<B64>(),
            KnownTypeCode.Single => new FloatType<B32>(),
            KnownTypeCode.Double => new FloatType<B64>(),
            _ => throw new NotSupportedException($"Unknown primitive type {primitiveType}")
        };
    }

    public INode? VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryContinuationClause(QueryContinuationClause queryContinuationClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryExpression(QueryExpression queryExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryFromClause(QueryFromClause queryFromClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryGroupClause(QueryGroupClause queryGroupClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryJoinClause(QueryJoinClause queryJoinClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryLetClause(QueryLetClause queryLetClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryOrderClause(QueryOrderClause queryOrderClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryOrdering(QueryOrdering queryOrdering)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQuerySelectClause(QuerySelectClause querySelectClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitQueryWhereClause(QueryWhereClause queryWhereClause)
    {
        throw new NotImplementedException();
    }

    public INode? VisitRecursivePatternExpression(RecursivePatternExpression recursivePatternExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitReturnStatement(ICSharpCode.Decompiler.CSharp.Syntax.ReturnStatement returnStatement)
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

    public INode? VisitSimpleType(SimpleType simpleType)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSingleVariableDesignation(SingleVariableDesignation singleVariableDesignation)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSizeOfExpression(SizeOfExpression sizeOfExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitStackAllocExpression(StackAllocExpression stackAllocExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSwitchExpression(SwitchExpression switchExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSwitchExpressionSection(SwitchExpressionSection switchExpressionSection)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSwitchSection(SwitchSection switchSection)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSwitchStatement(SwitchStatement switchStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitSyntaxTree(SyntaxTree syntaxTree)
    {
        var c = syntaxTree.Children.Single();
        return c.AcceptVisitor(this);
    }

    public INode? VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression)
    {
        // TODO: check if is referencing shader module object (only this case we can simply access member as global references)
        throw new NotImplementedException();
    }

    public INode? VisitThrowExpression(ThrowExpression throwExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitThrowStatement(ThrowStatement throwStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTryCatchStatement(TryCatchStatement tryCatchStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTupleExpression(TupleExpression tupleExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTupleType(TupleAstType tupleType)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTupleTypeElement(TupleTypeElement tupleTypeElement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTypeDeclaration(ICSharpCode.Decompiler.CSharp.Syntax.TypeDeclaration typeDeclaration)
    {
        var nodes = typeDeclaration.Members.Where(m => !m.Name.StartsWith("ILSLWGSL"))
                                           .Select(m => m.AcceptVisitor(this))
                                           .OfType<FunctionDeclaration>();
        return new IR.Module([.. nodes]);
    }

    public INode? VisitTypeOfExpression(TypeOfExpression typeOfExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTypeParameterDeclaration(TypeParameterDeclaration typeParameterDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
    {
        var expr = (IExpression)unaryOperatorExpression.Expression.AcceptVisitor(this)!;
        return unaryOperatorExpression.Operator switch
        {
            UnaryOperatorType.Not => new UnaryLogicalExpression(expr, UnaryLogicalOp.Not),
            UnaryOperatorType.Minus => new UnaryArithmeticExpression(expr, UnaryArithmeticOp.Minus),
            _ => throw new NotSupportedException($"{nameof(VisitUnaryOperatorExpression)} does not support {unaryOperatorExpression}")
        };
    }

    public INode? VisitUncheckedExpression(UncheckedExpression uncheckedExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUncheckedStatement(UncheckedStatement uncheckedStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUndocumentedExpression(UndocumentedExpression undocumentedExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUnsafeStatement(UnsafeStatement unsafeStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUsingAliasDeclaration(UsingAliasDeclaration usingAliasDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUsingDeclaration(UsingDeclaration usingDeclaration)
    {
        throw new NotImplementedException();
    }

    public INode? VisitUsingStatement(UsingStatement usingStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
    {
        // TODO: handle multiple variable declaration 
        // TODO: proper handling of variable type
        var v = variableDeclarationStatement.Variables.Single();
        var varDecl = new VariableDeclaration(DeclarationScope.Function, v.Name, ((IR.Declaration.IType)variableDeclarationStatement.Type.AcceptVisitor(this)), []);
        Symbols.Add(varDecl.Name, varDecl);
        var c = v.GetChildByRole(Roles.Expression);
        if (c is not null)
        {
            varDecl.Initializer = (IExpression)c.AcceptVisitor(this);
        }
        return new VariableOrValueStatement(varDecl);
    }

    public INode? VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        throw new NotImplementedException();
    }

    public INode? VisitWhileStatement(ICSharpCode.Decompiler.CSharp.Syntax.WhileStatement whileStatement)
    {
        return new IR.Statement.WhileStatement(
            Attributes: [],
            (IExpression)whileStatement.Condition.AcceptVisitor(this)!,
            (IStatement)whileStatement.EmbeddedStatement.AcceptVisitor(this)!
        );
    }

    public INode? VisitWithInitializerExpression(WithInitializerExpression withInitializerExpression)
    {
        throw new NotImplementedException();
    }

    public INode? VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement)
    {
        throw new NotImplementedException();
    }

    public INode? VisitYieldReturnStatement(YieldReturnStatement yieldReturnStatement)
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

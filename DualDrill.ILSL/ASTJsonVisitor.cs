using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

internal class ASTJsonVisitor : IAstVisitor<JsonNode>
{
    private JsonNode VisitDefault(AstNode node)
    {
        return new JsonObject(
            [
                new("type", Enum.GetName(node.NodeType)),
                new("children", new JsonArray( node.Children.Select(c => c.AcceptVisitor(this)).ToArray()))
            ]
        );

    }

    public JsonNode VisitAccessor(Accessor accessor)
    {
        return VisitDefault(accessor);
    }

    public JsonNode VisitAnonymousMethodExpression(AnonymousMethodExpression anonymousMethodExpression)
    {
        return VisitDefault(anonymousMethodExpression);
    }

    public JsonNode VisitAnonymousTypeCreateExpression(AnonymousTypeCreateExpression anonymousTypeCreateExpression)
    {
        return VisitDefault(anonymousTypeCreateExpression);
    }

    public JsonNode VisitArrayCreateExpression(ArrayCreateExpression arrayCreateExpression)
    {
        return VisitDefault(arrayCreateExpression);
    }

    public JsonNode VisitArrayInitializerExpression(ArrayInitializerExpression arrayInitializerExpression)
    {
        return VisitDefault(arrayInitializerExpression);
    }

    public JsonNode VisitArraySpecifier(ArraySpecifier arraySpecifier)
    {
        return VisitDefault(arraySpecifier);
    }

    public JsonNode VisitAsExpression(AsExpression asExpression)
    {
        return VisitDefault(asExpression);
    }

    public JsonNode VisitAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        return VisitDefault(assignmentExpression);
    }

    public JsonNode VisitAttribute(ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
    {
        return VisitDefault(attribute);
    }

    public JsonNode VisitAttributeSection(AttributeSection attributeSection)
    {
        return VisitDefault(attributeSection);
    }

    public JsonNode VisitBaseReferenceExpression(BaseReferenceExpression baseReferenceExpression)
    {
        return VisitDefault(baseReferenceExpression);
    }

    public JsonNode VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression)
    {
        return VisitDefault(binaryOperatorExpression);
    }

    public JsonNode VisitBlockStatement(BlockStatement blockStatement)
    {
        return VisitDefault(blockStatement);
    }

    public JsonNode VisitBreakStatement(BreakStatement breakStatement)
    {
        return VisitDefault(breakStatement);
    }

    public JsonNode VisitCaseLabel(CaseLabel caseLabel)
    {
        return VisitDefault(caseLabel);
    }

    public JsonNode VisitCastExpression(CastExpression castExpression)
    {
        return VisitDefault(castExpression);
    }

    public JsonNode VisitCatchClause(CatchClause catchClause)
    {
        return VisitDefault(catchClause);
    }

    public JsonNode VisitCheckedExpression(CheckedExpression checkedExpression)
    {
        return VisitDefault(checkedExpression);
    }

    public JsonNode VisitCheckedStatement(CheckedStatement checkedStatement)
    {
        return VisitDefault(checkedStatement);
    }

    public JsonNode VisitComment(Comment comment)
    {
        return VisitDefault(comment);
    }

    public JsonNode VisitComposedType(ComposedType composedType)
    {
        return VisitDefault(composedType);
    }

    public JsonNode VisitConditionalExpression(ConditionalExpression conditionalExpression)
    {
        return VisitDefault(conditionalExpression);
    }

    public JsonNode VisitConstraint(Constraint constraint)
    {
        return VisitDefault(constraint);
    }

    public JsonNode VisitConstructorDeclaration(ConstructorDeclaration constructorDeclaration)
    {
        return VisitDefault(constructorDeclaration);
    }

    public JsonNode VisitConstructorInitializer(ConstructorInitializer constructorInitializer)
    {
        return VisitDefault(constructorInitializer);
    }

    public JsonNode VisitContinueStatement(ContinueStatement continueStatement)
    {
        return VisitDefault(continueStatement);
    }

    public JsonNode VisitCSharpTokenNode(CSharpTokenNode cSharpTokenNode)
    {
        return VisitDefault(cSharpTokenNode);
    }

    public JsonNode VisitCustomEventDeclaration(CustomEventDeclaration customEventDeclaration)
    {
        return VisitDefault(customEventDeclaration);
    }

    public JsonNode VisitDeclarationExpression(DeclarationExpression declarationExpression)
    {
        return VisitDefault(declarationExpression);
    }

    public JsonNode VisitDefaultValueExpression(DefaultValueExpression defaultValueExpression)
    {
        return VisitDefault(defaultValueExpression);
    }

    public JsonNode VisitDelegateDeclaration(DelegateDeclaration delegateDeclaration)
    {
        return VisitDefault(delegateDeclaration);
    }

    public JsonNode VisitDestructorDeclaration(DestructorDeclaration destructorDeclaration)
    {
        return VisitDefault(destructorDeclaration);
    }

    public JsonNode VisitDirectionExpression(DirectionExpression directionExpression)
    {
        return VisitDefault(directionExpression);
    }

    public JsonNode VisitDocumentationReference(DocumentationReference documentationReference)
    {
        return VisitDefault(documentationReference);
    }

    public JsonNode VisitDoWhileStatement(DoWhileStatement doWhileStatement)
    {
        return VisitDefault(doWhileStatement);
    }

    public JsonNode VisitEmptyStatement(EmptyStatement emptyStatement)
    {
        return VisitDefault(emptyStatement);
    }

    public JsonNode VisitEnumMemberDeclaration(EnumMemberDeclaration enumMemberDeclaration)
    {
        return VisitDefault(enumMemberDeclaration);
    }

    public JsonNode VisitErrorNode(AstNode errorNode)
    {
        return VisitDefault(errorNode);
    }

    public JsonNode VisitEventDeclaration(EventDeclaration eventDeclaration)
    {
        return VisitDefault(eventDeclaration);
    }

    public JsonNode VisitExpressionStatement(ExpressionStatement expressionStatement)
    {
        return VisitDefault(expressionStatement);
    }

    public JsonNode VisitExternAliasDeclaration(ExternAliasDeclaration externAliasDeclaration)
    {
        return VisitDefault(externAliasDeclaration);
    }

    public JsonNode VisitFieldDeclaration(FieldDeclaration fieldDeclaration)
    {
        return VisitDefault(fieldDeclaration);
    }

    public JsonNode VisitFixedFieldDeclaration(FixedFieldDeclaration fixedFieldDeclaration)
    {
        return VisitDefault(fixedFieldDeclaration);
    }

    public JsonNode VisitFixedStatement(FixedStatement fixedStatement)
    {
        return VisitDefault(fixedStatement);
    }

    public JsonNode VisitFixedVariableInitializer(FixedVariableInitializer fixedVariableInitializer)
    {
        return VisitDefault(fixedVariableInitializer);
    }

    public JsonNode VisitForeachStatement(ForeachStatement foreachStatement)
    {
        return VisitDefault(foreachStatement);
    }

    public JsonNode VisitForStatement(ForStatement forStatement)
    {
        return VisitDefault(forStatement);
    }

    public JsonNode VisitFunctionPointerType(FunctionPointerAstType functionPointerType)
    {
        return VisitDefault(functionPointerType);
    }

    public JsonNode VisitGotoCaseStatement(GotoCaseStatement gotoCaseStatement)
    {
        return VisitDefault(gotoCaseStatement);
    }

    public JsonNode VisitGotoDefaultStatement(GotoDefaultStatement gotoDefaultStatement)
    {
        return VisitDefault(gotoDefaultStatement);
    }

    public JsonNode VisitGotoStatement(GotoStatement gotoStatement)
    {
        return VisitDefault(gotoStatement);
    }

    public JsonNode VisitIdentifier(Identifier identifier)
    {
        return VisitDefault(identifier);
    }

    public JsonNode VisitIdentifierExpression(IdentifierExpression identifierExpression)
    {
        return VisitDefault(identifierExpression);
    }

    public JsonNode VisitIfElseStatement(IfElseStatement ifElseStatement)
    {
        return VisitDefault(ifElseStatement);
    }

    public JsonNode VisitIndexerDeclaration(IndexerDeclaration indexerDeclaration)
    {
        return VisitDefault(indexerDeclaration);
    }

    public JsonNode VisitIndexerExpression(IndexerExpression indexerExpression)
    {
        return VisitDefault(indexerExpression);
    }

    public JsonNode VisitInterpolatedStringExpression(InterpolatedStringExpression interpolatedStringExpression)
    {
        return VisitDefault(interpolatedStringExpression);
    }

    public JsonNode VisitInterpolatedStringText(InterpolatedStringText interpolatedStringText)
    {
        return VisitDefault(interpolatedStringText);
    }

    public JsonNode VisitInterpolation(Interpolation interpolation)
    {
        return VisitDefault(interpolation);
    }

    public JsonNode VisitInvocationExpression(InvocationExpression invocationExpression)
    {
        return VisitDefault(invocationExpression);
    }

    public JsonNode VisitInvocationType(InvocationAstType invocationType)
    {
        return VisitDefault(invocationType);
    }

    public JsonNode VisitIsExpression(IsExpression isExpression)
    {
        return VisitDefault(isExpression);
    }

    public JsonNode VisitLabelStatement(LabelStatement labelStatement)
    {
        return VisitDefault(labelStatement);
    }

    public JsonNode VisitLambdaExpression(LambdaExpression lambdaExpression)
    {
        return VisitDefault(lambdaExpression);
    }

    public JsonNode VisitLocalFunctionDeclarationStatement(LocalFunctionDeclarationStatement localFunctionDeclarationStatement)
    {
        return VisitDefault(localFunctionDeclarationStatement);
    }

    public JsonNode VisitLockStatement(LockStatement lockStatement)
    {
        return VisitDefault(lockStatement);
    }

    public JsonNode VisitMemberReferenceExpression(MemberReferenceExpression memberReferenceExpression)
    {
        return VisitDefault(memberReferenceExpression);
    }

    public JsonNode VisitMemberType(MemberType memberType)
    {
        return VisitDefault(memberType);
    }

    public JsonNode VisitMethodDeclaration(MethodDeclaration methodDeclaration)
    {
        return VisitDefault(methodDeclaration);
    }

    public JsonNode VisitNamedArgumentExpression(NamedArgumentExpression namedArgumentExpression)
    {
        return VisitDefault(namedArgumentExpression);
    }

    public JsonNode VisitNamedExpression(NamedExpression namedExpression)
    {
        return VisitDefault(namedExpression);
    }

    public JsonNode VisitNamespaceDeclaration(NamespaceDeclaration namespaceDeclaration)
    {
        return VisitDefault(namespaceDeclaration);
    }

    public JsonNode VisitNullNode(AstNode nullNode)
    {
        return VisitDefault(nullNode);
    }

    public JsonNode VisitNullReferenceExpression(NullReferenceExpression nullReferenceExpression)
    {
        return VisitDefault(nullReferenceExpression);
    }

    public JsonNode VisitObjectCreateExpression(ObjectCreateExpression objectCreateExpression)
    {
        return VisitDefault(objectCreateExpression);
    }

    public JsonNode VisitOperatorDeclaration(OperatorDeclaration operatorDeclaration)
    {
        return VisitDefault(operatorDeclaration);
    }

    public JsonNode VisitOutVarDeclarationExpression(OutVarDeclarationExpression outVarDeclarationExpression)
    {
        return VisitDefault(outVarDeclarationExpression);
    }

    public JsonNode VisitParameterDeclaration(ParameterDeclaration parameterDeclaration)
    {
        return VisitDefault(parameterDeclaration);
    }

    public JsonNode VisitParenthesizedExpression(ParenthesizedExpression parenthesizedExpression)
    {
        return VisitDefault(parenthesizedExpression);
    }

    public JsonNode VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignation parenthesizedVariableDesignation)
    {
        return VisitDefault(parenthesizedVariableDesignation);
    }

    public JsonNode VisitPatternPlaceholder(AstNode placeholder, Pattern pattern)
    {
        return VisitDefault(placeholder);
    }

    public JsonNode VisitPointerReferenceExpression(PointerReferenceExpression pointerReferenceExpression)
    {
        return VisitDefault(pointerReferenceExpression);
    }

    public JsonNode VisitPreProcessorDirective(PreProcessorDirective preProcessorDirective)
    {
        return VisitDefault(preProcessorDirective);
    }

    public JsonNode VisitPrimitiveExpression(PrimitiveExpression primitiveExpression)
    {
        return VisitDefault(primitiveExpression);
    }

    public JsonNode VisitPrimitiveType(PrimitiveType primitiveType)
    {
        return VisitDefault(primitiveType);
    }

    public JsonNode VisitPropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        return VisitDefault(propertyDeclaration);
    }

    public JsonNode VisitQueryContinuationClause(QueryContinuationClause queryContinuationClause)
    {
        return VisitDefault(queryContinuationClause);
    }

    public JsonNode VisitQueryExpression(QueryExpression queryExpression)
    {
        return VisitDefault(queryExpression);
    }

    public JsonNode VisitQueryFromClause(QueryFromClause queryFromClause)
    {
        return VisitDefault(queryFromClause);
    }

    public JsonNode VisitQueryGroupClause(QueryGroupClause queryGroupClause)
    {
        return VisitDefault(queryGroupClause);
    }

    public JsonNode VisitQueryJoinClause(QueryJoinClause queryJoinClause)
    {
        return VisitDefault(queryJoinClause);
    }

    public JsonNode VisitQueryLetClause(QueryLetClause queryLetClause)
    {
        return VisitDefault(queryLetClause);
    }

    public JsonNode VisitQueryOrderClause(QueryOrderClause queryOrderClause)
    {
        return VisitDefault(queryOrderClause);
    }

    public JsonNode VisitQueryOrdering(QueryOrdering queryOrdering)
    {
        return VisitDefault(queryOrdering);
    }

    public JsonNode VisitQuerySelectClause(QuerySelectClause querySelectClause)
    {
        return VisitDefault(querySelectClause);
    }

    public JsonNode VisitQueryWhereClause(QueryWhereClause queryWhereClause)
    {
        return VisitDefault(queryWhereClause);
    }

    public JsonNode VisitRecursivePatternExpression(RecursivePatternExpression recursivePatternExpression)
    {
        return VisitDefault(recursivePatternExpression);
    }

    public JsonNode VisitReturnStatement(ReturnStatement returnStatement)
    {
        return VisitDefault(returnStatement);
    }

    public JsonNode VisitSimpleType(SimpleType simpleType)
    {
        return VisitDefault(simpleType);
    }

    public JsonNode VisitSingleVariableDesignation(SingleVariableDesignation singleVariableDesignation)
    {
        return VisitDefault(singleVariableDesignation);
    }

    public JsonNode VisitSizeOfExpression(SizeOfExpression sizeOfExpression)
    {
        return VisitDefault(sizeOfExpression);
    }

    public JsonNode VisitStackAllocExpression(StackAllocExpression stackAllocExpression)
    {
        return VisitDefault(stackAllocExpression);
    }

    public JsonNode VisitSwitchExpression(SwitchExpression switchExpression)
    {
        return VisitDefault(switchExpression);
    }

    public JsonNode VisitSwitchExpressionSection(SwitchExpressionSection switchExpressionSection)
    {
        return VisitDefault(switchExpressionSection);
    }

    public JsonNode VisitSwitchSection(SwitchSection switchSection)
    {
        return VisitDefault(switchSection);
    }

    public JsonNode VisitSwitchStatement(SwitchStatement switchStatement)
    {
        return VisitDefault(switchStatement);
    }

    public JsonNode VisitSyntaxTree(SyntaxTree syntaxTree)
    {
        return VisitDefault(syntaxTree);
    }

    public JsonNode VisitThisReferenceExpression(ThisReferenceExpression thisReferenceExpression)
    {
        return VisitDefault(thisReferenceExpression);
    }

    public JsonNode VisitThrowExpression(ThrowExpression throwExpression)
    {
        return VisitDefault(throwExpression);
    }

    public JsonNode VisitThrowStatement(ThrowStatement throwStatement)
    {
        return VisitDefault(throwStatement);
    }

    public JsonNode VisitTryCatchStatement(TryCatchStatement tryCatchStatement)
    {
        return VisitDefault(tryCatchStatement);
    }

    public JsonNode VisitTupleExpression(TupleExpression tupleExpression)
    {
        return VisitDefault(tupleExpression);
    }

    public JsonNode VisitTupleType(TupleAstType tupleType)
    {
        return VisitDefault(tupleType);
    }

    public JsonNode VisitTupleTypeElement(TupleTypeElement tupleTypeElement)
    {
        return VisitDefault(tupleTypeElement);
    }

    public JsonNode VisitTypeDeclaration(TypeDeclaration typeDeclaration)
    {
        return VisitDefault(typeDeclaration);
    }

    public JsonNode VisitTypeOfExpression(TypeOfExpression typeOfExpression)
    {
        return VisitDefault(typeOfExpression);
    }

    public JsonNode VisitTypeParameterDeclaration(TypeParameterDeclaration typeParameterDeclaration)
    {
        return VisitDefault(typeParameterDeclaration);
    }

    public JsonNode VisitTypeReferenceExpression(TypeReferenceExpression typeReferenceExpression)
    {
        return VisitDefault(typeReferenceExpression);
    }

    public JsonNode VisitUnaryOperatorExpression(UnaryOperatorExpression unaryOperatorExpression)
    {
        return VisitDefault(unaryOperatorExpression);
    }

    public JsonNode VisitUncheckedExpression(UncheckedExpression uncheckedExpression)
    {
        return VisitDefault(uncheckedExpression);
    }

    public JsonNode VisitUncheckedStatement(UncheckedStatement uncheckedStatement)
    {
        return VisitDefault(uncheckedStatement);
    }

    public JsonNode VisitUndocumentedExpression(UndocumentedExpression undocumentedExpression)
    {
        return VisitDefault(undocumentedExpression);
    }

    public JsonNode VisitUnsafeStatement(UnsafeStatement unsafeStatement)
    {
        return VisitDefault(unsafeStatement);
    }

    public JsonNode VisitUsingAliasDeclaration(UsingAliasDeclaration usingAliasDeclaration)
    {
        return VisitDefault(usingAliasDeclaration);
    }

    public JsonNode VisitUsingDeclaration(UsingDeclaration usingDeclaration)
    {
        return VisitDefault(usingDeclaration);
    }

    public JsonNode VisitUsingStatement(UsingStatement usingStatement)
    {
        return VisitDefault(usingStatement);
    }

    public JsonNode VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
    {
        return VisitDefault(variableDeclarationStatement);
    }

    public JsonNode VisitVariableInitializer(VariableInitializer variableInitializer)
    {
        return VisitDefault(variableInitializer);
    }

    public JsonNode VisitWhileStatement(WhileStatement whileStatement)
    {
        return VisitDefault(whileStatement);
    }

    public JsonNode VisitWithInitializerExpression(WithInitializerExpression withInitializerExpression)
    {
        return VisitDefault(withInitializerExpression);
    }

    public JsonNode VisitYieldBreakStatement(YieldBreakStatement yieldBreakStatement)
    {
        return VisitDefault(yieldBreakStatement);
    }

    public JsonNode VisitYieldReturnStatement(YieldReturnStatement yieldReturnStatement)
    {
        return VisitDefault(yieldReturnStatement);
    }
}

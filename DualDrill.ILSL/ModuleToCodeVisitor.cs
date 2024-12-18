using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.ILSL;

public sealed class ModuleToCodeVisitor(IndentStringWriter Writer)
    : IDeclarationVisitor<ValueTask>
    , IStatementVisitor<ValueTask>
    , IExpressionVisitor<ValueTask>
    , ITypeReferenceVisitor<ValueTask>

{
    async ValueTask WriteAttributeAsync(IShaderAttribute attr, CancellationToken cancellation = default)
    {
        switch (attr)
        {
            case ShaderMethodAttribute:
                break;
            case FragmentAttribute:
                Writer.Write("@");
                Writer.Write("fragment");
                break;
            case VertexAttribute:
                Writer.Write("@");
                Writer.Write("vertex");
                break;
            case BuiltinAttribute b:
                Writer.Write("@");
                Writer.Write("builtin(");
                Writer.Write(Enum.GetName(b.Slot));
                Writer.Write(")");
                break;
            case LocationAttribute a:
                Writer.Write("@");
                Writer.Write("location(");
                Writer.Write(a.Binding);
                Writer.Write(")");
                break;
            default:
                throw new NotSupportedException($"WriteAttribute not support {attr}");
        }
    }

    async ValueTask WriteAttributesAsync(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            await WriteAttributeAsync(a);
            if (newLine)
            {
                Writer.WriteLine();
            }
            else
            {
                Writer.Write(' ');
            }
        }
    }

    public async ValueTask VisitFunction(FunctionDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes, true);
        Writer.Write("fn ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        foreach (var p in decl.Parameters)
        {
            await p.AcceptVisitor(this);
        }
        Writer.Write(")");
        Writer.Write(" -> ");
        await WriteAttributesAsync(decl.Return.Attributes);
        if (decl.Return.Type is not null)
        {
            await VisitTypeReference(decl.Return.Type);
        }
        Writer.WriteLine();
        if (decl.Body is not null)
        {
            await decl.Body.AcceptVisitor(this);
        }
        Writer.WriteLine();
        Writer.WriteLine();
    }

    public async ValueTask VisitParameter(ParameterDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await VisitTypeReference(decl.Type);
        Writer.Write(", ");
    }

    public ValueTask VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public async ValueTask VisitVariable(VariableDeclaration decl)
    {
        foreach (var a in decl.Attributes)
        {
            switch (a)
            {
                case GroupAttribute g:
                    Writer.Write("@group(");
                    Writer.Write(g.Binding);
                    Writer.Write(") ");
                    break;
                case BindingAttribute b:
                    Writer.Write("@binding(");
                    Writer.Write(b.Binding);
                    Writer.Write(") ");
                    break;
                case UniformAttribute u:
                    Writer.Write("var<uniform> ");
                    break;
                default:
                    throw new NotSupportedException($"VisitVariableDeclaration attribute {a} not support ");
            }
        }
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await VisitTypeReference(decl.Type);

        Writer.WriteLine(";");
        Writer.WriteLine();
    }

    public async ValueTask VisitReturn(ReturnStatement stmt)
    {
        Writer.Write("return ");
        if (stmt.Expr is not null)
        {
            await stmt.Expr.AcceptVisitor(this);
        }
    }

    public async ValueTask VisitVariableOrValue(VariableOrValueStatement stmt)
    {
        Writer.Write("var ");
        Writer.Write(stmt.Variable.Name);
        Writer.Write(" : ");
        await VisitTypeReference(stmt.Variable.Type);
        if (stmt.Variable.Initializer is not null)
        {
            Writer.Write(" = ");
            await stmt.Variable.Initializer.AcceptVisitor(this);
        }
    }

    public async ValueTask VisitCompound(CompoundStatement stmt)
    {
        Writer.WriteLine('{');
        Writer.Indent();
        foreach (var s in stmt.Statements)
        {
            await s.AcceptVisitor(this);
        }
        Writer.Unindent();
        Writer.WriteLine('}');
    }

    public async ValueTask VisitIf(IfStatement stmt)
    {
        Writer.Write("if (");
        await stmt.Expr.AcceptVisitor(this);
        Writer.WriteLine(") {");
        if (stmt.TrueBody.Statements.Length > 0)
        {
            Writer.Indent();
            await stmt.TrueBody.AcceptVisitor(this);
            Writer.Unindent();
        }
        Writer.WriteLine("} else {");
        if (stmt.FalseBody.Statements.Length > 0)
        {
            Writer.Indent();
            await stmt.FalseBody.AcceptVisitor(this);
            Writer.Unindent();
        }
        Writer.WriteLine("}");
    }

    public async ValueTask VisitWhile(WhileStatement stmt)
    {
        Writer.Write("while ");
        await stmt.Expr.AcceptVisitor(this);
        Writer.WriteLine();
        await stmt.Statement.AcceptVisitor(this);
    }

    public async ValueTask VisitBreak(BreakStatement stmt)
    {
        Writer.Write("break");
    }

    public async ValueTask VisitFor(ForStatement stmt)
    {
        Writer.Write("for (");
        var header = stmt.ForHeader;
        if (header.Init != null)
        {
            await header.Init.AcceptVisitor(this);
        }
        Writer.Write("; ");
        if (header.Expr != null)
        {
            await header.Expr.AcceptVisitor(this);
        }
        Writer.Write("; ");
        if (header.Update != null)
        {
            await header.Update.AcceptVisitor(this);
        }
        Writer.WriteLine(')');
        await stmt.Statement.AcceptVisitor(this);
    }

    public async ValueTask VisitSimpleAssignment(SimpleAssignmentStatement stmt)
    {
        var op = stmt.Op switch
        {
            AssignmentOp.Assign => "=",
            AssignmentOp.Add => "+=",
            AssignmentOp.Subtract => "-=",
            AssignmentOp.Multiply => "*=",
            AssignmentOp.Divide => "/=",
            AssignmentOp.Modulus => "%=",
            AssignmentOp.BitwiseAnd => "&=",
            AssignmentOp.BitwiseOr => "|=",
            AssignmentOp.ExclusiveOr => "^=",
            AssignmentOp.ShiftLeft => "<<=",
            AssignmentOp.ShiftRight => ">>=",
            _ => throw new NotSupportedException()
        };

        await stmt.L.AcceptVisitor(this);
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await stmt.R.AcceptVisitor(this);
    }

    public async ValueTask VisitPhonyAssignment(PhonyAssignmentStatement stmt)
    {
        Writer.Write("_ = ");
        await stmt.Expr.AcceptVisitor(this);
    }

    public async ValueTask VisitIncrement(IncrementStatement stmt)
    {
        await stmt.Expr.AcceptVisitor(this);
        Writer.Write("++");
    }

    public async ValueTask VisitDecrement(DecrementStatement stmt)
    {
        await stmt.Expr.AcceptVisitor(this);
        Writer.Write("--");
    }

    public async ValueTask VisitFunctionCallExpression(FunctionCallExpression expr)
    {
        Writer.Write(expr.Callee.Name);
        Writer.Write('(');
        bool firstArgument = true;
        foreach (var a in expr.Arguments)
        {
            if (!firstArgument)
            {
                Writer.Write(", ");
            }
            firstArgument = false;
            await a.AcceptVisitor(this);
        }
        Writer.Write(')');
    }

    public async ValueTask VisitBinaryArithmeticExpression(BinaryArithmeticExpression expr)
    {
        Writer.Write("( ");
        await expr.L.AcceptVisitor(this);
        Writer.Write(") ");
        Writer.Write(BinaryArithmetic.GetInstance(expr.Op).Name);
        Writer.Write("( ");
        await expr.R.AcceptVisitor(this);
        Writer.Write(")");
    }



    public async ValueTask VisitLiteralValueExpression(LiteralValueExpression expr)
    {
        var code = expr.Literal switch
        {
            BoolLiteral l => l.Value ? "true" : "false",
            I32Literal { Value: var value } => value + "i",
            I64Literal { Value: var value } => value + "i",
            U32Literal { Value: var value } => value + "u",
            U64Literal { Value: var value } => value + "u",
            F32Literal { Value: var value } => value + "f",
            F64Literal { Value: var value } => value + "f",
            _ => throw new NotSupportedException($"{nameof(VisitLiteralValueExpression)} not support {expr}")
        };
        Writer.Write(code);
    }

    public async ValueTask VisitVariableIdentifierExpression(VariableIdentifierExpression expr)
    {
        Writer.Write(expr.Variable.Name);
    }

    public async ValueTask VisitFormalParameterExpression(FormalParameterExpression expr)
    {
        Writer.Write(expr.Parameter.Name);
    }

    public async ValueTask VisitBinaryBitwiseExpression(BinaryBitwiseExpression expr)
    {
        await expr.L.AcceptVisitor(this);
        var op = expr.Op switch
        {
            BinaryBitwiseOp.BitwiseOr => "|",
            BinaryBitwiseOp.BitwiseAnd => "&",
            BinaryBitwiseOp.BitwiseExclusiveOr => "^",
            _ => throw new NotSupportedException()
        };
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await expr.R.AcceptVisitor(this);

    }
    public async ValueTask VisitBinaryRelationalExpression(BinaryRelationalExpression expr)
    {
        await expr.L.AcceptVisitor(this);
        var op = expr.Op switch
        {
            BinaryRelationalOp.LessThan => "<",
            BinaryRelationalOp.GreaterThan => ">",
            BinaryRelationalOp.LessThanEqual => "<=",
            BinaryRelationalOp.GreaterThanEqual => ">=",
            BinaryRelationalOp.Equal => "==",
            BinaryRelationalOp.NotEqual => "!=",
            _ => throw new NotSupportedException()
        };
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await expr.R.AcceptVisitor(this);
    }

    public async ValueTask VisitBinaryLogicalExpression(BinaryLogicalExpression expr)
    {
        await expr.L.AcceptVisitor(this);
        var op = expr.Op switch
        {
            BinaryLogicalOp.And => "&&",
            BinaryLogicalOp.Or => "||",
            _ => throw new NotSupportedException()
        };
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await expr.R.AcceptVisitor(this);
    }

    public async ValueTask VisitUnaryLogicalExpression(UnaryLogicalExpression expr)
    {
        var op = expr.Op switch
        {
            UnaryLogicalOp.Not => "!",
            _ => throw new NotSupportedException()
        };
        Writer.Write(op);
        Writer.Write("(");
        await expr.Expr.AcceptVisitor(this);
        Writer.Write(")");
    }

    public async ValueTask VisitUnaryArithmeticExpression(UnaryArithmeticExpression expr)
    {
        var op = expr.Op switch
        {
            UnaryArithmeticOp.Minus => "-",
            _ => throw new NotSupportedException()
        };
        Writer.Write(op);
        await expr.Expr.AcceptVisitor(this);
    }

    public async ValueTask VisitParenthesizedExpression(ParenthesizedExpression expr)
    {
        Writer.Write("(");
        await expr.Expr.AcceptVisitor(this);
        Writer.Write(")");
    }

    public async ValueTask AppendSemicolon(ValueTask task)
    {
        await task;
        Writer.WriteLine(';');
    }

    public async ValueTask VisitVectorSwizzleAccessExpression(VectorSwizzleAccessExpression expr)
    {
        await expr.Base.AcceptVisitor(this);
        Writer.Write('.');
        foreach (SwizzleComponent c in expr.Components)
        {
            Writer.Write(c);
        }
    }

    public async ValueTask VisitStructure(StructureDeclaration decl)
    {
        Writer.Write("struct ");
        Writer.Write(decl.Name);
        Writer.WriteLine(" {");
        Writer.Indent();
        foreach (var m in decl.Members)
        {
            await m.AcceptVisitor(this);
        }
        Writer.Unindent();
        Writer.WriteLine("};");
        Writer.WriteLine();
    }

    public async ValueTask VisitMember(MemberDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes, true);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await VisitTypeReference(decl.Type);
        Writer.WriteLine(",");
    }

    public async ValueTask VisitTypeReference(IShaderType type)
    {
        if (type is IVecType { Size: var size, ElementType: var e })
        {
            Writer.Write($"vec{size.Value}<{e.Name}>");
        }
        else
        {
            Writer.Write(type.Name);
        }
    }

    public async ValueTask VisitNamedComponentExpression(NamedComponentExpression expr)
    {
        await expr.Base.AcceptVisitor(this);
        Writer.Write(".");
        Writer.Write(expr.ComponentName);
    }

    public async ValueTask VisitLoop(LoopStatement stmt)
    {
        Writer.WriteLine("loop {");
        Writer.Indent();
        await stmt.Body.AcceptVisitor(this);
        Writer.Unindent();
        Writer.WriteLine("}");
    }

    public async ValueTask VisitSwitch(SwitchStatement stmt)
    {
        Writer.Write("switch (");
        await stmt.Expr.AcceptVisitor(this);
        Writer.WriteLine(") {");
        Writer.Indent();
        foreach (var c in stmt.Cases)
        {
            Writer.Write("case ");
            await c.Label.AcceptVisitor(this);
            Writer.WriteLine(" : {");
            Writer.Indent();
            await c.Body.AcceptVisitor(this);
            Writer.Unindent();
            Writer.WriteLine("}");
        }
        Writer.Write("default : {");
        Writer.Indent();
        await stmt.DefaultCase.AcceptVisitor(this);
        Writer.Unindent();
        Writer.Write("}");
        Writer.Unindent();
        Writer.WriteLine("}");
    }

    public ValueTask VisitContinue(ContinueStatement stmt)
    {
        Writer.WriteLine("continue;");
        return ValueTask.CompletedTask;
    }
}

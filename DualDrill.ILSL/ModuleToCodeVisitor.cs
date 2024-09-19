using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;

namespace DualDrill.ILSL;

public sealed class WGSLLanguage : ITargetLanguage
{
    public string GetLiteralString(ILiteral literal)
    {
        return literal switch
        {
            BoolLiteral l => l.Value ? "true" : "false",
            IntLiteral<B32> l => l.Value + "i",
            UIntLiteral<B32> l => l.Value + "u",
            FloatLiteral<B32> l => l.Value + "f",
            _ => throw new NotSupportedException($"{nameof(GetLiteralString)} not support {literal}")
        };
    }

    public string GetName(BoolType type) => "bool";


    public string GetName<TBitWidth>(IntType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            IntType<B32> _ => "i32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TBitWidth>(UIntType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            UIntType<B32> _ => "u32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TBitWidth>(FloatType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            FloatType<B16> _ => "f16",
            FloatType<B32> _ => "f32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TSize, TElement>(VecType<TSize, TElement> type)
        where TSize : IRank
        where TElement : IScalarType, new()
    {
        return type switch
        {
            VecType<R4, FloatType<B32>> _ => $"vec4<f32>",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TRow, TCol, TElement>(MatType<TRow, TCol, TElement> type)
        where TRow : IRank
        where TCol : IRank
        where TElement : IScalarType, new()
    {
        throw new NotImplementedException();
    }
}

public sealed class ModuleToCodeVisitor(TextWriter Writer, ITargetLanguage TargetLanguage)
    : IDeclarationVisitor<ValueTask>
    , IStatementVisitor<ValueTask>
    , IExpressionVisitor<ValueTask>

{
    async ValueTask WriteAttributeAsync(IAttribute attr, CancellationToken cancellation = default)
    {
        Writer.Write("@");
        switch (attr)
        {
            case FragmentAttribute:
                Writer.Write("fragment");
                break;
            case VertexAttribute:
                Writer.Write("vertex");
                break;
            case BuiltinAttribute b:
                Writer.Write("builtin(");
                Writer.Write(Enum.GetName(b.Slot));
                Writer.Write(")");
                break;
            case LocationAttribute a:
                Writer.Write("location(");
                Writer.Write(a.Binding);
                Writer.Write(")");
                break;
            default:
                throw new NotSupportedException($"WriteAttribute not support {attr}");
        }
    }

    async ValueTask WriteAttributesAsync(IEnumerable<IAttribute> attributes, bool newLine = false)
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
            await decl.Return.Type.AcceptVisitor(this);
        }
        Writer.WriteLine();
        Writer.WriteLine('{');
        if (decl.Body is not null)
        {
            await decl.Body.AcceptVisitor(this);
        }
        Writer.WriteLine('}');
        Writer.WriteLine();
        Writer.WriteLine();
    }

    public async ValueTask VisitParameter(ParameterDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await decl.Type.AcceptVisitor(this);
        Writer.Write(", ");
    }

    public async ValueTask VisitType(IType type)
    {
        Writer.Write(type.Name);
    }

    public ValueTask VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public ValueTask VisitVariable(VariableDeclaration decl)
    {
        throw new NotImplementedException();
    }

    async ValueTask Indent()
    {
        await Writer.WriteAsync('\t');
    }

    public async ValueTask VisitReturn(ReturnStatement stmt)
    {
        Writer.Write("return ");
        if (stmt.Expr is not null)
        {
            await stmt.Expr.AcceptVisitor(this);
        }
        Writer.WriteLine(";");
    }

    public async ValueTask VisitVariableOrValue(VariableOrValueStatement stmt)
    {
        Writer.Write("var ");
        Writer.Write(stmt.Variable.Name);
        Writer.Write(" : ");
        await stmt.Variable.Type.AcceptVisitor(this);
        if (stmt.Variable.Initializer is not null)
        {
            Writer.Write(" = ");
            await stmt.Variable.Initializer.AcceptVisitor(this);
        }
        Writer.WriteLine(";");
    }

    public async ValueTask VisitCompound(CompoundStatement stmt)
    {
        foreach (var s in stmt.Statements)
        {
            await Indent();
            await s.AcceptVisitor(this);
        }
    }

    public async ValueTask VisitIf(IfStatement stmt)
    {
        var ifClause = stmt.IfClause;
        Writer.Write("if ");
        await ifClause.Expr.AcceptVisitor(this);
        Writer.WriteLine();
        Writer.WriteLine('{');
        await ifClause.Statement.AcceptVisitor(this);
        Writer.WriteLine('}');
        foreach (var elseIfClause in stmt.ElseIfClause)
        {
            Writer.WriteLine("else if ");
            await elseIfClause.Expr.AcceptVisitor(this);
            Writer.WriteLine('{');
            await elseIfClause.Statement.AcceptVisitor(this);
            Writer.WriteLine('}');
        }
        if (stmt.Else is not null)
        {
            Writer.WriteLine("else");
            Writer.WriteLine('{');
            await stmt.Else.AcceptVisitor(this);
            Writer.WriteLine('}');
        }
    }

    public async ValueTask VisitWhile(WhileStatement stmt)
    {
        Writer.Write("while ");
        await stmt.Expr.AcceptVisitor(this);
        Writer.WriteLine();
        Writer.WriteLine('{');
        await stmt.Statement.AcceptVisitor(this);
        Writer.WriteLine('}');
    }

    public async ValueTask VisitBreak(BreakStatement stmt)
    {
        Writer.WriteLine("break;");
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
        Writer.WriteLine(';');
    }

    public async ValueTask VisitPhonyAssignment(PhonyAssignmentStatement stmt)
    {
        Writer.Write("_ = ");
        await stmt.Expr.AcceptVisitor(this);
        Writer.WriteLine(';');
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
        await expr.L.AcceptVisitor(this);
        var op = expr.Op switch
        {
            BinaryArithmeticOp.Addition => "+",
            BinaryArithmeticOp.Subtraction => "-",
            BinaryArithmeticOp.Multiplication => "*",
            BinaryArithmeticOp.Division => "/",
            BinaryArithmeticOp.Remainder => "%",
            _ => throw new NotSupportedException()
        };
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await expr.R.AcceptVisitor(this);
    }

    public async ValueTask VisitLiteralValueExpression(LiteralValueExpression expr)
    {
        Writer.Write(TargetLanguage.GetLiteralString(expr.Literal));
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

    public async ValueTask VisitParenthesizedExpression(ParenthesizedExpression expr)
    {
        Writer.Write("(");
        await expr.Expr.AcceptVisitor(this);
        Writer.Write(")");
    }
}

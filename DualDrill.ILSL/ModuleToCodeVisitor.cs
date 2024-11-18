using DualDrill.Common.Nat;
using DualDrill.ILSL.IR;
using DualDrill.ILSL.IR.Declaration;
using DualDrill.ILSL.IR.Expression;
using DualDrill.ILSL.IR.Statement;
using DualDrill.ILSL.Types;

namespace DualDrill.ILSL;

public sealed class WGSLLanguage : ITargetLanguage
{
    public string GetLiteralString(ILiteral literal)
    {
        return literal switch
        {
            BoolLiteral l => l.Value ? "true" : "false",
            IntLiteral<N32> l => l.Value + "i",
            UIntLiteral<N32> l => l.Value + "u",
            FloatLiteral<N32> l => l.Value + "f",
            _ => throw new NotSupportedException($"{nameof(GetLiteralString)} not support {literal}")
        };
    }

    public string GetName(BoolType type) => "bool";


    public string GetName<TBitWidth>(IntType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            IntType<N32> _ => "i32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TBitWidth>(UIntType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            UIntType<N32> _ => "u32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TBitWidth>(FloatType<TBitWidth> type) where TBitWidth : IBitWidth
    {
        return type switch
        {
            FloatType<N16> _ => "f16",
            FloatType<N32> _ => "f32",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName<TSize, TElement>(VecType<TSize, TElement> type)
        where TSize : IRank<TSize>
        where TElement : IScalarType<TElement>
    {
        return type switch
        {
            VecType<N4, FloatType<N32>> _ => $"vec4<f32>",
            _ => throw new NotSupportedException()
        };
    }

    public string GetName(CLSL.Language.Types.MatType type)
    {
        throw new NotImplementedException();
    }
}

public sealed class ModuleToCodeVisitor(IndentStringWriter Writer, ITargetLanguage TargetLanguage)
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
                case StageAttribute _:
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
        var ifClause = stmt.IfClause;
        Writer.Write("if ");
        await ifClause.Expr.AcceptVisitor(this);
        Writer.WriteLine();
        await ifClause.Body.AcceptVisitor(this);
        foreach (var elseIfClause in stmt.ElseIfClause)
        {
            Writer.Write("else if ");
            await elseIfClause.Expr.AcceptVisitor(this);
            Writer.WriteLine();
            await elseIfClause.Body.AcceptVisitor(this);
        }
        if (stmt.Else is not null)
        {
            Writer.WriteLine("else");
            await stmt.Else.AcceptVisitor(this);
        }
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
        await expr.Expr.AcceptVisitor(this);
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

    public async ValueTask VisitTypeReference(IType type)
    {
        Writer.Write(type.Name);
    }

    public async ValueTask VisitNamedComponentExpression(NamedComponentExpression expr)
    {
        await expr.Base.AcceptVisitor(this);
        Writer.Write(".");
        Writer.Write(expr.ComponentName);
    }
}

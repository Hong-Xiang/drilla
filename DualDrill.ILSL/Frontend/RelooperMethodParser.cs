using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.Statement;
using Lokad.ILPack.IL;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Reflection.Metadata;

namespace DualDrill.ILSL.Frontend;

public sealed class RelooperMethodParser : IMethodParser
{
    public CompoundStatement ParseMethodBody(MethodParseContext env, MethodBase method)
    {
        var instructions = method.GetInstructions();
        var compiler = new MethodCompiler(env, method, instructions);
        var cfg = compiler.GetBasicBlocks();
        return new([.. compiler.Compile()]);
    }
}

sealed record class BasicBlock(int Index, int Length, bool HasLoopJump)
{
}

sealed record class Loop(int Index, int Depth)
{
}

sealed record class Block(int Index, int Depth)
{
}



sealed class MethodCompiler(
    MethodParseContext Context,
    MethodBase Method,
    IReadOnlyList<Instruction> Instructions
)
{
    public IReadOnlyList<BasicBlock> GetBasicBlocks()
    {
        Debug.Assert(Instructions.Count > 0);
        var isLeader = new bool[Instructions.Count];
        var hasLoopJump = new bool[Instructions.Count];
        isLeader[0] = true;

        for (var i = 0; i < Instructions.Count; i++)
        {
            var inst = Instructions[i];
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Br:
                case ILOpCode.Br_s:
                    {
                        var offset = (int)(sbyte)inst.Operand;
                        var target = i + offset + 1;
                        isLeader[target] = true;
                        if (offset < 0)
                        {
                            hasLoopJump[target] = true;
                        }
                        break;
                    }
                case ILOpCode.Brtrue:
                case ILOpCode.Brtrue_s:
                case ILOpCode.Brfalse:
                case ILOpCode.Brfalse_s:
                case ILOpCode.Switch:
                    hasLoopJump[i] = true;
                    break;
            }
        }
        var labelCount = isLeader.Count(x => x);
        var length = new int[labelCount];
        var results = new BasicBlock[labelCount];
        {
            var bbIndex = 0;
            var bbLength = 0;
            for (var i = 0; i < Instructions.Count; i++)
            {
                if (isLeader[i])
                {
                    if (bbIndex > 0)
                    {
                        length[bbIndex - 1] = bbLength;
                    }
                    bbLength = 0;
                    bbIndex++;
                }
                bbLength++;
            }
            length[bbIndex - 1] = bbLength;

            bbIndex = 0;
            for (var i = 0; i < Instructions.Count; i++)
            {
                if (isLeader[i])
                {
                    results[bbIndex] = new BasicBlock(i, length[bbIndex], hasLoopJump[i]);
                    bbIndex++;
                }
            }
        }
        return results;
    }

    public IEnumerable<IStatement> Compile()
    {
        var locals = new List<VariableDeclaration>();

        var mb = Method.GetMethodBody();
        var li = 0;
        foreach (var v in mb.LocalVariables)
        {
            var type = Context.Types[v.LocalType];
            var name = $"clsl_local_{li}";
            li++;
            var decl = new VariableDeclaration(CLSL.Language.DeclarationScope.Function, name, type, []);
            locals.Add(decl);
            Context.LocalVariables[name] = decl;
        }

        var stack = new Stack<IExpression>();
        for (var i = 0; i < Instructions.Count; i++)
        {
            var inst = Instructions[i];
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Nop:
                    break;
                case ILOpCode.Ldc_i4:
                    stack.Push(SyntaxFactory.Literal((int)inst.Operand));
                    break;
                case ILOpCode.Ldc_i4_0:
                    stack.Push(SyntaxFactory.Literal(0));
                    break;
                case ILOpCode.Ldc_i4_1:
                    stack.Push(SyntaxFactory.Literal(1));
                    break;
                case ILOpCode.Ldc_i4_2:
                    stack.Push(SyntaxFactory.Literal(2));
                    break;
                case ILOpCode.Ldc_i4_3:
                    stack.Push(SyntaxFactory.Literal(3));
                    break;
                case ILOpCode.Ldc_i4_4:
                    stack.Push(SyntaxFactory.Literal(4));
                    break;
                case ILOpCode.Ldc_i4_5:
                    stack.Push(SyntaxFactory.Literal(5));
                    break;
                case ILOpCode.Ldc_i4_6:
                    stack.Push(SyntaxFactory.Literal(6));
                    break;
                case ILOpCode.Ldc_i4_7:
                    stack.Push(SyntaxFactory.Literal(7));
                    break;
                case ILOpCode.Ldc_i4_8:
                    stack.Push(SyntaxFactory.Literal(8));
                    break;
                case ILOpCode.Ldc_i4_m1:
                    stack.Push(SyntaxFactory.Literal(-1));
                    break;
                case ILOpCode.Ldc_i4_s:
                    stack.Push(SyntaxFactory.Literal((int)(sbyte)inst.Operand));
                    break;
                case ILOpCode.Ldc_i8:
                    stack.Push(SyntaxFactory.Literal((long)inst.Operand));
                    break;
                case ILOpCode.Ldc_r4:
                    stack.Push(SyntaxFactory.Literal((float)inst.Operand));
                    break;
                case ILOpCode.Ldc_r8:
                    stack.Push(SyntaxFactory.Literal((double)inst.Operand));
                    break;
                case ILOpCode.Ldstr:
                    throw new NotSupportedException();
                case ILOpCode.Ldarg_0:
                    stack.Push(new VariableIdentifierExpression(Context.Parameters[0]));
                    break;
                case ILOpCode.Ldarg_1:
                    stack.Push(new VariableIdentifierExpression(Context.Parameters[1]));
                    break;
                case ILOpCode.Ldarg_2:
                    stack.Push(new VariableIdentifierExpression(Context.Parameters[2]));
                    break;
                case ILOpCode.Add:
                    {
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Addition));
                        break;
                    }
                case ILOpCode.Sub:
                    {
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(new BinaryArithmeticExpression(l, r, BinaryArithmeticOp.Subtraction));
                        break;
                    }
                case ILOpCode.Call:
                    {
                        var methodInfo = (MethodInfo)inst.Operand;
                        var callee = Context.Methods[methodInfo];
                        var parameters = callee.Parameters;
                        var args = new IExpression[parameters.Length];
                        for (var j = 0; j < args.Length; j++)
                        {
                            args[j] = stack.Pop();
                        }
                        stack.Push(new FunctionCallExpression(callee, [.. args.Reverse()]));
                        break;
                    }
                case ILOpCode.Newobj:
                    {
                        var ctorInfo = (ConstructorInfo)inst.Operand;
                        var callee = Context.Methods[ctorInfo];
                        var parameters = callee.Parameters;
                        var args = new IExpression[parameters.Length];
                        for (var j = 0; j < args.Length; j++)
                        {
                            args[j] = stack.Pop();
                        }
                        stack.Push(new FunctionCallExpression(callee, [.. args.Reverse()]));

                        break;
                    }
                case ILOpCode.Stloc:
                    {
                        yield return new SimpleAssignmentStatement(
                            new VariableIdentifierExpression(locals[(int)inst.Operand]),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }
                case ILOpCode.Stloc_0:
                    {
                        yield return new SimpleAssignmentStatement(
                            new VariableIdentifierExpression(locals[0]),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }
                case ILOpCode.Ldloc_0:
                    {
                        stack.Push(new VariableIdentifierExpression(locals[0]));
                        break;
                    }
                case ILOpCode.Br_s:
                    {
                        // TODO: actual implementation
                        break;
                    }
                case ILOpCode.Ret:
                    yield return new ReturnStatement(stack.Pop());
                    break;
                default:
                    throw new NotSupportedException($"Unsupported OpCode: {inst.OpCode}");
            }
        }
    }
}


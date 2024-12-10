using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.IR.Statement;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;

namespace DualDrill.ILSL.Frontend;

public sealed class RelooperMethodParser : IMethodParser
{
    public CompoundStatement ParseMethodBody(MethodParseContext env, MethodBase method)
    {
        var compiler = new MethodCompilation(env, method);
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



sealed class MethodCompilation
{
    MethodParseContext Context { get; }
    MethodBase Method { get; }
    ImmutableArray<Instruction> Instructions { get; }

    MethodBody MethodBody { get; }

    IReadOnlyDictionary<LocalVariableInfo, VariableDeclaration> LocalVariables { get; }

    IReadOnlyList<LocalVariableInfo> LocalVariableInfo { get; }
    IReadOnlyList<BasicBlock> BasicBlocks { get; }
    Stack<IExpression>?[] Stacks { get; }
    IReadOnlyList<int> InstructionToBasicBlockLookUp { get; }


    public MethodCompilation(MethodParseContext context, MethodBase method)
    {
        Context = context;
        Method = method;
        MethodBody = method.GetMethodBody() ?? throw new ArgumentException("Method body can not be null", nameof(method));
        Instructions = [.. method.GetInstructions()];
        BasicBlocks = GetBasicBlocks();
        InstructionToBasicBlockLookUp = GetInstructionToBasicBlockLookup();
        LocalVariables = CreateLocalVariableDeclarations();
        LocalVariableInfo = [.. MethodBody.LocalVariables];
        Stacks = new Stack<IExpression>?[BasicBlocks.Count];
    }

    IReadOnlyList<int> GetInstructionToBasicBlockLookup()
    {
        var result = new int[Instructions.Length];
        for (var ib = 0; ib < BasicBlocks.Count; ib++)
        {
            var b = BasicBlocks[ib];
            for (var i = b.Index; i < b.Index + b.Length; i++)
            {
                result[i] = ib;
            }
        }
        return result;
    }

    public IReadOnlyList<BasicBlock> GetBasicBlocks()
    {
        Debug.Assert(Instructions.Length > 0);
        var isLeader = new bool[Instructions.Length];
        var hasLoopJump = new bool[Instructions.Length];
        isLeader[0] = true;

        for (var i = 0; i < Instructions.Length; i++)
        {
            var inst = Instructions[i];
            var offset = 0;
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Br_s:
                case ILOpCode.Brtrue_s:
                case ILOpCode.Brfalse_s:
                    {
                        offset = (sbyte)inst.Operand;
                        break;
                    }
                case ILOpCode.Br:
                case ILOpCode.Brtrue:
                case ILOpCode.Brfalse:
                case ILOpCode.Switch:
                    {
                        offset = (int)inst.Operand;
                    }
                    break;
                default:
                    continue;
            }
            if (i + 1 < Instructions.Length)
            {
                isLeader[i + 1] = true;
            }
            var target = i + (offset == 0 ? 1 : offset);
            isLeader[target] = true;
            if (offset < 0)
            {
                hasLoopJump[target] = true;
            }
        }
        var labelCount = isLeader.Count(x => x);
        var length = new int[labelCount];
        var results = new BasicBlock[labelCount];
        {
            var bbIndex = 0;
            var bbLength = 0;
            for (var i = 0; i < Instructions.Length; i++)
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
            for (var i = 0; i < Instructions.Length; i++)
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
        foreach (var v in MethodBody.LocalVariables)
        {
            var decl = LocalVariables[v];
            if (decl.Type is OpaqueType)
            {
                continue;
            }
            yield return SyntaxFactory.VarDeclaration(decl);
        }

        if (BasicBlocks.Count == 1)
        {
            foreach (var s in CompileBasicBlock(null, new Stack<IExpression>(), BasicBlocks[0]))
            {
                yield return s;
            }
            yield break;
        }
        var bp = new VariableDeclaration(CLSL.Language.DeclarationScope.Function, "clsl_next", ShaderType.I32, []);
        yield return SyntaxFactory.VarDeclaration(bp);
        var cases = new List<SwitchCase>();
        for (var ib = 0; ib < BasicBlocks.Count(); ib++)
        {
            var bb = BasicBlocks[ib];
            cases.Add(new SwitchCase(
                SyntaxFactory.Literal(bb.Index),
                new([.. CompileBasicBlock(bp, Stacks[ib] ??= new Stack<IExpression>(), bb)])
            ));
        }
        yield return new LoopStatement(
            new CompoundStatement([
            new SwitchStatement(SyntaxFactory.VarIdentifier(bp), [.. cases], new CompoundStatement([])) ]));
    }
    public IReadOnlyDictionary<LocalVariableInfo, VariableDeclaration> CreateLocalVariableDeclarations()
    {
        var li = 0;
        var result = new Dictionary<LocalVariableInfo, VariableDeclaration>();
        foreach (var v in MethodBody.LocalVariables)
        {
            var type = Context.Types[v.LocalType];
            var name = $"clsl__v{li}";
            li++;
            var decl = new VariableDeclaration(CLSL.Language.DeclarationScope.Function, name, type, []);
            result.Add(v, decl);
        }
        return result;
    }


    IExpression LocalVariableRef(int index)
           => SyntaxFactory.VarIdentifier(LocalVariables[LocalVariableInfo[index]]);


    IEnumerable<IStatement> CompileBasicBlock(VariableDeclaration? bp, Stack<IExpression> stack, BasicBlock bb)
    {
        var ip = bb.Index - 1;
        var maxIp = bb.Index + bb.Length;
        while (ip < maxIp - 1)
        {
            ip++;
            var inst = Instructions[ip];
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
                    if (Context.Parameters[0].Type is OpaqueType)
                    {
                        break;
                    }
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
                            LocalVariableRef((int)inst.Operand),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }
                case ILOpCode.Stloc_0:
                    {
                        yield return new SimpleAssignmentStatement(
                            LocalVariableRef(0),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }
                case ILOpCode.Stloc_1:
                    {
                        yield return new SimpleAssignmentStatement(
                            LocalVariableRef(1),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }

                case ILOpCode.Ldloc_0:
                    {
                        stack.Push(LocalVariableRef(0));
                        break;
                    }
                case ILOpCode.Ldloc_1:
                    {
                        stack.Push(LocalVariableRef(1));
                        break;
                    }

                case ILOpCode.Br_s:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        var target = ip + Math.Max((int)(sbyte)inst.Operand, 1);
                        yield return new SimpleAssignmentStatement(
                            SyntaxFactory.VarIdentifier(bp),
                            SyntaxFactory.Literal(target),
                            AssignmentOp.Assign
                        );
                        yield return SyntaxFactory.Continue();
                        var ib = InstructionToBasicBlockLookUp[target];
                        Stacks[ib] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case ILOpCode.Brfalse_s:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        var target = ip + Math.Max((int)(sbyte)inst.Operand, 1);
                        yield return new IfStatement(
                            stack.Pop(),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(ip + 1),
                                    AssignmentOp.Assign
                                ),
                                SyntaxFactory.Continue()

                                ]),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target),
                                    AssignmentOp.Assign
                                ),
                                SyntaxFactory.Continue()
                                ]),
                            []
                        );

                        var ib = InstructionToBasicBlockLookUp[target];
                        Stacks[ib] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case ILOpCode.Ret:
                    if (stack.Count != 0)
                    {
                        yield return new ReturnStatement(stack.Pop());
                    }
                    else
                    {
                        yield return SyntaxFactory.Return(null);
                    }
                    break;
                case ILOpCode.Clt:
                    {
                        var insts = Instructions.AsSpan();
                        if (Instructions[(ip + 1)..Math.Min((ip + 3), maxIp)] is
                        [
                            var op0, var op1
                        ] && op0.OpCode.ToILOpCode() == ILOpCode.Ldc_i4_0
                          && op1.OpCode.ToILOpCode() == ILOpCode.Ceq)
                        {
                        }
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(new UnaryLogicalExpression(new BinaryRelationalExpression(l, r, BinaryRelationalOp.LessThan), UnaryLogicalOp.Not));
                        ip += 2;
                        break;
                        throw new NotSupportedException();
                    }

                default:
                    throw new NotSupportedException($"Unsupported OpCode: {inst.OpCode}");
            }
        }
    }
}


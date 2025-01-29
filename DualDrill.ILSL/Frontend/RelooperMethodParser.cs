using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using static DualDrill.CLSL.Language.Types.Signedness;

namespace DualDrill.ILSL.Frontend;

sealed class MethodCompilation
{
    MethodCompilationContext Context { get; }
    MethodBase Method { get; }
    ImmutableArray<Lokad.ILPack.IL.Instruction> Instructions { get; }

    MethodBody MethodBody { get; }

    IReadOnlyList<VariableDeclaration> LocalVariables { get; }
    IReadOnlyList<LocalVariableInfo> LocalVariableInfo { get; }
    IReadOnlyList<BasicBlock> BasicBlocks { get; }
    Stack<IExpression>?[] Stacks { get; }
    int CodeSize { get; }
    IReadOnlyList<int> NextInstructionOffset { get; }

    public MethodCompilation(MethodCompilationContext context, MethodBase method)
    {
        Context = context;
        Method = method;
        MethodBody = method.GetMethodBody() ?? throw new ArgumentException("Method body can not be null", nameof(method));
        LocalVariableInfo = [.. MethodBody.LocalVariables];
        Instructions = [.. method.GetInstructions()];
        CodeSize = MethodBody.GetILAsByteArray().Length;

        var nextOffset = new int[Instructions.Length];
        foreach (var (instIndex, inst) in Instructions.Index())
        {
            nextOffset[instIndex] = instIndex == Instructions.Length - 1 ? CodeSize : Instructions[instIndex + 1].Offset;
        }
        NextInstructionOffset = nextOffset;


        BasicBlocks = GetBasicBlocks();
        LocalVariables = CreateLocalVariableDeclarations();
        Stacks = new Stack<IExpression>?[BasicBlocks.Count];
    }


    public IReadOnlyList<BasicBlock> GetBasicBlocks()
    {
        Debug.Assert(Instructions.Length > 0);
        var lastInst = Instructions[^1];
        var blocks = new BasicBlock?[CodeSize];
        var shouldSkip = new bool[CodeSize];
        blocks[0] = new BasicBlock(0);
        foreach (var (instIndex, inst) in Instructions.Index())
        {
            var nextOffset = NextInstructionOffset[instIndex];
            var jump = 0;
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Br_s:
                case ILOpCode.Brtrue_s:
                case ILOpCode.Brfalse_s:
                case ILOpCode.Bge_un_s:
                case ILOpCode.Beq_s:
                case ILOpCode.Bge_s:
                case ILOpCode.Bgt_s:
                case ILOpCode.Ble_s:
                case ILOpCode.Blt_s:
                case ILOpCode.Bne_un_s:
                    {
                        jump = (sbyte)inst.Operand;
                        if (jump == 0)
                        {
                            shouldSkip[inst.Offset] = true;
                            continue;
                        }
                        break;
                    }
                case ILOpCode.Br:
                case ILOpCode.Brtrue:
                case ILOpCode.Brfalse:
                case ILOpCode.Bge_un:
                case ILOpCode.Beq:
                case ILOpCode.Bge:
                case ILOpCode.Bgt:
                case ILOpCode.Ble:
                case ILOpCode.Blt:
                case ILOpCode.Bne_un:
                    {
                        jump = (int)inst.Operand;
                        break;
                    }
                case ILOpCode.Switch:
                    {
                        throw new NotSupportedException();
                    }
                default:
                    continue;
            }
            //var nextOffset = inst.Offset + inst.OpCode.Size + inst.OpCode.OperandType;
            var jumpOffset = nextOffset + jump;
            if (nextOffset < blocks.Length)
            {
                blocks[nextOffset] ??= new BasicBlock(nextOffset);
            }
            blocks[jumpOffset] ??= new BasicBlock(jumpOffset);
        }
        {
            BasicBlock? bb = null;
            List<IInstruction>? instructions = null;
            foreach (var (ip, inst) in Instructions.Index())
            {
                if (inst.Offset == 11)
                {
                }
                if (blocks[inst.Offset] is { } v)
                {
                    if (bb is not null)
                    {
                        bb.Instructions = [.. instructions];
                    }
                    bb = v;
                    instructions = [];
                }
                if (shouldSkip[inst.Offset])
                {
                    continue;
                }
                Debug.Assert(instructions is not null);
                instructions.Add(CompileInstruction(inst, NextInstructionOffset[ip], blocks));
            }
            bb.Instructions = [.. instructions];
        }
        IReadOnlyList<BasicBlock> result = [.. blocks.OfType<BasicBlock>()];
        foreach (var (index, bb) in result.Index())
        {
            bb.Index = index;
        }
        return result;
    }

    IInstruction CompileInstruction(Lokad.ILPack.IL.Instruction inst, int nextOffset, IReadOnlyList<BasicBlock?> blocks)
    {
        throw new NotImplementedException();
        //IInstruction Branch<TCondition>(int offset)
        //    where TCondition : IBranchCondition
        //{
        //    var target = blocks[nextOffset + offset] ?? throw new NullReferenceException("Target block is null");
        //    return new BranchInstruction<TCondition>(target);
        //}

        //var argumentPositionBase = Method.IsStatic ? 0 : 1; // ParameterInfo.Position need + 1 for hidden this parameter

        //switch (inst.OpCode.ToILOpCode())
        //{
        //    case ILOpCode.Nop:
        //        return new NopInstruction();
        //    case ILOpCode.Beq:
        //        return Branch<Condition.Eq>((int)inst.Operand);
        //    case ILOpCode.Beq_s:
        //        return Branch<Condition.Eq>((sbyte)inst.Operand);
        //    case ILOpCode.Bge:
        //        return Branch<Condition.Ge<S>>((int)inst.Operand);
        //    case ILOpCode.Bge_s:
        //        return Branch<Condition.Ge<S>>((sbyte)inst.Operand);
        //    case ILOpCode.Bge_un:
        //        return Branch<Condition.Ge<U>>((int)inst.Operand);
        //    case ILOpCode.Bge_un_s:
        //        return Branch<Condition.Ge<U>>((sbyte)inst.Operand);
        //    case ILOpCode.Bgt:
        //        return Branch<Condition.Gt<S>>((int)inst.Operand);
        //    case ILOpCode.Bgt_s:
        //        return Branch<Condition.Gt<S>>((sbyte)inst.Operand);
        //    case ILOpCode.Bgt_un:
        //        return Branch<Condition.Gt<U>>((int)inst.Operand);
        //    case ILOpCode.Bgt_un_s:
        //        return Branch<Condition.Gt<U>>((sbyte)inst.Operand);
        //    case ILOpCode.Ble:
        //        return Branch<Condition.Le<S>>((int)inst.Operand);
        //    case ILOpCode.Ble_s:
        //        return Branch<Condition.Le<S>>((sbyte)inst.Operand);
        //    case ILOpCode.Ble_un:
        //        return Branch<Condition.Le<U>>((int)inst.Operand);
        //    case ILOpCode.Ble_un_s:
        //        return Branch<Condition.Le<U>>((sbyte)inst.Operand);
        //    case ILOpCode.Blt:
        //        return Branch<Condition.Lt<S>>((int)inst.Operand);
        //    case ILOpCode.Blt_s:
        //        return Branch<Condition.Lt<S>>((sbyte)inst.Operand);
        //    case ILOpCode.Blt_un:
        //        return Branch<Condition.Lt<U>>((int)inst.Operand);
        //    case ILOpCode.Blt_un_s:
        //        return Branch<Condition.Lt<U>>((sbyte)inst.Operand);
        //    case ILOpCode.Bne_un:
        //        return Branch<Condition.Ne>((int)inst.Operand);
        //    case ILOpCode.Bne_un_s:
        //        return Branch<Condition.Ne>((sbyte)inst.Operand);
        //    case ILOpCode.Br:
        //        return Branch<Condition.Unconditional>((int)inst.Operand);
        //    case ILOpCode.Br_s:
        //        return Branch<Condition.Unconditional>((sbyte)inst.Operand);
        //    case ILOpCode.Brfalse:
        //        return Branch<Condition.False>((int)inst.Operand);
        //    case ILOpCode.Brfalse_s:
        //        return Branch<Condition.False>((sbyte)inst.Operand);
        //    case ILOpCode.Brtrue:
        //        return Branch<Condition.True>((int)inst.Operand);
        //    case ILOpCode.Brtrue_s:
        //        return Branch<Condition.True>((sbyte)inst.Operand);
        //    case ILOpCode.Switch:
        //        throw new NotImplementedException("compile switch instruction is not implemented yet");

        //    case ILOpCode.Ceq:
        //        return new ConditionValueInstruction<Condition.Eq>();

        //    case ILOpCode.And:
        //        return new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseAnd);
        //    case ILOpCode.Or:
        //        return new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseOr);
        //    case ILOpCode.Xor:
        //        return new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseExclusiveOr);

        //    case ILOpCode.Ldc_i4:
        //        return new Const<I32Literal>(new((int)inst.Operand));
        //    case ILOpCode.Ldc_i4_0:
        //        return new Const<I32Literal>(new(0));
        //    case ILOpCode.Ldc_i4_1:
        //        return new Const<I32Literal>(new(1));
        //    case ILOpCode.Ldc_i4_2:
        //        return new Const<I32Literal>(new(2));
        //    case ILOpCode.Ldc_i4_3:
        //        return new Const<I32Literal>(new(3));
        //    case ILOpCode.Ldc_i4_4:
        //        return new Const<I32Literal>(new(4));
        //    case ILOpCode.Ldc_i4_5:
        //        return new Const<I32Literal>(new(5));
        //    case ILOpCode.Ldc_i4_6:
        //        return new Const<I32Literal>(new(6));
        //    case ILOpCode.Ldc_i4_7:
        //        return new Const<I32Literal>(new(7));
        //    case ILOpCode.Ldc_i4_8:
        //        return new Const<I32Literal>(new(8));
        //    case ILOpCode.Ldc_i4_m1:
        //        return new Const<I32Literal>(new(-1));
        //    case ILOpCode.Ldc_i4_s:
        //        return new Const<I32Literal>(new((sbyte)inst.Operand));
        //    case ILOpCode.Ldc_i8:
        //        return new Const<I64Literal>(new((long)inst.Operand));
        //    case ILOpCode.Ldc_r4:
        //        return new Const<F32Literal>(new((float)inst.Operand));
        //    case ILOpCode.Ldc_r8:
        //        return new Const<F64Literal>(new((double)inst.Operand));

        //    case ILOpCode.Ldarg:
        //    case ILOpCode.Ldarg_s:
        //        return new LoadArgumentInstruction(((ParameterInfo)inst.Operand).Position + argumentPositionBase);
        //    case ILOpCode.Ldarg_0:
        //        return new LoadArgumentInstruction(0);
        //    case ILOpCode.Ldarg_1:
        //        return new LoadArgumentInstruction(1);
        //    case ILOpCode.Ldarg_2:
        //        return new LoadArgumentInstruction(2);
        //    case ILOpCode.Ldarg_3:
        //        return new LoadArgumentInstruction(3);
        //    case ILOpCode.Ldarga:
        //    case ILOpCode.Ldarga_s:
        //        return new LoadArgumentAddressInstruction(((ParameterInfo)inst.Operand).Position + argumentPositionBase);
        //    case ILOpCode.Starg:
        //    case ILOpCode.Starg_s:
        //        return new StoreArgumentInstruction(((ParameterInfo)inst.Operand).Position + argumentPositionBase);

        //    case ILOpCode.Ldloc_0:
        //        return new LoadLocalInstruction(LocalVariableInfo[0]);
        //    case ILOpCode.Ldloc_1:
        //        return new LoadLocalInstruction(LocalVariableInfo[1]);
        //    case ILOpCode.Ldloc_2:
        //        return new LoadLocalInstruction(LocalVariableInfo[2]);
        //    case ILOpCode.Ldloc_3:
        //        return new LoadLocalInstruction(LocalVariableInfo[3]);
        //    case ILOpCode.Ldloc:
        //    case ILOpCode.Ldloc_s:
        //        return new LoadLocalInstruction((LocalVariableInfo)inst.Operand);
        //    case ILOpCode.Stloc_0:
        //        return new StoreLocalInstruction(LocalVariableInfo[0]);
        //    case ILOpCode.Stloc_1:
        //        return new StoreLocalInstruction(LocalVariableInfo[1]);
        //    case ILOpCode.Stloc_2:
        //        return new StoreLocalInstruction(LocalVariableInfo[2]);
        //    case ILOpCode.Stloc_3:
        //        return new StoreLocalInstruction(LocalVariableInfo[3]);
        //    case ILOpCode.Stloc:
        //        return new StoreLocalInstruction(((LocalVariableInfo)inst.Operand));
        //    case ILOpCode.Stloc_s:
        //        return new StoreLocalInstruction((LocalVariableInfo)inst.Operand);
        //    case ILOpCode.Ldloca:
        //        return new LoadLocalAddressInstruction((LocalVariableInfo)inst.Operand);
        //    case ILOpCode.Ldloca_s:
        //        return new LoadLocalAddressInstruction((LocalVariableInfo)inst.Operand);
        //    case ILOpCode.Add:
        //        return new BinaryArithmeticInstruction<BinaryArithmetic.Add>();
        //    case ILOpCode.Sub:
        //        return new BinaryArithmeticInstruction<BinaryArithmetic.Sub>();
        //    case ILOpCode.Mul:
        //        return new BinaryArithmeticInstruction<BinaryArithmetic.Mul>();
        //    case ILOpCode.Div:
        //        return new BinaryArithmeticInstruction<BinaryArithmetic.Div>();
        //    case ILOpCode.Rem:
        //        return new BinaryArithmeticInstruction<BinaryArithmetic.Rem>();

        //    case ILOpCode.Call:
        //        return new CallInstruction((MethodInfo)inst.Operand);
        //    case ILOpCode.Newobj:
        //        return new NewObjInstruction((ConstructorInfo)inst.Operand);

        //    case ILOpCode.Ret:
        //        return new ReturnInstruction();

        //    case ILOpCode.Ldsfld:
        //        return new LoadStaticFieldInstruction((FieldInfo)inst.Operand);
        //    case ILOpCode.Ldfld:
        //        return new LoadInstanceFieldInstruction((FieldInfo)inst.Operand);
        //    case ILOpCode.Ldflda:
        //        return new LoadInstanceFieldAddressInstruction((FieldInfo)inst.Operand);
        //    case ILOpCode.Clt:
        //        return new ConditionValueInstruction<Condition.Lt<S>>();
        //    case ILOpCode.Clt_un:
        //        return new ConditionValueInstruction<Condition.Lt<U>>();
        //    case ILOpCode.Cgt:
        //        return new ConditionValueInstruction<Condition.Gt<S>>();
        //    case ILOpCode.Cgt_un:
        //        return new ConditionValueInstruction<Condition.Gt<U>>();

        //    case ILOpCode.Conv_r4:
        //        return new ConvertInstruction(ShaderType.F32);
        //    case ILOpCode.Conv_r8:
        //        return new ConvertInstruction(ShaderType.F64);

        //    case ILOpCode.Neg:
        //        return new NegateInstruction();

        //    default:
        //        throw new NotSupportedException($"Compile {inst.OpCode}@{inst.Offset} of {Method.Name} is not supported");
        //}
    }

    public IEnumerable<IStatement> CompileBody()
    {
        foreach (var decl in LocalVariables)
        {
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
        var bp = new VariableDeclaration(DeclarationScope.Function, LocalVariables.Count + 1, "clsl_next", ShaderType.I32, []);
        yield return SyntaxFactory.VarDeclaration(bp);
        var cases = new List<SwitchCase>();
        var phiLocals = new List<VariableDeclaration>[BasicBlocks.Count];
        foreach (var (ib, bb) in BasicBlocks.Index())
        {
            Stacks[ib] ??= new Stack<IExpression>();
            var stack = Stacks[ib];
            var stmts = CompileBasicBlock(bp, stack, bb).ToList();
            foreach (var s in stmts)
            {
                Console.WriteLine(s);
            }
            Console.WriteLine();
            cases.Add(new SwitchCase(
                SyntaxFactory.Literal(bb.Index),
                new([.. stmts])
            ));
        }
        yield return new LoopStatement(
            new CompoundStatement([
            new SwitchStatement(SyntaxFactory.VarIdentifier(bp), [.. cases], new CompoundStatement([
                //SyntaxFactory.Break()
                ])) ]));
    }
    public IReadOnlyList<VariableDeclaration> CreateLocalVariableDeclarations()
    {
        var result = new List<VariableDeclaration>(MethodBody.LocalVariables.Count);
        foreach (var (idx, v) in MethodBody.LocalVariables.Index())
        {
            var type = Context.Types[v.LocalType];
            var name = $"clsl__v{idx}";
            var decl = new VariableDeclaration(DeclarationScope.Function, idx, name, type, []);
            result.Add(decl);
        }
        return result;
    }


    IExpression LocalVariableRef(int index)
           => SyntaxFactory.VarIdentifier(LocalVariables[index]);


    IEnumerable<IStatement> CompileBasicBlock(VariableDeclaration? bp, Stack<IExpression> stack, BasicBlock basicBlock)
    {
        var isUnconditionalBranchedOut = false;
        foreach (var inst in basicBlock.Instructions)
        {
            isUnconditionalBranchedOut = false;
            switch (inst)
            {
                case NopInstruction:
                    break;
                case IConstInstruction { Literal: var literal }:
                    stack.Push(new LiteralValueExpression(literal));
                    break;
                case LoadArgumentInstruction { Index: var index }:
                    stack.Push(SyntaxFactory.ArgIdentifier(Context.Parameters[index]));
                    break;
                case StoreArgumentInstruction { Index: var index }:
                    {
                        var value = stack.Pop();
                        yield return new SimpleAssignmentStatement(
                            SyntaxFactory.ArgIdentifier(Context.Parameters[index]),
                            value,
                            AssignmentOp.Assign
                        );
                    }
                    break;
                case IBinaryArithmeticInstruction binst:
                    {
                        Debug.Assert(stack.Count >= 2);
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(binst.CreateExpression(l, r));
                        break;
                    }
                case CallInstruction { Callee: var methodInfo }:
                    {
                        if (TryGetVectorSwizzle(methodInfo, out var swizzles, out var isSetter))
                        {
                            if (isSetter)
                            {
                                var value = stack.Pop();
                                var v = stack.Pop();
                                if (v is AddressOfExpression { Base: var bv })
                                {
                                    yield return new SimpleAssignmentStatement(
                                            new VectorSwizzleAccessExpression(bv, swizzles),
                                            value,
                                            AssignmentOp.Assign);
                                }
                                else
                                {
                                    throw new NotSupportedException("Swizzle argument should be pointer type");
                                }
                            }
                            else
                            {
                                var v = stack.Pop();
                                if (v is AddressOfExpression { Base: var bv })
                                {
                                    stack.Push(
                                        new VectorSwizzleAccessExpression(bv, swizzles)
                                    );
                                }
                                else
                                {
                                    throw new NotSupportedException("Swizzle argument should be pointer type");
                                }
                            }

                            break;
                        }
                        if (TryGetOp(methodInfo, out var op))
                        {
                            var r = stack.Pop();
                            var l = stack.Pop();
                            stack.Push(
                                new BinaryArithmeticExpression(l, r, op)
                            );
                            break;
                        }
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
                case NewObjInstruction { Constructor: MethodBase ctorInfo }:
                    {
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
                case ConvertInstruction { Target: var targetType }:
                    {
                        var source = stack.Pop();
                        var f = ShaderFunction.Instance.GetFunction(targetType.Name, targetType, [source.Type]);
                        stack.Push(SyntaxFactory.Call(f, [source]));
                        break;
                        throw new NotImplementedException();
                    }
                case StoreLocalInstruction { Target.LocalIndex: var index }:
                    {
                        yield return new SimpleAssignmentStatement(
                            LocalVariableRef(index),
                            stack.Pop(),
                            AssignmentOp.Assign
                        );
                        break;
                    }
                case LoadLocalInstruction { Source.LocalIndex: var index }:
                    stack.Push(LocalVariableRef(index));
                    break;
                case BranchInstruction<Condition.Unconditional> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        yield return new SimpleAssignmentStatement(
                            SyntaxFactory.VarIdentifier(bp),
                            SyntaxFactory.Literal(target.Index),
                            AssignmentOp.Assign
                        );
                        yield return SyntaxFactory.Continue();
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        isUnconditionalBranchedOut = true;
                        break;
                    }
                case BranchInstruction<Condition.True> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        yield return new IfStatement(
                            stack.Pop(),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target.Index),
                                    AssignmentOp.Assign
                                ),
                                SyntaxFactory.Continue()
                                ]),
                            new([]),
                            []
                        );
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case BranchInstruction<Condition.False> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        yield return new IfStatement(
                            stack.Pop(),
                            new([]),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target.Index),
                                    AssignmentOp.Assign
                                ),
                                SyntaxFactory.Continue()
                                ]),
                            []
                        );
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case BranchInstruction<Condition.Gt<S>> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        var r = stack.Pop();
                        var l = stack.Pop();
                        yield return new IfStatement(
                            new BinaryRelationalExpression(
                                l, r,
                                BinaryRelation.OpKind.gt
                            ),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target.Index),
                                    AssignmentOp.Assign )
                                ]),
                            new([]),
                            []
                        );
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case BranchInstruction<Condition.Lt<S>> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        var r = stack.Pop();
                        var l = stack.Pop();
                        yield return new IfStatement(
                            new BinaryRelationalExpression(
                                l, r,
                                BinaryRelation.OpKind.lt
                            ),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target.Index),
                                    AssignmentOp.Assign )
                                ]),
                            new([]),
                            []
                        );
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        break;
                    }
                case BranchInstruction<Condition.Le<S>> { Target: var target }:
                    {
                        if (bp is null)
                        {
                            throw new NotSupportedException();
                        }
                        var r = stack.Pop();
                        var l = stack.Pop();
                        yield return new IfStatement(
                            new BinaryRelationalExpression(
                                l, r,
                                BinaryRelation.OpKind.le
                            ),
                            new([
                                new SimpleAssignmentStatement(
                                    SyntaxFactory.VarIdentifier(bp),
                                    SyntaxFactory.Literal(target.Index),
                                    AssignmentOp.Assign )
                                ]),
                            new([]),
                            []
                        );
                        Stacks[target.Index] ??= new Stack<IExpression>(stack);
                        break;
                    }

                case BinaryBitwiseInstruction { Op: var op }:
                    {
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(new BinaryBitwiseExpression(l, r, op));
                        break;
                    }
                case ReturnInstruction:
                    if (stack.Count != 0)
                    {
                        yield return SyntaxFactory.Return(stack.Pop());
                    }
                    else
                    {
                        yield return SyntaxFactory.Return(null);
                    }
                    break;
                case IConditionValueInstruction cinst:
                    {
                        var r = stack.Pop();
                        var l = stack.Pop();
                        stack.Push(cinst.CreateExpression(l, r));
                        break;
                    }
                case LoadArgumentAddressInstruction { Index: var index }:
                    {
                        stack.Push(SyntaxFactory.AddressOf(SyntaxFactory.ArgIdentifier(Context.Parameters[index])));
                        break;
                    }
                case LoadLocalAddressInstruction { Source: var info }:
                    {
                        //var info = (LocalVariableInfo)inst.Operand;
                        //if (ip + 1 < Instructions.Length)
                        //{
                        //    var next = Instructions[ip + 1];
                        //    if (next.OpCode.ToILOpCode() == ILOpCode.Call)
                        //    {
                        //        var f = (MethodInfo)next.Operand;
                        //        // TODO: add attribute for this hack
                        //        if (TryGetVectorSwizzle(f, out var swizzles))
                        //        {
                        //            stack.Push(new VectorSwizzleAccessExpression(
                        //                (LocalVariableRef(info.LocalIndex)),
                        //                swizzles));
                        //            ip++;
                        //            break;
                        //        }
                        //    }
                        //}
                        //throw new NotSupportedException();
                        stack.Push(SyntaxFactory.AddressOf(SyntaxFactory.VarIdentifier(LocalVariables[info.LocalIndex])));
                        break;
                    }

                case LoadStaticFieldInstruction { Field: var info }:
                    {
                        // TODO: better handling other than this ad hoc one
                        if (info.GetCustomAttribute<CLSL.Language.ShaderAttribute.UniformAttribute>() is not null)
                        {
                            stack.Push(SyntaxFactory.VarIdentifier(
                                new VariableDeclaration(DeclarationScope.Module,
                                -1,
                                info.Name,
                                Context.Types[info.FieldType],
                                []
                            )));
                            break;
                        }
                        throw new NotImplementedException();
                    }
                case LoadInstanceFieldInstruction { Field: var info }:
                    {
                        var obj = stack.Pop();
                        stack.Push(new NamedComponentExpression(obj, info.Name));
                        break;
                    }
                case NegateInstruction:
                    {
                        var value = stack.Pop();
                        if (value.Type is BoolType)
                        {
                            stack.Push(new UnaryLogicalExpression(value, UnaryLogicalOp.Not));
                        }
                        else
                        {
                            stack.Push(new UnaryArithmeticExpression(value, UnaryArithmeticOp.Minus));
                        }
                        break;
                    }
                case LoadInstanceFieldAddressInstruction { Field: var info }:
                    {
                        var obj = stack.Pop();
                        if (obj is AddressOfExpression { Base: var b })
                        {
                            stack.Push(SyntaxFactory.AddressOf(new NamedComponentExpression(b, info.Name)));
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    }
                default:
                    //throw new NotSupportedException($"Unsupported OpCode: {inst.OpCode}@{inst.Offset:X} {inst.Operand} - Method {Method.DeclaringType.Name}.{Method.Name}");
                    throw new NotSupportedException($"Unsupported OpCode: {inst}({inst.GetType().Name}) - Method {Method.DeclaringType.Name}.{Method.Name}");
            }
        }

        if (!isUnconditionalBranchedOut && (basicBlock.Index + 1 < BasicBlocks.Count))
        {
            yield return new SimpleAssignmentStatement(
                 SyntaxFactory.VarIdentifier(bp),
                 SyntaxFactory.Literal(BasicBlocks[basicBlock.Index + 1].Index),
                 AssignmentOp.Assign
             );
        }
    }

    bool TryGetOp(MethodInfo f, out BinaryArithmetic.OpKind op)
    {
        switch (f.Name)
        {
            case "op_Addition":
                op = BinaryArithmetic.OpKind.add;
                return true;
            case "op_Subtraction":
                op = BinaryArithmetic.OpKind.sub;
                return true;
            case "op_Multiply":
                op = BinaryArithmetic.OpKind.mul;
                return true;
            case "op_Division":
                op = BinaryArithmetic.OpKind.div;
                return true;
            case "op_Modulus":
                op = BinaryArithmetic.OpKind.rem;
                return true;
            default:
                op = default;
                return false;
        }
    }
    bool TryGetVectorSwizzle(MethodInfo f, out ImmutableArray<SwizzleComponent> swizzles, out bool isSet)
    {
        // TODO: use explicit attribute for swizzles
        if (f.DeclaringType.Namespace.StartsWith("DualDrill.Mathematics"))
        {
            if (f.Name.StartsWith("get_"))
            {
                swizzles = [.. f.Name.Substring(4).Select(c => Enum.Parse<SwizzleComponent>(c.ToString()))];
                isSet = false;
                return true;
            }
            if (f.Name.StartsWith("set_"))
            {
                swizzles = [.. f.Name.Substring(4).Select(c => Enum.Parse<SwizzleComponent>(c.ToString()))];
                isSet = true;
                return true;
            }
        }
        isSet = false;
        swizzles = default;
        return false;
    }
}


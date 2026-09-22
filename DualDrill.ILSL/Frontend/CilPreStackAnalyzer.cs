using System.Collections.Immutable;
using System.Reflection;
using DualDrill.CLSL.Frontend.SymbolTable;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Frontend;

internal static class CilPreStackAnalyzer
{
    public static LinearCode<Annotated<CilInstructionInfo, PreStack>> Analyze(
        LinearCode<CilInstructionInfo> source,
        FunctionDeclaration function,
        ISymbolTableView table)
    {
        var environment = source.Environment;
        if (source.Count == 0)
            throw new ValidationException("A method body must contain at least one CIL instruction.",
                environment.Method);

        environment.ValidateControlBoundaries(source);

        Dictionary<int, ImmutableStack<CilStackType>> pre = new()
        {
            [0] = []
        };
        Queue<int> worklist = new([0]);

        while (worklist.TryDequeue(out var index))
        {
            var instruction = source[index];
            var post = Transfer(environment, function, table, instruction, pre[index]);
            foreach (var target in environment.SuccessorInstructionIndices(source, instruction))
            {
                if (!pre.TryGetValue(target, out var current))
                {
                    pre.Add(target, post);
                    worklist.Enqueue(target);
                    continue;
                }

                Merge(source, instruction, target, current, post);
            }
        }

        var instructions = pre.OrderBy(pair => pair.Key)
                              .Select(pair => new Annotated<CilInstructionInfo, PreStack>(
                                  source[pair.Key],
                                  new PreStack(pair.Value),
                                  CilStagePrettyPrinter.PrintAnnotatedInstruction))
                              .ToImmutableArray();
        return new LinearCode<Annotated<CilInstructionInfo, PreStack>>(
            environment,
            instructions,
            CilStagePrettyPrinter.PrintPreAnnotatedLinearCode);
    }

    private static ImmutableStack<CilStackType> Transfer(
        CilMethodEnvironment environment,
        FunctionDeclaration function,
        ISymbolTableView table,
        CilInstructionInfo instruction,
        ImmutableStack<CilStackType> pre)
    {
        try
        {
            var visitor = new TransferVisitor(environment, function, table, instruction, pre);
            return instruction.Evaluate(visitor, environment.IsStatic, table);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (NotImplementedException exception)
        {
            throw Unsupported(environment, instruction, exception);
        }
        catch (NotSupportedException exception)
        {
            throw Unsupported(environment, instruction, exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw Invalid(environment, instruction, $"symbol resolution failed: {exception.Message}", exception);
        }
        catch (InvalidCastException exception)
        {
            throw Invalid(environment, instruction, $"malformed operand: {exception.Message}", exception);
        }
    }

    private static void Merge(
        LinearCode<CilInstructionInfo> code,
        CilInstructionInfo source,
        int target,
        ImmutableStack<CilStackType> existing,
        ImmutableStack<CilStackType> incoming)
    {
        if (existing.Count() != incoming.Count())
            throw new ValidationException(
                $"CIL stack height mismatch from IL_{source.ByteOffset:X4} to IL_{code[target].ByteOffset:X4}: " +
                $"existing {existing.Count()}, incoming {incoming.Count()}.",
                code.Environment.Method);

        foreach (var (slot, pair) in existing.Zip(incoming).Index())
            if (pair.First != pair.Second)
                throw new ValidationException(
                    $"CIL stack type mismatch at top-based slot {slot} from IL_{source.ByteOffset:X4} " +
                    $"to IL_{code[target].ByteOffset:X4}: existing {pair.First}, incoming {pair.Second}.",
                    code.Environment.Method);
    }

    private static ValidationException Unsupported(
        CilMethodEnvironment environment,
        CilInstructionInfo instruction,
        Exception exception) =>
        Invalid(environment, instruction, "reachable instruction semantics are not supported", exception);

    private static ValidationException Invalid(
        CilMethodEnvironment environment,
        CilInstructionInfo instruction,
        string message,
        Exception? innerException = null) =>
        new(
            $"{message} at IL_{instruction.ByteOffset:X4} ({instruction.Instruction.OpCode}).",
            environment.Method,
            innerException);

    private sealed class TransferVisitor(
        CilMethodEnvironment environment,
        FunctionDeclaration function,
        ISymbolTableView table,
        CilInstructionInfo instruction,
        ImmutableStack<CilStackType> stack)
        : ICilInstructionVisitor<ImmutableStack<CilStackType>>
    {
        private ImmutableStack<CilStackType> Stack { get; set; } = stack;

        public ImmutableStack<CilStackType> VisitNop(CilInstructionInfo inst) => Stack;

        public ImmutableStack<CilStackType> VisitBreak(CilInstructionInfo inst) => Unsupported("break");

        public ImmutableStack<CilStackType> VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p) =>
            Push(CilStackType.FromShaderType(p.Type));

        public ImmutableStack<CilStackType> VisitLoadArgumentAddress(
            CilInstructionInfo inst,
            ParameterDeclaration p) =>
            Push(CilStackType.FromShaderType(p.Value.Type));

        public ImmutableStack<CilStackType> VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
        {
            RequireStorage(p.Type, Pop());
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitLdThis(CilInstructionInfo inst) => Unsupported("ldarg.0 this");

        public ImmutableStack<CilStackType> VisitStThis(CilInstructionInfo inst) => Unsupported("starg this");

        public ImmutableStack<CilStackType> VisitLoadLocal(CilInstructionInfo inst, VariableDeclaration v) =>
            Push(CilStackType.FromShaderType(v.Type));

        public ImmutableStack<CilStackType> VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v) =>
            Push(CilStackType.FromShaderType(v.Value.Type));

        public ImmutableStack<CilStackType> VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v)
        {
            RequireStorage(v.Type, Pop());
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
        {
            _ = PopFieldOwner();
            return Push(CilStackType.FromShaderType(m.Type));
        }

        public ImmutableStack<CilStackType> VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m)
        {
            var owner = PopFieldOwner();
            return Push(CilStackType.FromShaderType(m.Type.GetPtrType(owner.Type.AddressSpace)));
        }

        public ImmutableStack<CilStackType> VisitStoreField(CilInstructionInfo inst, MemberDeclaration m)
        {
            RequireStorage(m.Type, Pop());
            _ = PopFieldOwner();
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v) =>
            Push(CilStackType.FromShaderType(v.Type));

        public ImmutableStack<CilStackType> VisitLoadStaticFieldAddress(
            CilInstructionInfo inst,
            VariableDeclaration v) =>
            Push(CilStackType.FromShaderType(v.Value.Type));

        public ImmutableStack<CilStackType> VisitLoadNull(CilInstructionInfo info) => Unsupported("ldnull");

        public ImmutableStack<CilStackType> VisitCall(CilInstructionInfo info, FunctionDeclaration f) =>
            Call(f, f.Return.Type is not UnitType);

        public ImmutableStack<CilStackType> VisitNewObject(CilInstructionInfo info, FunctionDeclaration f) =>
            Call(f, true);

        public ImmutableStack<CilStackType> VisitReturn(CilInstructionInfo info)
        {
            if (function.Return.Type is UnitType)
            {
                if (!Stack.IsEmpty)
                    throw Error($"void return requires an empty stack, got {Stack.Count()} slots");
                return Stack;
            }

            RequireStorage(function.Return.Type, Pop());
            if (!Stack.IsEmpty)
                throw Error($"return leaves {Stack.Count()} extra stack slots");
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal)
            where TLiteral : ILiteral =>
            Push(CilStackType.FromShaderType(literal.Type));

        public ImmutableStack<CilStackType> VisitBranch(CilInstructionInfo inst, int jumpOffset) => Stack;

        public ImmutableStack<CilStackType> VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
        {
            Require(CilStackType.Int32.Instance, Pop(), "brtrue/brfalse operand");
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitBranchIf<TOp>(
            CilInstructionInfo inst,
            int jumpOffset,
            bool isUn = false)
            where TOp : BinaryRelational.IOp<TOp>
        {
            var right = Pop();
            var left = Pop();
            RequireEqualNumeric(left, right, $"branch comparison {TOp.Instance.Name}");
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitSwitch(CilInstructionInfo inst)
        {
            Require(CilStackType.Int32.Instance, Pop(), "switch selector");
            return Stack;
        }

        public ImmutableStack<CilStackType> VisitBinaryArithmetic<TOp>(
            CilInstructionInfo inst,
            bool isUn = false,
            bool isChecked = false)
            where TOp : BinaryArithmetic.IOp<TOp>
        {
            var right = Pop();
            var left = Pop();
            RequireEqualNumeric(left, right, $"binary arithmetic {TOp.Instance.Name}");
            return Push(left);
        }

        public ImmutableStack<CilStackType> VisitBinaryLogical<TOp>(CilInstructionInfo inst)
            where TOp : BinaryLogical.IOp<TOp>
        {
            Require(CilStackType.Int32.Instance, Pop(), $"binary logical {TOp.Instance.Name} right operand");
            Require(CilStackType.Int32.Instance, Pop(), $"binary logical {TOp.Instance.Name} left operand");
            return Push(CilStackType.Int32.Instance);
        }

        public ImmutableStack<CilStackType> VisitBinaryRelation<TOp>(
            CilInstructionInfo inst,
            bool isUn = false,
            bool isChecked = false)
            where TOp : BinaryRelational.IOp<TOp>
        {
            var right = Pop();
            var left = Pop();
            RequireEqualNumeric(left, right, $"binary relation {TOp.Instance.Name}");
            return Push(CilStackType.Int32.Instance);
        }

        public ImmutableStack<CilStackType> VisitConversion<TTarget>(CilInstructionInfo inst)
            where TTarget : IScalarType<TTarget>
        {
            RequireScalar(Pop(), "conversion operand");
            return Push(CilStackType.FromShaderType(TTarget.Instance));
        }

        public ImmutableStack<CilStackType> VisitLogicalNot(CilInstructionInfo inst) => Unsupported("not");

        public ImmutableStack<CilStackType> VisitUnaryArithmetic<TOp>(CilInstructionInfo inst)
            where TOp : UnaryArithmetic.IOp<TOp>
        {
            var operand = Pop();
            RequireScalar(operand, $"unary arithmetic {TOp.Instance.Name} operand");
            return Push(operand);
        }

        public ImmutableStack<CilStackType> VisitLoadIndirect<TShaderType>(CilInstructionInfo inst)
            where TShaderType : IShaderType =>
            Unsupported("indirect load");

        public ImmutableStack<CilStackType> VisitStoreIndirect<TShaderType>(CilInstructionInfo inst)
            where TShaderType : IShaderType =>
            Unsupported("indirect store");

        public ImmutableStack<CilStackType> VisitLoadIndirectNativeInt(CilInstructionInfo inst) =>
            Unsupported("native integer indirect load");

        public ImmutableStack<CilStackType> VisitLoadIndirectRef(CilInstructionInfo inst) =>
            Unsupported("reference indirect load");

        public ImmutableStack<CilStackType> VisitStoreIndirectRef(CilInstructionInfo inst) =>
            Unsupported("reference indirect store");

        public ImmutableStack<CilStackType> VisitDup(CilInstructionInfo inst)
        {
            var value = Pop();
            if (value is CilStackType.ObjectReference)
                throw Error("dup does not support object references");
            Push(value);
            return Push(value);
        }

        public ImmutableStack<CilStackType> VisitPop(CilInstructionInfo inst)
        {
            _ = Pop();
            return Stack;
        }

        private ImmutableStack<CilStackType> Call(FunctionDeclaration callee, bool hasReturnValue)
        {
            var allowsGenericPointer =
                callee.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault()?.Operation
                is IUnaryExpressionOperation or IBinaryStatementOperation;
            foreach (var parameter in callee.Parameters.Reverse())
            {
                var actual = Pop();
                var expected = CilStackType.FromShaderType(parameter.Type);
                var compatiblePointer = allowsGenericPointer &&
                                        expected is CilStackType.ManagedPointer expectedPointer &&
                                        actual is CilStackType.ManagedPointer actualPointer &&
                                        expectedPointer.Type.BaseType.Equals(actualPointer.Type.BaseType);
                if (expected != actual && !compatiblePointer)
                    throw Error($"parameter {parameter} not match: stack {actual}, expected {expected}");
            }

            return hasReturnValue ? Push(CilStackType.FromShaderType(callee.Return.Type)) : Stack;
        }

        private CilStackType.ManagedPointer PopFieldOwner()
        {
            var owner = Pop();
            if (owner is not CilStackType.ManagedPointer pointer)
                throw Error($"field access requires a managed pointer, got {owner}");

            if (instruction.Instruction.Operand is not FieldInfo field ||
                field.DeclaringType is null ||
                table[field.DeclaringType] is not { } declaringType ||
                !pointer.Type.BaseType.Equals(declaringType))
                throw Error($"field owner does not match managed pointer {pointer}");

            return pointer;
        }

        private ImmutableStack<CilStackType> Push(CilStackType value)
        {
            Stack = Stack.Push(value);
            return Stack;
        }

        private CilStackType Pop()
        {
            if (Stack.IsEmpty)
                throw Error("CIL evaluation stack underflow");

            Stack = Stack.Pop(out var value);
            return value;
        }

        private void RequireStorage(IShaderType target, CilStackType source) =>
            Require(CilStackType.FromShaderType(target), source, $"storage/call target {target.Name}");

        private void RequireEqualNumeric(CilStackType left, CilStackType right, string operation)
        {
            Require(left, right, operation);
            RequireScalar(left, operation);
        }

        private void RequireScalar(CilStackType value, string operation)
        {
            if (value is not (CilStackType.Int32 or CilStackType.Int64 or
                CilStackType.Float32 or CilStackType.Float64))
                throw Error($"{operation} requires a supported scalar, got {value}");
        }

        private void Require(CilStackType expected, CilStackType actual, string operation)
        {
            if (expected != actual)
                throw Error($"{operation} requires {expected}, got {actual}");
        }

        private ImmutableStack<CilStackType> Unsupported(string operation) =>
            throw Error($"{operation} is not supported");

        private ValidationException Error(string message) =>
            new(
                $"{message} at IL_{instruction.ByteOffset:X4} ({instruction.Instruction.OpCode}).",
                environment.Method);
    }
}

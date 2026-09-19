using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Region;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend.Wasm;

public sealed class WasmLoweringException(string message) : NotSupportedException(message);

public static class WasmLowering
{
    public static WasmFunctionPlan Lower(FunctionBody4 body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Declaration is null)
            throw new WasmLoweringException("WASM function <missing>: function declaration is missing.");
        return new Lowerer(body).Lower();
    }

    private sealed class Lowerer
    {
        private readonly FunctionBody4 body;
        private readonly string context;
        private readonly Dictionary<Label, ShaderRegionBody> definitions =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Label, int> blockIndices =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ParameterPointerValue, int> parameterLocals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<VariableDeclaration, int> storageLocals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IShaderValue, int> valueLocals =
            new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<VariableDeclaration> referencedVariables =
            new(ReferenceEqualityComparer.Instance);
        private readonly List<VariableDeclaration> variableOrder = [];
        private readonly List<Label> labels = [];
        private int nextLocal;
        private int pcLocal;
        private int scratchBase;
        private int scratchCount;

        internal Lowerer(FunctionBody4 body)
        {
            this.body = body;
            context = $"WASM function {body.Declaration.Name ?? "<unnamed>"}";
        }

        internal WasmFunctionPlan Lower()
        {
            ValidateSignature();
            DiscoverDefinitions(body.Body);
            DiscoverReachableBlocks();
            ValidateBlocks();
            AllocateLocals();

            var loop = ImmutableArray.CreateBuilder<WasmInstruction>();
            foreach (var label in labels)
            {
                loop.Add(new WasmInstruction.LocalGet(pcLocal));
                loop.Add(new WasmInstruction.I32Const(blockIndices[label]));
                loop.Add(new WasmInstruction.I32Eq());
                loop.Add(new WasmInstruction.If(LowerBlock(definitions[label]), []));
            }
            loop.Add(new WasmInstruction.Unreachable());

            return new WasmFunctionPlan(
                body.Declaration.Parameters.Length,
                nextLocal - body.Declaration.Parameters.Length,
                [new WasmInstruction.I32Const(0), new WasmInstruction.LocalSet(pcLocal),
                    new WasmInstruction.Loop(loop.ToImmutable()), new WasmInstruction.Unreachable()]);
        }

        private void ValidateSignature()
        {
            var declaration = body.Declaration;
            if (declaration.Parameters.IsDefault)
                Reject("the function parameter sequence is default.");
            if (declaration.Return is null || declaration.Return.Type is null)
                Reject("the function result type is missing.");
            if (declaration.Attributes is null ||
                declaration.Return.Attributes is null ||
                declaration.Parameters.Any(parameter => parameter is null || parameter.Attributes is null))
                Reject("the function attribute sets are missing.");
            if (declaration.Parameters.Any(parameter => parameter.Type is null))
                Reject("a function parameter type is missing.");
            if (declaration.Attributes.Count != 0 ||
                declaration.Return.Attributes.Count != 0 ||
                declaration.Parameters.Any(parameter => parameter.Attributes.Count != 0))
                Reject("shader semantic attributes are not supported.");
            if (!IsI32(declaration.ReturnType) || declaration.Parameters.Any(parameter => !IsI32(parameter.Type)))
                Reject("the public signature must contain only i32 parameters and exactly one i32 result.");
            if (declaration.Parameters.Select(parameter => parameter.Value)
                    .Distinct(ReferenceEqualityComparer.Instance).Count() != declaration.Parameters.Length)
                Reject("parameter storage definitions must be unique.");
            foreach (var (parameter, index) in declaration.Parameters.Select((value, index) => (value, index)))
                parameterLocals.Add(parameter.Value, index);
        }

        private void DiscoverDefinitions(RegionTree<Label, ShaderRegionBody> tree)
        {
            if (tree.Body.Parameters.IsDefault)
                Reject($"{Describe(tree.Label)}: the block parameter sequence is default.");
            if (!ReferenceEquals(tree.Label, tree.Body.Label))
                Reject($"tree label {Describe(tree.Label)} does not match its body label {Describe(tree.Body.Label)}.");
            if (!definitions.TryAdd(tree.Label, tree.Body))
                Reject($"duplicate block definition {Describe(tree.Label)}.");
            foreach (var child in tree.Bindings)
                DiscoverDefinitions(child);
        }

        private void DiscoverReachableBlocks()
        {
            var queued = new HashSet<Label>(ReferenceEqualityComparer.Instance);
            var queue = new Queue<Label>();
            queued.Add(body.Entry);
            queue.Enqueue(body.Entry);
            while (queue.TryDequeue(out var label))
            {
                if (!definitions.TryGetValue(label, out var block))
                    Reject($"reachable target {Describe(label)} has no definition.");
                blockIndices.Add(label, labels.Count);
                labels.Add(label);
                foreach (var target in Targets(block))
                    if (queued.Add(target.Label))
                        queue.Enqueue(target.Label);
            }
            if (labels.Count != definitions.Count)
            {
                var unreachable = definitions.Keys.First(label => !queued.Contains(label));
                Reject($"definition {Describe(unreachable)} is unreachable from entry.");
            }
        }

        private void ValidateBlocks()
        {
            if (!definitions[body.Entry].Parameters.IsEmpty)
                Reject($"{Describe(body.Entry)}: entry block parameters are not supported.");

            var allDefinitions = new HashSet<IShaderValue>(ReferenceEqualityComparer.Instance);
            foreach (var label in labels)
            {
                var block = definitions[label];
                foreach (var parameter in block.Parameters)
                    AddDefinition(allDefinitions, parameter, label, "block parameter");
                foreach (var instruction in block.Body.Elements)
                    if (instruction.Result is { } result)
                        AddDefinition(allDefinitions, result, label, "instruction result");
            }

            foreach (var label in labels)
                ValidateBlock(definitions[label]);

            ValidateEntryInitialization();
        }

        private void AddDefinition(
            HashSet<IShaderValue> allDefinitions,
            IShaderValue? value,
            Label label,
            string kind)
        {
            var type = ValueType(label, value, kind);
            if (value is not IntermediateValue || !IsScalar(type))
                Reject($"{Describe(label)}: {kind} must be a distinct i32 or bool intermediate value.");
            if (!allDefinitions.Add(value))
                Reject($"{Describe(label)}: duplicate value definition {Describe(value)}.");
        }

        private void ValidateBlock(ShaderRegionBody block)
        {
            var available = new HashSet<IShaderValue>(block.Parameters, ReferenceEqualityComparer.Instance);
            foreach (var instruction in block.Body.Elements)
            {
                ValidateInstructionShape(block.Label, instruction);
                ValidateInstruction(block.Label, instruction, available);
                if (instruction.Result is { } result)
                    available.Add(result);
            }

            switch (block.Body.Last)
            {
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned:
                    ValidateValueUse(block.Label, returned.Expr, available, ShaderType.I32, "return value");
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch:
                    ValidateEdge(block.Label, branch.Target, available);
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch:
                    ValidateValueUse(block.Label, branch.Condition, available, ShaderType.Bool, "branch condition");
                    ValidateEdge(block.Label, branch.TrueTarget, available);
                    ValidateEdge(block.Label, branch.FalseTarget, available);
                    break;
                case Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue>:
                    Reject($"{Describe(block.Label)}: void returns are not supported.");
                    break;
                default:
                    Reject($"{Describe(block.Label)}: unsupported terminator {block.Body.Last.GetType().Name}.");
                    break;
            }
        }

        private void ValidateInstruction(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            HashSet<IShaderValue> available)
        {
            switch (instruction.Operation)
            {
                case NopOperation:
                    RequireShape(label, instruction, 0, false);
                    break;
                case StoreOperation:
                    RequireShape(label, instruction, 2, false);
                    var storeType = ValidateStorage(label, Operand(label, instruction, 0), "store");
                    ValidateValueUse(label, Operand(label, instruction, 1), available, storeType, "stored value");
                    break;
                case LoadOperation:
                    RequireShape(label, instruction, 1, true);
                    var loadType = ValidateStorage(label, Operand(label, instruction, 0), "load");
                    RequireType(label, instruction.Result!, loadType, "load result");
                    break;
                case LiteralOperation:
                    RequireShape(label, instruction, 1, true);
                    var literal = Operand(label, instruction, 0);
                    if (literal is not LiteralValue)
                        Reject($"{Describe(label)}: literal operation requires a literal operand.");
                    ValidateLiteral(label, literal);
                    RequireType(label, instruction.Result!, literal.Type, "literal result");
                    break;
                case NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>:
                    ValidateBinary(label, instruction, available, ShaderType.I32, ShaderType.I32);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>:
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ne>:
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>:
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Le>:
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>:
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ge>:
                    ValidateBinary(label, instruction, available, ShaderType.I32, ShaderType.Bool);
                    break;
                case ScalarConversionOperation<IntType<N32>, BoolType>:
                    ValidateUnary(label, instruction, available, ShaderType.I32, ShaderType.Bool);
                    break;
                case ScalarConversionOperation<BoolType, IntType<N32>>:
                    ValidateUnary(label, instruction, available, ShaderType.Bool, ShaderType.I32);
                    break;
                default:
                    Reject($"{Describe(label)}: unsupported operation {instruction.Operation.Name}.");
                    break;
            }
        }

        private void ValidateBinary(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            HashSet<IShaderValue> available,
            IShaderType operandType,
            IShaderType resultType)
        {
            RequireShape(label, instruction, 2, true);
            ValidateValueUse(label, Operand(label, instruction, 0), available, operandType, "left operand");
            ValidateValueUse(label, Operand(label, instruction, 1), available, operandType, "right operand");
            RequireType(label, instruction.Result!, resultType, "binary result");
        }

        private void ValidateUnary(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            HashSet<IShaderValue> available,
            IShaderType operandType,
            IShaderType resultType)
        {
            RequireShape(label, instruction, 1, true);
            ValidateValueUse(label, Operand(label, instruction, 0), available, operandType, "conversion operand");
            RequireType(label, instruction.Result!, resultType, "conversion result");
        }

        private void ValidateInstructionShape(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction)
        {
            if (instruction.Operation is null)
                Reject($"{Describe(label)}: instruction operation is missing.");
            if (instruction.RestOperands.IsDefault)
                Reject($"{Describe(label)}: instruction rest operands are default.");
            var valid = instruction.OperandCount switch
            {
                0 => instruction.Operand0 is null && instruction.Operand1 is null && instruction.RestOperands.IsEmpty,
                1 => instruction.Operand0 is not null && instruction.Operand1 is null &&
                     instruction.RestOperands.IsEmpty,
                2 => instruction.Operand0 is not null && instruction.Operand1 is not null &&
                     instruction.RestOperands.IsEmpty,
                > 2 => instruction.Operand0 is not null && instruction.Operand1 is not null &&
                       instruction.RestOperands.Length == instruction.OperandCount - 2 &&
                       instruction.RestOperands.All(value => value is not null),
                _ => false
            };
            if (!valid)
                Reject($"{Describe(label)}: malformed operand storage for count {instruction.OperandCount}.");
        }

        private void RequireShape(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            int operands,
            bool hasResult)
        {
            if (instruction.OperandCount != operands)
                Reject($"{Describe(label)}: {instruction.Operation.Name} requires {operands} operands.");
            if ((instruction.Result is not null) != hasResult)
                Reject($"{Describe(label)}: {instruction.Operation.Name} has an invalid result presence.");
        }

        private IShaderType ValidateStorage(Label label, IShaderValue value, string operation)
        {
            switch (value)
            {
                case ParameterPointerValue parameter
                    when parameterLocals.ContainsKey(parameter):
                    return parameter.Declaration.Type;
                case ParameterPointerValue:
                    Reject($"{Describe(label)}: {operation} references a parameter outside the function signature.");
                    break;
                case VariablePointerValue variable:
                    var declaration = variable.Declaration;
                    if (!ReferenceEquals(value, declaration.Value))
                        Reject($"{Describe(label)}: {operation} uses noncanonical local storage.");
                    if (declaration.AddressSpace is not FunctionAddressSpace)
                        Reject($"{Describe(label)}: {operation} uses non-function storage {declaration.AddressSpace.Kind}.");
                    if (declaration.Attributes.Count != 0)
                        Reject($"{Describe(label)}: local shader semantic attributes are not supported.");
                    if (!IsScalar(declaration.Type))
                        Reject($"{Describe(label)}: local storage must contain i32 or bool.");
                    if (referencedVariables.Add(declaration))
                        variableOrder.Add(declaration);
                    return declaration.Type;
                default:
                    Reject($"{Describe(label)}: {operation} requires parameter or function-local storage.");
                    break;
            }
            throw new UnreachableException();
        }

        private void ValidateValueUse(
            Label label,
            IShaderValue? value,
            HashSet<IShaderValue> available,
            IShaderType expectedType,
            string role)
        {
            var checkedValue = RequireValue(label, value, role);
            if (checkedValue is LiteralValue)
                ValidateLiteral(label, checkedValue);
            else if (!available.Contains(checkedValue))
                Reject($"{Describe(label)}: {role} {Describe(checkedValue)} is not a current block parameter or earlier result.");
            RequireType(label, checkedValue, expectedType, role);
        }

        private void ValidateLiteral(Label label, IShaderValue value)
        {
            if (value is LiteralValue { Value: null })
                Reject($"{Describe(label)}: literal payload is missing.");
            if (value is not LiteralValue { Value: I32Literal or BoolLiteral })
                Reject($"{Describe(label)}: only i32 and bool literals are supported.");
        }

        private void ValidateEdge(
            Label source,
            RegionJump<IShaderValue> edge,
            HashSet<IShaderValue> available)
        {
            if (edge.Arguments.IsDefault)
                Reject($"{Describe(source)} -> {Describe(edge.Label)}: edge arguments are default.");
            if (!definitions.TryGetValue(edge.Label, out var target))
                Reject($"{Describe(source)}: target {Describe(edge.Label)} has no definition.");
            if (edge.Arguments.Length != target.Parameters.Length)
                Reject($"{Describe(source)} -> {Describe(edge.Label)}: expected {target.Parameters.Length} edge arguments, got {edge.Arguments.Length}.");
            for (var i = 0; i < edge.Arguments.Length; i++)
                ValidateValueUse(source, edge.Arguments[i], available, target.Parameters[i].Type,
                    $"edge argument {i}");
        }

        private void ValidateEntryInitialization()
        {
            var initialized = new HashSet<VariableDeclaration>(ReferenceEqualityComparer.Instance);
            foreach (var instruction in definitions[body.Entry].Body.Elements)
            {
                if (instruction.Operation is LoadOperation &&
                    instruction.Operand0 is VariablePointerValue loaded &&
                    !initialized.Contains(loaded.Declaration))
                    Reject($"{Describe(body.Entry)}: local {loaded.Declaration.Name} is read before its entry-block initialization.");
                if (instruction.Operation is StoreOperation &&
                    instruction.Operand0 is VariablePointerValue stored)
                    initialized.Add(stored.Declaration);
            }
            var missing = referencedVariables.FirstOrDefault(variable => !initialized.Contains(variable));
            if (missing is not null)
                Reject($"local {missing.Name} must be unconditionally stored in the entry block before any read.");
        }

        private void AllocateLocals()
        {
            nextLocal = body.Declaration.Parameters.Length;
            pcLocal = nextLocal++;
            foreach (var variable in variableOrder)
                storageLocals.Add(variable, nextLocal++);
            foreach (var label in labels)
            {
                var block = definitions[label];
                foreach (var parameter in block.Parameters)
                    valueLocals.Add(parameter, nextLocal++);
                foreach (var instruction in block.Body.Elements)
                    if (instruction.Result is { } result)
                        valueLocals.Add(result, nextLocal++);
            }
            scratchCount = definitions.Values.Max(block => block.Parameters.Length);
            scratchBase = nextLocal;
            nextLocal += scratchCount;
        }

        private ImmutableArray<WasmInstruction> LowerBlock(ShaderRegionBody block)
        {
            var instructions = ImmutableArray.CreateBuilder<WasmInstruction>();
            foreach (var instruction in block.Body.Elements)
                LowerInstruction(block.Label, instruction, instructions);
            switch (block.Body.Last)
            {
                case Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> returned:
                    EmitValue(returned.Expr, instructions);
                    instructions.Add(new WasmInstruction.Return());
                    break;
                case Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch:
                    LowerTransfer(branch.Target, 1, instructions);
                    break;
                case Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch:
                    EmitValue(branch.Condition, instructions);
                    var whenTrue = ImmutableArray.CreateBuilder<WasmInstruction>();
                    var whenFalse = ImmutableArray.CreateBuilder<WasmInstruction>();
                    LowerTransfer(branch.TrueTarget, 2, whenTrue);
                    LowerTransfer(branch.FalseTarget, 2, whenFalse);
                    instructions.Add(new WasmInstruction.If(whenTrue.ToImmutable(), whenFalse.ToImmutable()));
                    break;
                default:
                    throw new UnreachableException(
                        $"{context}, block {Describe(block.Label)}: unsupported terminator escaped validation.");
            }
            return instructions.ToImmutable();
        }

        private void LowerInstruction(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            ImmutableArray<WasmInstruction>.Builder output)
        {
            switch (instruction.Operation)
            {
                case NopOperation:
                    break;
                case StoreOperation:
                    EmitValue(instruction.Operand1!, output);
                    output.Add(new WasmInstruction.LocalSet(StorageLocal(instruction.Operand0!)));
                    break;
                case LoadOperation:
                    output.Add(new WasmInstruction.LocalGet(StorageLocal(instruction.Operand0!)));
                    output.Add(new WasmInstruction.LocalSet(valueLocals[instruction.Result!]));
                    break;
                case LiteralOperation:
                    EmitValue(instruction.Operand0!, output);
                    output.Add(new WasmInstruction.LocalSet(valueLocals[instruction.Result!]));
                    break;
                case NumericBinaryArithmeticOperation<IntType<N32>, BinaryArithmetic.Add>:
                    EmitBinary(instruction, new WasmInstruction.I32Add(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Eq>:
                    EmitBinary(instruction, new WasmInstruction.I32Eq(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ne>:
                    EmitBinary(instruction, new WasmInstruction.I32Ne(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Lt>:
                    EmitBinary(instruction, new WasmInstruction.I32LtS(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Le>:
                    EmitBinary(instruction, new WasmInstruction.I32LeS(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Gt>:
                    EmitBinary(instruction, new WasmInstruction.I32GtS(), output);
                    break;
                case NumericBinaryRelationalOperation<IntType<N32>, BinaryRelational.Ge>:
                    EmitBinary(instruction, new WasmInstruction.I32GeS(), output);
                    break;
                case ScalarConversionOperation<IntType<N32>, BoolType>:
                    EmitValue(instruction.Operand0!, output);
                    output.Add(new WasmInstruction.I32Const(0));
                    output.Add(new WasmInstruction.I32Ne());
                    output.Add(new WasmInstruction.LocalSet(valueLocals[instruction.Result!]));
                    break;
                case ScalarConversionOperation<BoolType, IntType<N32>>:
                    EmitValue(instruction.Operand0!, output);
                    output.Add(new WasmInstruction.LocalSet(valueLocals[instruction.Result!]));
                    break;
                default:
                    throw new UnreachableException(
                        $"{context}, block {Describe(label)}: unsupported operation escaped validation.");
            }
        }

        private void EmitBinary(
            Instruction<IShaderValue, IShaderValue> instruction,
            WasmInstruction operation,
            ImmutableArray<WasmInstruction>.Builder output)
        {
            EmitValue(instruction.Operand0!, output);
            EmitValue(instruction.Operand1!, output);
            output.Add(operation);
            output.Add(new WasmInstruction.LocalSet(valueLocals[instruction.Result!]));
        }

        private void LowerTransfer(
            RegionJump<IShaderValue> edge,
            int loopDepth,
            ImmutableArray<WasmInstruction>.Builder output)
        {
            for (var i = 0; i < edge.Arguments.Length; i++)
            {
                EmitValue(edge.Arguments[i], output);
                output.Add(new WasmInstruction.LocalSet(scratchBase + i));
            }
            var target = definitions[edge.Label];
            for (var i = 0; i < target.Parameters.Length; i++)
            {
                output.Add(new WasmInstruction.LocalGet(scratchBase + i));
                output.Add(new WasmInstruction.LocalSet(valueLocals[target.Parameters[i]]));
            }
            output.Add(new WasmInstruction.I32Const(blockIndices[edge.Label]));
            output.Add(new WasmInstruction.LocalSet(pcLocal));
            output.Add(new WasmInstruction.Br(loopDepth));
        }

        private void EmitValue(IShaderValue value, ImmutableArray<WasmInstruction>.Builder output)
        {
            switch (value)
            {
                case LiteralValue { Value: I32Literal integer }:
                    output.Add(new WasmInstruction.I32Const(integer.Value));
                    break;
                case LiteralValue { Value: BoolLiteral boolean }:
                    output.Add(new WasmInstruction.I32Const(boolean.Value ? 1 : 0));
                    break;
                default:
                    output.Add(new WasmInstruction.LocalGet(valueLocals[value]));
                    break;
            }
        }

        private int StorageLocal(IShaderValue storage) => storage switch
        {
            ParameterPointerValue parameter => parameterLocals[parameter],
            VariablePointerValue variable => storageLocals[variable.Declaration],
            _ => throw new UnreachableException()
        };

        private IEnumerable<RegionJump<IShaderValue>> Targets(ShaderRegionBody block) =>
            block.Body.Last switch
            {
                Terminator.D.ReturnExpr<RegionJump<IShaderValue>, IShaderValue> => [],
                Terminator.D.Br<RegionJump<IShaderValue>, IShaderValue> branch => [branch.Target],
                Terminator.D.BrIf<RegionJump<IShaderValue>, IShaderValue> branch =>
                    [branch.TrueTarget, branch.FalseTarget],
                Terminator.D.ReturnVoid<RegionJump<IShaderValue>, IShaderValue> => [],
                _ => Reject<IEnumerable<RegionJump<IShaderValue>>>(
                    $"{Describe(block.Label)}: unsupported terminator {block.Body.Last.GetType().Name}.")
            };

        private static IShaderValue Operand(
            Label label,
            Instruction<IShaderValue, IShaderValue> instruction,
            int index) =>
            instruction[index] ??
            throw new WasmLoweringException(
                $"WASM block {Describe(label)}: {instruction.Operation.Name} operand {index} is missing.");

        private IShaderValue RequireValue(Label label, IShaderValue? value, string role)
        {
            if (value is null)
                Reject($"{Describe(label)}: {role} is missing.");
            return value;
        }

        private IShaderType ValueType(Label label, IShaderValue? value, string role)
        {
            var checkedValue = RequireValue(label, value, role);
            if (checkedValue is LiteralValue { Value: null })
                Reject($"{Describe(label)}: {role} has a missing literal payload.");
            var type = checkedValue.Type;
            if (type is null)
                Reject($"{Describe(label)}: {role} type is missing.");
            return type;
        }

        private void RequireType(Label label, IShaderValue value, IShaderType type, string role)
        {
            var actualType = ValueType(label, value, role);
            if (!actualType.Equals(type))
                Reject($"{Describe(label)}: {role} must be {type.Name}, got {actualType.Name}.");
        }

        private static bool IsI32(IShaderType? type) => type is not null && type.Equals(ShaderType.I32);
        private static bool IsScalar(IShaderType? type) =>
            type is not null && (IsI32(type) || type.Equals(ShaderType.Bool));
        private static string Describe(Label? label) => label?.Name ?? "<unnamed>";
        private static string Describe(IShaderValue? value)
        {
            if (value is null)
                return "<missing>";
            return value is IntermediateValue intermediate && intermediate.Name is { } name
                ? name
                : value.GetType().Name;
        }

        [DoesNotReturn]
        private void Reject(string message) => throw new WasmLoweringException($"{context}: {message}");

        [DoesNotReturn]
        private T Reject<T>(string message) => throw new WasmLoweringException($"{context}: {message}");
    }
}

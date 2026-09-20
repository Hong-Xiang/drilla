using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend;

public sealed class SlangEmitter(ShaderModuleDeclaration<SlangFunctionBody> module)
{
    public ShaderModuleDeclaration<SlangFunctionBody> Module { get; } = module;

    public string Emit() => new PrintSession(Module).Emit();

    private sealed class PrintSession(ShaderModuleDeclaration<SlangFunctionBody> module)
        : IDeclarationVisitor<SlangFunctionBody, Unit>
    {
        private readonly Dictionary<IShaderValue, int> valueIds =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Label, int> labelIds = [];
        private readonly IndentedTextWriter writer = new(new StringWriter());
        private readonly Stack<VisitingEntity> visiting = [];

        public string Emit()
        {
            DumpTypeAliases();
            writer.WriteLine();
            module.Accept(this);
            return writer.InnerWriter.ToString() ?? string.Empty;
        }

        public Unit VisitFunction(FunctionDeclaration declaration)
        {
            if (!module.TryGetBody(declaration, out var body))
                throw new NotSupportedException(
                    $"Slang emission requires a target body for function '{declaration.Name}'.");

            visiting.Push(VisitingEntity.Function);
            WriteAttributes(declaration.Attributes);
            visiting.Push(VisitingEntity.FunctionReturn);
            VisitType(declaration.Return.Type);
            visiting.Pop();
            writer.Write(' ');
            writer.Write(declaration.Name);
            writer.Write('(');
            visiting.Push(VisitingEntity.Parameter);
            foreach (var parameter in declaration.Parameters) parameter.AcceptVisitor(this);
            visiting.Pop();
            writer.Write(')');
            if (declaration.Return.Attributes.Count > 0)
            {
                visiting.Push(VisitingEntity.FunctionReturn);
                WriteAttributes(declaration.Return.Attributes);
                visiting.Pop();
            }

            writer.WriteLine();
            using (writer.IndentedScopeWithBracket())
                WriteBlock(body.Body);
            writer.WriteLine();
            visiting.Pop();
            return default;
        }

        public Unit VisitMember(MemberDeclaration declaration)
        {
            VisitType(declaration.Type);
            writer.Write(declaration.Name);
            writer.WriteLine(';');
            return default;
        }

        public Unit VisitModule(ShaderModuleDeclaration<SlangFunctionBody> declaration)
        {
            foreach (var item in declaration.Declarations) item.AcceptVisitor(this);
            return default;
        }

        public Unit VisitParameter(ParameterDeclaration declaration)
        {
            VisitType(declaration.Type);
            writer.Write(' ');
            writer.Write(declaration.Name);
            WriteAttributes(declaration.Attributes);
            writer.Write(", ");
            return default;
        }

        public Unit VisitStructure(StructureDeclaration declaration)
        {
            writer.Write("struct ");
            writer.Write(declaration.Name);
            using (writer.IndentedScopeWithBracket())
                foreach (var member in declaration.Members)
                    member.AcceptVisitor(this);
            writer.WriteLine();
            return default;
        }

        public Unit VisitValue(ValueDeclaration declaration) =>
            throw new NotSupportedException($"Slang emission does not support value declaration '{declaration.Name}'.");

        public Unit VisitVariable(VariableDeclaration declaration)
        {
            var group = declaration.Attributes.OfType<GroupAttribute>().FirstOrDefault()?.Binding;
            var binding = declaration.Attributes.OfType<BindingAttribute>().FirstOrDefault()?.Binding;
            if ((group, binding) is (int groupValue, int bindingValue))
                writer.WriteLine($"[[vk::binding({bindingValue}, {groupValue})]]");

            var isUniform = declaration.Attributes.OfType<UniformAttribute>().Any();
            if (isUniform) writer.Write("ConstantBuffer<");
            VisitType(declaration.Type);
            if (isUniform) writer.Write('>');
            writer.Write(' ');
            writer.Write(GetValueName(declaration.Value));
            writer.WriteLine(';');
            return default;
        }

        private void WriteBlock(SlangBlock block)
        {
            foreach (var statement in block.Statements) WriteStatement(statement);
        }

        private void WriteStatement(SlangStatement statement)
        {
            switch (statement)
            {
                case SlangDeclare declaration:
                    writer.Write("var ");
                    writer.Write(GetValueName(declaration.Variable.Value));
                    writer.Write(" : ");
                    VisitType(declaration.Variable.Type);
                    writer.WriteLine(';');
                    break;
                case SlangBind binding:
                    writer.Write("let ");
                    writer.Write(GetValueName(binding.Instruction.Result!));
                    writer.Write(" : ");
                    VisitType(binding.Instruction.Result!.Type);
                    writer.Write(" = ");
                    writer.Write(RenderExpression(binding.Instruction));
                    writer.WriteLine(';');
                    break;
                case SlangEffect effect:
                    if (RenderEffect(effect.Instruction) is { Length: > 0 } expression)
                    {
                        writer.Write(expression);
                        writer.WriteLine(';');
                    }
                    break;
                case SlangAssign assignment:
                    writer.Write(RenderPlace(assignment.Target));
                    writer.Write(" = ");
                    writer.Write(RenderOperand(assignment.Value));
                    writer.WriteLine(';');
                    break;
                case SlangScope scope:
                    using (writer.IndentedScopeWithBracket())
                    {
                        WriteMarker("block", scope.OriginalLabel);
                        WriteBlock(scope.Body);
                    }
                    break;
                case SlangIf conditional:
                    writer.Write("if(");
                    writer.Write(RenderOperand(conditional.Condition));
                    writer.WriteLine(')');
                    using (writer.IndentedScopeWithBracket())
                        WriteBlock(conditional.WhenTrue);
                    writer.WriteLine("else");
                    using (writer.IndentedScopeWithBracket())
                        WriteBlock(conditional.WhenFalse);
                    break;
                case SlangDoOnce once:
                    writer.WriteLine("do");
                    using (writer.IndentedScopeWithBracket())
                        WriteBlock(once.Body);
                    writer.WriteLine("while(false);");
                    break;
                case SlangLoop loop:
                    writer.WriteLine("while(true)");
                    using (writer.IndentedScopeWithBracket())
                    {
                        WriteMarker("loop", loop.OriginalLabel);
                        WriteBlock(loop.Body);
                    }
                    break;
                case SlangReturnValue returned:
                    writer.Write("return ");
                    writer.Write(RenderOperand(returned.Value));
                    writer.WriteLine(';');
                    break;
                case SlangReturnVoid:
                    writer.WriteLine("return;");
                    break;
                case SlangBreak:
                    writer.WriteLine("break;");
                    break;
                case SlangContinue:
                    writer.WriteLine("continue;");
                    break;
                default:
                    throw new NotSupportedException($"Unknown Slang statement {statement.GetType().Name}.");
            }
        }

        private string RenderExpression(Instruction<SlangOperand, IShaderValue> instruction)
        {
            var operands = instruction.Operands.ToArray();
            string Operand(int index) => RenderOperand(operands[index]);
            return instruction.Operation switch
            {
                LoadOperation when operands.Length == 1 => Operand(0),
                CallOperation when operands.Length >= 1 =>
                    $"{Operand(0)}({string.Join(',', operands[1..].Select(RenderOperand))})",
                LiteralOperation when operands.Length == 1 => Operand(0),
                IConversionOperation conversion when operands.Length == 1 =>
                    $"{conversion.ResultType.Name}({Operand(0)})",
                IVectorSwizzleGetOperation swizzle when operands.Length == 1 =>
                    $"{Operand(0)}.{swizzle.Pattern.Name}",
                IVectorComponentGetOperation component when operands.Length == 1 =>
                    $"{Operand(0)}.{component.Component.Name}",
                IVectorFromScalarConstructOperation construction when operands.Length == 1 =>
                    $"{construction.ResultType.Name}({Operand(0)})",
                LogicalNotOperation when operands.Length == 1 => $"!{Operand(0)}",
                UnaryNumericArithmeticExpressionOperation<IntType<N32>, UnaryArithmetic.Negate> or
                UnaryNumericArithmeticExpressionOperation<IntType<N64>, UnaryArithmetic.Negate> or
                UnaryNumericArithmeticExpressionOperation<FloatType<N32>, UnaryArithmetic.Negate> or
                UnaryNumericArithmeticExpressionOperation<FloatType<N64>, UnaryArithmetic.Negate>
                    when operands.Length == 1 => $"- {Operand(0)}",
                VectorNumericUnaryOperation<N3, FloatType<N32>, UnaryArithmetic.Negate>
                    when operands.Length == 1 => $"- {Operand(0)}",
                IUnaryExpressionOperation unary when operands.Length == 1 =>
                    $"{unary.Name}({Operand(0)})",
                IBinaryExpressionOperation { BinaryOp: ISymbolOp symbol } when operands.Length == 2 =>
                    $"{Operand(0)} {symbol.Symbol} {Operand(1)}",
                IBinaryExpressionOperation binary when operands.Length == 2 =>
                    $"{binary.Name}({Operand(0)},{Operand(1)})",
                VectorCompositeConstructionOperation vector =>
                    $"vector<{vector.ElementType.Name}, {vector.Size.Value}>" +
                    $"({string.Join(',', operands.Select(RenderOperand))})",
                ZeroConstructorOperation { ResultType: IVecType } => "{}",
                _ => throw MalformedInstruction(instruction)
            };
        }

        private string? RenderEffect(Instruction<SlangOperand, IShaderValue> instruction) =>
            instruction.Operation switch
            {
                NopOperation when instruction.OperandCount == 0 => null,
                CallOperation => RenderExpression(instruction),
                _ => throw MalformedInstruction(instruction)
            };

        private static NotSupportedException MalformedInstruction(
            Instruction<SlangOperand, IShaderValue> instruction) =>
            new($"Malformed Slang target instruction '{instruction.Operation.Name}'.");

        private string RenderOperand(SlangOperand operand) =>
            operand switch
            {
                SlangValueOperand value => GetValueName(value.Value),
                SlangPlaceOperand place => RenderPlace(place.Place),
                _ => throw new NotSupportedException($"Unknown Slang operand {operand.GetType().Name}.")
            };

        private string RenderPlace(SlangPlace place) =>
            place switch
            {
                SlangVariablePlace variable => GetValueName(variable.Variable.Value),
                SlangParameterPlace parameter => parameter.Parameter.Name,
                SlangMemberPlace member => $"{RenderPlace(member.Target)}.{member.Member.Name}",
                SlangComponentPlace component => $"{RenderPlace(component.Target)}.{component.Component}",
                SlangSwizzlePlace swizzle => $"{RenderPlace(swizzle.Target)}.{swizzle.Pattern}",
                _ => throw new NotSupportedException($"Unknown Slang place {place.GetType().Name}.")
            };

        private string GetValueName(IShaderValue value) =>
            value switch
            {
                LiteralValue literal => SlangLiteralFormatter.Source(literal.Value),
                FunctionDeclaration function => function.Name == "mix" ? "lerp" : function.Name,
                VariablePointerValue variable =>
                    $"v_{GetValueId(variable)}_{variable.Declaration.Name}",
                ParameterPointerValue parameter => parameter.Declaration.Name,
                _ => $"v_{GetValueId(value)}"
            };

        private int GetValueId(IShaderValue value)
        {
            if (valueIds.TryGetValue(value, out var index)) return index;
            index = valueIds.Count;
            valueIds.Add(value, index);
            return index;
        }

        private void WriteMarker(string kind, Label? label)
        {
            if (label is null) return;
            writer.Write("// ");
            writer.Write(kind);
            writer.Write(' ');
            writer.Write('^');
            writer.Write(GetLabelId(label));
            writer.WriteLine(label.ToString());
        }

        private int GetLabelId(Label label)
        {
            if (labelIds.TryGetValue(label, out var index)) return index;
            index = labelIds.Count;
            labelIds.Add(label, index);
            return index;
        }

        private Unit WriteAttribute(IShaderAttribute attribute)
        {
            switch (attribute)
            {
                case ShaderMethodAttribute:
                case IShaderMetadataAttribute:
                    break;
                case FragmentAttribute:
                    writer.WriteLine("[shader(\"fragment\")]");
                    break;
                case ComputeAttribute:
                    writer.WriteLine("[shader(\"compute\")]");
                    break;
                case WorkgroupSizeAttribute size:
                    writer.WriteLine($"[numthreads({size.X}, {size.Y}, {size.Z})]");
                    break;
                case VertexAttribute:
                    writer.WriteLine("[shader(\"vertex\")]");
                    break;
                case BuiltinAttribute builtin:
                    writer.Write(" : ");
                    writer.Write(builtin.Slot switch
                    {
                        BuiltinBinding.position => "SV_POSITION",
                        BuiltinBinding.vertex_index => "SV_VertexId",
                        BuiltinBinding.global_invocation_id => "SV_DispatchThreadID",
                        _ => throw new NotSupportedException(
                            $"Unsupported Slang builtin binding {builtin.Slot}.")
                    });
                    break;
                case LocationAttribute location:
                    writer.Write(visiting.Peek() switch
                    {
                        VisitingEntity.FunctionReturn => $" : SV_TARGET{location.Binding}",
                        VisitingEntity.Parameter => $" : TEXCOORD{location.Binding}",
                        _ => throw new NotSupportedException(
                            $"Slang location is invalid while visiting {visiting.Peek()}.")
                    });
                    break;
                default:
                    throw new NotSupportedException($"Slang attribute {attribute} is not supported.");
            }
            return default;
        }

        private void WriteAttributes(IEnumerable<IShaderAttribute> attributes)
        {
            foreach (var attribute in attributes)
            {
                WriteAttribute(attribute);
                writer.Write(' ');
            }
        }

        private void VisitType(IShaderType type) => writer.Write(type is UnitType ? "void" : type.Name);

        private void DumpTypeAliases()
        {
            TypeAlias("f64", "double");
            TypeAlias("f32", "float");
            TypeAlias("i64", "int64_t");
            TypeAlias("u32", "uint");
            TypeAlias("i32", "int");
            TypeAlias("vec4<t>", "vector<t, 4>");
            TypeAlias("vec3<t>", "vector<t, 3>");
            TypeAlias("vec2<t>", "vector<t, 2>");
        }

        private void TypeAlias(string name, string target)
        {
            writer.Write("typealias ");
            writer.Write(name);
            writer.Write(" = ");
            writer.Write(target);
            writer.WriteLine(';');
        }

        private enum VisitingEntity
        {
            Function,
            Parameter,
            FunctionReturn
        }
    }
}

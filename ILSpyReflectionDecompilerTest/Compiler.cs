using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.Nat;
using Lokad.ILPack.IL;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ILSpyReflectionDecompilerTest;

public class TypeJsonConverter : JsonConverter<Type>
{
    public override Type Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(
        Utf8JsonWriter writer,
        Type value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Name);
}

internal sealed class Compiler
{
    IEnumerable<IStatement> Interpret(IReadOnlyList<Instruction> instructions)
    {
        var stack = new Stack<IExpression>();
        for (var i = 0; i < instructions.Count; i++)
        {
            var inst = instructions[i];
            switch (inst.OpCode.ToILOpCode())
            {
                case ILOpCode.Ldc_i4:
                case ILOpCode.Ldc_i4_s:
                case ILOpCode.Ldc_i4_0:
                case ILOpCode.Ldc_i4_1:
                case ILOpCode.Ldc_i4_2:
                case ILOpCode.Ldc_i4_3:
                case ILOpCode.Ldc_i4_4:
                case ILOpCode.Ldc_i4_5:
                case ILOpCode.Ldc_i4_6:
                case ILOpCode.Ldc_i4_7:
                case ILOpCode.Ldc_i4_8:
                {
                    // Load integer constant onto the stack
                    int value = inst.Operand is sbyte v ? v : (int)inst.Operand;
                    stack.Push(new LiteralValueExpression(new I32Literal(value)));
                    break;
                }

                case ILOpCode.Ldstr:
                    throw new NotSupportedException();
                case ILOpCode.Add:
                {
                    var r = stack.Pop();
                    var l = stack.Pop();
                    stack.Push(NumericBinaryArithmeticOperation.CreateExpression<BinaryArithmetic.Add>(l, r));
                    break;
                }
                case ILOpCode.Sub:
                {
                    var r = stack.Pop();
                    var l = stack.Pop();
                    stack.Push(NumericBinaryArithmeticOperation.CreateExpression<BinaryArithmetic.Sub>(l, r));
                    break;
                }
                case ILOpCode.Call:
                    //var methodInfo = (MethodInfo)inst.Operand;
                    //var parameters = methodInfo.GetParameters();
                    //var args = new IExpression[parameters.Length];
                    //for (int j = parameters.Length - 1; j >= 0; j--)
                    //{
                    //    args[j] = stack.Pop();
                    //}
                    //stack.Push(new FunctionCallExpression(methodInfo, [.. args]));
                    //break;
                    throw new NotImplementedException();

                case ILOpCode.Ret:
                    yield return new ReturnStatement(stack.Pop());
                    break;
                default:
                    throw new NotSupportedException($"Unsupported OpCode: {inst.OpCode}");
            }
        }
    }

    public CompoundStatement Compile(MethodBase method)
    {
        var instructions = method.GetInstructions();
        return new([.. Interpret(instructions)]);
    }
}
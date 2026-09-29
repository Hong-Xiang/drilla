using System.Reflection.Emit;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.NativeTest;

public sealed partial class NativeComputeDifferentialTests
{
    private sealed record WordGolden(
        uint Value, uint Left1, uint Left31, uint SignedRight1, uint SignedRight31,
        uint UnsignedRight1, uint UnsignedRight31, uint Complement);

    private static readonly int[] ShiftCounts =
        [-1, 0, 1, 31, 32, 33, 63, 64, int.MinValue, int.MaxValue];

    // Literal bit-pattern goldens are independent of the CLR and shader operations.
    private static readonly WordGolden[] BitWords =
    [
        new(0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0xffffffffu),
        new(0x00000001u, 0x00000002u, 0x80000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0x00000000u, 0xfffffffeu),
        new(0x7fffffffu, 0xfffffffeu, 0x80000000u, 0x3fffffffu, 0x00000000u, 0x3fffffffu, 0x00000000u, 0x80000000u),
        new(0x80000000u, 0x00000000u, 0x00000000u, 0xc0000000u, 0xffffffffu, 0x40000000u, 0x00000001u, 0x7fffffffu),
        new(0x80000001u, 0x00000002u, 0x80000000u, 0xc0000000u, 0xffffffffu, 0x40000000u, 0x00000001u, 0x7ffffffeu),
        new(0xffffffffu, 0xfffffffeu, 0x80000000u, 0xffffffffu, 0xffffffffu, 0x7fffffffu, 0x00000001u, 0x00000000u),
        new(0xfffffff9u, 0xfffffff2u, 0x80000000u, 0xfffffffcu, 0xffffffffu, 0x7ffffffcu, 0x00000001u, 0x00000006u),
        new(0xfffffffeu, 0xfffffffcu, 0x00000000u, 0xffffffffu, 0xffffffffu, 0x7fffffffu, 0x00000001u, 0x00000001u)
    ];

    private static uint[] ShiftGoldens(Func<WordGolden, (uint One, uint ThirtyOne)> select) =>
        BitWords.SelectMany(word =>
        {
            var (one, thirtyOne) = select(word);
            return new[] { thirtyOne, word.Value, one, thirtyOne, word.Value, one,
                thirtyOne, word.Value, word.Value, thirtyOne };
        }).ToArray();

    private static int[] SignedWords(IEnumerable<uint> words) =>
        words.Select(word => unchecked((int)word)).ToArray();

    [Fact]
    public void Integer_bitops_public_CSharp_matches_literal_goldens_and_actual_Cil_contract()
    {
        var cases = BitWords.SelectMany(word => ShiftCounts.Select(count => (word.Value, Count: count))).ToArray();
        var left = ShiftGoldens(word => (word.Left1, word.Left31));
        var arithmetic = ShiftGoldens(word => (word.SignedRight1, word.SignedRight31));
        var logical = ShiftGoldens(word => (word.UnsignedRight1, word.UnsignedRight31));

        AssertLanes(SignedWords(left), cases.Select(c => SignedBitOps.ShiftLeft(unchecked((int)c.Value), c.Count)).ToArray());
        AssertLanes(SignedWords(arithmetic), cases.Select(c => SignedBitOps.ShiftRight(unchecked((int)c.Value), c.Count)).ToArray());
        AssertLanes(SignedWords(logical), cases.Select(c => SignedBitOps.ShiftRightUnsigned(unchecked((int)c.Value), c.Count)).ToArray());
        AssertLanes(left, cases.Select(c => UnsignedBitOps.ShiftLeft(c.Value, c.Count)).ToArray());
        AssertLanes(logical, cases.Select(c => UnsignedBitOps.ShiftRight(c.Value, c.Count)).ToArray());
        AssertLanes(SignedWords(BitWords.Select(word => word.Complement)),
            BitWords.Select(word => SignedBitOps.Complement(unchecked((int)word.Value))).ToArray());
        AssertLanes(BitWords.Select(word => word.Complement).ToArray(),
            BitWords.Select(word => UnsignedBitOps.Complement(word.Value)).ToArray());

        foreach (var (type, name, opcode) in new[]
        {
            (typeof(SignedBitOps), nameof(SignedBitOps.ShiftLeft), OpCodes.Shl),
            (typeof(SignedBitOps), nameof(SignedBitOps.ShiftRight), OpCodes.Shr),
            (typeof(SignedBitOps), nameof(SignedBitOps.ShiftRightUnsigned), OpCodes.Shr_Un),
            (typeof(UnsignedBitOps), nameof(UnsignedBitOps.ShiftLeft), OpCodes.Shl),
            (typeof(UnsignedBitOps), nameof(UnsignedBitOps.ShiftRight), OpCodes.Shr_Un)
        })
        {
            var method = type.GetMethod(name)!;
            var cil = CilMethodDecoder.Decode(method).Instructions;
            var shift = Assert.Single(cil, instruction => instruction.Instruction.OpCode == opcode);
            Assert.Equal(OpCodes.And, cil[shift.Index - 1].Instruction.OpCode);
            Assert.Equal(OpCodes.Ldc_I4_S, cil[shift.Index - 2].Instruction.OpCode);
            Assert.Equal(31, Convert.ToInt32(cil[shift.Index - 2].Instruction.Operand));
            output.WriteLine($"{method}: actual Roslyn ldc.i4.s 31; and; {opcode}");
        }
        foreach (var type in new[] { typeof(SignedBitOps), typeof(UnsignedBitOps) })
        {
            var cil = CilMethodDecoder.Decode(type.GetMethod(nameof(SignedBitOps.Complement))!).Instructions;
            Assert.Single(cil, instruction => instruction.Instruction.OpCode == OpCodes.Not);
            Assert.DoesNotContain(cil, instruction => instruction.Instruction.OpCode == OpCodes.Ceq);
        }
    }

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Integer_bitops_signed_public_CSharp_matches_exact_hardware_goldens()
    {
        var pairs = BitWords.SelectMany(word => ShiftCounts.SelectMany(count =>
            new[] { unchecked((int)word.Value), count })).ToArray();
        var source = new SignedBitOps();
        await RunCompiledAsync(source, nameof(SignedBitOps.Left), pairs,
            SignedWords(ShiftGoldens(word => (word.Left1, word.Left31))), int.MinValue);
        await RunCompiledAsync(source, nameof(SignedBitOps.Right), pairs,
            SignedWords(ShiftGoldens(word => (word.SignedRight1, word.SignedRight31))), int.MinValue);
        await RunCompiledAsync(source, nameof(SignedBitOps.LogicalRight), pairs,
            SignedWords(ShiftGoldens(word => (word.UnsignedRight1, word.UnsignedRight31))), int.MinValue);
        await RunCompiledAsync(source, nameof(SignedBitOps.Not),
            SignedWords(BitWords.Select(word => word.Value)),
            SignedWords(BitWords.Select(word => word.Complement)), int.MinValue);
    }

    [Fact]
    [Trait("Category", "GPU")]
    public async Task Integer_bitops_unsigned_public_CSharp_matches_exact_hardware_goldens_and_negative_control()
    {
        var pairs = BitWords.SelectMany(word => ShiftCounts.SelectMany(count =>
            new[] { word.Value, unchecked((uint)count) })).ToArray();
        var source = new UnsignedBitOps();
        var expected = ShiftGoldens(word => (word.Left1, word.Left31));
        var actual = await RunCompiledAsync(source, nameof(UnsignedBitOps.Left), pairs, expected, 0xD15EA5E0u);
        var corrupted = (uint[])actual.Clone();
        corrupted[10] = 0u;
        var error = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertLanes(expected, corrupted));
        Assert.Contains("lane 10: expected 2147483648, actual 0", error.Message);
        output.WriteLine($"Negative control (corrupted actual GPU high bit): {error.Message}");

        await RunCompiledAsync(source, nameof(UnsignedBitOps.Right), pairs,
            ShiftGoldens(word => (word.UnsignedRight1, word.UnsignedRight31)), 0xD15EA5E0u);
        await RunCompiledAsync(source, nameof(UnsignedBitOps.Not),
            BitWords.Select(word => word.Value).ToArray(),
            BitWords.Select(word => word.Complement).ToArray(), 0xD15EA5E0u);
    }

    private sealed class SignedBitOps : ISharpShader
    {
        [Group(0), Binding(0)]
        private static readonly StructuredBuffer<int> Input = default;

        [Group(0), Binding(1)]
        private static readonly RWStructuredBuffer<int> Output = default;

        [ShaderMethod]
        public static int ShiftLeft(int value, int count) => value << count;

        [ShaderMethod]
        public static int ShiftRight(int value, int count) => value >> count;

        [ShaderMethod]
        public static int ShiftRightUnsigned(int value, int count) => value >>> count;

        [ShaderMethod]
        public static int Complement(int value) => ~value;

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Left([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && 2u * i + 1u < Input.Length)
                Output[i] = ShiftLeft(Input[2u * i], Input[2u * i + 1u]);
        }

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Right([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && 2u * i + 1u < Input.Length)
                Output[i] = ShiftRight(Input[2u * i], Input[2u * i + 1u]);
        }

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void LogicalRight([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && 2u * i + 1u < Input.Length)
                Output[i] = ShiftRightUnsigned(Input[2u * i], Input[2u * i + 1u]);
        }

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Not([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && i < Input.Length)
                Output[i] = Complement(Input[i]);
        }
    }

    private sealed class UnsignedBitOps : ISharpShader
    {
        [Group(0), Binding(0)]
        private static readonly StructuredBuffer<uint> Input = default;

        [Group(0), Binding(1)]
        private static readonly RWStructuredBuffer<uint> Output = default;

        [ShaderMethod]
        public static uint ShiftLeft(uint value, int count) => value << count;

        [ShaderMethod]
        public static uint ShiftRight(uint value, int count) => value >> count;

        [ShaderMethod]
        public static uint Complement(uint value) => ~value;

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Left([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && 2u * i + 1u < Input.Length)
                Output[i] = ShiftLeft(Input[2u * i], (int)Input[2u * i + 1u]);
        }

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Right([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && 2u * i + 1u < Input.Length)
                Output[i] = ShiftRight(Input[2u * i], (int)Input[2u * i + 1u]);
        }

        [Compute, WorkgroupSize(128, 1, 1)]
        public static void Not([Builtin(BuiltinBinding.global_invocation_id)] vec3u32 id)
        {
            var i = id.x;
            if (i < Output.Length && i < Input.Length)
                Output[i] = Complement(Input[i]);
        }
    }
}

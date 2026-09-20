using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

public abstract record CilStackType
{
    private CilStackType(IShaderType shaderType)
    {
        ShaderType = shaderType;
    }

    public IShaderType ShaderType { get; }

    public sealed record Int32 : CilStackType
    {
        private Int32() : base(IntType<N32>.Instance)
        {
        }

        public static Int32 Instance { get; } = new();
    }

    public sealed record Int64 : CilStackType
    {
        private Int64() : base(IntType<N64>.Instance)
        {
        }

        public static Int64 Instance { get; } = new();
    }

    public sealed record Float32 : CilStackType
    {
        private Float32() : base(FloatType<N32>.Instance)
        {
        }

        public static Float32 Instance { get; } = new();
    }

    public sealed record Float64 : CilStackType
    {
        private Float64() : base(FloatType<N64>.Instance)
        {
        }

        public static Float64 Instance { get; } = new();
    }

    public sealed record Value : CilStackType
    {
        internal Value(IShaderType type) : base(type)
        {
            Type = type;
        }

        public IShaderType Type { get; }
    }

    public sealed record ObjectReference : CilStackType
    {
        internal ObjectReference(IShaderType type) : base(type)
        {
            Type = type;
        }

        public IShaderType Type { get; }
    }

    public sealed record ManagedPointer : CilStackType
    {
        internal ManagedPointer(IPtrType type) : base(type)
        {
            Type = type;
        }

        public IPtrType Type { get; }
    }

    internal static CilStackType FromShaderType(IShaderType type) =>
        type switch
        {
            BoolType or IntType<N8> or IntType<N16> or IntType<N32> or
                UIntType<N8> or UIntType<N16> or UIntType<N32> => Int32.Instance,
            IntType<N64> or UIntType<N64> => Int64.Instance,
            FloatType<N32> => Float32.Instance,
            FloatType<N64> => Float64.Instance,
            IPtrType pointer => new ManagedPointer(pointer),
            OpaqueType or IRefType => new ObjectReference(type),
            _ => new Value(type)
        };
}

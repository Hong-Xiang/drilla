# CLSL Type System Reference

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

The CLSL type system provides a bridge between C# types and shader language types,
enabling seamless translation of C# code to shader code.
Additional shader attributes are introduced to add more shader specific information to C# types.
Additional shader meta attributes are introduced to add more information about how to connect C# types/methods to CLSL types/methods.

## Core Type Interfaces

### Base Types

`IShaderType` is the base interface for all shader types.

### Type Categories

1. **Scalar Types**
   ```csharp
   public interface IScalarType : IPlainType, IStorableType, ICreationFixedFootprintType
   {
       IBitWidth BitWidth { get; }
       IScalarConversionOperation GetConversionToOperation<TTarget>();
       IVecType GetVecType<TRank>();
   }
   ```

2. **Vector Types**
   ```csharp
   public interface IVecType : IPlainType
   {
       IRank Size { get; }
       IShaderType ElementType { get; }
   }
   ```

3. **Matrix Types**
   ```csharp
   public interface IMatType : IPlainType
   {
       IRank Row { get; }
       IRank Column { get; }
       IShaderType ElementType { get; }
   }
   ```

## Type Mapping

### Primitive Types
| C# Type  | CLSL Type | WGSL Type |
|----------|-----------|------------|
| bool     | `BoolType`  | bool      |
| int      | `IntType<N32>`       | i32       |
| uint     | `UIntType<N32>`      | u32       |
| float    | `FloatType<N32>`      | f32       |
| double   | `FloatType<N64>`       | f64       |

### Vector Types
| C# Type     | CLSL Type            | WGSL Type |
|-------------|----------------------|------------|
| Vector2     | VecType<N2, FloatType<N32>>    | vec2<f32> |
| Vector3     | VecType<N3, FloatType<N32>>    | vec3<f32> |
| Vector4     | VecType<N4, FloatType<N32>>    | vec4<f32> |

## Type Features

### Vector Features

1. **Swizzling Support**
   ```csharp
   public interface ISizedComponent<TRank>
   {
       IOperation ComponentGetOperation<TVector, TElement>();
       IOperation ComponentSetOperation<TVector, TElement>();
       ISizedPattern<TRank> PatternAfterComponent(ISizedComponent<TRank> c);
   }
   ```

2. **SIMD Optimization**
   - Automatic SIMD for vectors >= 64 bits
   - Special handling for vec3 using vec4 storage
   - Platform-specific optimizations

### Structure Types

1. **Declaration**
   ```csharp
   public sealed class StructureDeclaration : IShaderType, IDeclaration
   {
       public string Name { get; init; }
       public ImmutableArray<MemberDeclaration> Members { get; set; }
       public ImmutableHashSet<IShaderAttribute> Attributes { get; set; }
   }
   ```

2. **Member Access**
   - Field reflection
   - Property handling
   - Attribute processing

## Type Operations

### Arithmetic Operations

1. **Binary Operations**
   ```csharp
   public sealed record class BinaryArithmeticOperatorDefinition(
       BinaryArithmetic.OpKind Op,
       IShaderType Left,
       IShaderType Right,
       IShaderType Result);
   ```

2. **Unary Operations**
   ```csharp
   public sealed record class UnaryArithmeticOperatorDefinition(
       UnaryArithmeticOp Op,
       IShaderType Source,
       IShaderType Result);
   ```

### Type Conversion

1. **Implicit Conversions**
   - Widening numeric conversions
   - Vector component widening
   - Structure compatibility

2. **Explicit Conversions**
   - Numeric type casts
   - Vector type conversions
   - Custom conversion operations

## Resource Types

### Texture Types
```csharp
public interface ITexture2D<T>
{
    T Sample(ISampler sampler, Vector2 coordinate);
    T Sample(ISampler sampler, Vector2D<float> coordinate);
}
```

### Buffer Types
```csharp
[Group(0)]
[Binding(0)]
public struct UniformBuffer
{
    public Matrix4x4 Transform;
    public Vector4 Color;
}
```

## Type Context

The type system maintains a compilation context that tracks:
- Known types and their relationships
- Function declarations and bodies
- Variable declarations and scopes
- Type metadata and attributes

## Implementation Notes

1. **Performance Considerations**
   - Zero-cost abstractions where possible
   - Efficient type comparison and equality
   - Minimal runtime overhead

2. **Safety Features**
   - Compile-time type checking
   - Resource binding validation
   - Memory layout verification

3. **Extensibility**
   - Custom type definitions
   - User-defined attributes
   - Backend-specific type mappings

See also:
- [Type System Implementation](./implementation.md#type-system-implementation)
- [Shader Language Mapping](./shader_mapping.md)
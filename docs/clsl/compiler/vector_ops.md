# Vector Operations and Swizzling

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

CLSL implements a sophisticated vector operation system with full swizzling support, SIMD optimization where possible, and zero-cost abstractions for basic operations. The implementation is parameterized over vector rank and element type, providing a flexible yet performant foundation for shader computations.

## Vector Implementation Strategies

### Component-based Implementation
Used for smaller vectors or when SIMD isn't beneficial:
- Direct component storage
- Individual getters/setters
- Per-component operations

### SIMD-optimized Implementation
Used for vectors >= 64 bits:
- Uses System.Numerics.Vector
- Special vec3 handling using vec4 storage
- Optimized arithmetic operations

## Swizzle System

### Component Definition
```csharp
public interface IComponent
{
    ComponentKind Kind { get; }
    string Name { get; }
    SwizzleComponent LegacySwizzleComponent { get; }
}
```

### Pattern System
```csharp
public interface IPattern<TSelf>
{
    IEnumerable<IComponent> Components { get; }
    string Name { get; }
    IVecType SourceType<TElement>();
    IVecType TargetType<TElement>();
    bool HasDuplicateComponent { get; }
}
```

### Supported Patterns
1. **Single Component**
   - x, y, z, w selections
   - Direct component access
   - Read/write support

2. **Two Components**
   - xy, xz, yw, etc.
   - Returns vec2 result
   - Write if no duplicates

3. **Three Components**
   - xyz, xyw, zyx, etc.
   - Returns vec3 result
   - Write if no duplicates

4. **Four Components**
   - xyzw, wzyx, etc.
   - Returns vec4 result
   - Write if no duplicates

## Operation Implementation

### Vector Arithmetic
1. **Component-wise**
   ```csharp
   // vec + vec
   x = left.x + right.x
   y = left.y + right.y
   ...
   
   // vec + scalar
   x = left.x + right
   y = left.y + right
   ...
   ```

2. **SIMD Operations**
   ```csharp
   // Direct SIMD operation
   return new() { Data = left.Data + right.Data };
   ```

### Swizzle Operations

1. **Get Operations**
   ```csharp
   // Component get
   public T ComponentGet<T>(int index);
   
   // Pattern get
   public IVecType SwizzleGet<TPattern>();
   ```

2. **Set Operations**
   ```csharp
   // Component set
   public void ComponentSet<T>(int index, T value);
   
   // Pattern set (if no duplicates)
   public void SwizzleSet<TPattern>(IVecType value);
   ```

## Code Generation

### Vector Type Generation
```csharp
[StructLayout(...)]
public partial struct vec4f32
{
    // SIMD backing field when applicable
    internal Vector128<float> Data;
    
    // Component access
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
    public float w { get; set; }
    
    // Generated swizzles
    public vec2f32 xy { get; set; }
    public vec3f32 xyz { get; set; }
    // etc...
}
```

### Operation Generation
```csharp
// Binary operations
[OperationMethod]
public static vec4f32 operator+(vec4f32 left, vec4f32 right);

// Unary operations
[OperationMethod]
public static vec4f32 operator-(vec4f32 v);
```

## Performance Considerations

### Memory Layout
- Aligned storage for SIMD
- Efficient component access
- Cache-friendly patterns

### Operation Costs
- Zero-cost abstractions
- Inlined simple operations
- Optimized SIMD paths

### Optimization Opportunities
- Swizzle combination
- Operation fusion
- Dead write elimination

## Examples

### Basic Usage
```csharp
vec4f32 v = new(1,2,3,4);
vec2f32 xy = v.xy;  // Swizzle get
v.zw = xy;          // Swizzle set
```

### Generated WGSL
```wgsl
let v = vec4<f32>(1.0, 2.0, 3.0, 4.0);
let xy = v.xy;
v.zw = xy;
```

See also:
- [Type System](../type_system.md)
- [SIMD Optimization](./optimizations.md)
- [WGSL Backend](../backends/wgsl.md)
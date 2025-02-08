# WGSL Backend

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

The WGSL (WebGPU Shading Language) backend is the primary code generation target for CLSL. It transforms the CLSL IR into valid WGSL code while preserving type safety and performance characteristics.

## Translation Strategy

### Type Translation

1. **Scalar Types**
   ```wgsl
   bool   -> bool
   i32    -> i32
   u32    -> u32
   f32    -> f32
   f64    -> f32 (with precision warning)
   ```

2. **Vector Types**
   ```wgsl
   vec2<T> -> vec2<T>
   vec3<T> -> vec3<T>
   vec4<T> -> vec4<T>
   ```

3. **Structure Types**
   ```wgsl
   struct VertexInput {
       @location(0) position: vec3<f32>,
       @location(1) normal: vec3<f32>,
       @location(2) uv: vec2<f32>,
   }
   ```

### Resource Bindings

1. **Uniform Buffers**
   ```wgsl
   @group(0) @binding(0)
   var<uniform> ubo: UniformBuffer;
   ```

2. **Storage Buffers**
   ```wgsl
   @group(0) @binding(1)
   var<storage> data: array<f32>;
   ```

3. **Textures and Samplers**
   ```wgsl
   @group(1) @binding(0)
   var texture: texture_2d<f32>;
   @group(1) @binding(1)
   var sampler: sampler;
   ```

## Code Generation

### Function Translation
1. **Entry Points**
   ```wgsl
   @vertex
   fn vs_main(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32>
   ```

2. **Helper Functions**
   ```wgsl
   fn compute_normal(a: vec3<f32>, b: vec3<f32>, c: vec3<f32>) -> vec3<f32>
   ```

### Control Flow

1. **If Statements**
   ```wgsl
   if condition {
       // true branch
   } else {
       // false branch
   }
   ```

2. **Loops**
   ```wgsl
   loop {
       if condition {
           break;
       }
       // loop body
       continue;
   }
   ```

## Special Features

### Vector Swizzling
- Direct translation of CLSL swizzle operations
- Component extraction and construction
- Assignment swizzling with validation

### Built-in Functions
- Mathematical operations
- Vector/matrix operations
- Texture sampling

## Implementation Details

### Code Generation Pipeline

1. **IR Analysis**
   - Type collection
   - Entry point identification
   - Resource usage analysis

2. **WGSL Structure Generation**
   - Type declarations
   - Global variables
   - Function signatures

3. **Statement Translation**
   - Expression generation
   - Control flow structuring
   - Resource access

### Validation

1. **WGSL Constraints**
   - Valid entry point signatures
   - Resource binding limits
   - Type compatibility

2. **WebGPU Requirements**
   - Storage class validation
   - Address space rules
   - Entry point requirements

### Optimizations

1. **Vector Operations**
   - Swizzle combination
   - Component-wise operation fusion
   - Common subexpression elimination

2. **Control Flow**
   - Branch simplification
   - Loop optimizations
   - Dead code elimination

## Usage Example

C# Input:
```csharp
[Vertex]
public static float4 VertexShader(
    [VertexIndex] uint index,
    [Uniform] Transform transform)
{
    float3 position = vertices[index];
    return mul(transform.ViewProj, float4(position, 1.0f));
}
```

WGSL Output:
```wgsl
struct Transform {
    view_proj: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> transform: Transform;

@vertex
fn vertex_shader(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {
    let position = vertices[index];
    return transform.view_proj * vec4<f32>(position, 1.0);
}
```

See also:
- [WebGPU Resource Binding](../../graphics/webgpu_bindings.md)
- [WGSL Validation Rules](./wgsl_validation.md)
- [Backend Optimization Passes](../compiler/passes.md)
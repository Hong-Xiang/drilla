# CLSL Intermediate Representation Specification

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

CLSL IR is a stack-based intermediate representation designed specifically for shader compilation. It combines concepts from WebAssembly, .NET CIL, and SPIR-V to create an IR that is both easy to generate from C# and efficient to translate to various shader languages.

## Design Principles

1. **Type Richness**
   - Every instruction carries complete type information
   - No implicit type conversions
   - Full shader type system support

2. **Stack-Based Operation**
   - Simple instruction format
   - Explicit evaluation stack management
   - Easy to validate and transform

3. **Structured Control Flow**
   - Block-based nesting
   - Direct mapping to high-level constructs
   - Natural translation to shader languages

## Instruction Set

### Stack Instructions

1. **Constant Loading**
   ```
   const.i32 <value>    ; Push 32-bit integer
   const.f32 <value>    ; Push 32-bit float
   const.bool <value>   ; Push boolean
   ```

2. **Stack Manipulation**
   ```
   pop                  ; Remove top value
   dup                  ; Duplicate top value
   ```

3. **Memory Operations**
   ```
   load <target>        ; Load from variable/parameter
   store <target>       ; Store to variable/parameter
   load.address <target>; Load address for member access
   ```

### Control Flow Instructions

1. **Basic Control**
   ```
   br <label>          ; Unconditional branch
   br_if <label>       ; Conditional branch
   return              ; Return from function
   ```

2. **Structured Control**
   ```
   block               ; 
   loop                ; Begin a loop
   if-then-else        ; Begin if construct
   ```
   all structured control flow instructions has similar semantics like in WebAssembly

### Arithmetic Instructions

1. **Binary Operations**
   ```
   add.i32            ; Integer addition
   add.f32            ; Float addition
   sub.i32            ; Integer subtraction
   mul.f32            ; Float multiplication
   div.f32            ; Float division
   ```

2. **Vector Operations**
   ```
   vec4.construct     ; Construct vec4 from components
   vec3.swizzle.xyz   ; Apply swizzle pattern
   vec4.dot           ; Vector dot product
   ```

## Control Flow Structure

### Basic Blocks
```
block_0:
    const.f32 1.0
    const.f32 2.0
    add.f32
    br block_1

block_1:
    return
```

### Structured Control Flow
```
block main:
    loop l:
        // Loop body
        br_if l
    if:
        // True branch
    else:
        // False branch
```

## Type System Integration

### Type Declarations
```
struct VertexInput:
    pos: vec3<f32>
    normal: vec3<f32>
    uv: vec2<f32>

struct UniformBuffer:
    view: mat4x4<f32>
    proj: mat4x4<f32>
```

### Function Signatures
```
@vertex
fn main(
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>
) -> @builtin(position) vec4<f32>
```

## Validation Rules

1. **Type Checking**
   - Stack effect validation
   - Type compatibility verification
   - Resource access validation

2. **Control Flow**
   - Structured nesting
   - Reachability analysis
   - Stack consistency

3. **Resource Usage**
   - Binding validation
   - Access pattern checking
   - Stage compatibility

## Memory Model

1. **Storage Classes**
   - Function locals
   - Module globals
   - Uniform buffers
   - Storage buffers

2. **Address Spaces**
   - Private
   - Uniform
   - Storage
   - Workgroup

## Optimization Opportunities

1. **Instruction-Level**
   - Constant folding
   - Common subexpression elimination
   - Dead code elimination

2. **Control Flow**
   - Loop optimization
   - Branch simplification
   - Block merging

3. **Vector/Matrix**
   - Swizzle optimization
   - Matrix multiplication patterns
   - SIMD-friendly transformations

See also:
- [WGSL Backend](./backends/wgsl.md)
- [IR Transformation Passes](./compiler/passes.md)
- [Validation Rules](./compiler/validation.md)
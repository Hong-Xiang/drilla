# CLSL (C# to Shader Language) System

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

CLSL is a sophisticated compiler system that translates C# code into various shader languages. It's designed to provide a seamless development experience by allowing developers to write shader code directly in C# while maintaining full type safety and IDE support.

## Key Features

1. **Native C# Shader Authoring**
   - Write shaders using standard C# syntax
   - Full IDE support including IntelliSense
   - Compile-time type checking
   - Attribute-based shader stage and resource binding

2. **Multi-stage Compilation Pipeline**
   - Runtime reflection-based analysis
   - Stack-based intermediate representation
   - Structured control flow analysis
   - Multiple backend targets

3. **Type System Integration**
   - Seamless mapping between C# and shader types
   - Support for vectors, matrices, and custom structures
   - Automatic SIMD optimization for supported types

## Compiler Architecture

### Frontend

The compiler frontend is primarily based on runtime reflection, analyzing C# code at runtime:

```csharp
public interface ICLSLCompiler
{
    ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> Reflect(ISharpShader shader);
    ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Compile(ISharpShader shader);
    ValueTask<string> EmitWGSL(ISharpShader module);
}
```

### Intermediate Representation

CLSL uses a stack-based IR inspired by WebAssembly:

1. **Instruction Set**
   - Type-aware instructions (e.g., i32.add, f32.mul)
   - Structured control flow primitives
   - Full type information preservation

2. **Control Flow**
   - Block-based structure
   - Support for loops and conditionals
   - Dominator tree analysis

### Type System

The type system bridges C# and shader languages:

1. **Scalar Types**
   - Numeric types (int, float, etc.)
   - Boolean type
   - Precise bit-width control

2. **Vector Types**
   - SIMD-optimized implementations
   - Swizzling support
   - Component-wise operations

3. **Custom Types**
   - Structure support
   - Resource binding types
   - Shader-specific attributes

## Usage Example

```csharp
public struct SimpleShader : ISharpShader
{
    [Vertex]
    public static float4 VertexShader([VertexIndex] uint index)
    {
        // C# code that gets compiled to shader
        return new float4(position, 1.0f);
    }
}
```

## Compilation Stages

1. **Reflection Analysis**
   - Method and type inspection
   - Attribute processing
   - Type mapping

2. **IR Generation**
   - Stack-based instruction generation
   - Control flow analysis
   - Optimization passes

3. **Backend Code Generation**
   - WGSL output (primary target)
   - Future backend support planned

## Design Decisions

1. **Why Stack-based IR?**
   - Simple instruction format
   - Easy to analyze and transform
   - Natural mapping to many shader languages

2. **Runtime Reflection Frontend**
   - Direct integration with C# runtime
   - No need for separate compilation step
   - Access to full type information

See also:
- [Compiler Implementation Details](./implementation.md)
- [Type System Reference](./type_system.md)
- [IR Specification](./ir_spec.md)
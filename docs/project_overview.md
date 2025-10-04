> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

# DualDrill Engine: GPU Rendering and Computation Framework

## Project Overview

DualDrill Engine is a code-first GPU rendering and computation framework developed in C#. It provides a unified platform for graphics programming and general-purpose GPU computing, with a strong emphasis on compile-time shader generation and type safety.

## Core Components

1. [CLSL (C# to Shader Language) System](./clsl/index.md)
   - Advanced shader compilation pipeline converting C# code to various shader targets
   - Strong type system integration with C#
   - Multiple compilation stages with intermediate representations
   - Support for WGSL, with planned support for SPIR-V and CUDA

2. [GPU Abstraction Layer](./graphics/index.md)
   - High-level WebGPU-based abstraction
   - Platform-agnostic GPU resource management
   - Shader pipeline management

3. [Mathematics Library](./mathematics/index.md)
   - GPU-optimized mathematical operations
   - Vector, matrix, and quaternion implementations
   - Code generation for SIMD optimization

4. [Engine Core](./engine/index.md)
   - Rendering pipeline management
   - Resource handling and synchronization
   - Cross-platform implementation

## Architecture Design

### Shader Compilation Pipeline

The core innovation of DualDrill is its CLSL system, which enables writing shaders directly in C#:

1. Frontend Layer
   - Runtime reflection-based C# code analysis
   - Support for shader-specific attributes
   - Type system bridging between C# and shader languages

2. Intermediate Representation
   - Stack-based IR inspired by WASM
   - Structured control flow representation
   - Type-aware instruction set

3. Backend Layer
   - WGSL code generation
   - Planned support for multiple targets (SPIR-V, CUDA)
   - Optimization passes

### Design Principles

1. **Type Safety**: Strong compile-time type checking between C# and shader code
2. **Performance**: Zero-overhead abstractions and SIMD optimization
3. **Extensibility**: Modular design for multiple frontends and backends
4. **Developer Experience**: Native C# shader authoring with IDE support

## Project Status

Currently, development is focused on the CLSL system, particularly:
- Enhancing the shader compilation pipeline
- Implementing control flow analysis
- Expanding shader language feature support
- Optimizing generated code

For detailed documentation about specific components, please refer to the linked sections above.

See also:
- [CLSL Implementation Details](./clsl/implementation.md)
- [GPU API Design](./graphics/api_design.md)
- [Development Roadmap](./roadmap.md)

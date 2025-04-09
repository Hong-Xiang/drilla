# CLSL (C# to Shader Language) System

> **Note**: This documentation is generated and maintained with the assistance of AI/LLM tools. While we strive for accuracy, please verify critical information and report any inconsistencies.

## Overview

CLSL is a compiler system that translates csharp code (or more specifically .NET IL) into various shader languages.
It's designed to provide a seamless development experience by allowing developers to write shader code directly in csharp while maintaining full type safety and IDE support.

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

The compiler frontend is primarily based on runtime reflection, analyzing csharp code at runtime:

```csharp
public interface ICLSLCompiler
{
    ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> Reflect(ISharpShader shader);
    ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> Compile(ISharpShader shader);
    ValueTask<string> EmitWGSL(ISharpShader module);
}
```

### Intermediate Representation

CLSL uses a multiple IR inspired by WASM, SPIR-V, and MLIR:

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

## Design Challenges and Notes

1. **Runtime Reflection Frontend**
   - Direct integration with C# runtime
   - No need for separate compilation step
   - Access to full type information

### Choose a Proper IR

#### Design Goals
- [ ] Support for structured control flow
- [ ] (Optional) representing unstructured control flow, so that we can convert from unstructured control flow to structured control flow using a IR transformation pass, not a parser functionality.
- [ ] Easier to convert from and to stack-based IR, including .NET CIL byte code and WASM bytecode
- [ ] Easier to track order of evaluation (effects), especially for those implicitly transferred values between basic blocks in stack-based IR
- [ ] Easier to convert to SPIR-V
- [ ] (Optional) Support for convert to LLVM IR
- [ ] (Optional) Support for convert to abstract syntax tree

#### Current Design

Since our primary frontend is .NET CIL,
and WASM would be a important frontend/backend target for us in the future,
we need to choose a IR easier to work with linear stack instructions.

On the other hand, we need to support structured control flow,
since our primary backends are shader languages, 
which generally do not support unstructured control flow.

But since .NET CIL is a unstructured stack-based IR,
and convert it to structured control flow is not trivial,
we'd like to represent that step as a transformation pass in the IR.

As a result, we use a IR close similar to WASM nested regions, i.e.
there is `BlockRegion`, `LoopRegion`, `IfElseRegion` regions,
`BlockRegion` and `LoopRegion` regions have label,
and a body of sequence of `RegionElement`s,
`RegionElement` can be one of `Block`, `Loop`, `IfElse` or `BasicBlock`.
`IfElseRegion` contains two sequence of `RegionElements`s, 
one for true branch and one for false branch.
Function body is a sequence of `RegionElement`s.
Only `BlockRegion` and `LoopRegion` has label,
i.e. only they can be used as jump targets.
`BlockRegion` and `LoopRegion` can have input values,
when `br` to them, proper values are required as parameters.

To support unstructured control flow,
we simply don't require the `br` to target proper nested outer `BlockRegion` or `LoopRegion`,
thus we can simply pub all basic blocks into `BlockRegion`,
each of them has only a single `BasicBlock`,
and `br` could jump to any `BlockRegion` in function body.

A `UnstructuredControlFlowToStructuredControlFlow` pass is required to convert unstructured control flow to structured control flow,
after that,
we would transform the flatten block based function body into a properly nested function body.

Overall structure would be `ShaderModule -> FunctionBody -> RegionElements -> (tree of nested elements)`.

To represent SSA/TAC instructions, we may use sequence of values as instructions,
for those instructions that does not produce values, e.g. `store` or control flow instructions,
we use `Unit` type values to represent them.
Use reference type for those values to get unique identity for each value.

What does TCA/SSA value over stack with expression IR solve:

why use stack with expression IR - make it easier to handle cases of transfer values between basic blocks
but it suffers on expression evaluation side effects. In original stack ir,
middle results should evaluate immediately,
but in stack with expression IR,
expressions evaluate lazily,
which have different semantics to the original stack ir.
By using SSA/TAC values for middle results,
side effects orders are preserved.

Why not pure stack ir? - hard to represent basic block transfer values, it's implicit via evaluation stack state.
Also harder to represent basic block boundaries,
e.g. for a basic block with `br_ir` end,
the condition value is on the top of the stack,
but all values below it are required to pass to target basic block,
which causes representation of basic block harder.

Can we use structured control flow + basic blocks with both stack ir and TAC like SSA instructions support?

All major forms we are interested in (WGSL AST, SPIR-V, etc) are structured control flow,
our abstract syntax tree representation could be easily adapted and mapped to structured control flow.
The remaining questions are:

How do we represent structured control flow, 
nested regions might be a good way to make it easier to translate to WASM and MLIR,
and if we can workout how to translate it into SPIR-V, then it would be ideal representation.

Another question is about how to represent transfer values between basic blocks.
For stack ir, values could be transfer to other basic blocks via non-empty 

For simplicity and uniformity, each basic block should have at least one instruction,
no matter using stack ir or SSA ir,
and the last instruction must be a terminator instruction,
thus one of `br`, `br_if`, `return`, `return_value`.
For wasm byte code, the `br` to next block/loop is not allowed,
but in our IR, we allow that,
thus branching to properly nested outer `BlockRegion` or `LoopRegion` is allowed,
and unconditional branching to exact next block is allowed,
the semantic of exact next block dependents on context,
e.g.
For any basic block in a block/loop region,
but not the last one,
the next block is the next block is the next block in the region.
For the last basic block in a loop region, the next block is the first block in the loop region,
For the last basic block in a block region, the next block is the first block in the next region.

For basic block with stack ir, the input and outputs are list of types.
For basic block with SSA ir, the input and outputs are list of values.


# Relation of dominator tree and nested control flow graph ?
```
bb0
if
   bb1
else
   bb2
   if
      bb3
   else
      bb4
bb5
bb6
```
should have a dominator tree like this:
```
bb0 -> [bb1, (bb2 -> [bb3, bb4]), (bb5 -> [bb6])]
```
thus for block and loop region,
the first block dominates all following blocks in the region.
for if-else region, the block before if-else dominates all block in both branches,
thus get a dominator tree from properly nested control flow IR should be easy.

Block and Loop's label should be implicitly defined by their context basic blocks.
i.e. Block's label should be the next element's header basic block's label,
Loop's label should be the header basic block's label.
if corresponding element is a if-else region, then the label should be a undefined label,
which should be a arbitrary value which would not be used as jump target,
and this implies that there loop region should not start with a if-else region,
and block with next if-else region should be optimized that inner elements should be moved out of the block region and the block region should be removed.
For control flow graph constructed structured control flow IR,
those kinds of regions would not be constructed.

See also:
- [Compiler Implementation Details](./implementation.md)
- [Type System Reference](./type_system.md)
- [IR Specification](./ir_spec.md)
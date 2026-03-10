# CLSL (CIL Shader Language) System

## Overview

CLSL is a compiler system that translates C# code (specifically .NET CIL bytecode) into shader languages. Developers write shader code directly in C# as classes implementing `ISharpShader`, with full IDE support, type checking, and attribute-based shader stage / resource binding annotations.

The current backend emits Slang source code, which the Slang toolchain (`slangc`) compiles to WGSL (and potentially other targets). An alternative approach -- emitting Slang IR directly -- is under investigation.

## Key Features

1. **Native C# Shader Authoring**
   - Write shaders using standard C# syntax
   - Full IDE support including IntelliSense
   - Compile-time type checking
   - Attribute-based shader stage and resource binding

2. **Multi-stage Compilation Pipeline**
   - Runtime reflection-based CIL analysis
   - SSA-based intermediate representation with region trees
   - Structured control flow recovery (dominator/post-dominator analysis)
   - Slang source code emission backend

3. **Type System Integration**
   - Custom `IShaderType` hierarchy mapping C# types to shader types
   - Vectors, matrices, and custom structures
   - Pointer/reference types for CIL address-of patterns

## Compiler Architecture

### Compiler Entry Point

```csharp
public interface ICLSLCompiler
{
    ShaderModuleDeclaration<FunctionBody4> Parse(ISharpShader shader);
    string Emit(ISharpShader shader);
}

public enum CLSLCompileTarget { IR, WGSL, SLang }
```

Usage:
- `CLSLCompileTarget.IR` -- dumps the custom IR as text
- `CLSLCompileTarget.SLang` -- emits Slang source code
- `CLSLCompileTarget.WGSL` -- emits Slang, then compiles to WGSL via `slangc`

### Pipeline

```
C# Shader (ISharpShader)
    |
    v
CompilationContext.Create()           -- creates symbol tables with builtin types/functions
RuntimeReflectionParser.ParseShaderModule()
    |-- ParseStructDeclaration        -- struct types
    |-- ParseMethod                   -- function declarations
    |-- MethodBodyAnalysisModel       -- CIL instruction analysis
    |-- ControlFlowGraphBuilder       -- CFG from linear IL
    |-- DominatorTree / PostDominatorTree
    |-- Region tree construction      -- structured control flow
    v
ShaderModuleDeclaration<FunctionBody4>    -- SSA IR with region tree
    |
    v
module.RunPass(FunctionToOperationPass)   -- lowers function calls to operations
module.RunPass(RegionParameterToLocalVariablePass)
    |
    v
SlangEmitter.Emit()                       -- walks IR, emits Slang source
    |
    v
SlangService.CompileToWgslAsync()         -- (optional) invokes slangc for WGSL
```

### Frontend

The compiler frontend reads C# shader classes via .NET runtime reflection:

- `RuntimeReflectionParser` -- entry point, iterates methods/fields/types
- `RuntimeReflectionInstructionParserVisitor3` -- walks CIL instructions, builds SSA IR
- `SharedBuiltinSymbolTable` -- maps .NET types/methods to shader types/operations
- `MethodBodyAnalysisModel` -- analyzes IL method bodies

### Intermediate Representation

CLSL's IR is SSA-based with structured control flow encoded as region trees:

**Module level:**
- `ShaderModuleDeclaration<FunctionBody4>` -- contains declarations + function bodies

**Function level:**
- `FunctionDeclaration` -- name, parameters, return type, attributes
- `FunctionBody4` -- entry label, local variables, region tree of blocks

**Block level:**
- `RegionTree<Label, ShaderRegionBody>` -- tree of Block or Loop regions
- `ShaderRegionBody` -- sequence of instructions (`Seq<..., ShaderRegionBody>`) + terminator
- Terminators: `Br` (unconditional), `BrIf` (conditional), `Return`, `ReturnExpr`

**Instruction level:**
- `Instruction<TResult, TArg>` -- an operation applied to SSA values
- `IOperation` hierarchy (30+ types): arithmetic, comparison, load/store, vector ops, conversions, calls
- `IShaderValue` -- SSA values: `IntermediateValue`, `LiteralValue`, `VariablePointerValue`, `ParameterPointerValue`

### Type System

```
IShaderType
├── IScalarType
│   ├── BoolType
│   ├── FloatType<N>  (N32, N64)
│   ├── IntType<N>    (N32, N64)
│   └── UIntType<N>   (N32, N64)
├── IVecType  (vec2/3/4 parameterized by rank and element type)
├── IMatType
├── IPtrType / IRefType
├── StructureType
├── FunctionType
└── OpaqueType (Texture2D, Sampler, etc.)
```

### Backend

**SlangEmitter** (primary backend):
- Implements `IDeclarationVisitor`, `IOperationSemantic`, `ITerminatorSemantic`, etc.
- Emits Slang/HLSL-style source code with type aliases (`typealias f32 = float`, etc.)
- Maps shader stages to Slang attributes (`[shader("vertex")]`, etc.)
- Handles structured control flow: `if/else` from `BrIf`, `while(true)` from loops, `break`/`continue`

**SPIRVEmitter** (partial):
- Infrastructure for SPIR-V emission exists but instruction body generation is not complete.

## Current Challenges and Active Work

### IR Complexity
The current IR uses deeply generic C# type hierarchies (`NumericBinaryArithmeticOperation<TType, TOp>`, etc.) which makes transforms ad-hoc. Replacing an SSA value with a new one requires threading through many visitor implementations. The planned CIL-based encoding aims to simplify this.

### Slang Source Recovery
Converting SSA IR back to Slang source code requires recovering structured control flow including identifying `break`/`continue` from branch targets, handling reused/duplicated blocks, and managing merge points. An alternative approach of emitting Slang IR directly is being explored (see discussion in Slang issue #8609).

### Control Flow
- Dominator/post-dominator trees and region tree construction work for tested cases
- Merge point placement for GPU cooperative operations (subgroup ops, texture sampling) needs careful treatment
- Labelled break encoding (similar to WASM `br` to nested scopes) is being considered as a more general approach

### Pointer Elimination
CIL allows taking addresses of locals/parameters. Direct translation produces invalid shader code. Current approach requires ensuring pointers are eliminated before emission. Strategies under consideration include enum-based encoding and SSA promotion.

## Design Discussion: IR Choices

The IR was designed to handle the gap between .NET CIL (unstructured, stack-based) and shader languages (structured control flow, no arbitrary pointers):

- **Why SSA over stack IR?** -- Stack IR makes inter-block value transfer implicit and hard to reason about. SSA makes data flow explicit and preserves evaluation order for side effects.
- **Why region trees?** -- Shader targets require structured control flow. Region trees (Block/Loop regions with nested elements) map naturally to WASM-style structured scoping and from there to shader language constructs.
- **Why Slang as intermediate?** -- Slang provides higher-level abstractions than SPIR-V, handles legalization to target APIs, and supports reflection. The cost is the source code recovery step.

See also:
- [Implementation Details](./implementation.md)
- [Type System Reference](./type_system.md)
- [IR Specification](./ir_spec.md)
- [ECMA-335 SSA IR Design](../../DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md)

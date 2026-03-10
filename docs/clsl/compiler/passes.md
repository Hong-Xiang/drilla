# CLSL Compiler Passes and Transformations

## Overview

The CLSL compiler uses a series of passes to transform code from C# CIL to Slang source code. The pipeline has three main phases: frontend analysis, IR transformation, and backend emission.

## Pass Pipeline

```
C# Shader (ISharpShader)
    |
    v
[Frontend: RuntimeReflectionParser]
    |-- Type resolution (SharedBuiltinSymbolTable)
    |-- CIL instruction parsing (RuntimeReflectionInstructionParserVisitor3)
    |-- CFG construction (ControlFlowGraphBuilder)
    |-- Dominator / post-dominator analysis
    |-- Region tree construction (structured control flow recovery)
    v
ShaderModuleDeclaration<FunctionBody4>
    |
    v
[FunctionToOperationPass]              -- IR transformation
    |
    v
[RegionParameterToLocalVariablePass]   -- IR transformation
    |
    v
[SlangEmitter]                         -- backend emission
```

## Frontend Passes

### Type and Symbol Resolution

The `SharedBuiltinSymbolTable` pre-registers:
- Scalar types: `float` -> `FloatType<N32>`, `int` -> `IntType<N32>`, etc.
- Vector types: `Vector2/3/4` -> `VecType<TRank, TElement>`
- Builtin functions: `Math.Sin` -> `sin`, `Vector3.Dot` -> `dot`, etc.
- Shader attributes: `[Vertex]`, `[Fragment]`, `[Uniform]`, `[Location(n)]`, etc.

### CIL Instruction Parsing

`RuntimeReflectionInstructionParserVisitor3` walks the CIL instruction stream and:
- Maintains a simulated evaluation stack (converting stack operations to SSA values)
- Maps CIL opcodes to `IOperation` instances (e.g., `add` -> `NumericBinaryArithmeticOperation<T, Add>`)
- Handles method calls by resolving to builtin operations or user function calls
- Produces `Instruction<TResult, TArg>` sequences within basic blocks

### Control Flow Analysis

1. **CFG Construction** (`ControlFlowGraphBuilder`):
   - Scans for branch targets in CIL
   - Creates `Label` nodes for basic block boundaries
   - Establishes `ISuccessor` edges (Unconditional, Conditional, Terminate)

2. **Dominator Tree** (`DominatorTree`):
   - Computes dominance relationships
   - Used for identifying natural loops and region boundaries

3. **Post-Dominator Tree** (`PostDominatorTree`):
   - Computes reverse dominance
   - Used for identifying merge points (immediate post-dominators)

4. **Region Tree Construction**:
   - Identifies natural loops via back-edges in DFS tree
   - Lifts flat CFG into nested Block/Loop regions
   - Uses immediate post-dominator as merge point for if/else

## IR Transformation Passes

### FunctionToOperationPass

Located in `DualDrill.CLSL.Language/Transform/FunctionToOperationPass.cs`.

Resolves high-level function references to concrete operation instances. Lowers `CallOperation` nodes where the target is a known builtin into the appropriate typed operation.

### RegionParameterToLocalVariablePass

Located in `DualDrill.CLSL.Language/Transform/RegionParameterToLocalVariablePass.cs`.

Converts region/block parameters (values passed along branch edges as `RegionJump` arguments) into explicit local variable declarations with store/load sequences. This eliminates the need for the backend to handle block arguments during code generation.

Before:
```
br block_1(%value)

block_1(%param):
    use %param
```

After:
```
store %local, %value
br block_1

block_1:
    %param = load %local
    use %param
```

### CommonOperationLoweringPass

Located in `DualDrill.CLSL.Language/Transform/CommonOperationLoweringPass.cs`.

Lowers common operation patterns that don't map directly to backend constructs.

## Backend Emission

The `SlangEmitter` is technically a pass that consumes the IR and produces text output. It implements several visitor interfaces:

- `IDeclarationVisitor` -- emits struct/function/variable declarations
- `IOperationSemantic` -- translates operations to Slang expression syntax
- `ITerminatorSemantic` -- translates branches to `if/else`, `break`, `continue`, `return`
- `IRegionDefinitionSemantic` -- translates Block/Loop regions to scoped blocks and `while(true)` loops

## Pass Architecture

Passes operate on `ShaderModuleDeclaration<FunctionBody4>` and return a new instance:

```csharp
module = module.RunPass(pass);
```

The `RunPass` method applies the pass to each function body in the module, producing a new module with transformed bodies. The module and its IR components use immutable data structures (`ImmutableArray`, etc.) to ensure passes don't mutate shared state.

See also:
- [IR Specification](../ir_spec.md)
- [Control Flow Analysis](./control_flow.md)

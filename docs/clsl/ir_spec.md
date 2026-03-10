# CLSL Intermediate Representation Specification

## Overview

CLSL uses an SSA-based intermediate representation with structured control flow encoded as region trees. The IR sits between the .NET CIL frontend (unstructured, stack-based) and the Slang/WGSL backend (structured, no arbitrary pointers).

The IR supports multiple representations of function bodies at different compilation stages:

- **ControlFlowGraph** (`ControlFlowGraph<CilInstructionBlock>`): Flat CFG with `Label`s, `BasicBlock`s, and `ISuccessor` edges. Produced by `ControlFlowGraphBuilder` from CIL method bodies.
- **FunctionBody4** (with `RegionTree<Label, ShaderRegionBody>`): SSA IR with structured control flow via nested Block/Loop regions. This is the primary representation used by backends.

## Design Principles

1. **SSA Values**: Every computed result is a unique `IShaderValue` instance. Values carry type information (`IShaderType`). No implicit value sharing or mutation.

2. **Typed Operations**: Every operation (`IOperation`) carries complete type information. Operations are parameterized by type and operator kind (e.g., `NumericBinaryArithmeticOperation<FloatType<N32>, Add>`). No implicit type conversions.

3. **Structured Control Flow**: Function bodies are organized as region trees (`RegionTree<Label, ShaderRegionBody>`), with Block and Loop regions that map directly to shader language constructs. This enables straightforward code generation without needing a separate control flow restructuring pass in the backend.

4. **Explicit Terminators**: Each basic block ends with an explicit terminator: `Br` (unconditional branch), `BrIf` (conditional), `Return`/`ReturnExpr`, or `Discard`.

## IR Structure

### Module
```
ShaderModuleDeclaration<FunctionBody4>
├── Declarations: IDeclaration[]
│   ├── StructureDeclaration    -- user-defined structs
│   ├── FunctionDeclaration     -- function signatures
│   └── VariableDeclaration     -- module-level variables
└── FunctionBodies: Map<FunctionDeclaration, FunctionBody4>
```

### Function Body
```
FunctionBody4
├── Entry: Label               -- entry block label
├── LocalVariables: VariableDeclaration[]
└── Body: RegionTree<Label, ShaderRegionBody>
```

### Region Tree
```
RegionTree<Label, ShaderRegionBody>
├── Label: Label               -- region label
├── Definition: RegionDefinition
│   ├── BlockRegionDefinition  -- straight-line region
│   └── LoopRegionDefinition   -- loop with back-edge
├── Children: RegionTree[]     -- nested sub-regions
└── ImmediatePostDominator: Label?
```

### Basic Block Body
```
ShaderRegionBody
├── Body: Seq<Instruction<string, string>, ShaderRegionBody>
│   ├── Element: Instruction   -- SSA instruction
│   └── Rest: ...              -- remaining instructions
└── Last: Terminator           -- Br, BrIf, Return, ReturnExpr
```

### Instructions
```
Instruction<TResult, TArg>
├── Operation: IOperation      -- computation description
├── Result: IShaderValue?      -- produced SSA value (null for statements)
└── Operands: IShaderValue[]   -- input SSA values
```

## Operations

### Arithmetic
- `NumericBinaryArithmeticOperation<TType, TOp>` where TOp: Add, Sub, Mul, Div, Rem
- `VectorNumericBinaryOperation<TRank, TElement, TOp>` -- component-wise vector arithmetic

### Comparison
- `NumericBinaryRelationalOperation<TType, TOp>` where TOp: Lt, Gt, Le, Ge, Eq, Ne

### Logical
- `LogicalBinaryOperation<TOp>` where TOp: And, Or
- `LogicalNot`

### Conversion
- `ScalarConversionOperation<TSource, TTarget>` -- e.g., int -> float
- `Bitcast`

### Vector
- `VectorCompositeConstructionOperation` -- construct vector from components
- `VectorFromScalarConstructOperation` -- broadcast scalar to vector
- `VectorComponentGetOperation` / `VectorComponentSetOperation` -- .x, .y, .z, .w
- `VectorSwizzleGetOperation` / `VectorSwizzleSetOperation` -- .xyz, .xz, etc.

### Memory
- `LoadOperation` -- load value from variable/parameter
- `StoreOperation` -- store value to variable/parameter
- `AddressOfMemberOperation` -- get pointer to struct member
- `AddressOfVecComponentOperation` -- get pointer to vector component
- `AccessChainOperation` -- indexed access

### Other
- `CallOperation` -- function call
- `LiteralOperation` -- constant value
- `NopOperation` -- no operation
- `ZeroConstructorOperation` -- zero-initialize a type

## Control Flow

### Terminators
- `Br(target: Label)` -- unconditional branch
- `BrIf(condition: IShaderValue, trueTarget: Label, falseTarget: Label)` -- conditional
- `Return` -- void return
- `ReturnExpr(value: IShaderValue)` -- return with value

### CFG Edge Types
- `UnconditionalSuccessor` -- single target
- `ConditionalSuccessor` -- true/false targets
- `TerminateSuccessor` -- return/discard (no successor)

## Type System

See [Type System Reference](./type_system.md) for details.

Scalar types: `BoolType`, `IntType<N32>`, `UIntType<N32>`, `FloatType<N32>`, `FloatType<N64>`
Vector types: `VecType<TRank, TElement>` -- rank 2/3/4 with any scalar element
Pointer types: `PtrType`, `RefType` -- for CIL address-of patterns
Composite: `StructureType`, `FunctionType`
Opaque: `Texture2D`, `Sampler`, etc.

## Validation Rules

1. **SSA**: Each `IntermediateValue` is produced by exactly one instruction
2. **Type consistency**: Operation operand types must match operation's expected types
3. **Terminator**: Every basic block must end with exactly one terminator
4. **Region structure**: Region tree must be well-nested (no cross-region jumps)
5. **Value liveness**: Values used in a block must dominate that block's definition

## Planned: CIL-Based Encoding

The IR is being migrated to an ECMA-335 CIL encoding where:
- Operations become `Builtin.*` method calls (no more `IOperation` hierarchy)
- Values become `ref struct` fields (no more `IShaderValue` hierarchy)
- Types use .NET `Type` directly (no more `IShaderType` hierarchy)

See [ECMA-335 SSA IR Design](../../DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md).

See also:
- [WGSL Backend](./backends/wgsl.md)
- [Compiler Passes](./compiler/passes.md)

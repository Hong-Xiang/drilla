# Documentation

## CLSL Compiler

- [Project Overview](./project_overview.md) -- high-level architecture and current status
- [CLSL Overview](./clsl/index.md) -- compiler architecture, IR, pipeline
- [Design](./design.md) -- IR design decisions and rationale
- [Current Status and Plans](./plans/overview.md) -- active work and direction

### CLSL Subsystems

- [Implementation Details](./clsl/implementation.md) -- frontend, IR, backend details
- [IR Specification](./clsl/ir_spec.md) -- IR structure and instruction set
- [Type System](./clsl/type_system.md) -- shader type hierarchy
- [Structural Control Flow](./clsl/structural-control-flow.md) -- region tree design

### CLSL Compiler Internals

- [Control Flow Analysis](./clsl/compiler/control_flow.md) -- CFG, dominators, region recovery
- [Compiler Passes](./clsl/compiler/passes.md) -- transformation passes
- [Validation](./clsl/compiler/validation.md) -- IR validation rules
- [Vector Operations](./clsl/compiler/vector_ops.md) -- vector/swizzle handling

### Backends

- [WGSL Backend](./clsl/backends/wgsl.md) -- Slang emission + slangc pipeline

### CIL-Based IR Encoding (Planned)

- [ECMA-335 SSA IR Design](../DualDrill.ILSL/docs/ECMA335-SSA-IR-Design.md) -- target IR encoding spec
- [Migration Plan](../DualDrill.ILSL/docs/ECMA335-SSA-IR-Migration-Plan.md) -- migration steps

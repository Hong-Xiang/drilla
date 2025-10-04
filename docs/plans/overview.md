# Current Processing and Planned Tasks

* [ ] Remove DupInstruction  
      - Eliminate DupInstruction from the language since WGSL backend lacks a corresponding operation.  
      - Lower its usage to a defined variable store–load–load sequence.

* [ ] Refactor Structured Control Flow Region Implementation  
      - Introduce a new element sequence type to represent a list of instructions.  
      - Update Block, Loop, If, and Switch constructs to directly own an element sequence rather than nesting Blocks unnecessarily.

* [ ] Implement Correct BrInstruction Handling in AST Compilation  
      - Improve the visitor in StructuredInstructionToAbstractSyntaxTreeBuilder to track label targets (distinguishing between Loop and Block).  
      - Map `br` to `break` for Blocks and to `continue` for Loops, reflecting their semantics in WGSL.

* [ ] Implement Stack State Delta Tracking for Control Flow Regions  
      - Add explicit parameter and result fields to Block, Loop, If-Then-Else, and Switch types.  
      - Track the evaluation stack changes on entering and exiting regions, ensuring correct type propagation.

* [ ] Transform Implicit Stack Transfers to Explicit Locals  
      - Create a pass that analyzes stack state deltas in blocks and transforms them into explicit local variable assignments in the AST.  
      - This transformation will prepare the IR for AST generation by removing implicit stack dependencies.

* [ ] Remove Index field from `VariableDeclaration`
      - Remove related methods like `CompilationContext`'s `AddVariable`'s related index tracking logic.
      - Adding unique indexing logic to `Dump` and to code related logic

* [ ] Emit Variable Declaration Code at beginning of function body in (structured) bytecode to AST compilation logic

* [ ] Refactor `VariableDeclaration` to `FunctionVariableDeclaration` and `ModuleVariableDeclaration`, instead of using `DeclarationScope` to differentiate between function and module variables.

* [ ] Moving `DominatorTree` to `ControlFlowGraph` to simplify control flow analysis usage. Use lazy to avoid unnecessary dominator tree build process.

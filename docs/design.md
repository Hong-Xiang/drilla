# CLSL (CIL Shader Language) Compiler

CLSL is a embedded DSL in dotnet for writing shader program.
It uses a subset of dotnet CIL with additional attribute to support shader-specific constructs.


## IR Design

ShaderModule
- TypeDeclaration
  - StructDeclaration
    - StructFieldDeclaration
  - FunctionDeclaration
    - FunctionParameter

- ModuleLevelVariables
- FunctionDefinition
   - FunctionLocalVariable
   - RegionTree
      - BasicBlock
         - Instruction
         - Terminator


### Design Discussion

#### Should we use singleton for shader types?
Using singleton for shader types has following benefits:
- Automatically deduplicate types
- Easier to implement associated properties
- Easier to express in dotnet attribute for adding additional information to other related objects, e.g. a `AddOperationMethod<IntType<N32>>>` could be added to a method so that it would be easier to parse
    - But this maybe not necessary, as we could use enum for this purpose

But there are also some challenges:
- Handling for custom types would be challenging:
    - Function types, since user could define lots of different function signatures
    - User defined structures


#### Should we use singleton for operations?

We distinguish between operation and instructions by operation are "class" of instructions, thus instructions without operands. Operands are just instance of `IShaderValue`.

- How do we model compile time known operands? e.g. for AccessChain of structures, logically it is used as `AccessChain <index> %value-ptr`, the `<index>` need to be compile time constant and in valid range of the structure member count. Then should be model it in operation so that each `AccessChain` to each structure/member is a distinct operation? Or should we model it as a single operation with a compile time constant operand in instruction? Model each access chain for a member seems a good idea since at least structure is not a value, thus anyway we need to model `AccessChain` to different structure as different operations, by modeling members as different `AccessChain` operation ensured that the index is in valid range.
But then it comes tricky that how do we handle access to vector/matrix indices. For vector, `.x` is just a special case for member access, should we encode it as a special access chain operation?
If so, then we still need to encode another version of dynamic access chain for vector, using runtime shader value as index.

Another question is for those argument that is not a value, we need to encode it in operation itself, but should we encode it using properties or type arguments?
For type arguments, it is more 




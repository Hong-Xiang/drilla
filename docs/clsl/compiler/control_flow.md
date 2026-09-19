# Control Flow Analysis in CLSL

## Input contract

Control analysis accepts a finite, closed control-flow graph with one entry.
Every defined block must be reachable from that entry. Generic
`ControlFlowGraph<TBlock>` construction checks that successor labels are
defined; the shared DFS analysis boundary separately rejects disconnected
definitions with `ArgumentException`.

Successor order is semantic. An unconditional successor has ordinal `0`; a
conditional successor is ordered true (`0`) then false (`1`). Parallel arms to
the same target remain distinct.

## Published block facts

`CilBlockControlFactsPass` publishes immutable `BlockControlFacts` on each
reachable basic block:

- `ReversePostOrderIndex` is the existing DFS reverse-postorder number.
- `ImmediateDominator` is the nearest strict dominator, or `null` for the entry.
- `ImmediatePostDominator` retains the existing nullable result. Its
  finite-exit semantics are not established by the ordered-arm change.
- `IncomingArms` is ordered by source reverse-postorder, then source successor
  ordinal. Each `IncomingControlArm` records the original source label and
  ordinal; the target is the block that owns the facts.
- `IsLoopHeader` is derived: it is true exactly when at least one incoming arm
  is a backedge.

An arm from `S` to `T` is a backedge exactly when `T` dominates `S`. Every other
arm is forward. “Forward” therefore means non-backedge, not increasing
reverse-postorder. Irreducible cycles can contain only forward arms, and a loop
header flag does not claim reducibility or natural-loop membership.

## Preservation and invalidation

Analysis preserves block payloads, successor objects, label identities, arm
multiplicity, and successor order. Changing the entry, block set, successor
destination/order, or arm multiplicity invalidates all published control facts.
A payload-only mapping may retain them only when topology and ordered arm
identity are unchanged. There is no incremental invalidation registry.

See also:

- [IR Specification](../ir_spec.md)
- [Compiler Passes](./passes.md)

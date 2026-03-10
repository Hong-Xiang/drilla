# Coding Principles

## Core Philosophy

Algebraic domain modeling. Make illegal states unrepresentable. Prefer composition over ad-hoc assembly. Type-driven development; when types are insufficient, test-driven development. Pure functions and immutability by default. Use FP concepts, but express them idiomatically in the host language.

## Type & Data Modeling

- Model domain precisely with ADTs/GADTs; use sum types to eliminate invalid states by construction
- Newtypes for domain primitives (IDs, quantities, names) — prevent accidental mixing at compile time
- Parse, don't validate: convert unstructured data into well-typed models at system boundaries; after parsing, invalid input is gone
- Prefer algebraically closed types with many operations over scattered types with few ("100 operations on 1 type" principle)
- Object algebra (tagless final), recursive schemes for complex domains
- Monoids, functors, monads, optics for composable data transformations; use them when they clarify intent and reduce boilerplate

## Functions & Composition

- Pure and total by default; no partial functions, no hidden side effects
- No exceptions for domain logic — use sum types (Either/Result/Option) for recoverable error paths; reserve exceptions for truly exceptional failures at system boundaries
- Adapt error strategy to the host language's idioms when they conflict with the above
- Effects at the edge: push IO/state/errors to system boundaries; keep core logic pure
- Parametricity: prefer polymorphic signatures that constrain behavior through types
- Equational reasoning: naming vs inlining an expression must not change behavior
- Reason about functions by their algebraic properties and type signatures, not implementation details
- Build from small algebras that compose at every level — functions, components, services

## Abstraction Discipline

- Semantic compression: an abstraction must capture a real pattern (domain or mathematical); if it has no name, question its existence
- Property-based thinking: design with algebraic laws (associativity, commutativity, idempotence) in mind, even without property tests
- Minimal export surface: expose the algebra (operations), hide representation
- Pragmatic ceiling: if simulating a type-level feature demands boilerplate that harms reasoning and maintenance, stop; use the language's native abstraction power, don't fight the type system

## Testing

- Prefer property-based tests over example-based tests — they verify algebraic laws and invariants directly
- Use example tests for edge cases, integration paths, and documentation value
- Write tests before implementation whenever possible, especially for complex logic and refactors
- Keep core logic pure so it remains trivially testable

## Dependency Passing

- When available: use reader monad, algebraic effects, or similar for dependency passing
- Otherwise: use constructor/factory-method injection to keep components testable and composable
- Avoid service locators, global singletons, and hidden ambient state

## Trade-offs

- Professional maintainability over naive first-read simplicity — documentation explains the "why"
- Prefer clean mathematical modeling; don't dumb down designs for approachability

## Agent Interaction

- Understand the codebase and requirements accurately before proposing changes
- Before introducing a new abstraction, search for existing algebras or patterns in the codebase that overlap — reuse structure rather than reinvent it
- Ask for missing information instead of guessing
- For design / architecture / large refactor discussions: maintain a tracking markdown that reflects the latest conclusions; update it as new information arrives, keep only current state — no change history
- When planning implementation, break down into small, composable steps that can be implemented and tested independently

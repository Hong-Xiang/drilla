namespace DualDrill.ApiGen.Mini;

/// <summary>
/// Mini TypeSystem is a simple type system defined to support API definition and code generation
/// It is designed to be simple to describe FFI and external API
/// * No namespace/module support, all declerations are global
/// It only support common primitive types
/// For struct like composite types, it supports
/// Handle - a class like type with methods and properties, requires dispose
/// 
/// For generics types, it only support limited builtin generics
/// Future -> Taks/ValueTask/Promise like
/// Sequence -> IEnumerable like
/// </summary>
public sealed class TypeSystem
{
}

using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.DrillLang.Visitors;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang.Declaration;

/// <summary>
/// DrillLang module provides a simple type system defined to support API definition and code generation
/// It is designed to be simple to describe FFI and external API
/// * No namespace/module support, all declarations are global
/// It only support common primitive types
/// For struct like composite types, it supports
/// Handle - a class(reference type) like type with methods and properties, requires dispose
/// 
/// For generics types, it only support limited builtin generics
/// Future -> Taks/ValueTask/Promise like
/// Sequence -> IEnumerable like
/// 
/// Nullable/Future/Sequence like type are primitive like, generic types is not provided
/// </summary>
public sealed record class ModuleDeclaration(
    string Name,
    ImmutableHashSet<HandleDeclaration> Handles,
    ImmutableHashSet<StructDeclaration> Structs,
    ImmutableHashSet<EnumDeclaration> Enums,
    ImmutableHashSet<ITypeDeclaration> Others
) : IDeclaration
{
    public static ModuleDeclaration Create(string name, IEnumerable<ITypeDeclaration> typeDeclarations)
    {
        List<HandleDeclaration> handles = [];
        List<StructDeclaration> structs = [];
        List<EnumDeclaration> enums = [];
        List<ITypeDeclaration> others = [];
        foreach (var t in typeDeclarations)
        {
            if (t is HandleDeclaration h)
            {
                handles.Add(h);
            }
            else if (t is StructDeclaration s)
            {
                structs.Add(s);
            }
            else if (t is EnumDeclaration e)
            {
                enums.Add(e);
            }
            else
            {
                others.Add(t);
            }
        }
        return new(name,
                   [.. handles],
                   [.. structs],
                   [.. enums],
                   [.. others]);
    }

    public IEnumerable<ITypeDeclaration> AllTypeDeclarations =>
            [.. Handles,
             .. Structs,
             .. Enums,
             .. Others];

    public int DeclarationCount => Handles.Count + Structs.Count + Enums.Count + Others.Count;
}

public sealed class IdentityNameTransform : INameTransform
{
    public static readonly IdentityNameTransform Instance = new IdentityNameTransform();
}

internal sealed class ModuleTransformException() : Exception("Failed to transform module")
{
}

public static partial class DrillLangExtension
{
    public static ModuleDeclaration Transform(this ModuleDeclaration module,
                                              INameTransform transform)
    {
        var visitor = new Visitors.NameTransformVisitor(transform);
        return module.AcceptVisitor(visitor) as ModuleDeclaration ?? throw new ModuleTransformException();
    }

    public static ModuleDeclaration Transform(this ModuleDeclaration module,
                                              ITypeReferenceVisitor<ITypeReference> typeTransform)
    {
        var visitor = new Visitors.ModuleTypeReferenceVisitor(typeTransform);
        return module.AcceptVisitor(visitor) as ModuleDeclaration ?? throw new ModuleTransformException();
    }

    public static ModuleDeclaration RefineOpaqueType(this ModuleDeclaration module, Func<string, ITypeReference> refinement)
        => module.Transform(new RefineOpaqueTypeVisitor(refinement));
}



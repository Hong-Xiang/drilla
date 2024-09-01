using DualDrill.ApiGen.Mini;
using DualDrill.ApiGen.WebIDL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace DualDrill.ApiGen;

public sealed record class GPUApi(
    ImmutableArray<Handle> Handles
)
{
    public void Validate()
    {
        Debug.Assert(Handles.Length == 22, $"GPU API should provide 22 handles, got {Handles.Length}");
    }

    public static ImmutableArray<string> ParseEvergineGPUHandleTypeNames()
    {
        var assembly = typeof(Evergine.Bindings.WebGPU.WebGPUNative).Assembly;
        var types = assembly.GetTypes();
        var handles = types.Where(t => t.IsValueType
                                         && !t.IsEnum
                                         && t.Name.StartsWith("WGPU")
                                         && HasHandleField(t))
                           .Select(t => t.Name[1..]);
        return handles.ToImmutableArray();

        static bool HasHandleField(Type t)
        {
            return t.GetMembers()
                    .OfType<FieldInfo>()
                    .Any(f => f.Name == "Handle" && f.FieldType == typeof(nint));
        }
    }

    public static GPUApi ParseIDLSpec(JsonDocument doc)
    {
        var idlSpec = WebIDLSpec.Parse(doc);
        var handleNames = ParseEvergineGPUHandleTypeNames().ToImmutableHashSet();
        var handles = handleNames.Select(n => ParseHandle(n, idlSpec))
                                 .OrderBy(t => t.Name);
        var result = new GPUApi([.. handles]);
        result.Validate();
        return result;

        static string SpecName(string idlName)
        {
            return idlName switch
            {
                "GPU" => "GPUInstance",
                "GPUCanvasContext" => "GPUSurface",
                _ => idlName
            };
        }

        static Handle ParseHandle(string name, WebIDLSpec spec)
        {
            var interfaces = spec.Declarations
                           .OfType<Interface>();
            var idlName = interfaces.First(t => SpecName(t.Name) == name).Name;

            var includedNames = GetAllIncludes(idlName, [.. spec.Declarations.OfType<IncludeDecl>()]).ToImmutableHashSet();

            var members = from t in spec.Declarations
                          from m in t switch
                          {
                              Interface t_ when includedNames.Contains(t_.Name) => t_.Members,
                              InterfaceMixin m when includedNames.Contains(m.Name) => m.Members,
                              _ => []
                          }
                          select m;
            var methods = members.OfType<Operation>()
                                 .Select(m =>
                                 {
                                     var parameters = m.Arguments.Select(p =>
                                            new Parameter(p.Name, WebIDLSpec.ParseWebIDLTypeRef(p.IdlType), null));
                                     return new Method(
                                         m.Name,
                                         [.. parameters],
                                         WebIDLSpec.ParseWebIDLTypeRef(m.IdlType));
                                 })
                                 .ToImmutableArray();
            var properties = members.OfType<WebIDLAttribute>()
                                    .Select(p =>
                                        new Property(p.Name,
                                                              WebIDLSpec.ParseWebIDLTypeRef(p.IdlType)))
                                    .ToImmutableArray();
            return new Handle(name, methods, properties);

            static IEnumerable<string> GetAllIncludes(string name, ImmutableArray<IncludeDecl> includes)
            {
                var targets = includes.Where(t => t.Target == name);
                return targets.Select(t => t.Includes)
                              .Concat(targets.SelectMany(t => GetAllIncludes(t.Includes, includes)))
                              .Concat([name]);
            }
        }
    }

}

public static class ApiGen
{
}

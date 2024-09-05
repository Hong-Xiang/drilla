using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed class GPUEnumCodeGen
{
    Assembly EvergineAssembly { get; } = typeof(Evergine.Bindings.WebGPU.WebGPUNative).Assembly;

    private string? GetMappedEverginValueName(string enumName, string valueName)
    {
        return (enumName, valueName) switch
        {
            ("GPUDeviceLostReason", "Unknown") => "Undefined",
            (_, "1d") => "_1D",
            (_, "2d") => "_2D",
            (_, "3d") => "_3D",
            (_, "2dArray") => "_2DArray",
            _ => null
        };
    }

    public void EmitEnumDecl(StringBuilder sb, EnumDeclaration decl)
    {
        var evergineType = EvergineAssembly.GetType("Evergine.Bindings.WebGPU.W" + decl.Name);
        Debug.Assert(evergineType.IsEnum);
        var evergineMembers = evergineType.GetMembers()
                                          .OfType<FieldInfo>()
                                          .Where(m => m.FieldType == evergineType).ToImmutableArray();

        if (decl.IsFlag)
        {
            sb.AppendLine("[Flags]");
        }
        sb.AppendLine($"public enum {decl.Name} : int");
        sb.AppendLine("{");

        if (evergineType is not null)
        {
            List<KeyValuePair<string, int>> values = [];

            foreach (var v in decl.Values)
            {
                var name = GetCSharpFriendlyEnumName(v.Name);
                var evergineName = GetMappedEverginValueName(decl.Name, name) ?? name;
                var evergineMember = evergineMembers.Single(m => string.Equals(m.Name, evergineName, StringComparison.OrdinalIgnoreCase));
                var value = (int)Enum.Parse(evergineType, evergineMember.Name);
                values.Add(KeyValuePair.Create(evergineName, value));
            }

            Debug.Assert(values.Count == evergineMembers.Length - 1
                         || (values.Count == evergineMembers.Length - 2 && evergineMembers.Select(m => m.Name).Contains("Undefined")));

            foreach (var kv in values.OrderBy(kv => kv.Value))
            {
                sb.Append(kv.Key);
                sb.Append(" = ");
                if (decl.IsFlag)
                {
                    sb.Append($"0x{kv.Value:X}");
                }
                else
                {
                    sb.Append(kv.Value);
                }
                sb.AppendLine(",");
            }
        }
        else
        {
            throw new NotSupportedException($"Can not process enum {decl.Name}");
        }


        sb.AppendLine("}");
    }

    string GetCSharpFriendlyEnumName(string value)
    {
        return string.Join(string.Empty, value.Split('-', '_').Select(s => s.ToLower().Capitalize()));
    }
}

using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using DualDrill.CLSL.Frontend;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Graphics;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Reflection;

public interface IShaderModuleReflection
{
    public ImmutableArray<ShaderUniformBinding> GetUniformBindings(IShaderModuleDeclaration module);
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IShaderModuleDeclaration module,
        int group);
    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(
        IShaderModuleDeclaration module,
        int group);

    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout,
        THostLayout>();

    public IVertexBufferLayoutBuilder<TGPULayout> GetVertexBufferLayoutBuilder<TGPULayout>() where TGPULayout : struct;
}

public interface IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout>
{
    IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
        Expression<Func<TGPULayout, TElement>> targetBinding,
        Expression<Func<THostLayout, TElement>> sourceBuffer);

    ImmutableArray<GPUVertexBufferLayout> Build();
}

public interface IVertexBufferLayoutBuilder<TGPULayout> where TGPULayout : struct
{
    ImmutableArray<GPUVertexBufferLayout> Build();
}

internal sealed class HostBufferLayout<TBufferModel>(int Binding)
    where TBufferModel : unmanaged
{
}

internal sealed record VertexDataMapping<THostBufferModel, TShaderModel>(
    Expression<Func<THostBufferModel, TShaderModel>> Mapping)
{
}

public class VertexBufferLayoutHelper
{
    public static Type GetTypeFromMemberInfo(MemberInfo member)
    {
        switch (member.MemberType)
        {
            case MemberTypes.Event:
                return ((EventInfo)member).EventHandlerType;
            case MemberTypes.Field:
                return ((FieldInfo)member).FieldType;
            case MemberTypes.Method:
                return ((MethodInfo)member).ReturnType;
            case MemberTypes.Property:
                return ((PropertyInfo)member).PropertyType;
            default:
                throw new ArgumentException
                (
                    "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                );
        }
    }

    public static int GetByteSize(Type type)
    {
        //TODO: Add more types
        if (type == typeof(Vector4)) return 16;

        if (type == typeof(Vector3)) return 12;

        if (type == typeof(Vector2)) return 8;

        if (type == typeof(float)) return 4;

        if (type == typeof(int)) return 4;

        if (type == typeof(vec2f32)) return 8;

        throw new Exception("Not in the ILSL typs");
    }

    public static GPUVertexFormat GetGPUVertexFormat(Type type)
    {
        //TODO: Add more types
        if (type == typeof(Vector4)) return GPUVertexFormat.Float32x4;

        if (type == typeof(Vector3)) return GPUVertexFormat.Float32x3;

        if (type == typeof(Vector2)) return GPUVertexFormat.Float32x2;

        if (type == typeof(float)) return GPUVertexFormat.Float32;

        if (type == typeof(int)) return GPUVertexFormat.Sint32;

        if (type == typeof(vec2f32)) return GPUVertexFormat.Float32x2;

        throw new Exception("Not in the ILSL GPUVertexFormat");
    }
}

public sealed class VertexBufferLayoutBuilder<TGPULayout> : IVertexBufferLayoutBuilder<TGPULayout>
    where TGPULayout : struct
{
    public ImmutableArray<GPUVertexBufferLayout> Build()
    {
        var attributes = new List<GPUVertexAttribute>();
        Dictionary<int, MemberInfo> LayoutDict = new();
        var members = typeof(TGPULayout).GetMembers();
        foreach (var member in members)
        {
            if (member.MemberType is not MemberTypes.Field) continue;
            var location = member.GetCustomAttributes(false).OfType<LocationAttribute>().First().Binding;
            LayoutDict[location] = member;
        }

        ulong offset = 0;
        foreach (var keyValue in LayoutDict)
        {
            var key = keyValue.Key;
            var member = keyValue.Value;
            attributes.Add(new GPUVertexAttribute
            {
                ShaderLocation = key,
                Format = VertexBufferLayoutHelper.GetGPUVertexFormat(
                    VertexBufferLayoutHelper.GetTypeFromMemberInfo(member)),
                Offset = offset
            });
            offset += (ulong)VertexBufferLayoutHelper.GetByteSize(
                VertexBufferLayoutHelper.GetTypeFromMemberInfo(member));
        }

        return new[]
        {
            new GPUVertexBufferLayout
            {
                ArrayStride = offset,
                StepMode = GPUVertexStepMode.Vertex,
                Attributes = attributes.ToArray()
            }
        }.ToImmutableArray();
    }
}

public sealed class
    VertexBufferMappingBuilder<TGPULayout, THostLayout> : IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout>
{
    private readonly Dictionary<MemberInfo, List<MemberInfo>> LayoutMap = new();

    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
        Expression<Func<TGPULayout, TElement>> targetBinding,
        Expression<Func<THostLayout, TElement>> sourceBuffer)
    {
        var gpuLayoutElement = ParseMemberExpression(targetBinding.Body);
        var hostLayoutElement = ParseMemberExpression(sourceBuffer.Body);
        if (LayoutMap.ContainsKey(hostLayoutElement))
            LayoutMap[hostLayoutElement].Add(gpuLayoutElement);
        else
            LayoutMap.Add(hostLayoutElement, new List<MemberInfo> { gpuLayoutElement });
        return this;
    }

    public ImmutableArray<GPUVertexBufferLayout> Build()
    {
        var gpuVertexBufferLayouts = new List<GPUVertexBufferLayout>();
        Dictionary<int, GPUVertexAttribute> vertexAttributeDict = new();
        Dictionary<int, int> byteSizeDict = new();
        foreach (var userDefinedElement in LayoutMap)
        {
            var key = userDefinedElement.Key;
            var gpuVertexBufferLayout = new GPUVertexBufferLayout
            {
                StepMode = key.GetCustomAttributes(false).OfType<VertexStepModeAttribute>().First().StepMode
            };
            var attributes = new List<GPUVertexAttribute>();
            var stride = 0;
            var locationList = new List<int>();
            foreach (var vertexLayoutElementType in userDefinedElement.Value)
            {
                var location = vertexLayoutElementType.GetCustomAttributes(false).OfType<LocationAttribute>().First()
                                                      .Binding;
                var byteSize =
                    VertexBufferLayoutHelper.GetByteSize(
                        VertexBufferLayoutHelper.GetTypeFromMemberInfo(vertexLayoutElementType));
                var format =
                    VertexBufferLayoutHelper.GetGPUVertexFormat(
                        VertexBufferLayoutHelper.GetTypeFromMemberInfo(vertexLayoutElementType));
                vertexAttributeDict[location] = new GPUVertexAttribute
                {
                    ShaderLocation = location,
                    Format = format,
                    Offset = 0
                };
                stride += byteSize;
                locationList.Add(location);
                byteSizeDict[location] = byteSize;
            }

            locationList.Sort();
            ulong offset = 0;
            for (var i = 0; i < locationList.Count; i++)
            {
                var binding = locationList[i];
                var value = vertexAttributeDict[binding];
                value.Offset = offset;
                attributes.Add(value);
                offset += (ulong)byteSizeDict[binding];
            }

            gpuVertexBufferLayout.ArrayStride = (ulong)stride;
            gpuVertexBufferLayout.Attributes = attributes.ToArray();
            vertexAttributeDict.Clear();
            gpuVertexBufferLayouts.Add(gpuVertexBufferLayout);
        }

        return gpuVertexBufferLayouts.ToImmutableArray();
    }

    private MemberInfo ParseMemberExpression(Expression exp)
    {
        if (exp is MemberExpression memberExp)
        {
            if (memberExp.Expression is MemberExpression innerMemberExp) return ParseMemberExpression(innerMemberExp);

            return memberExp.Member;
        }

        throw new Exception("Not a member expression");
    }
}

public sealed class ShaderModuleReflection : IShaderModuleReflection
{
    public ImmutableArray<ShaderUniformBinding> GetUniformBindings(IShaderModuleDeclaration module)
    {
        ShaderModuleMetadataValidator.Validate(module);
        return
        [
            .. module.Declarations
                     .OfType<VariableDeclaration>()
                     .Where(declaration => declaration.Attributes.OfType<UniformAttribute>().Any())
                     .Select(CreateUniformBinding)
                     .OrderBy(binding => binding.Group)
                     .ThenBy(binding => binding.Binding)
                     .ThenBy(binding => binding.Name, StringComparer.Ordinal)
        ];
    }

    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IShaderModuleDeclaration module,
        int group)
    {
        ValidateGroup(group);
        return new GPUBindGroupLayoutDescriptor
        {
            Entries = GetUniformBindings(module)
                     .Where(uniform => uniform.Group == group)
                     .Select(uniform => new GPUBindGroupLayoutEntry
                     {
                         Binding = uniform.Binding,
                         Visibility = uniform.Visibility,
                         Buffer = CreateBufferLayout(uniform)
                     })
                     .ToArray()
        };
    }

    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(
        IShaderModuleDeclaration module,
        int group)
    {
        ValidateGroup(group);
        return new GPUBindGroupLayoutDescriptorBuffer
        {
            Entries = GetUniformBindings(module)
                     .Where(uniform => uniform.Group == group)
                     .Select(uniform => new GPUBindGroupLayoutEntryBuffer
                     {
                         Binding = uniform.Binding,
                         Visibility = uniform.Visibility,
                         Buffer = CreateBufferLayout(uniform)
                     })
                     .ToArray()
        };
    }

    private static ShaderUniformBinding CreateUniformBinding(VariableDeclaration declaration)
    {
        var group = declaration.Attributes.OfType<GroupAttribute>().Single().Binding;
        var binding = declaration.Attributes.OfType<BindingAttribute>().Single();
        var visibility = declaration.Attributes
                                    .OfType<IShaderStageAttribute>()
                                    .Aggregate(GPUShaderStage.None, (stages, stage) => stages | stage.Stage);
        if (visibility == GPUShaderStage.None)
            visibility = GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute;

        return new ShaderUniformBinding(
            declaration.Name,
            group,
            binding.Binding,
            visibility,
            binding.HasDynamicOffset,
            WgslUniformLayoutCalculator.Calculate(declaration));
    }

    private static GPUBufferBindingLayout CreateBufferLayout(ShaderUniformBinding uniform) =>
        new()
        {
            Type = uniform.Kind,
            HasDynamicOffset = uniform.HasDynamicOffset,
            MinBindingSize = uniform.Layout.Size
        };

    private static void ValidateGroup(int group)
    {
        if (group < 0)
            throw new ArgumentOutOfRangeException(nameof(group), group, "Bind group must be nonnegative.");
    }

    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout>
        GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>() =>
        new VertexBufferMappingBuilder<TGPULayout, THostLayout>();

    public IVertexBufferLayoutBuilder<TGPULayout> GetVertexBufferLayoutBuilder<TGPULayout>()
        where TGPULayout : struct => new VertexBufferLayoutBuilder<TGPULayout>();

    private static int GetByteSize(IShaderType type)
    {
        return type switch
        {
            ICreationFixedFootprintType bt => bt.ByteSize,
            _ => throw new NotImplementedException()
        };
    }
}
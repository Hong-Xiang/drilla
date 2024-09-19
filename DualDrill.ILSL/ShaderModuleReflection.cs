using DualDrill.Graphics;
using DualDrill.ILSL.IR.Declaration;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL.Patterns;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Numerics;
namespace DualDrill.ILSL;

public interface IShaderModuleReflection
{
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IR.Module module
    );

    public IVertexBufferMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>();
}

public interface IVertexBufferMappingBuilder<TGPULayout, THostLayout>
{
    IVertexBufferMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
           Expression<Func<TGPULayout, TElement>> targetBinding,
           Expression<Func<THostLayout, TElement>> sourceBuffer);

    GPUVertexBufferLayout[] Build();
}

sealed class HostBufferLayout<TBufferModel>(int Binding)
    where TBufferModel : unmanaged
{
}

sealed record VertexDataMapping<THostBufferModel, TShaderModel>(
    Expression<Func<THostBufferModel, TShaderModel>> Mapping)
{
}

public sealed class VertexBufferMappingBuilder<TGPULayout, THostLayout> : IVertexBufferMappingBuilder<TGPULayout, THostLayout>
{
    private Dictionary<MemberInfo, List<MemberInfo>> LayoutMap = new();
    private static Type GetTypeFromMemberInfo(MemberInfo member)
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

    private static int GetByteSize(Type type)
    {
        //TODO: Add more types
        if (type == typeof(Vector4))
        {
            return 16;
        }
        else if(type == typeof(Vector3))
        {
            return 12;
        }
        else if(type == typeof(Vector2))
        {
            return 8;
        }
        else if(type == typeof(float))
        {
            return 4;
        }
        else if(type == typeof(int))
        {
            return 4;
        }
        else
        {
            throw new Exception("Not in the ILSL typs");
        }
    }

    private static GPUVertexFormat GetGPUVertexFormat(Type type)
    {
        //TODO: Add more types
        if (type == typeof(Vector4))
        {
            return GPUVertexFormat.Float32x4;
        }
        else if (type == typeof(Vector3))
        {
            return GPUVertexFormat.Float32x3;
        }
        else if (type == typeof(Vector2))
        {
            return GPUVertexFormat.Float32x2;
        }
        else if (type == typeof(float))
        {
            return GPUVertexFormat.Float32;
        }
        else if (type == typeof(int))
        {
            return GPUVertexFormat.Sint32;
        }
        else
        {
            throw new Exception("Not in the ILSL GPUVertexFormat");
        }
    }
    private MemberInfo ParseMemberExpression(System.Linq.Expressions.Expression exp)
    {
        if (exp is MemberExpression memberExp)
        {
            if (memberExp.Expression is MemberExpression innerMemberExp)
            {
                return ParseMemberExpression(innerMemberExp);
            }
            else
            {
                return memberExp.Member;
            }
        }
        else
        {
            throw new Exception("Not a member expression");
        }
    }
    public IVertexBufferMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
            Expression<Func<TGPULayout, TElement>> targetBinding,
            Expression<Func<THostLayout, TElement>> sourceBuffer)
    {
        var gpuLayoutElement = ParseMemberExpression(targetBinding.Body);
        var hostLayoutElement = ParseMemberExpression(sourceBuffer.Body);
        if (LayoutMap.ContainsKey(hostLayoutElement))
        {
            LayoutMap[hostLayoutElement].Add(gpuLayoutElement);
        }
        else
        {
            LayoutMap.Add(hostLayoutElement, new List<MemberInfo> { gpuLayoutElement });
        }
        return (IVertexBufferMappingBuilder<TGPULayout, THostLayout>)this;
    }
    public GPUVertexBufferLayout[] Build()
    {
        var gpuVertexBufferLayouts = new List<GPUVertexBufferLayout>();
        Dictionary<int, GPUVertexAttribute> vertexAttributeDict = new();
        Dictionary<int, int> byteSizeDict = new();
        foreach (var userDefinedElement in LayoutMap)
        {
            var key = userDefinedElement.Key;
            var gpuVertexBufferLayout = new GPUVertexBufferLayout()
            {
                StepMode = key.GetCustomAttributes(false).OfType<VertexStepModeAttribute>().First().StepMode,
            };
            var attributes =new List<GPUVertexAttribute>();
            int stride = 0;
            var locationList = new List<int>();
            foreach (var vertexLayoutElementType in userDefinedElement.Value)
            {
                var location = vertexLayoutElementType.GetCustomAttributes(false).OfType<LocationAttribute>().First().Binding;
                var byteSize = GetByteSize(GetTypeFromMemberInfo(vertexLayoutElementType));
                var format = GetGPUVertexFormat(GetTypeFromMemberInfo(vertexLayoutElementType));
                vertexAttributeDict[location] = new GPUVertexAttribute()
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
            for(int i = 0; i < locationList.Count; i++)
            {
                int binding = locationList[i];
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
        return gpuVertexBufferLayouts.ToArray();
    }
}

public sealed class ShaderModuleReflection : IShaderModuleReflection
{
    private static int GetByteSize(IType type)
    {
        return type switch
        {
            IBuiltinType bt => bt.ByteSize,
            _ => throw new NotImplementedException()
        };
    }
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(IR.Module module)
    {

        var gpuBindGroupLayoutEntries = new List<GPUBindGroupLayoutEntry>();
        foreach (var decl in module.Declarations.OfType<VariableDeclaration>())
        {
            gpuBindGroupLayoutEntries.Add(new GPUBindGroupLayoutEntry()
            {
                Binding = 0,
                Visibility = GPUShaderStage.Vertex,
                Buffer = new GPUBufferBindingLayout()
                {
                    Type = GPUBufferBindingType.Uniform,
                    HasDynamicOffset = decl.Attributes.OfType<BindingAttribute>().First().HasDynamicOffset,
                    MinBindingSize = (ulong)GetByteSize(decl.Type)
                }
            });
        }
        var gpuBindGroupDescriptor = new GPUBindGroupLayoutDescriptor()
        {
            Entries = gpuBindGroupLayoutEntries.ToArray()
        };
        return gpuBindGroupDescriptor;
    }

    public IVertexBufferMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>()
    {
        return new VertexBufferMappingBuilder<TGPULayout, THostLayout>();
    }
}
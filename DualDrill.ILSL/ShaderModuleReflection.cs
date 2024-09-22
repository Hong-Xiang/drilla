using DualDrill.Graphics;
using DualDrill.ILSL.IR.Declaration;
using System.Linq.Expressions;
using System.Reflection;
using System.Numerics;
using System.Collections.Immutable;
using Microsoft.VisualBasic.FileIO;
using System.Runtime.InteropServices;
using Silk.NET.SDL;
using System.Diagnostics;
using System.Text.Json;
namespace DualDrill.ILSL;

public interface IShaderModuleReflection
{
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(IR.Module module);
    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(IR.Module module);
    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>();
    public IVertexBufferLayoutBuilder<TGPULayout> GetVertexBufferLayoutBuilder<TGPULayout>() where TGPULayout : struct ;
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

sealed class HostBufferLayout<TBufferModel>(int Binding)
    where TBufferModel : unmanaged
{
}

sealed record VertexDataMapping<THostBufferModel, TShaderModel>(
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
        if (type == typeof(Vector4))
        {
            return 16;
        }
        else if (type == typeof(Vector3))
        {
            return 12;
        }
        else if (type == typeof(Vector2))
        {
            return 8;
        }
        else if (type == typeof(float))
        {
            return 4;
        }
        else if (type == typeof(int))
        {
            return 4;
        }
        else
        {
            throw new Exception("Not in the ILSL typs");
        }
    }

    public static GPUVertexFormat GetGPUVertexFormat(Type type)
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

}

public sealed class VertexBufferLayoutBuilder<TGPULayout> : IVertexBufferLayoutBuilder<TGPULayout> where TGPULayout : struct
{
    public ImmutableArray<GPUVertexBufferLayout> Build()
    {
        var attributes = new List<GPUVertexAttribute>();
        Dictionary<int, MemberInfo> LayoutDict = new();
        var members = typeof(TGPULayout).GetMembers();
        foreach(var member in members)
        {
            if(member.MemberType is not MemberTypes.Field)
            {
                continue;
            }
            var location = member.GetCustomAttributes(false).OfType<LocationAttribute>().First().Binding;
            LayoutDict[location] = member;
        }
        ulong offset = 0;
        foreach(var keyValue in LayoutDict)
        {
            int key = keyValue.Key;
            var member = keyValue.Value;
            attributes.Add(new GPUVertexAttribute()
            {
                ShaderLocation = key,
                Format = VertexBufferLayoutHelper.GetGPUVertexFormat(VertexBufferLayoutHelper.GetTypeFromMemberInfo(member)),
                Offset = offset
            });
            offset += (ulong)VertexBufferLayoutHelper.GetByteSize(VertexBufferLayoutHelper.GetTypeFromMemberInfo(member));
        }
        return new GPUVertexBufferLayout[]
        {
            new GPUVertexBufferLayout()
            {
                ArrayStride = offset,
                StepMode = GPUVertexStepMode.Vertex,
                Attributes = attributes.ToArray()
            }
        }.ToImmutableArray();
    }
}

public sealed class VertexBufferMappingBuilder<TGPULayout, THostLayout> : IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout>
{
    private Dictionary<MemberInfo, List<MemberInfo>> LayoutMap = new();
    
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

    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
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
                var byteSize = VertexBufferLayoutHelper.GetByteSize(VertexBufferLayoutHelper.GetTypeFromMemberInfo(vertexLayoutElementType));
                var format = VertexBufferLayoutHelper.GetGPUVertexFormat(VertexBufferLayoutHelper.GetTypeFromMemberInfo(vertexLayoutElementType));
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
        return gpuVertexBufferLayouts.ToImmutableArray();
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
            //var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            //var type = assemblies.Where(a => a.FullName.Contains("DualDrill.Engine")).First().GetTypes().FirstOrDefault(e => e.Name == decl.Type.Name);
            gpuBindGroupLayoutEntries.Add(new GPUBindGroupLayoutEntry()
            {
                Binding = decl.Attributes.OfType<BindingAttribute>().First().Binding,
                Visibility = (decl.Attributes.OfType<StageAttribute>().Count() != 0) ? decl.Attributes.OfType<StageAttribute>().First().Stage : (GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute),
                Buffer = new GPUBufferBindingLayout()
                {
                    Type = GPUBufferBindingType.Uniform,
                    HasDynamicOffset = decl.Attributes.OfType<BindingAttribute>().First().HasDynamicOffset,
                    MinBindingSize = 8
                }
            });
        }
        var gpuBindGroupDescriptor = new GPUBindGroupLayoutDescriptor()
        {
            Entries = gpuBindGroupLayoutEntries.ToArray()
        };

        return gpuBindGroupDescriptor;
    }

    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(IR.Module module)
    {
        var gpuBindGroupLayoutEntries = new List<GPUBindGroupLayoutEntryBuffer>();
        foreach (var decl in module.Declarations.OfType<VariableDeclaration>())
        {
            //var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            //var type = assemblies.Where(a => a.FullName.Contains("DualDrill.Engine")).First().GetTypes().FirstOrDefault(e => e.Name == decl.Type.Name);
            gpuBindGroupLayoutEntries.Add(new GPUBindGroupLayoutEntryBuffer()
            {
                Binding = decl.Attributes.OfType<BindingAttribute>().First().Binding,
                Visibility = (decl.Attributes.OfType<StageAttribute>().Count() != 0) ? decl.Attributes.OfType<StageAttribute>().First().Stage : (GPUShaderStage.Vertex | GPUShaderStage.Fragment | GPUShaderStage.Compute),
                Buffer = new GPUBufferBindingLayout()
                {
                    Type = GPUBufferBindingType.Uniform,
                    HasDynamicOffset = decl.Attributes.OfType<BindingAttribute>().First().HasDynamicOffset,
                    MinBindingSize = 8
                }
            });
        }
        var gpuBindGroupDescriptor = new GPUBindGroupLayoutDescriptorBuffer()
        {
            Entries = gpuBindGroupLayoutEntries.ToArray()
        };

        return gpuBindGroupDescriptor;
    }

    public IVertexBufferLayoutMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>()
    {
        return new VertexBufferMappingBuilder<TGPULayout, THostLayout>();
    }

    public IVertexBufferLayoutBuilder<TGPULayout> GetVertexBufferLayoutBuilder<TGPULayout>() where TGPULayout : struct
    {
        return new VertexBufferLayoutBuilder<TGPULayout>();
    }
}
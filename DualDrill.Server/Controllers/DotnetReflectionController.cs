#pragma warning disable DuckDBNET001
using CommunityToolkit.HighPerformance;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Engine.Shader;
using DuckDB.NET.Data;
using Lokad.ILPack.IL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Silk.NET.Vulkan;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

public sealed class DuckDBConnectionService : IDisposable
{
    DuckDBConnection Connection { get; }
    public DuckDBConnectionService()
    {
        var connection = new DuckDBConnection($"Data Source=:memory:");
        //var connection = new DuckDBConnection();
        connection.Open();

        connection.RegisterScalarFunction<long, string>("do_get_type_info", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var handle = readers[0].GetValue<long>(index);
                var type = Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr((nint)handle));
                if (type is null)
                {
                    continue;
                }
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                var info = new
                {
                    handle,
                    name = type.CSharpFullName(),
                    methods = type.IsPrimitive ? [] : type.GetMethods(bindingFlags).Select(m => (long)m.MethodHandle.Value).ToArray(),
                    fields = type.IsPrimitive ? [] : type.GetFields(bindingFlags).Select(f => (long)f.FieldHandle.Value).ToArray(),
                    is_value_type = type.IsValueType,
                    properties = type.IsPrimitive ? [] : type.GetProperties(bindingFlags).Select(f => new
                    {
                        name = f.Name,
                        getter = (long?)(f.GetMethod is not null ? (long)f.GetMethod.MethodHandle.Value : null),
                        setter = (long?)(f.SetMethod is not null ? (long)f.SetMethod.MethodHandle.Value : null),
                        type = (long)f.PropertyType.TypeHandle.Value
                    }).ToArray()
                };
                var result = JsonSerializer.Serialize(info);
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterScalarFunction<int, float>("bitcast_i32_to_f32", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var value = readers[0].GetValue<int>(index);
                var result = BitConverter.Int32BitsToSingle(value);
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterScalarFunction<long, double>("bitcast_i64_to_f64", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var value = readers[0].GetValue<long>(index);
                var result = BitConverter.Int64BitsToDouble(value);
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterScalarFunction<long, string>("do_get_method_body", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var handle = readers[0].GetValue<long>(index);
                var method = MethodBase.GetMethodFromHandle(RuntimeMethodHandle.FromIntPtr((nint)handle));
                if (method is null)
                {
                    continue;
                }
                var methodBody = method.GetMethodBody();
                if (methodBody is null)
                {
                    continue;
                }
                var local_variables = methodBody.LocalVariables.Select(lv => new
                {
                    index = lv.LocalIndex,
                    type_handle = (long)lv.LocalType.TypeHandle.Value,
                });
                var instructions = method.GetInstructions().Select(inst =>
                {
                    var operand = default(long);

                    switch (inst.Operand)
                    {
                        case byte v:
                            operand = v;
                            break;
                        case sbyte v:
                            operand = v;
                            break;
                        case int v:
                            operand = v;
                            break;
                        case uint v:
                            operand = v;
                            break;
                        case long v:
                            operand = v;
                            break;
                        case ulong v:
                            operand = (long)v;
                            break;
                        case float v:
                            operand = BitConverter.SingleToInt32Bits(v);
                            break;
                        case double v:
                            operand = BitConverter.DoubleToInt64Bits(v);
                            break;
                        case LocalVariableInfo v:
                            operand = v.LocalIndex;
                            break;
                        case MethodInfo m:
                            operand = m.MethodHandle.Value;
                            break;
                        case ConstructorInfo m:
                            operand = m.MethodHandle.Value;
                            break;
                        case ParameterInfo p:
                            operand = p.Position;
                            break;
                        default:
                            break;
                    }


                    return new
                    {
                        ilOpCode = inst.OpCode.ToILOpCode(),
                        offset = inst.Offset,
                        operand
                    };
                });
                var result = JsonSerializer.Serialize(new
                {
                    local_variables,
                    code_size = methodBody.GetILAsByteArray()?.Length ?? 0,
                    instructions = instructions.ToArray()
                });
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterScalarFunction<long, string>("do_get_method_info", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var handle = readers[0].GetValue<long>(index);
                var method = MethodBase.GetMethodFromHandle(RuntimeMethodHandle.FromIntPtr((nint)handle));
                var shaderAttributes = method.GetCustomAttributes().OfType<IShaderAttribute>().ToImmutableArray();
                var is_entry_method = shaderAttributes.OfType<IShaderStageAttribute>().Any();
                var is_runtime_method = shaderAttributes.OfType<IOperationMethodAttribute>().Any();
                var info = new
                {
                    handle,
                    name = method.Name,
                    decl_type = (long)method.DeclaringType.TypeHandle.Value,
                    parameters = method.GetParameters().Select((p, pi) => new
                    {
                        index = pi,
                        name = p.Name,
                        type = (long)p.ParameterType.TypeHandle.Value
                    }).ToArray(),
                    is_static = method.IsStatic,
                    is_special_name = method.IsSpecialName,
                    is_entry_method,
                    is_runtime_method,
                    return_type = (long?)(method is MethodInfo m ? m.ReturnType.TypeHandle.Value : null)
                };
                var result = JsonSerializer.Serialize(info);
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterScalarFunction<long, string>("do_get_field_info", (readers, writer, count) =>
        {
            for (var index = 0ul; index < count; index++)
            {
                var handle = readers[0].GetValue<long>(index);
                var field = FieldInfo.GetFieldFromHandle(RuntimeFieldHandle.FromIntPtr((nint)handle));
                var info = new
                {
                    handle,
                    name = field.Name,
                    decl_type = (long)field.DeclaringType.TypeHandle.Value,
                };
                var result = JsonSerializer.Serialize(info);
                writer.WriteValue(result, index);
            }
        });

        connection.RegisterTableFunction<int>("do_list_entry_types", (paramters) =>
        {
            return new TableFunction([
                            new ColumnInfo("type_handle", typeof(long)),
                new ColumnInfo("type_name", typeof(string)),
                ], (IEnumerable<Type>)[
                            typeof(MandelbrotDistanceShader),
                            typeof(RaymarchingPrimitiveShader),
                typeof(SimpleUniformShader),
                typeof(MinimumTriangle)
                        ]);
        }, (item, writers, index) =>
        {
            var type = (Type)item;
            writers[0].WriteValue((long)type.TypeHandle.Value, index);
            writers[1].WriteValue(type.Name, index);
        });

        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CALL start_ui();";
            cmd.ExecuteNonQuery();
        }

        Connection = connection;
    }



    public void Dispose()
    {
        Connection.Close();
        Connection.Dispose();
    }
}


[Route("api/[controller]")]
[ApiController]
public class DotnetReflectionController(DuckDBConnectionService DuckDB) : ControllerBase
{
    [HttpGet("entry-type")]
    public long[] GetEntryType()
    {
        IEnumerable<Type> Entries = [
               typeof(MandelbrotDistanceShader),
                typeof(RaymarchingPrimitiveShader),
                typeof(SimpleUniformShader),
                typeof(MinimumTriangle)
            ];
        return [.. Entries.Select(t => (long)t.TypeHandle.Value)];
    }

    [HttpGet("type/{handle}")]
    public IActionResult GetTypeInfo(long handle)
    {
        var rh = RuntimeTypeHandle.FromIntPtr((nint)handle);
        var type = Type.GetTypeFromHandle(rh);
        return Ok(new
        {
            handle,
            name = type.CSharpFullName(),
            methods = type.GetMethods().Select(m => (long)m.MethodHandle.Value).ToArray(),
            fields = type.GetFields().Select(f => (long)f.FieldHandle.Value).ToArray(),
            is_value_type = type.IsValueType,
            properties = type.GetProperties().Select(f => new
            {
                name = f.Name,
                getter = (long?)(f.GetMethod is not null ? (long)f.GetMethod.MethodHandle.Value : null),
                setter = (long?)(f.SetMethod is not null ? (long)f.SetMethod.MethodHandle.Value : null),
                type = (long)f.PropertyType.TypeHandle.Value
            }).ToArray()
        });
    }

    [HttpGet("method/{handle}")]
    public IActionResult GetMethodInfo(long handle)
    {
        var rh = RuntimeMethodHandle.FromIntPtr((nint)handle);
        var method = MethodBase.GetMethodFromHandle(rh);
        return Ok(new
        {
            handle,
            name = method.Name,
            decl_type = (long)method.DeclaringType.TypeHandle.Value,
            parameters = method.GetParameters().Select((p, pi) => new
            {
                index = pi,
                name = p.Name,
                type = (long)p.ParameterType.TypeHandle.Value
            }).ToArray(),
            is_static = method.IsStatic,
            is_special_name = method.IsSpecialName
        });
    }

    [HttpGet("field/{handle}")]
    public IActionResult GetFieldInfo(long handle)
    {
        var rh = RuntimeFieldHandle.FromIntPtr((nint)handle);
        var field = FieldInfo.GetFieldFromHandle(rh);
        return Ok(new
        {
            handle,
            name = field.Name,
            type = (long)field.FieldType.TypeHandle.Value,
        });
    }

    static void M<T>()
    {
    }

    static MethodBase GetMethod<T>(Action action)
    {
        return action.Method;
    }

    [HttpGet("test")]
    public IActionResult HandleTest()
    {

        return Ok((long[])[
            GetMethod<int>(M<int>).MethodHandle.Value,
            GetMethod<string>(M<string>).MethodHandle.Value,
        ]);
    }

    [HttpGet("start")]
    public IActionResult Start()
    {
        return Ok("Ok");
    }


    [HttpGet("echo")]
    public int[] Echo([FromQuery(Name = "value")] int[] values)
    {
        return values;
    }

}

public sealed record class ReflectionFieldInfo(
    long Handle,
    string Name,
    long Type
)
{
}

public sealed record class ReflectionTypeInfo(
    long Handle,
    int MetadataToken,
    string Name,
    long[] Methods
)
{
    public static ReflectionTypeInfo Create(Type type)
        => new(type.TypeHandle.Value, type.MetadataToken, type.Name, [.. type.GetMethods().Select(ReflectionMethodInfo.Create).Select(m => m.Handle)]);
}

public sealed record class ReflectionParameterInfo(
    int Index,
    string Name,
    long Type
)
{
    public static ReflectionParameterInfo Create(ParameterInfo p, int index)
        => new(index, p.Name, p.ParameterType.TypeHandle.Value);
}

public sealed record class ReflectionMethodInfo(
    long Handle,
    int MetadataToken,
    string Name,
    ReflectionParameterInfo[] Parameters,
    long? ReturnType,
    bool IsStatic
)
{
    public static ReflectionMethodInfo Create(MethodBase method)
        => new(method.MethodHandle.Value, method.MetadataToken, method.Name,
            [.. method.GetParameters().Select(ReflectionParameterInfo.Create)],
            method is MethodInfo info ? (long)info.ReturnType.TypeHandle.Value : null,
            method.IsStatic);
}

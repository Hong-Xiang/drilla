﻿using DualDrill.Engine.Shader;
using DualDrill.ILSL;
using System.Reflection;
namespace DualDrill.Server.Services;

public sealed class ILSLDevelopShaderModuleService
{
    public Dictionary<string, IILSLDevelopShaderModule> ShaderModules { get; } = new()
    {
        [nameof(MinimumTriangle)] = new MinimumTriangle(),
        //[nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(VertexOutputShader)] = new VertexOutputShader(),
        //[nameof(SimpleUniformShader)] = new SimpleUniformShader(),
    };

    public Dictionary<string, ISharpShader> DemoShaderModules { get; } = new()
    {
        [nameof(SampleFragmentShader)] = new SampleFragmentShader(),
        [nameof(SimpleUniformShader)] = new SimpleUniformShader(),
        [nameof(QuadShader)] = new QuadShader(),

    };

    public IReadOnlyDictionary<string, Assembly> KnownAssemblies { get; } = ((IEnumerable<Assembly>)[
        typeof(MinimumTriangle).Assembly,
    ]).Select(a => KeyValuePair.Create<string, Assembly>(a.FullName, a)).ToDictionary();
}

using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

namespace DualDrill.CLSL.Language.ShaderAttribute;

public sealed class ShaderRuntimeMethodAttribute : Attribute, INoArgumentShaderMetadataAttribute
{
}

public sealed class VecConstructorMethodAttribute : Attribute, INoArgumentShaderMetadataAttribute { }

public sealed class VecMethodRenamedForOverloadAttribute : Attribute, INoArgumentShaderMetadataAttribute { }

public sealed class ConversionMethodAttribute : Attribute, INoArgumentShaderMetadataAttribute { }
public sealed class ZeroConstructorMethodAttribute : Attribute, INoArgumentShaderMetadataAttribute { }

public sealed class RuntimeFieldGetMethodAttribute(string Name) : Attribute, IShaderMetadataAttribute
{
    public string Name { get; } = Name;

    public string GetCSharpUsageCode()
    {
        return $"{nameof(RuntimeFieldGetMethodAttribute)}(\"{Name}\")";
    }
}

public sealed class RuntimeFieldSetMethodAttribute(string Name) : Attribute, IShaderMetadataAttribute
{
    public string Name { get; } = Name;

    public string GetCSharpUsageCode()
    {
        return $"{nameof(RuntimeFieldSetMethodAttribute)}(\"{Name}\")";
    }
}

public sealed class RuntimeVectorSwizzleGetMethodAttribute(SwizzleComponent[] Components) : Attribute, IShaderMetadataAttribute
{
    public SwizzleComponent[] Components { get; } = Components;

    public string GetCSharpUsageCode()
    {
        var fn = typeof(SwizzleComponent).FullName;
        var arguments = string.Join(", ", Components.Select(c => $"{fn}.{Enum.GetName(c)}"));
        return $"{nameof(RuntimeVectorSwizzleGetMethodAttribute)}([{arguments}])";
    }
}

public sealed class RuntimeVectorSwizzleSetMethodAttribute(SwizzleComponent[] Components) : Attribute, IShaderMetadataAttribute
{
    public SwizzleComponent[] Components { get; } = Components;
    public string GetCSharpUsageCode()
    {
        var fn = typeof(SwizzleComponent).FullName;
        var arguments = string.Join(", ", Components.Select(c => $"{fn}.{Enum.GetName(c)}"));
        return $"{nameof(RuntimeVectorSwizzleSetMethodAttribute)}([{arguments}])";
    }
}



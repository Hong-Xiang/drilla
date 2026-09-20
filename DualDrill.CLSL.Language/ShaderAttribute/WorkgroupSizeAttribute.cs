namespace DualDrill.CLSL.Language.ShaderAttribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WorkgroupSizeAttribute(int x, int y, int z) : Attribute, IShaderAttribute
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
}

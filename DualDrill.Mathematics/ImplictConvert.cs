using System.Numerics;

namespace DualDrill.Mathematics;

public partial struct vec4f32
{
    public static implicit operator Vector4(vec4f32 v) => new(v.x, v.y, v.z, v.w);
}

public partial struct vec3f32
{
    public static implicit operator Vector3(vec3f32 v) => new(v.x, v.y, v.z);
}

public partial struct vec2f32
{
    public static implicit operator Vector2(vec2f32 v) => new(v.x, v.y);
}

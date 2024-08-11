using System.Numerics;

namespace DualDrill.Engine.Scene;

public record class Cube(
    Vector3 Position,
    Vector3 Rotation,
    float Scale = 1.0f)
{
    public Matrix4x4 ModelMatrix
    {
        get
        {
            var t = Matrix4x4.CreateTranslation(Position);
            var r = Matrix4x4.CreateFromYawPitchRoll(Rotation.X, Rotation.Y, Rotation.Z);
            var s = Matrix4x4.CreateScale(Scale);
            var m = s * r * t;
            return m;
        }
    }
}

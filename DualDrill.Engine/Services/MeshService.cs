using DualDrill.Engine.Mesh;

namespace DualDrill.Engine.Services;

public sealed class MeshService
{
    static readonly Dictionary<string, IMesh> Meshes = new Dictionary<string, IMesh>
    {
        [nameof(Cube)] = new Cube(),
        [nameof(Quad)] = new Quad(),
        [nameof(WebGPULogo)] = new WebGPULogo(),
        [nameof(ScreenQuad)] = new ScreenQuad()
    };

    public IMesh? GetMesh(string name)
    {
        if (Meshes.TryGetValue(name, out var result))
        {
            return result;
        }
        return null;
    }
}

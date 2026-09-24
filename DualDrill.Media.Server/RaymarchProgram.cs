using DualDrill.CLSL;
using DualDrill.Shaders;

internal sealed class RaymarchProgram
{
    internal string Wgsl { get; }

    private RaymarchProgram(string wgsl)
    {
        if (string.IsNullOrWhiteSpace(wgsl))
        {
            throw new ArgumentException("Raymarch WGSL must not be empty.", nameof(wgsl));
        }

        Wgsl = wgsl;
    }

    internal static RaymarchProgram Compile() =>
        new(
            new CLSLCompiler(new(CLSLCompileTarget.WGSL))
                .Emit(new RaymarchingPrimitiveShader()));
}

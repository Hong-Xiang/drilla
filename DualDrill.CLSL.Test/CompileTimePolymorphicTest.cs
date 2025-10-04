using DualDrill.CLSL.Language.ShaderAttribute;
using Silk.NET.Maths;
using System.Numerics;

namespace DualDrill.CLSL.Test;

public class CompileTimePolymorphicTest
{
    struct VSToFS
    {
        [Location(0)]
        public Vector2D<float> TextureCoordiate { get; }

        [Location(1)]
        public Vector3D<float> Normal { get; }
        [Location(2)]
        public Vector3D<float> Tagnent { get; }

    }

    interface INormalShaderSource
    {
        public Vector3D<float> GetNormal(VSToFS input);
    }

    struct SimpleNormal : INormalShaderSource
    {
        public Vector3D<float> GetNormal(VSToFS input)
        {
            return input.Normal;
        }
    }

    // from https://blog.csdn.net/qq_35312463/article/details/127022932
    struct BumpMapNormal : INormalShaderSource
    {
        [Group(0)]
        [Binding(2)]
        ISampler NormalSampler { get; }

        [Group(0)]
        [Binding(3)]
        ITexture2D<Vector3D<float>> NormalTexture { get; }

        public Vector3D<float> GetNormal(VSToFS input)
        {
            var bumpMap = NormalTexture.Sample(NormalSampler, input.TextureCoordiate);
            bumpMap = bumpMap * 2.0f - new Vector3D<float>(1.0f);
            var viewSpaceBinormal = Vector3D.Cross(input.Normal, input.Tagnent);
            Matrix3X3<float> texSpace = new Matrix3X3<float>(input.Tagnent, viewSpaceBinormal, input.Normal);
            return Vector3D.Normalize(bumpMap * texSpace);
        }
    }

    sealed class LitFragmentShader<T>
        where T : INormalShaderSource
    {

        [Group(0)]
        [Binding(0)]
        ISampler Sampler { get; }

        [Group(0)]
        [Binding(1)]
        ITexture2D<Vector4> DiffuseTexture { get; }

        [Group(1)]
        [Binding(0)]
        [Uniform]
        Vector3D<float> LightDirection { get; }

        [Group(2)]
        [Binding(0)]
        [Uniform]
        Vector4 DiffuseColor { get; }

        T NormalData { get; }

        [Fragment]
        Vector4 LitPixel(VSToFS data)
        {
            Vector4 textureColor = DiffuseTexture.Sample(Sampler, data.TextureCoordiate);
            var normal = NormalData.GetNormal(data);
            var nDotL = MathF.Max(MathF.Min(Vector3D.Dot(normal, LightDirection), 1.0f), 0.0f);
            return textureColor * DiffuseColor * nDotL;
        }
    }

    [Fact]
    public void GenericBasedNormalMapSwitchTest()
    {
        var simpleLit = new LitFragmentShader<SimpleNormal>();
        var bumpMapLit = new LitFragmentShader<BumpMapNormal>();
    }
}

using DualDrill.Graphics;

namespace DualDrill.Engine.Services;

public interface ITexture
{
    int Width { get; }
    int Height { get; }
    int Depth { get; }
    GPUTextureFormat Format { get; }
    IGPUTexture GPUTexture { get; }
}

public sealed class HeadVolumeTexture : ITexture, IDisposable
{
    public static readonly string Name = "head-volume";
    public static readonly string Path = "/head256x256x109";

    public int Width => 256;

    public int Height => 256;

    public int Depth => 109;

    public GPUTextureFormat Format => GPUTextureFormat.R8Unorm;
    public IGPUTexture GPUTexture { get; }

    public HeadVolumeTexture(IGPUDevice device, ReadOnlyMemory<byte> data)
    {
        GPUTexture = device.CreateTexture(new()
        {
            Dimension = GPUTextureDimension._3D,
            Size = new()
            {
                Width = (uint)Width,
                Height = (uint)Height,
                DepthOrArrayLayers = (uint)Depth
            },
            Format = GPUTextureFormat.R8Unorm,
            Usage = GPUTextureUsage.TextureBinding | GPUTextureUsage.CopyDst,
            SampleCount = 1,
            MipLevelCount = 1,
        });
        device.Queue.WriteTexture(new()
        {
            Texture = GPUTexture
        },
        data.Span,
        new()
        {
            Offset = 0,
            BytesPerRow = (uint)Width,
            RowsPerImage = (uint)Height
        },
        new()
        {
            Width = (uint)Width,
            Height = (uint)Height,
            DepthOrArrayLayers = (uint)Depth
        });
    }

    public void Dispose()
    {
        GPUTexture.Dispose();
    }
}

public sealed class TextureService
{

    private string DataPath { get; }
    static readonly string DATA_ROOT_NAME = "DUALDRILL_DATA_ROOT";
    public ReadOnlyMemory<byte> LoadData(string path)
    {
        return File.ReadAllBytes(DataPath + path);
    }

    public TextureService()
    {
        var dataPath = System.Environment.GetEnvironmentVariable(DATA_ROOT_NAME);
        if (string.IsNullOrEmpty(dataPath))
        {
            throw new InvalidOperationException($"{DATA_ROOT_NAME} environment variable is not set");
        }
        DataPath = dataPath;

    }

    public ITexture GetTexture(IGPUDevice device, string name)
    {
        if (name == "head-volume")
        {
            return new HeadVolumeTexture(device, LoadData(HeadVolumeTexture.Path));
        }
        throw new KeyNotFoundException($"Texture with name ${name} not found");
    }
}

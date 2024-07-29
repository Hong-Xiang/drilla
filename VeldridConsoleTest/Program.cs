using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;
using Veldrid.StartupUtilities;

namespace VeldridConsoleTest;

struct VertexPositionColor
{
    public Vector2 Position; // This is the position, in normalized device coordinates.
    public RgbaFloat Color; // This is the color of the vertex.
    public VertexPositionColor(Vector2 position, RgbaFloat color)
    {
        Position = position;
        Color = color;
    }
    public const uint SizeInBytes = 24;
}

internal class Program
{
    private static GraphicsDevice _graphicsDevice;
    private static CommandList _commandList;
    private static DeviceBuffer _vertexBuffer;
    private static DeviceBuffer _indexBuffer;
    private static Shader[] _shaders;
    private static Pipeline _pipeline;
    private static DeviceBuffer _outputBuffer;
    private static Texture _outputTexture;

    private const string VertexCode = @"
#version 450

layout(location = 0) in vec2 Position;
layout(location = 1) in vec4 Color;

layout(location = 0) out vec4 fsin_Color;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_Color = Color;
}";

    private const string FragmentCode = @"
#version 450

layout(location = 0) in vec4 fsin_Color;
layout(location = 0) out vec4 fsout_Color;

void main()
{
    fsout_Color = fsin_Color;
}";

    private static readonly uint Width = 960;
    private static readonly uint Height = 540;

    static void Main(string[] args)
    {
        WindowCreateInfo windowCI = new WindowCreateInfo()
        {
            X = 100,
            Y = 100,
            WindowWidth = (int)Width,
            WindowHeight = (int)Height,
            WindowTitle = "Veldrid Tutorial"
        };
        Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);
        GraphicsDeviceOptions options = new GraphicsDeviceOptions
        {
            Debug = true,
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };
        _graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options);
        CreateResources();
        while (window.Exists)
        {
            window.PumpEvents();
            Draw();
        }
        DisposeResources();
    }

    private unsafe static void Draw()
    {
        _commandList.Begin();
        _commandList.SetFramebuffer(_graphicsDevice.SwapchainFramebuffer);
        _commandList.ClearColorTarget(0, RgbaFloat.Black);
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _commandList.SetPipeline(_pipeline);
        _commandList.DrawIndexed(
            indexCount: 4,
            instanceCount: 1,
            indexStart: 0,
            vertexOffset: 0,
            instanceStart: 0);
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
        _graphicsDevice.WaitForIdle();
        _commandList.Begin();
        _commandList.CopyTexture(_graphicsDevice.MainSwapchain.Framebuffer.ColorTargets[0].Target, _outputTexture);
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
        _graphicsDevice.SwapBuffers();
        var mappedResource = _graphicsDevice.Map(_outputTexture, MapMode.Read);
        //var mappedResource = _graphicsDevice.Map(_outputBuffer, MapMode.Read);
        var mappedSpan = new Span<byte>((void*)mappedResource.Data, (int)mappedResource.SizeInBytes);
        var maxValue = byte.MinValue;
        var minValue = byte.MaxValue;
        foreach (var d in mappedSpan)
        {
            maxValue = Math.Max(maxValue, d);
            minValue = Math.Min(maxValue, d);
        }
        _graphicsDevice.Unmap(_outputTexture);
        Console.WriteLine($"max {maxValue}, min {minValue}");
        Console.WriteLine(maxValue);
    }

    private static void CreateResources()
    {
        ResourceFactory factory = _graphicsDevice.ResourceFactory;
        VertexPositionColor[] quadVertices =
        {
    new VertexPositionColor(new Vector2(-0.75f, 0.75f), RgbaFloat.Red),
    new VertexPositionColor(new Vector2(0.75f, 0.75f), RgbaFloat.Green),
    new VertexPositionColor(new Vector2(-0.75f, -0.75f), RgbaFloat.Blue),
    new VertexPositionColor(new Vector2(0.75f, -0.75f), RgbaFloat.Yellow)
        };
        ushort[] quadIndices = { 0, 1, 2, 3 };
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(4 * VertexPositionColor.SizeInBytes, BufferUsage.VertexBuffer));
        _indexBuffer = factory.CreateBuffer(new BufferDescription(4 * sizeof(ushort), BufferUsage.IndexBuffer));
        _graphicsDevice.UpdateBuffer(_vertexBuffer, 0, quadVertices);
        _graphicsDevice.UpdateBuffer(_indexBuffer, 0, quadIndices);
        VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4));
        ShaderDescription vertexShaderDesc = new ShaderDescription(
        ShaderStages.Vertex,
        Encoding.UTF8.GetBytes(VertexCode),
        "main");
        ShaderDescription fragmentShaderDesc = new ShaderDescription(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(FragmentCode),
            "main");

        _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
        GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
        pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
        pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
            depthTestEnabled: true,
            depthWriteEnabled: true,
            comparisonKind: ComparisonKind.LessEqual);
        pipelineDescription.RasterizerState = new RasterizerStateDescription(
            cullMode: FaceCullMode.Back,
            fillMode: PolygonFillMode.Solid,
            frontFace: FrontFace.Clockwise,
            depthClipEnabled: true,
            scissorTestEnabled: false);
        pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        pipelineDescription.ResourceLayouts = System.Array.Empty<ResourceLayout>();
        pipelineDescription.ShaderSet = new ShaderSetDescription(
            vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
            shaders: _shaders);
        pipelineDescription.Outputs = _graphicsDevice.SwapchainFramebuffer.OutputDescription;
        _pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        _commandList = factory.CreateCommandList();
        _outputBuffer = factory.CreateBuffer(new BufferDescription
        {
            SizeInBytes = Width * Height * 32,
            Usage = BufferUsage.Staging
        });
        _outputTexture = factory.CreateTexture(
            TextureDescription.Texture2D(
                Width,
                Height,
                1, 1,
                _graphicsDevice.SwapchainFramebuffer.ColorTargets[0].Target.Format,
                TextureUsage.Staging 
            )
        );
    }

    private static void DisposeResources()
    {
        _pipeline.Dispose();
        //_vertexShader.Dispose();
        //_fragmentShader.Dispose();
        _commandList.Dispose();
        _outputBuffer.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _graphicsDevice.Dispose();
    }

}

using Silk.NET.Core.Native;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using SixLabors.ImageSharp;
using System.IO;

namespace DualDrill.Server;



public sealed unsafe class VulkanHeadlessService : IDisposable
{
    public const int WIDTH = 800;
    public const int HEIGHT = 600;

    const int MAX_FRAMES_IN_FLIGHT = 2;

    bool EnableValidationLayers = true;

    private readonly string[] validationLayers = new[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    private readonly string[] deviceExtensions = [];
    //new[]
    //{
    //    KhrSwapchain.ExtensionName
    //};

    private Vk? vk;

    private Graphics.GPUInstance Instance;

    private ExtDebugUtils? debugUtils;
    private DebugUtilsMessengerEXT debugMessenger;

    private PhysicalDevice physicalDevice;
    private Device device;

    private Queue graphicsQueue;

    private Extent2D TextureSize = new()
    {
        Width = WIDTH,
        Height = HEIGHT
    };

    private RenderPass renderPass;
    private PipelineLayout pipelineLayout;
    private Pipeline graphicsPipeline;

    private CommandPool commandPool;

    private CommandBuffer CommandBuffer;

    private Semaphore ImageAvailableSemaphore;
    private Semaphore RenderFinishedSemaphore;

    private Fence RenderingFence;

    private int currtFrame = 0;
    private Silk.NET.Vulkan.Image offscreenImage;
    private DeviceMemory offscreenImageMemory;
    private ImageView offscreenImageView;
    private Framebuffer offscreenFramebuffer;
    private Silk.NET.Vulkan.Buffer readbackBuffer;
    private DeviceMemory readbackBufferMemory;

    private Format Format = Format.R8G8B8A8Srgb;

    public VulkanHeadlessService()
    {
        //CreateInstance();
        Instance = new(Graphics.GPUInstanceDescriptor.Default with
        {
            ApplicationName = "Hello Triangle",
        });
        //SetupDebugMessenger();
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateReadbackBuffer();
        CreateOffscreenImage();
        CreateOffscreenImageView();
        CreateRenderPass();
        CreateGraphicsPipeline();
        CreateOffscreenFramebuffer();
        CreateCommandPool();
        CreateCommandBuffer();
        CreateSyncObjects();
    }

    byte[] ResultBuffer = new byte[4 * WIDTH * HEIGHT];

    public byte[] Render()
    {
        DrawFrame(0);
        vk!.DeviceWaitIdle(device);
        CopyToCPUBuffer(ResultBuffer);
        //var image = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(ResultBuffer, WIDTH, HEIGHT);
        //image.SaveAsPng("./output-vulkan-headless.png");
        //Console.WriteLine($"None Zero #{noneZeroCount} / {4 * WIDTH * HEIGHT}");
        return ResultBuffer;
    }

    private void CreateOffscreenImage()
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format,
            Extent = new Extent3D
            {
                Width = (uint)WIDTH,
                Height = (uint)HEIGHT,
                Depth = 1
            },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        if (vk.CreateImage(device, &imageInfo, null, out offscreenImage) != Result.Success)
        {
            throw new Exception("failed to create offscreen image!");
        }

        vk.GetImageMemoryRequirements(device, offscreenImage, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (vk.AllocateMemory(device, &allocInfo, null, out offscreenImageMemory) != Result.Success)
        {
            throw new Exception("failed to allocate offscreen image memory!");
        }

        vk.BindImageMemory(device, offscreenImage, offscreenImageMemory, 0);
    }
    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out var memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new Exception("failed to find suitable memory type!");
    }

    void CreateOffscreenImageView()
    {
        offscreenImageView = CreateImageView(offscreenImage);
    }
    void CreateOffscreenFramebuffer()
    {
        offscreenFramebuffer = CreateFramebuffer(offscreenImageView);
    }

    private void CreateReadbackBuffer()
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = (ulong)(WIDTH * HEIGHT * 4), // Size in bytes, adjust according to your image format
            Usage = BufferUsageFlags.TransferDstBit,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(device, &bufferInfo, null, out readbackBuffer) != Result.Success)
        {
            throw new Exception("failed to create readback buffer!");
        }

        vk.GetBufferMemoryRequirements(device, readbackBuffer, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit)
        };

        if (vk.AllocateMemory(device, &allocInfo, null, out readbackBufferMemory) != Result.Success)
        {
            throw new Exception("failed to allocate readback buffer memory!");
        }

        vk.BindBufferMemory(device, readbackBuffer, readbackBufferMemory, 0);
    }


    public void CopyToCPUBuffer(Span<byte> buffer)
    {
        void* data = null;
        vk!.MapMemory(device, readbackBufferMemory, 0, (ulong)buffer.Length, MemoryMapFlags.None, &data);
        var dataSpan = new Span<byte>(data, buffer.Length);
        dataSpan.CopyTo(buffer);
        vk.UnmapMemory(device, readbackBufferMemory);
    }

    private void CleanUp()
    {
        vk!.DestroySemaphore(device, RenderFinishedSemaphore, null);
        vk!.DestroySemaphore(device, ImageAvailableSemaphore, null);
        vk!.DestroyFence(device, RenderingFence, null);

        vk!.DestroyCommandPool(device, commandPool, null);

        vk!.DestroyFramebuffer(device, offscreenFramebuffer, null);

        vk!.DestroyPipeline(device, graphicsPipeline, null);
        vk!.DestroyPipelineLayout(device, pipelineLayout, null);
        vk!.DestroyRenderPass(device, renderPass, null);

        vk!.DestroyImageView(device, offscreenImageView, null);

        vk!.DestroyDevice(device, null);

        //if (EnableValidationLayers)
        //{
        //    //DestroyDebugUtilsMessenger equivilant to method DestroyDebugUtilsMessengerEXT from original tutorial.
        //    debugUtils!.DestroyDebugUtilsMessenger(Instance, debugMessenger, null);
        //}

        //vk!.DestroyInstance(Instance, null);
        Instance.Dispose();
        vk!.Dispose();
    }

    //private void CreateInstance()
    //{
    //    vk = Vk.GetApi();

    //    if (EnableValidationLayers && !CheckValidationLayerSupport())
    //    {
    //        throw new Exception("validation layers requested, but not available!");
    //    }

    //    ApplicationInfo appInfo = new()
    //    {
    //        SType = StructureType.ApplicationInfo,
    //        PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
    //        ApplicationVersion = new Version32(1, 0, 0),
    //        PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
    //        EngineVersion = new Version32(1, 0, 0),
    //        ApiVersion = Vk.Version12
    //    };

    //    InstanceCreateInfo createInfo = new()
    //    {
    //        SType = StructureType.InstanceCreateInfo,
    //        PApplicationInfo = &appInfo
    //    };

    //    var extensions = GetRequiredExtensions();
    //    createInfo.EnabledExtensionCount = (uint)extensions.Length;
    //    createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions); ;

    //    if (EnableValidationLayers)
    //    {
    //        createInfo.EnabledLayerCount = (uint)validationLayers.Length;
    //        createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);

    //        DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
    //        PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
    //        createInfo.PNext = &debugCreateInfo;
    //    }
    //    else
    //    {
    //        createInfo.EnabledLayerCount = 0;
    //        createInfo.PNext = null;
    //    }

    //    if (vk.CreateInstance(createInfo, null, out Instance) != Result.Success)
    //    {
    //        throw new Exception("failed to create instance!");
    //    }

    //    Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
    //    Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
    //    SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

    //    if (EnableValidationLayers)
    //    {
    //        SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    //    }
    //}

    private void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityWarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.DebugUtilsMessageSeverityErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeGeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypePerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.DebugUtilsMessageTypeValidationBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    //private void SetupDebugMessenger()
    //{
    //    if (!EnableValidationLayers) return;

    //    //TryGetInstanceExtension equivilant to method CreateDebugUtilsMessengerEXT from original tutorial.
    //    if (!vk!.TryGetInstanceExtension(Instance, out debugUtils)) return;

    //    DebugUtilsMessengerCreateInfoEXT createInfo = new();
    //    PopulateDebugMessengerCreateInfo(ref createInfo);

    //    if (debugUtils!.CreateDebugUtilsMessenger(Instance.NativeHandle, in createInfo, null, out debugMessenger) != Result.Success)
    //    {
    //        throw new Exception("failed to set up debug messenger!");
    //    }
    //}


    private void PickPhysicalDevice()
    {
        uint devicedCount = 0;
        vk!.EnumeratePhysicalDevices(Instance.NativeHandle, ref devicedCount, null);

        if (devicedCount == 0)
        {
            throw new Exception("failed to find GPUs with Vulkan support!");
        }

        var devices = new PhysicalDevice[devicedCount];
        fixed (PhysicalDevice* devicesPtr = devices)
        {
            vk!.EnumeratePhysicalDevices(Instance.NativeHandle, ref devicedCount, devicesPtr);
        }

        if (devices.Length == 1)
        {
            physicalDevice = devices[0];
        }

        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device))
            {
                physicalDevice = device;
                break;
            }
        }

        if (physicalDevice.Handle == 0)
        {
            throw new Exception("failed to find a suitable GPU!");
        }
    }

    private void CreateLogicalDevice()
    {
        var indices = FindQueueFamilies(physicalDevice);

        var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value };
        uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

        using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

        float queuePriority = 1.0f;
        for (int i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        PhysicalDeviceFeatures deviceFeatures = new();

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,

            PEnabledFeatures = &deviceFeatures,

            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions)
        };

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
        }

        if (vk!.CreateDevice(physicalDevice, in createInfo, null, out device) != Result.Success)
        {
            throw new Exception("failed to create logical device!");
        }

        vk!.GetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, out graphicsQueue);

        if (EnableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

    }

    private ImageView CreateImageView(Silk.NET.Vulkan.Image image)
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = Format,
            Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
            SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                }

        };

        ImageView imageView = default;
        var result = vk!.CreateImageView(device, in createInfo, null, &imageView);
        if (result != Result.Success)
        {
            throw new Exception("failed to create image views!");
        }
        return imageView;
    }

    private void CreateRenderPass()
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.TransferSrcOptimal,
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        if (vk!.CreateRenderPass(device, renderPassInfo, null, out renderPass) != Result.Success)
        {
            throw new Exception("failed to create render pass!");
        }
    }
    private void CreateGraphicsPipeline()
    {
        var vertShaderCode = File.ReadAllBytes("shaders/vert.spv");
        var fragShaderCode = File.ReadAllBytes("shaders/frag.spv");

        var vertShaderModule = CreateShaderModule(vertShaderCode);
        var fragShaderModule = CreateShaderModule(fragShaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        var shaderStages = stackalloc[]
        {
            vertShaderStageInfo,
            fragShaderStageInfo
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0,
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false,
        };

        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = TextureSize.Width,
            Height = TextureSize.Height,
            MinDepth = 0,
            MaxDepth = 1,
        };

        Rect2D scissor = new()
        {
            Offset = { X = 0, Y = 0 },
            Extent = TextureSize,
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor,
        };

        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false,
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
        };

        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false,
        };

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment,
        };

        colorBlending.BlendConstants[0] = 0;
        colorBlending.BlendConstants[1] = 0;
        colorBlending.BlendConstants[2] = 0;
        colorBlending.BlendConstants[3] = 0;

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PushConstantRangeCount = 0,
        };

        if (vk!.CreatePipelineLayout(device, pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
        {
            throw new Exception("failed to create pipeline layout!");
        }

        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = shaderStages,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterizer,
            PMultisampleState = &multisampling,
            PColorBlendState = &colorBlending,
            Layout = pipelineLayout,
            RenderPass = renderPass,
            Subpass = 0,
            BasePipelineHandle = default
        };

        if (vk!.CreateGraphicsPipelines(device, default, 1, in pipelineInfo, null, out graphicsPipeline) != Result.Success)
        {
            throw new Exception("failed to create graphics pipeline!");
        }


        vk!.DestroyShaderModule(device, fragShaderModule, null);
        vk!.DestroyShaderModule(device, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }

    private Framebuffer CreateFramebuffer(ImageView imageView)
    {
        FramebufferCreateInfo framebufferInfo = new()
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = renderPass,
            AttachmentCount = 1,
            PAttachments = &imageView,
            Width = TextureSize.Width,
            Height = TextureSize.Height,
            Layers = 1,
        };
        var result = vk!.CreateFramebuffer(device, in framebufferInfo, null, out var frameBuffer);
        if (result != Result.Success)
        {
            throw new Exception("failed to create framebuffer!");
        }
        return frameBuffer;
    }

    private void CreateCommandPool()
    {
        var queueFamiliyIndicies = FindQueueFamilies(physicalDevice);

        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamiliyIndicies.GraphicsFamily!.Value,
        };

        if (vk!.CreateCommandPool(device, poolInfo, null, out commandPool) != Result.Success)
        {
            throw new Exception("failed to create command pool!");
        }
    }

    private void CreateCommandBuffer()
    {
        var commandBuffer = new CommandBuffer();

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        if (vk!.AllocateCommandBuffers(device, in allocInfo, &commandBuffer) != Result.Success)
        {
            throw new Exception("failed to allocate command buffers!");
        }


        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
        };

        if (vk!.BeginCommandBuffer(commandBuffer, beginInfo) != Result.Success)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        ClearValue clearColor = new()
        {
            Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 },
        };

        RenderPassBeginInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = renderPass,
            Framebuffer = offscreenFramebuffer,
            RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = TextureSize,
                },
            ClearValueCount = 1,
            PClearValues = &clearColor
        };

        vk!.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);

        vk!.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, graphicsPipeline);

        vk!.CmdDraw(commandBuffer, 3, 1, 0, 0);

        vk!.CmdEndRenderPass(commandBuffer);
        Span<BufferImageCopy> bufferImageCopy =
        [
            new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlags.ColorBit, LayerCount = 1 },
                ImageExtent = new Silk.NET.Vulkan.Extent3D { Width = WIDTH, Height = HEIGHT, Depth = 1 }
            }
        ];
        vk.CmdCopyImageToBuffer(commandBuffer, offscreenImage, ImageLayout.TransferSrcOptimal, readbackBuffer, bufferImageCopy);

        if (vk!.EndCommandBuffer(commandBuffer) != Result.Success)
        {
            throw new Exception("failed to record command buffer!");
        }

        CommandBuffer = commandBuffer;
    }

    private void CreateSyncObjects()
    {
        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        if (vk!.CreateSemaphore(device, in semaphoreInfo, null, out ImageAvailableSemaphore) != Result.Success ||
            vk!.CreateSemaphore(device, in semaphoreInfo, null, out RenderFinishedSemaphore) != Result.Success ||
            vk!.CreateFence(device, in fenceInfo, null, out RenderingFence) != Result.Success)
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    private void DrawFrame(double delta)
    {
        vk!.WaitForFences(device, 1, in RenderingFence, true, ulong.MaxValue);
        vk!.ResetFences(device, 1, in RenderingFence);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
        };

        var waitSemaphores = stackalloc[] { ImageAvailableSemaphore };
        var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };

        var buffer = CommandBuffer;

        submitInfo = submitInfo with
        {
            WaitSemaphoreCount = 0,
            PWaitDstStageMask = waitStages,

            CommandBufferCount = 1,
            PCommandBuffers = &buffer
        };

        var signalSemaphores = stackalloc[] { RenderFinishedSemaphore };
        submitInfo = submitInfo with
        {
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores,
        };


        if (vk!.QueueSubmit(graphicsQueue, 1, in submitInfo, RenderingFence) != Result.Success)
        {
            throw new Exception("failed to submit draw command buffer!");
        }
    }

    private Silk.NET.Vulkan.ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length,
        };

        Silk.NET.Vulkan.ShaderModule shaderModule;

        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            if (vk!.CreateShaderModule(device, createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception();
            }
        }

        return shaderModule;

    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
    {
        foreach (var availableFormat in availableFormats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.ColorSpaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        var indices = FindQueueFamilies(device);

        bool extensionsSupported = CheckDeviceExtensionsSupport(device);

        bool swapChainAdequate = false;
        //if (extensionsSupported)
        //{
        //    var swapChainSupport = QuerySwapChainSupport(device);
        //    swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
        //}

        return indices.IsComplete() && extensionsSupported && swapChainAdequate;
    }

    private bool CheckDeviceExtensionsSupport(PhysicalDevice device)
    {
        uint extentionsCount = 0;
        vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, null);

        var availableExtensions = new ExtensionProperties[extentionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
        {
            vk!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, availableExtensionsPtr);
        }

        var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

        return deviceExtensions.All(availableExtensionNames.Contains);

    }

    private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
    {
        var indices = new QueueFamilyIndices();

        uint queueFamilityCount = 0;
        vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilityCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            vk!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
        }


        uint i = 0;
        foreach (var queueFamily in queueFamilies)
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                indices.GraphicsFamily = i;
                break;
            }

            //khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

            //if (presentSupport)
            //{
            //    indices.PresentFamily = i;
            //}

            //if (indices.IsComplete())
            //{
            //    break;
            //}

            i++;
        }

        return indices;
    }

    private string[] GetRequiredExtensions()
    {
        //var glfwExtensions = window!.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        //var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
        string[] extensions = [];
        foreach (var ext in extensions)
        {
            Console.WriteLine($"required extension {ext}");
        }

        if (EnableValidationLayers)
        {
            return extensions.Append(ExtDebugUtils.ExtensionName).ToArray();
        }

        return extensions;
    }

    private bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        vk!.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            vk!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

        return validationLayers.All(availableLayerNames.Contains);
    }

    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False;
    }

    public void Dispose()
    {
        CleanUp();
    }
}

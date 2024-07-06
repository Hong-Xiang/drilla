﻿using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPUSurface
{

    public unsafe GPUSurfaceTexture GetCurrentTexture()
    {
        WGPUSurfaceTexture texture;
        WGPU.wgpuSurfaceGetCurrentTexture(Handle, &texture);
        var result = new GPUSurfaceTexture
        {
            Texture = new GPUTexture(texture.texture),
            Status = (GPUSurfaceGetCurrentTextureStatus)texture.status,
            Suboptimal = texture.suboptimal != 0
        };
        if (result.Status != GPUSurfaceGetCurrentTextureStatus.Success)
        {
            throw new GraphicsApiException($"Failed to get current texture, status {result.Status}");
        }
        return result;
    }

    public unsafe void Configure(GPUSurfaceConfiguration configuration)
    {
        var viewFormatsPtr = stackalloc WGPUTextureFormat[configuration.ViewFormats.Length];
        for (var i = 0; i < configuration.ViewFormats.Length; i++)
        {
            viewFormatsPtr[i] = (WGPUTextureFormat)configuration.ViewFormats.Span[i];
        }
        var nativeConfig = new WGPUSurfaceConfiguration
        {
            device = configuration.Device.Handle,
            format = (WGPUTextureFormat)configuration.Format,
            usage = (uint)configuration.Usage,
            viewFormatCount = (nuint)configuration.ViewFormats.Length,
            viewFormats = configuration.ViewFormats.Length == 0 ? null : viewFormatsPtr,
            alphaMode = (WGPUCompositeAlphaMode)configuration.AlphaMode,
            width = (uint)configuration.Width,
            height = (uint)configuration.Height,
            presentMode = (WGPUPresentMode)configuration.PresentMode
        };
        WGPU.wgpuSurfaceConfigure(Handle, &nativeConfig);
    }

    public unsafe void Unconfigure()
    {
        WGPU.wgpuSurfaceUnconfigure(Handle);
    }
}

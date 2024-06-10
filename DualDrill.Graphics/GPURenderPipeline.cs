using DualDrill.Graphics.Native;
using DualDrill.Graphics.WebGPU.Native;
using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPURenderPipeline
{
    public static unsafe GPURenderPipeline Create(GPUDevice device, GPURenderPipelineDescriptor descriptor)
    {
        WGPURenderPipelineDescriptor nativeDescriptor = default;
        try
        {
            nativeDescriptor.vertex.module = descriptor.Vertex.Module.Handle;
            nativeDescriptor.vertex.entryPoint = (sbyte*)SilkMarshal.StringToPtr(descriptor.Vertex.EntryPoint);
            if (descriptor.Vertex.Constants.Length > 0)
            {
                throw new NotImplementedException("Constant Entry is not support yet");
            }

            using var vertexEntryPoint = NativeStringRef.Create(descriptor.Vertex.EntryPoint);
            using var fragmentEntryPoint =
                descriptor.Fragment.HasValue ? NativeStringRef.Create(descriptor.Fragment.Value.EntryPoint) : default;

            var vertexConstants = stackalloc WGPUConstantEntry[descriptor.Vertex.Constants.Length];
            WGPUConstantEntry* fragmentConstants = null;
            WGPUFragmentState fragment = new()
            {
                constantCount = (nuint)(descriptor.Fragment.HasValue ? descriptor.Fragment.Value.Constants.Length : 0),
                constants = fragmentConstants
            };
            if (descriptor.Fragment.HasValue)
            {
                fragment.module = descriptor.Fragment.Value.Module.Handle;
                fragment.entryPoint = (sbyte*)fragmentEntryPoint.Handle;
                fragment.targetCount = (uint)descriptor.Fragment.Value.Targets.Length;
                WGPUColorTargetState* targets = stackalloc WGPUColorTargetState[descriptor.Fragment.Value.Targets.Length];
                for (var i = 0; i < descriptor.Fragment.Value.Targets.Length; i++)
                {
                    var c = descriptor.Fragment.Value.Targets.Span[i];
                    if (c.Blend.HasValue)
                    {
                        throw new NotImplementedException();
                    }
                    targets[i] = new WGPUColorTargetState
                    {
                        blend = null,
                        writeMask = c.WriteMask,
                        format = (WGPUTextureFormat)c.Format
                    };
                }
                fragment.targets = targets;
            }

            var renderPipelineDescriptor = new WGPURenderPipelineDescriptor
            {
                vertex =
                {
                    module = descriptor.Vertex.Module.Handle,
                    entryPoint = (sbyte*)vertexEntryPoint.Handle,
                    constantCount = (nuint) descriptor.Vertex.Constants.Length,
                },
                primitive =
                {
                    topology = (WGPUPrimitiveTopology)descriptor.Primitive.Topology,
                    //StripIndexFormat = IndexFormat.Undefined,
                    //FrontFace = FrontFace.Ccw,
                    //CullMode = CullMode.None
                },
                multisample = new WGPUMultisampleState
                {
                    count = 1,
                    mask = ~0u,
                    //AlphaToCoverageEnabled = false
                },
                fragment = descriptor.Fragment.HasValue ? &fragment : null,
                //DepthStencil = null,
                //layout = PipelineLayout
            };

            var handle = WGPU.wgpuDeviceCreateRenderPipeline(device.Handle, &renderPipelineDescriptor);
            return new(handle);
        }
        finally
        {
            if (nativeDescriptor.vertex.entryPoint != null)
            {
                SilkMarshal.Free((nint)nativeDescriptor.vertex.entryPoint);
            }

            if (nativeDescriptor.fragment != null && nativeDescriptor.fragment->entryPoint != null)
            {
                SilkMarshal.Free((nint)nativeDescriptor.fragment->entryPoint);
            }
        }
    }
}

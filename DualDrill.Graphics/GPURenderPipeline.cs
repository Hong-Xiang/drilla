using DualDrill.Graphics.Interop;
using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Graphics;

public sealed partial class GPURenderPipeline
{

    public unsafe GPUBindGroupLayout GetBindGroupLayout(int index)
    {
        return new GPUBindGroupLayout(WGPU.RenderPipelineGetBindGroupLayout(Handle, (uint)index));
    }

    public static unsafe GPURenderPipeline Create(GPUDevice device, GPURenderPipelineDescriptor descriptor)
    {
        using var vertexEntryPoint = descriptor.Vertex.EntryPoint.Pin();
        WGPURenderPipelineDescriptor nativeDescriptor = default;
        try
        {
            nativeDescriptor.vertex.module = descriptor.Vertex.Module.Handle;
            nativeDescriptor.vertex.entryPoint = vertexEntryPoint.Pointer;
            if (descriptor.Vertex.Constants.Length > 0)
            {
                throw new NotImplementedException("Constant Entry is not support yet");
            }

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
                        format = c.Format
                    };
                }
                fragment.targets = targets;
            }


            var renderPipelineDescriptor = new WGPURenderPipelineDescriptor
            {
                vertex =
                {
                    module = descriptor.Vertex.Module.Handle,
                    entryPoint = vertexEntryPoint.Pointer,
                    constantCount = (nuint) descriptor.Vertex.Constants.Length,
                    bufferCount = (nuint)descriptor.Vertex.Buffers.Length,
                },
                primitive =
                {
                    topology = descriptor.Primitive.Topology,
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
                //layout = descriptor.Layout.Handle,
                layout = null
            };

            var vertexBuffer = stackalloc WGPUVertexBufferLayout[descriptor.Vertex.Buffers.Length];
            var attributesTotalCount = 0;
            {
                var index = 0;
                foreach (var buffer in descriptor.Vertex.Buffers.Span)
                {
                    vertexBuffer[index] = new WGPUVertexBufferLayout
                    {
                        arrayStride = buffer.ArrayStride,
                        attributeCount = (nuint)buffer.Attributes.Length,
                    };
                    attributesTotalCount += buffer.Attributes.Length;
                    index++;
                }
            }
            using var disposables = new CompositeDisposable();
            //var attributes = stackalloc GPUVertexAttribute[attributesTotalCount];
            {
                var bufferIndex = 0;
                //var attributeIndex = 0;
                foreach (var buffer in descriptor.Vertex.Buffers.Span)
                {
                    var pin = buffer.Attributes.Pin();
                    disposables.Add(pin);
                    vertexBuffer[bufferIndex].attributes = (GPUVertexAttribute*)pin.Pointer;

                    //foreach (var attribute in buffer.Attributes.Span)
                    //{
                    //    attributes[attributeIndex] = attribute;
                    //    Console.WriteLine(attributes[attributeIndex].Format);
                    //    Console.WriteLine(attribute.Format);
                    //    attributeIndex++;
                    //}

                    bufferIndex++;
                }
            }
            renderPipelineDescriptor.vertex.buffers = descriptor.Vertex.Buffers.Length > 0 ? vertexBuffer : null;

            var handle = WGPU.DeviceCreateRenderPipeline(device.Handle, &renderPipelineDescriptor);
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

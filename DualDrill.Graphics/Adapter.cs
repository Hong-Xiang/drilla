using DualDrill.Graphics.Interop;
using Silk.NET.WebGPU;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DualDrill.Graphics;

public sealed partial class GPUAdapter : IDisposable
{

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void RequestDeviceCallback(GPURequestDeviceStatus status, WGPUDeviceImpl* device, sbyte* message, void* data)
    {
        RequestCallback<WGPUNativeApiInterop, WGPUDeviceImpl, GPURequestDeviceStatus>.Callback(status, device, message, data);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static unsafe void DeviceUncapturedErrorCallback(GPUErrorType errorType, sbyte* message, void* data)
    {
        Console.Error.WriteLine($"Device uncaptured error type = {Enum.GetName(errorType)}, message {Marshal.PtrToStringUTF8((nint)message)}");
    }


    public unsafe GPUDevice RequestDevice()
    {
        WGPUDeviceDescriptor descriptor = new();
        var result = new RequestCallbackResult<WGPUDeviceImpl, GPURequestDeviceStatus>();
        WGPU.AdapterRequestDevice(
            Handle,
            &descriptor,
            &RequestDeviceCallback,
            &result
        );
        if (result.Handle is null)
        {
            throw new GraphicsApiException($"Request {nameof(GPUDevice)} failed, status {result.Status}, message {Marshal.PtrToStringUTF8((nint)result.Message)}");
        }
        WGPU.DeviceSetUncapturedErrorCallback(result.Handle, &DeviceUncapturedErrorCallback, null);
        return new GPUDevice(result.Handle);
    }

    public override string? ToString()
    {
        return $"{nameof(GPUAdapter)}{Handle}";
    }
}

public unsafe sealed class Adapter(Silk.NET.WebGPU.WebGPU Api, Silk.NET.WebGPU.Adapter* Handle)
{
    public Silk.NET.WebGPU.Adapter* Handle { get; } = Handle;



    public void PrintInfo()
    {
        Console.WriteLine($"Adapter@{(nuint)Handle:X}");
        var result = new AdapterProperties();
        Api.AdapterGetProperties(Handle, ref result);
        Console.WriteLine($"Adapter Name {Marshal.PtrToStringUTF8((nint)result.Name)}");
        Console.WriteLine($"Adapter VendorName {Marshal.PtrToStringUTF8((nint)result.VendorName)}");
        Console.WriteLine($"Adapter BackendType {Enum.GetName(result.BackendType)}");
        Console.WriteLine($"Adapter AdapterType {Enum.GetName(result.AdapterType)}");
    }

    public void PrintFeatures()
    {
        var count = (int)Api.AdapterEnumerateFeatures(Handle, null);

        var features = stackalloc FeatureName[count];

        Api.AdapterEnumerateFeatures(Handle, features);

        Console.WriteLine("Adapter features:");

        for (var i = 0; i < count; i++)
        {
            Console.WriteLine($"\t{features[i]}");
        }
    }

    struct RequestDeviceResult
    {
        public Silk.NET.WebGPU.Device* Device;
    }

    public Device RequestDevice(in DeviceDescriptor descriptor)
    {
        var result = new RequestDeviceResult();
        Api.AdapterRequestDevice(
            Handle,
            in descriptor,
            new PfnRequestDeviceCallback((status, device, message, data) =>
                      {
                          var resultData = (RequestDeviceResult*)data;
                          if (status == RequestDeviceStatus.Success && resultData->Device is null)
                          {
                              resultData->Device = device;
                          }
                          else
                          {
                              Console.Error.WriteLine(Marshal.PtrToStringUTF8((nint)message));
                          }
                      }),
                      ref result);
        Api.DeviceSetUncapturedErrorCallback(result.Device, new PfnErrorCallback(UncapturedError), null);
        if (result.Device is null)
        {
            throw new NullReferenceException("Failed to create device");
        }
        return new(Api, result.Device);
    }

    private static void UncapturedError(ErrorType arg0, byte* arg1, void* arg2)
    {
        Console.WriteLine($"{arg0}: {Marshal.PtrToStringUTF8((nint)arg1)}");
    }


}

using DualDrill.ApiGen;
using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.WebIDL;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ApiGenController(
    HttpClient HttpClient
) : ControllerBase
{
    [HttpGet("webgpu/spec")]
    public async Task<IActionResult> GetWebGPUHandles(CancellationToken cancellation)
    {
        var api = await GetGPUApiSpecAsync(cancellation);
        return Ok(api);
    }

    [HttpGet("webgpu/handles/name")]
    public async Task<IActionResult> GetWebGPUHandleNamesAsync(CancellationToken cancellation)
    {
        var api = await GetGPUApiSpecAsync(cancellation);
        return Ok(api.Handles.Select(h => h.Name));
    }

    [HttpGet("webgpu/codegen/backend")]
    public async Task<IActionResult> BackendCodeGenAsync([FromQuery] string? kind, CancellationToken cancellation)
    {
        var spec = await GetGPUApiSpecAsync(cancellation);
        var generator = new GPUBackendCodeGen(spec);
        var sb = new StringBuilder();
        switch (kind)
        {
            case nameof(GPUBackendCodeGen.GPUHandleDisposer):
                generator.GPUHandleDisposer(sb);
                break;
            case nameof(GPUBackendCodeGen.DisposeImpl):
                generator.DisposeImpl(sb);
                break;
            default:
                generator.GenerateAll(sb);
                break;
        }

        return Ok(sb.ToString());
    }


    [HttpGet("webgpu/handles/codegen")]
    public async Task<IActionResult> HandlesCodeGen([FromQuery] string? kind, CancellationToken cancellation)
    {
        var spec = await GetGPUApiSpecAsync(cancellation);
        var generator = new GPUBackendCodeGen(spec);
        var handlesCodeGen = new GPUHandlesCodeGen(spec);
        var sb = new StringBuilder();
        switch (kind)
        {
            case nameof(GPUBackendCodeGen.GPUHandleDisposer):
                generator.GPUHandleDisposer(sb);
                break;
            case nameof(GPUBackendCodeGen.DisposeImpl):
                generator.DisposeImpl(sb);
                break;
            case nameof(GPUHandlesCodeGen.HandleDecl):
                handlesCodeGen.HandleDecl(sb);
                break;
            default:
                generator.GenerateAll(sb);
                break;
        }

        return Ok(sb.ToString());
    }


    [HttpGet("webgpu/webidl")]
    public async Task<IActionResult> ParseRawNodes(CancellationToken cancellation)
    {
        var root = await GetWebGPUIDLSpecAsync(cancellation);
        var spec = WebIDLSpec.Parse(root);
        return Ok(spec);
    }
    private async ValueTask<JsonDocument> GetWebGPUIDLSpecAsync(CancellationToken cancellation)
    {
        var uri = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/spec/raw-nodes.json";
        var content = await HttpClient.GetStringAsync(uri, cancellation);
        return JsonDocument.Parse(content);
    }
    private async ValueTask<GPUApi> GetGPUApiSpecAsync(CancellationToken cancellation)
    {
        //var uri = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/spec/gpu.json";

        //await HttpClient.GetFromJsonAsync<GPUApi>(uri, cancellation);

        return GPUApi.ParseIDLSpec(await GetWebGPUIDLSpecAsync(cancellation));
    }
}

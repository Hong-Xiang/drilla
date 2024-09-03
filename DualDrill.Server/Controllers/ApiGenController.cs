using DualDrill.ApiGen;
using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.WebIDL;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using Vortice.Direct3D11;

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

    [HttpGet("webgpu/spec/handle/name")]
    public async Task<IActionResult> GetWebGPUHandleNamesAsync(CancellationToken cancellation)
    {
        var api = await GetGPUApiSpecAsync(cancellation);
        return Ok(api.Handles.Select(h => h.Name));
    }



    [HttpGet("webgpu/codegen/backend")]
    public async Task<IActionResult> GenerateBackendCodeAsync([FromQuery] string? part, CancellationToken cancellation)
    {
        var spec = await GetGPUApiSpecAsync(cancellation);
        var generator = new GPUBackendCodeGen(spec);
        var sb = new StringBuilder();
        switch (part)
        {
            case nameof(GPUBackendCodeGen.EmitIGPUHandleDisposer):
                generator.EmitIGPUHandleDisposer(sb);
                break;
            default:
                generator.EmitAll(sb);
                break;
        }

        return Ok(sb.ToString());
    }



    [HttpGet("webgpu/codegen/handle")]
    public async Task<IActionResult> GenerateAllGPUHandleCodeAsync(CancellationToken cancellation)
    {
        var spec = await GetGPUApiSpecAsync(cancellation);
        var generator = new GPUHandlesCodeGen(spec);
        var sb = new StringBuilder();
        foreach (var h in spec.Handles)
        {
            generator.EmitHandleDeclaration(sb, h);
        }
        return Ok(sb.ToString());
    }

    [HttpGet("webgpu/codegen/handle/{name}")]
    public async Task<IActionResult> GenerateGPUHandleCodeAsync(string name, CancellationToken cancellation)
    {
        var spec = await GetGPUApiSpecAsync(cancellation);
        var generator = new GPUHandlesCodeGen(spec);
        var sb = new StringBuilder();
        var h = spec.Handles.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        if (h is null)
        {
            return NotFound();
        }
        generator.EmitHandleDeclaration(sb, h);
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

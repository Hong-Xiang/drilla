using DualDrill.ApiGen;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Silk.NET.Vulkan;

namespace DualDrill.Server.WebApi;

[Route("api/[controller]")]
[ApiController]
public class ApiBindingController : ControllerBase
{

    static string ReadWebGPUXmlContent()
    {
        return System.IO.File.ReadAllText("C:\\Users\\Xiang\\Downloads\\wgpu-windows-x86_64-release\\webgpu.xml");
    }


    [HttpGet("webgpu")]
    public IResult GetWebGPUApiSpec()
    {
        var builder = new WebGPUApiSpecBuilder(ReadWebGPUXmlContent());
        var spec = builder.Build();
        Console.WriteLine(spec.Types.Length);
        var count = 0;
        foreach (var e in spec.Types)
        {
            if (e is ApiEnumType ee)
            {
                foreach (var ev in ee.Values)
                {
                    if (!char.IsLetter(ev.Name.First()))
                    {
                        count++;
                        Console.WriteLine($"{e.Name}.{ev.Name}");
                    }
                }
            }

        }
        Console.WriteLine($"Invalid count {count}");
        return Results.Ok(spec);

    }

    [HttpPost("webgpu/csharp")]
    public IResult GenerateCSharpCode([FromBody] WebGPUApiSpec spec)
    {
        var builder = new GraphicsCSharpApiSourceCodeBuilder();
        var code = builder.BuildEnums(spec);
        return Results.Text(code);
    }
}

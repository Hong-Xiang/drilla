using DualDrill.Engine.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DataController(TextureService TextureService) : ControllerBase
{
    [HttpGet("head")]
    public IActionResult GetHeadVolumeDataAsync()
    {
        return File(TextureService.LoadData(HeadVolumeTexture.Path).ToArray(), "application/octet-stream");
    }
}

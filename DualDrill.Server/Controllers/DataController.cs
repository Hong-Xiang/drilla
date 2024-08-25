using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DataController : ControllerBase
{
    static readonly string DATA_ROOT_NAME = "DUALDRILL_DATA_ROOT";
    private string DataPath { get; }
    public DataController() : base()
    {
        var dataPath = System.Environment.GetEnvironmentVariable(DATA_ROOT_NAME);
        if (string.IsNullOrEmpty(dataPath))
        {
            throw new InvalidOperationException($"{DATA_ROOT_NAME} environment variable is not set");
        }
        DataPath = dataPath;
    }

    [HttpGet("head")]
    public IActionResult GetHeadVolumeDataAsync()
    {
        var data = System.IO.File.ReadAllBytes(DataPath + "/head256x256x109");
        return File(data, "application/octet-stream");
    }
}

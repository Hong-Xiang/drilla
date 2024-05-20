using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.WebApi
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShaderCController : ControllerBase
    {
        [HttpGet]
        public byte[] Compile(string code)
        {
            return [];
        }
    }
}

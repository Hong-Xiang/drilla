using DualDrill.Engine.Services;
using Microsoft.AspNetCore.Mvc;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MeshController(MeshService MeshService) : ControllerBase
{
    [HttpGet("{name}/meta")]
    public ActionResult GetMeshMetaData(string name)
    {
        var mesh = MeshService.GetMesh(name);
        if (mesh is null)
        {
            return NotFound();
        }
        return Ok(new
        {
            Name = name,
            mesh.BufferLayout,
            mesh.IndexCount,
            mesh.IndexFormat
        });
    }


    [HttpGet("{name}/vertex")]
    public ActionResult GetMeshVertexData(string name)
    {
        var mesh = MeshService.GetMesh(name);
        if (mesh is null)
        {
            return NotFound();
        }
        return File(mesh.VertexData.ToArray(), "application/octet-stream");
    }

    [HttpGet("{name}/index")]
    public ActionResult GetMeshIndexData(string name)
    {
        var mesh = MeshService.GetMesh(name);
        if (mesh is null)
        {
            return NotFound();
        }
        return File(mesh.IndexData.ToArray(), "application/octet-stream");
    }
}

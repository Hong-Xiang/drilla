﻿using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DualDrill.Server.Controllers;

[Route("/ilsl")]
public class ILSLController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("expected")]
    public IActionResult ExpectedCode()
    {
        return Ok(Engine.Shader.MinimumTriangleExpectedCode.Code);
    }


    [HttpGet("generated")]
    public IActionResult GeneratedCode()
    {
        var code = ILSL.ILSLCompiler.Compile<Engine.Shader.MinimumTriangle>();
        return Ok(code);
    }

    [HttpGet("ast")]
    public IActionResult GeneratedAst()
    {
        return Ok(ILSL.ILSLCompiler.ASTToJson<Engine.Shader.MinimumTriangle>());
    }
}

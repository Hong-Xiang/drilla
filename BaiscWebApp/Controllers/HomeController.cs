using BaiscWebApp.Models;
using BaiscWebApp.WGSLGen;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace BaiscWebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        var compiler = new ILSLCompiler();
        var testType = typeof(WGSLGen.Data.BasicShader.ShaderModule);
        var decompiler = new CSharpDecompiler(testType.Assembly.Location, new DecompilerSettings()
        {
            UsingDeclarations = false,
        });
        var name = new FullTypeName(testType.FullName);
        var ast = decompiler.DecompileType(name);
        var writer = new StringWriter();
        ast.AcceptVisitor(new SimpleWGSLOutputVisitor(writer));
        var expectedCode = BaiscWebApp.WGSLGen.Data.BasicShader.ExpectedResult.Code;
        ViewData["ExpectedCode"] = expectedCode;
        ViewData["GeneratedCode"] = writer.ToString();
        var astDumper = new AstToJsonDumper();
        astDumper.Dump("", ast);
        ViewData["AstJson"] = astDumper.Writer.ToString();
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using DualDrill.CLSL;
using DualDrill.CLSL.Test.ShaderModule;
using DualDrill.Compiler.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5083");

var app = builder.Build();
var slangService = new SlangService();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/ilsl/compile/{name}/{target}", Compile);
app.MapGet("/ilsl/reflect/{name}", ReflectAsync);

await app.RunAsync();

static ISharpShader? GetShader(string name) =>
    name switch
    {
        nameof(MinimumTriangleShader) => new MinimumTriangleShader(),
        nameof(SimpleStructUniformShaderModule) => new SimpleStructUniformShaderModule(),
        nameof(MandelbrotDistanceShaderModule) => new MandelbrotDistanceShaderModule(),
        nameof(RaymarchingPrimitiveShader) => new RaymarchingPrimitiveShader(),
        _ => null
    };

static IResult Compile(string name, string target)
{
    var shader = GetShader(name);
    if (shader is null)
    {
        return Results.NotFound();
    }

    var compileTarget = target.ToLowerInvariant() switch
    {
        "ir" => CLSLCompileTarget.IR,
        "slang" => CLSLCompileTarget.SLang,
        "wgsl" => CLSLCompileTarget.WGSL,
        _ => (CLSLCompileTarget?)null
    };
    if (compileTarget is null)
    {
        return Results.BadRequest($"Unsupported compile target '{target}'. Expected IR, SLang, or WGSL.");
    }

    try
    {
        var compiler = new CLSLCompiler(new CLSLCompileOption(compileTarget.Value));
        return Results.Text(compiler.Emit(shader), "text/plain");
    }
    catch (Exception exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Shader compilation failed");
    }
}

async Task<IResult> ReflectAsync(string name, CancellationToken cancellationToken)
{
    var shader = GetShader(name);
    if (shader is null)
    {
        return Results.NotFound();
    }

    try
    {
        var compiler = new CLSLCompiler(new CLSLCompileOption(CLSLCompileTarget.SLang));
        var reflection = await slangService.ReflectAsync(compiler.Emit(shader), cancellationToken);
        return Results.Text(reflection, "application/json");
    }
    catch (Exception exception)
    {
        return Results.Problem(
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Shader reflection failed");
    }
}

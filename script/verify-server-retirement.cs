#:property TreatWarningsAsErrors=true
#:property TargetFramework=net10.0
#:package SixLabors.ImageSharp@3.1.11

using System.Net;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length != 1 ||
    !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? server) ||
    server.Scheme is not ("http" or "https"))
{
    Console.Error.WriteLine(
        "Usage: dotnet run -p:ImportDirectoryPackagesProps=false " +
        "script/verify-server-retirement.cs -- <server-url>");
    return 2;
}

using var http = new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(30) };
(string Path, Func<byte[], bool> Check)[] retained =
[
    ("/health", body => Text(body).Equals("Healthy", StringComparison.Ordinal)),
    ("/", body => Text(body).Contains("Welcome", StringComparison.Ordinal)),
    ("/api/Mesh/Cube/meta", CubeHasIndices),
    ("/api/Mesh/Cube/vertex", body => body.Length > 0),
    ("/api/ApiGen/webgpu/native/enum/name", HasItems),
    ("/api/DotnetReflection/entry-type", HasItems),
    ("/ILSL/compile/MinimumTriangle/wgsl", body => Text(body).Contains("@fragment", StringComparison.Ordinal)),
    ("/ILSL", body => Text(body).Contains("ILSL Development", StringComparison.Ordinal) &&
        Text(body).Contains("/js/dist/client.css", StringComparison.Ordinal)),
    ("/js/dist/client.css", body => Text(body).Contains(".monaco-editor", StringComparison.Ordinal)),
    ("/render/repl", body => Text(body).Contains("REPL-Style Renderer", StringComparison.Ordinal)),
    ("/home/volume", body => Text(body).Contains("Volume Rendering", StringComparison.Ordinal)),
    ("/swagger/v1/swagger.json", body => Text(body).Contains("openapi", StringComparison.Ordinal)),
];
foreach (var (path, check) in retained)
{
    using var response = await http.GetAsync(path);
    byte[] body = await response.Content.ReadAsByteArrayAsync();
    Require(response.StatusCode == HttpStatusCode.OK && check(body), $"{path}: invalid response ({response.StatusCode})");
    Console.WriteLine($"{path}: 200");
}

const string pngPath = "/render/cube?width=64&height=48";
using var pngResponse = await http.GetAsync(pngPath);
Require(pngResponse.StatusCode == HttpStatusCode.OK, $"{pngPath}: expected 200, got {pngResponse.StatusCode}");
byte[] png = await pngResponse.Content.ReadAsByteArrayAsync();
Require(Image.DetectFormat(png).Name == "PNG", "Renderer did not return PNG data.");
using (Image<Rgba32> image = Image.Load<Rgba32>(png))
{
    Require(image.Width == 64 && image.Height == 48, "Unexpected PNG dimensions.");
    Require(HasDistinctPixels(image), "GPU produced an empty/uniform frame.");
}
Console.WriteLine($"{pngPath}: 200, decoded 64x48 non-uniform PNG");

string[] retired =
[
    "/home/desktop",
    "/home/webview2",
    "/api/SignalConnection/server",
    "/api/ServerConnection/client",
    "/api/WebViewInterop",
    "/hub/signal-connection",
    "/ws/signal-connection/00000000-0000-0000-0000-000000000000",
    "/js/browserclient-interop.js",
];
foreach (string path in retired)
{
    using var response = await http.GetAsync(path);
    Require(response.StatusCode == HttpStatusCode.NotFound, $"{path}: expected 404, got {response.StatusCode}");
    Console.WriteLine($"{path}: 404");
}
return 0;

static string Text(byte[] body) => Encoding.UTF8.GetString(body);

static bool CubeHasIndices(byte[] body)
{
    using var json = JsonDocument.Parse(body);
    return json.RootElement.GetProperty("indexCount").GetInt32() == 36;
}

static bool HasItems(byte[] body)
{
    using var json = JsonDocument.Parse(body);
    return json.RootElement.GetArrayLength() > 0;
}

static bool HasDistinctPixels(Image<Rgba32> image)
{
    Rgba32 first = image[0, 0];
    for (int y = 0; y < image.Height; y++)
    {
        for (int x = 0; x < image.Width; x++)
        {
            if (image[x, y] != first)
            {
                return true;
            }
        }
    }
    return false;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

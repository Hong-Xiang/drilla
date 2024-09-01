using AngleSharp;
using DualDrill.ApiGen;
using DualDrill.ApiGen.Mini;
using DualDrill.ApiGen.WebIDL;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DualDrill.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ApiGenController(
    HttpClient HttpClient
) : ControllerBase
{
    [HttpGet("webgpu/handles")]
    public async Task<IActionResult> GetWebGPUHandles(CancellationToken cancellation)
    {
        //var spec = await GetGPUApiFromSpecFileAsync(cancellation);
        var api = GPUApi.ParseIDLSpec(await GetWebGPUIDLSpecAsync(cancellation));
        return Ok(api);
    }

    [HttpGet("webgpu/handles/codegen")]
    public async Task<IActionResult> HandlesCodeGen([FromQuery] string kind, CancellationToken cancellation)
    {
        var spec = await GetGPUApiFromSpecFileAsync(cancellation);
        if (kind == "GPUHandleDisposer")
        {
            return Ok(string.Join("\n", spec.Handles.Select(h => $", IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>")));
        }

        if (kind == "DisposeImpl")
        {
            var codes = spec.Handles.Select(h =>
            {

                return $"    void IGPUHandleDisposer<Backend, {h.Name}<Backend>>.DisposeHandle(GPUHandle<Backend, {h.Name}<Backend>> handle)\r\n    {{\r\n        wgpu{h.Name[3..]}Release(handle.ToNative());\r\n    }}";
            });
            return Ok(string.Join("\n", codes));
        }

        if (kind == "ResourceDecl")
        {
            var codes = spec.Handles.Select(h =>
            {
                return $"public sealed partial record class {h.Name}<TBackend>(GPUHandle<TBackend, {h.Name}<TBackend>> Handle)\r\n    : IDisposable, IGPUInstance\r\n    where TBackend : IBackend<TBackend>\r\n{{\r\n    public void Dispose()\r\n    {{\r\n        TBackend.Instance.DisposeHandle(Handle);\r\n    }} }}";
            });

            return Ok(string.Join("\n", codes));
        }

        return NotFound();
    }

    [HttpGet("modify")]
    public async Task<IActionResult> ModifyApiSpec()
    {
        var uri = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/spec/gpu.json";
        var data = await HttpClient.GetStringAsync(uri);
        var jsonObj = JsonObject.Parse(data);
        var result = jsonObj["handles"].AsArray();
        var pt = from h in jsonObj["handles"].AsArray()
                 from m in h["methods"].AsArray()
                 from p in m["parameters"].AsArray()
                 where p["type"] is JsonValue v && v.GetValueKind() == JsonValueKind.String
                 select p["type"];
        foreach (var n in pt)
        {
            var arr = new JsonArray();
            arr.Add(n.GetValue<string>());
            n.ReplaceWith(arr);
        }
        return Ok(result);
    }

    sealed record class LinkItem(string Name, string Link, string Kind)
    {
    }

    [HttpGet("fetch")]
    public async Task<IActionResult> FetchTestAsync()
    {
        var dataDir = "C:\\Users\\Xiang\\Documents\\webgpu-webidl-test\\idl-dump-rocks";
        var dataJson = System.IO.File.ReadAllText(dataDir + "\\allLinks.json");
        var items = JsonSerializer.Deserialize<LinkItem[]>(dataJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        List<string> fetched = [];
        foreach (var item in items)
        {
            var target = dataDir + $"\\{item.Name}.html";
            var content = await HttpClient.GetStringAsync(item.Link);
            if (Path.Exists(target))
            {
                continue;
            }
            System.IO.File.WriteAllText(target, content);
            fetched.Add(item.Name);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        return Ok(fetched);
    }

    [HttpGet("webgpu/webidl")]
    public async Task<IActionResult> ParseRawNodes(CancellationToken cancellation)
    {
        var root = await GetWebGPUIDLSpecAsync(cancellation);
        var spec = WebIDLSpec.Parse(root);
        return Ok(spec);
    }

    [HttpGet("parse")]
    public async Task<IActionResult> ParseDocPage([FromQuery] string name)
    {
        var dataDir = "C:\\Users\\Xiang\\Documents\\webgpu-webidl-test\\idl-dump-rocks";
        var config = AngleSharp.Configuration.Default;
        var context = BrowsingContext.New(config);
        var source = System.IO.File.ReadAllText(dataDir + $"\\{name}.html");

        var document = await context.OpenAsync(req => req.Content(source));
        var defRoot = document.QuerySelector(".idl-block > .idl-interface");
        List<Property> properties = [];
        Console.WriteLine(defRoot.OuterHtml);
        Console.WriteLine(new string('-', 20));
        Console.WriteLine(defRoot.InnerHtml);
        Console.WriteLine(new string('=', 20));
        foreach (var n in defRoot.QuerySelectorAll(".idl-member.idl-attribute"))
        {
            Console.WriteLine(n.InnerHtml);
            properties.Add(ParseProperty(n));
        }
        var methods = defRoot.QuerySelectorAll(".idl-member.idl-operation").ToArray();

        Console.WriteLine(methods.Count());
        Console.WriteLine(properties.Count());

        Property ParseProperty(AngleSharp.Dom.IElement element)
        {
            var types = element.QuerySelectorAll(".idl-type");
            //var typeRef = types.Length == 1
            //    ? new TypeRef(types[0].TextContent, [])
            //    : new TypeRef(types.Last().TextContent, types.Take(types.Length - 1).Select(n => n.TextContent).ToImmutableArray());
            throw new NotImplementedException();

            //var result = new GPUHandleProperty(
            //    element.QuerySelector(".idl-name").TextContent,
            //    typeRef
            //);
            //return result;
        }

        Method ParseMethod(AngleSharp.Dom.IElement element)
        {
            var returnTypeElements = element.ChildNodes.SkipWhile(
                n => !(n.NodeType == AngleSharp.Dom.NodeType.Text
                       && n.TextContent.Contains("):")
                      )
            ).Where(n => n is AngleSharp.Dom.IElement e && e.ClassName.Contains("idl-type"))
            .ToArray();

            IEnumerable<(string?, AngleSharp.Dom.IElement[])> SplitElements()
            {
                string? name = null;
                List<AngleSharp.Dom.IElement> current = [];
                foreach (var c in element.ChildNodes)
                {
                    if (c.NodeType == AngleSharp.Dom.NodeType.Text && c.TextContent.Contains("("))
                    {
                        current.Clear();
                        continue;
                    }
                    if (c is AngleSharp.Dom.IElement ev && ev.ClassList.Contains("idl-var"))
                    {
                        name = c.TextContent;
                        continue;
                    }
                    if (c.NodeType == AngleSharp.Dom.NodeType.Text && (c.TextContent.Contains(")") || c.TextContent.Contains(",")))
                    {
                        yield return (name, current.ToArray());
                        name = null;
                        current.Clear();
                    }
                    if (c is AngleSharp.Dom.IElement e)
                    {
                        current.Add(e);
                    }
                }
                yield return (name, current.ToArray());
            }
            var ps = SplitElements().ToArray();

            ApiGen.Mini.ITypeRef ToTypeRef(AngleSharp.Dom.INode[] elements)
            {
                //return elements.Length == 1
                //    ? new TypeRef(elements[0].TextContent, [])
                //    : new TypeRef(elements[^1].TextContent,
                //                  elements[..^1].Select(n => n.TextContent).ToImmutableArray());
                throw new NotImplementedException();
            }

            Debug.Assert(returnTypeElements.Length >= 1, "should parse at least one element for return type");
            var returnType = ToTypeRef(returnTypeElements);
            var parameters = element.QuerySelectorAll(".idl-var");

            var splitNode = element.ChildNodes;


            return new Method(
                element.QuerySelector(".idl-name").TextContent,
                parameters.Select(p =>
                {
                    var name = p.TextContent;
                    var typeNodes = ps.Where(p_ => p_.Item1 == name).Select(p_ => p_.Item2).FirstOrDefault();
                    Debug.Assert(typeNodes is not null, "parameter should have type annotation");
                    return new Parameter(
                        name,
                        ToTypeRef(typeNodes),
                         null
                     );
                }).ToImmutableArray(),
                ToTypeRef(ps[^1].Item2)
                );
        }

        var handle = new Handle(
            name == "GPU" ? "GPUInstance" : name,
            methods.Select(ParseMethod).ToImmutableArray(),
            properties.ToImmutableArray()
        );

        //var root = doc.DocumentNode.SelectSingleNode("//[contains(@class, 'idl-block'])//[contains(@class, \"idl-interface\"])");
        //var operations = from n in root.SelectNodes("//[@class=\"idl-operation\"")
        //                 select n;
        //Console.WriteLine(operations.ToArray());
        return Ok(handle);
    }
    private async ValueTask<JsonDocument> GetWebGPUIDLSpecAsync(CancellationToken cancellation)
    {
        var uri = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/spec/raw-nodes.json";
        var content = await HttpClient.GetStringAsync(uri, cancellation);
        return JsonDocument.Parse(content);
    }
    private async ValueTask<GPUApi> GetGPUApiFromSpecFileAsync(CancellationToken cancellation)
    {
        var uri = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/spec/gpu.json";
        return await HttpClient.GetFromJsonAsync<GPUApi>(uri, cancellation);
    }
}

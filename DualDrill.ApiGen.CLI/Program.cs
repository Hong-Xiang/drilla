using System.CommandLine;
using System.Text.Json;
using System.Xml.Linq;

namespace DualDrill.ApiGen.CLI;

sealed record class SpecGenerateCSharpOption(string SpecJsonPath)
{
}

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Api Source Generator");
        var specJsonFileOption = new Option<FileInfo>(name: "--spec", description: "Path to spec json file");
        rootCommand.AddGlobalOption(specJsonFileOption);

        var parseWGPUNativeXmlCommand = new Command("parse-xml", "Parse webgpu-native xml dump");
        var webgpuNaitveXmlFileOption = new Option<FileInfo>(name: "--webgpu-native", description: "Path to webgpu native xml file");
        parseWGPUNativeXmlCommand.AddOption(webgpuNaitveXmlFileOption);


        rootCommand.AddCommand(parseWGPUNativeXmlCommand);
        parseWGPUNativeXmlCommand.SetHandler((FileInfo specJsonFile, FileInfo xmlFile) =>
        {
            var root = XElement.Load(xmlFile.FullName);
            var parser = new WebGPUNativeXmlParser(root);
            IApiType[] enums = parser.ParseEnums();
            IApiType[] opaqueHandles = parser.ParseOpaqueHandles();
            IApiType[] types = [
                ..enums.Concat(opaqueHandles).Concat(parser.ParseValueRecordTypes()),
            ];
            var spec = new WebGPUApiSpec(types, [], parser.ParseMethods());
            File.WriteAllText(specJsonFile.FullName, JsonSerializer.Serialize(spec));
        },
        specJsonFileOption, webgpuNaitveXmlFileOption);


        var genEnum = new Command("enum", "Generate enum definitions");
        rootCommand.AddCommand(genEnum);
        genEnum.SetHandler((FileInfo file) =>
        {
            var specJsonContent = File.ReadAllText(file.FullName);
            var spec = JsonSerializer.Deserialize<WebGPUApiSpec>(
                specJsonContent
            );
            var builder = new GraphicsCSharpApiSourceCodeBuilder();
            var code = builder.BuildEnums(spec);
            Console.WriteLine(code);
        }, specJsonFileOption);
        var result = await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        return result;
    }
}

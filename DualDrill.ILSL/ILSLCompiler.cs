using DualDrill.CLSL.Language.Declaration;
using DualDrill.ILSL.Frontend;
using ICSharpCode.Decompiler.Disassembler;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static async ValueTask<string> Compile(ISharpShader shaderModule)
    {
        var ir = Parse(shaderModule);
        var code = await EmitCode(ir);
        return code;
    }

    public static async ValueTask<string> CompileV2(ISharpShader shaderModule)
    {
        var type = shaderModule.GetType();
        //using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
        //{
        //    HotReloadAssemblies = [
        //       type.Assembly,
        //       typeof(ILSLCompiler).Assembly
        //    ]
        //});
        var bodyParser = new RelooperMethodParser();

        var parser = new CLSLParser(bodyParser);
        var module = parser.ParseShaderModule(shaderModule);
        var tw = new IndentStringWriter("  ");
        var visitor = new ModuleToCodeVisitor(tw);
        foreach (var d in module.Declarations.OfType<VariableDeclaration>())
        {
            await d.AcceptVisitor(visitor);
        }
        foreach (var d in module.Declarations.OfType<StructureDeclaration>())
        {
            await d.AcceptVisitor(visitor);
        }
        foreach (var d in module.Declarations.OfType<FunctionDeclaration>())
        {
            await d.AcceptVisitor(visitor);
        }
        var code = tw.ToString();
        return code;
    }

    public static ShaderModuleDeclaration Parse(ISharpShader module)
    {
        var type = module.GetType();
        using var methodParser = new ILSpyMethodParser(new ILSpyOption()
        {
            HotReloadAssemblies = [
               type.Assembly,
               typeof(ILSLCompiler).Assembly
            ]
        });
        var parser = new CLSLParser(methodParser);
        return parser.ParseShaderModule(module);
    }

    public static async ValueTask<string> EmitCode(this ShaderModuleDeclaration module)
    {
        var tw = new IndentStringWriter("\t");
        var wgslVisitor = new ModuleToCodeVisitor(tw);
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return tw.ToString();
    }

    unsafe public static List<ILOpCode> ILReader(MethodBase m)
    {
        List<ILOpCode> ops = [];
        var ilBytes = m.GetMethodBody().GetILAsByteArray();
        fixed (byte* p = ilBytes)
        {
            var blobReader = new BlobReader(p, ilBytes.Length);
            while (blobReader.Offset != blobReader.Length - 1)
            {
                var op = ILParser.DecodeOpCode(ref blobReader);
                if (op == ILOpCode.Newobj)
                {
                    var token = blobReader.ReadInt32();
                    var handle = MetadataTokens.EntityHandle(token);
                    var c = m.Module.ResolveMethod(token);
                    Console.WriteLine(Enum.GetName(handle.Kind));
                }
                ops.Add(op);
            }

        }
        return ops;
    }
}

using DualDrill.ILSL.IR.Declaration;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System.Data.SqlTypes;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static async ValueTask<string> Compile(IShaderModule shaderModule)
    {
        var ast = Decompile(shaderModule);
        var ir = CompileFrontend(ast);
        var code = await CompileBackend(ir);
        return code;
    }

    static SyntaxTree Decompile(IShaderModule shaderModule)
    {
        var target = shaderModule.GetType();
        var module = target.Assembly.Modules.ToArray();
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            AlwaysQualifyMemberReferences = true,
            AlwaysUseGlobal = true,
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var ast = decompiler.DecompileType(name);
        return ast;
    }

    static IR.Module CompileFrontend(SyntaxTree ast)
    {
        return (IR.Module)ast.AcceptVisitor(new ILSpyASTToModuleVisitor([]));
    }

    static async ValueTask<string> CompileBackend(this IR.Module module)
    {
        var tw = new StringWriter();
        var wgslVisitor = new ModuleToCodeVisitor(tw, new WGSLLanguage());
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return tw.ToString();
    }

    public static SyntaxTree DecompileMethod(MethodBase m)
    {
        var target = m.DeclaringType;
        var module = target.Assembly.Modules.ToArray();
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            AlwaysQualifyMemberReferences = true,
            AlwaysUseGlobal = true,
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var mdh = (MethodDefinitionHandle)MetadataTokens.EntityHandle(m.GetMetadataToken());
        var ast = decompiler.Decompile(mdh);
        return ast;

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
                    Console.WriteLine(Enum.GetName(handle.Kind));
                }
                ops.Add(op);
            }

        }
        return ops;
    }
    unsafe public static string ILReaderFromRuntimeAssembly(MethodBase method)
    {
        Assembly assembly = method.DeclaringType.Assembly;
        Module module = method.Module;
        var generator = new Lokad.ILPack.AssemblyGenerator();
        var bytes = generator.GenerateAssemblyBytes(assembly);

        // Get the MetadataReader for the assembly
        using (MemoryStream assemblyStream = new MemoryStream())
        {

            assemblyStream.Write(bytes);
            assemblyStream.Position = 0;

            using (PEReader peReader = new PEReader(assemblyStream))
            {
                var fn = "InMemoryAssembly";
                using var file = new PEFile(fn, peReader);
                var settings = new DecompilerSettings()
                {
                    AlwaysQualifyMemberReferences = true,
                    AlwaysUseGlobal = true,
                    UsingDeclarations = false,
                };
                var resolver = new UniversalAssemblyResolver(assembly.Location, settings.ThrowOnAssemblyResolveErrors,
                    file.DetectTargetFrameworkId(), file.DetectRuntimePack(),
                    settings.LoadInMemory ? PEStreamOptions.PrefetchMetadata : PEStreamOptions.Default,
                    settings.ApplyWindowsRuntimeProjections ? MetadataReaderOptions.ApplyWindowsRuntimeProjections : MetadataReaderOptions.None);

                var decompiler = new CSharpDecompiler(file, resolver, settings);
                var name = new FullTypeName(method.DeclaringType.FullName);
                var mdh = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.GetMetadataToken());
                var ast = decompiler.Decompile(mdh);
                return ast.ToString();
            }
        }
    }



    public static IR.Module CompileIR(IShaderModule shaderModule)
    {
        var ast = Decompile(shaderModule);
        return CompileFrontend(ast);
    }

    public static JsonNode ASTToJson(IShaderModule shaderModule)
    {
        var target = shaderModule.GetType();
        var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
        {
            UsingDeclarations = false,
        });
        var name = new FullTypeName(target.FullName);
        var ast = decompiler.DecompileType(name);
        var jsonVisitor = new ASTJsonVisitor();
        return ast.AcceptVisitor(jsonVisitor);
    }
}

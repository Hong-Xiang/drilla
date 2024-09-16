using DualDrill.ILSL.Frontend;
using DualDrill.ILSL.IR.Declaration;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;

namespace DualDrill.ILSL;

public static class ILSLCompiler
{
    public static async ValueTask<string> Compile(IShaderModule shaderModule)
    {
        var ir = Parse(shaderModule);
        var code = await EmitCode(ir);
        return code;
    }

    public static IR.Module Parse(IShaderModule module)
    {
        var type = module.GetType();
        using var parser = new ILSpyFrontend(new ILSpyOption()
        {
            HotReloadAssemblies = [
               type.Assembly,
               typeof(ILSLCompiler).Assembly
            ]
        });

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        List<FunctionDeclaration> functionDeclarations = [];
        foreach (var m in methods)
        {
            if (m.IsSpecialName)
            {
                continue;
            }
            if (m.GetCustomAttribute<ShaderMethodAttribute>() is null
                && m.GetCustomAttribute<VertexAttribute>() is null
                && m.GetCustomAttribute<FragmentAttribute>() is null)
            {
                continue;
            }
            functionDeclarations.Add(parser.ParseMethod(m));
        }
        return new IR.Module([.. functionDeclarations]);
    }

    //public static SyntaxTree DecompileMethod(MethodBase shaderModule)
    //{
    //    var target = shaderModule.GetType();
    //    var module = target.Assembly.Modules.ToArray();
    //    var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
    //    {
    //        AlwaysQualifyMemberReferences = true,
    //        AlwaysUseGlobal = true,
    //        UsingDeclarations = false,
    //    });
    //    var name = new FullTypeName(target.FullName);
    //    var ast = decompiler.DecompileType(name);
    //    return ast;
    //}

    //unsafe public static SyntaxTree DecompileWithHotReload(IModuleSharp module)
    //{
    //    var moduleType = module.GetType();
    //    Assembly assembly = moduleType.Assembly;
    //    var generator = new Lokad.ILPack.AssemblyGenerator();
    //    var assemblyData = generator.GenerateAssemblyBytes(assembly);
    //    var hash = MD5.HashData(assemblyData);
    //    var fileName = "InMemoryAssembly" + Convert.ToHexString(hash);

    //    fixed (byte* assemblyDataPtr = assemblyData)
    //    {
    //        using PEReader peReader = new PEReader(assemblyDataPtr, assemblyData.Length);
    //        using var file = new PEFile(fileName, peReader);
    //        var settings = new DecompilerSettings()
    //        {
    //            AlwaysQualifyMemberReferences = true,
    //            AlwaysUseGlobal = true,
    //            UsingDeclarations = false,
    //            LoadInMemory = true
    //        };
    //        var resolver = new UniversalAssemblyResolver(assembly.Location, settings.ThrowOnAssemblyResolveErrors,
    //            file.DetectTargetFrameworkId(), file.DetectRuntimePack(),
    //            settings.LoadInMemory ? PEStreamOptions.PrefetchMetadata : PEStreamOptions.Default,
    //            settings.ApplyWindowsRuntimeProjections ? MetadataReaderOptions.ApplyWindowsRuntimeProjections : MetadataReaderOptions.None);

    //        var decompiler = new CSharpDecompiler(file, resolver, settings);
    //        var name = new FullTypeName(moduleType.FullName);
    //        var ast = decompiler.DecompileType(name);
    //        return ast;
    //    }
    //}

    //unsafe public static SyntaxTree DecompileMethod(MethodBase method)
    //{
    //    Assembly assembly = method.DeclaringType.Assembly;
    //    var generator = new Lokad.ILPack.AssemblyGenerator();
    //    var assemblyData = generator.GenerateAssemblyBytes(assembly);
    //    var hash = MD5.HashData(assemblyData);
    //    var fileName = "InMemoryAssembly" + Convert.ToHexString(hash);

    //    fixed (byte* assemblyDataPtr = assemblyData)
    //    {
    //        using PEReader peReader = new PEReader(assemblyDataPtr, assemblyData.Length);
    //        using var file = new PEFile(fileName, peReader);
    //        var settings = new DecompilerSettings()
    //        {
    //            AlwaysQualifyMemberReferences = true,
    //            AlwaysUseGlobal = true,
    //            UsingDeclarations = false,
    //            LoadInMemory = true
    //        };
    //        var resolver = new UniversalAssemblyResolver(assembly.Location, settings.ThrowOnAssemblyResolveErrors,
    //            file.DetectTargetFrameworkId(), file.DetectRuntimePack(),
    //            settings.LoadInMemory ? PEStreamOptions.PrefetchMetadata : PEStreamOptions.Default,
    //            settings.ApplyWindowsRuntimeProjections ? MetadataReaderOptions.ApplyWindowsRuntimeProjections : MetadataReaderOptions.None);

    //        var decompiler = new CSharpDecompiler(file, resolver, settings);
    //        var mdh = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.GetMetadataToken());
    //        var ast = decompiler.Decompile(mdh);
    //        return ast;
    //    }
    //}

    static IR.Module CompileFrontend(SyntaxTree ast)
    {
        return (IR.Module)ast.AcceptVisitor(new ILSpyASTToModuleVisitor([]));
    }

    public static async ValueTask<string> EmitCode(this IR.Module module)
    {
        var tw = new StringWriter();
        var wgslVisitor = new ModuleToCodeVisitor(tw, new WGSLLanguage());
        foreach (var d in module.Declarations)
        {
            await d.AcceptVisitor(wgslVisitor);
        }
        return tw.ToString();
    }

    //public static SyntaxTree DecompileMethod(MethodBase m)
    //{
    //    var target = m.DeclaringType;
    //    var module = target.Assembly.Modules.ToArray();
    //    var decompiler = new CSharpDecompiler(target.Assembly.Location, new DecompilerSettings()
    //    {
    //        AlwaysQualifyMemberReferences = true,
    //        AlwaysUseGlobal = true,
    //        UsingDeclarations = false,
    //    });
    //    var name = new FullTypeName(target.FullName);
    //    var mdh = (MethodDefinitionHandle)MetadataTokens.EntityHandle(m.GetMetadataToken());
    //    var ast = decompiler.Decompile(mdh);
    //    return ast;

    //}

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

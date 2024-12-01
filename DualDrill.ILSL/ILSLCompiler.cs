using DualDrill.CLSL.Language.IR.Declaration;
using DualDrill.CLSL.Language.IR.ShaderAttribute;
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
        using var bodyParser = new ILSpyMethodParser(new ILSpyOption()
        {
            HotReloadAssemblies = [
               type.Assembly,
               typeof(ILSLCompiler).Assembly
            ]
        });

        var parser = new CLSLParser(bodyParser);
        var module = parser.ParseShaderModule(shaderModule);
        var tw = new IndentStringWriter("\t");
        var visitor = new ModuleToCodeVisitor(tw, new WGSLLanguage());
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

    public static CLSL.Language.IR.ShaderModule Parse(ISharpShader module)
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

        //var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        //List<CLSL.Language.IR.Declaration.FunctionDeclaration> functionDeclarations = [];
        //foreach (var m in methods)
        //{
        //    if (m.IsSpecialName)
        //    {
        //        continue;
        //    }
        //    if (m.GetCustomAttribute<ShaderMethodAttribute>() is null
        //        && m.GetCustomAttribute<VertexAttribute>() is null
        //        && m.GetCustomAttribute<FragmentAttribute>() is null)
        //    {
        //        continue;
        //    }
        //    functionDeclarations.Add(methodParser.ParseMethod(m));
        //}
        //return new([.. functionDeclarations]);
        return parser.ParseShaderModule(module);
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

    public static async ValueTask<string> EmitCode(this CLSL.Language.IR.ShaderModule module)
    {
        var tw = new IndentStringWriter("\t");
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
}

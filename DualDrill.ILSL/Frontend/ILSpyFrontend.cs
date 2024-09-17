using DualDrill.ILSL.IR.Declaration;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DualDrill.ILSL.Frontend;

public record struct ILSpyOption()
{
    public ImmutableHashSet<Assembly> HotReloadAssemblies { get; set; } = [];
}


public sealed class ILSpyFrontend(ILSpyOption Option) : IParser, IDisposable
{
    public ParserContext Context { get; } = new ParserContext([]);

    Dictionary<Assembly, CSharpDecompiler> Decompilers = [];

    sealed record class RuntimePEData(PEReader Reader, PEFile File, IntPtr BytesData) : IDisposable
    {
        unsafe public void Dispose()
        {
            File.Dispose();
            Reader.Dispose();
            NativeMemory.Free((void*)BytesData);
        }
    }

    Dictionary<Assembly, RuntimePEData> RuntimePEDatas = [];

    public IR.Module ParseModule(IShaderModule module)
    {
        var moduleType = module.GetType();
        var methods = moduleType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        var context = ParserContext.Create();
        foreach (var m in methods)
        {
            ParseMethod(m);
        }
        return new([.. context.FunctionDeclarations.Values]);
    }

    CSharpDecompiler GetOrCreateDecompiler(Assembly assembly)
    {
        if (Decompilers.TryGetValue(assembly, out var result))
        {
            return result;
        }
        var created = Option.HotReloadAssemblies.Contains(assembly)
                ? CreateRuntimeDecompiler(assembly)
                : CreateStaticDecompiler(assembly);
        Decompilers.Add(assembly, created);
        return created;
    }

    DecompilerSettings GetDecompilerSettings()
    {
        return new()
        {
            AlwaysQualifyMemberReferences = true,
            AlwaysUseGlobal = true,
            UsingDeclarations = false,
        };
    }

    CSharpDecompiler CreateStaticDecompiler(Assembly assembly)
    {
        return new CSharpDecompiler(assembly.Location, GetDecompilerSettings());
    }

    unsafe CSharpDecompiler CreateRuntimeDecompiler(Assembly assembly)
    {
        var generator = new Lokad.ILPack.AssemblyGenerator();
        var assemblyData = generator.GenerateAssemblyBytes(assembly);
        var hash = MD5.HashData(assemblyData);
        var fileName = "InMemoryAssembly" + Convert.ToHexString(hash);
        byte* dataPtr = (byte*)NativeMemory.Alloc((nuint)assemblyData.Length);
        assemblyData.AsSpan().CopyTo(new Span<byte>(dataPtr, assemblyData.Length));
        var peReader = new PEReader(dataPtr, assemblyData.Length);
        var file = new PEFile(fileName, peReader);
        var peData = new RuntimePEData(peReader, file, (nint)dataPtr);
        var settings = GetDecompilerSettings();
        if (RuntimePEDatas.TryGetValue(assembly, out var existedPEData))
        {
            existedPEData.Dispose();
            RuntimePEDatas.Remove(assembly);
        }
        RuntimePEDatas.Add(assembly, peData);

        settings.LoadInMemory = true;
        var resolver = new UniversalAssemblyResolver(assembly.Location, settings.ThrowOnAssemblyResolveErrors,
            file.DetectTargetFrameworkId(), file.DetectRuntimePack(),
            settings.LoadInMemory ? PEStreamOptions.PrefetchMetadata : PEStreamOptions.Default,
            settings.ApplyWindowsRuntimeProjections ? MetadataReaderOptions.ApplyWindowsRuntimeProjections : MetadataReaderOptions.None);
        return new CSharpDecompiler(file, resolver, settings);
    }

    public SyntaxTree Decompile(MethodBase method)
    {
        var handle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.GetMetadataToken());
        var decompiler = GetOrCreateDecompiler(method.DeclaringType.Assembly);
        return decompiler.Decompile(handle);
    }

    bool IsCacheable(MethodBase method)
    {
        return Option.HotReloadAssemblies.Contains(method.DeclaringType.Assembly);
    }

    public FunctionDeclaration ParseMethod(MethodBase method)
    {
        var shouldCache = IsCacheable(method);
        if (!shouldCache && Context.FunctionDeclarations.TryGetValue(method, out var existedDecl))
        {
            return existedDecl;
        }
        else
        {
            var ast = Decompile(method);
            var result = (FunctionDeclaration)ast.AcceptVisitor(new ILSpyASTToModuleVisitor([]));
            // Ad hoc fixing of return type attribute missing
            if (method is MethodInfo minfo)
            {
                var ret = result.Return;
                var pinfo = minfo.ReturnParameter;
                ret = ret with
                {
                    Attributes = [.. ret.Attributes,
                    .. pinfo.GetCustomAttributes<BuiltinAttribute>(),
                    .. pinfo.GetCustomAttributes<LocationAttribute>()
                    ]
                };
                result = result with { Return = ret };
            }
            //result = result with
            //{
            //    Attributes = [..result.Attributes, ]
            //};
            Context.FunctionDeclarations.TryAdd(method, result);
            return result;
        }
    }

    public void Dispose()
    {
        foreach (var d in RuntimePEDatas.Values)
        {
            d.Dispose();
        }
    }
}

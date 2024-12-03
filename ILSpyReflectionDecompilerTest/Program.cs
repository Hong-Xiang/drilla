using System;
using System.Collections.Generic;
using System.Reflection;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.CSharp;

namespace ILSpyReflectionDecompilerTest;

public class ReflectionCompilation : ICompilation
{
    public IModule MainModule { get; }
    public IEnumerable<IModule> Assemblies { get; }

    public ReflectionCompilation()
    {
        MainModule = new ReflectionModule(this);
        Assemblies = new[] { MainModule };
    }

    // Implement other members as needed
    // ...
}

public class ReflectionModule : IModule
{
    public ICompilation Compilation { get; }
    public string Name => "RuntimeModule";
    public IList<ITypeDefinition> TypeDefinitions { get; }

    public ReflectionModule(ICompilation compilation)
    {
        Compilation = compilation;
        TypeDefinitions = new List<ITypeDefinition>();
    }

    // Implement other members as needed
    // ...
}

public class ReflectionTypeDefinition : ITypeDefinition
{
    private readonly Type type;
    public IType DeclaringType => null;
    public string FullName => type.FullName;
    public string Name => type.Name;
    public IList<IMethod> Methods { get; }

    public ReflectionTypeDefinition(Type type)
    {
        this.type = type;
        Methods = new List<IMethod>();

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Methods.Add(new ReflectionMethod(this, method));
        }
    }

    // Implement other members as needed
    // ...
}

public class ReflectionMethod : IMethod
{
    private readonly MethodBase methodBase;
    public ITypeDefinition DeclaringType { get; }
    public string Name => methodBase.Name;

    public ReflectionMethod(ITypeDefinition declaringType, MethodBase methodBase)
    {
        DeclaringType = declaringType;
        this.methodBase = methodBase;
    }

    // Implement other members as needed
    // ...
}

public class DecompilerHelper
{
    public ILFunction DecompileMethod(MethodBase methodBase)
    {
        //var compilation = new ReflectionCompilation();
        //var typeDefinition = new ReflectionTypeDefinition(methodBase.DeclaringType);
        //((ReflectionModule)compilation.MainModule).TypeDefinitions.Add(typeDefinition);

        //var method = new ReflectionMethod(typeDefinition, methodBase);

        //// Create the decompiler
        //var decompilerSettings = new DecompilerSettings();
        //var decompiler = new CSharpDecompiler(compilation, decompilerSettings);

        //// Decompile the method to ILFunction
        //var ilFunction = decompiler.Decompile(method);

        //return ilFunction;
        throw new NotImplementedException();
    }
}

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}

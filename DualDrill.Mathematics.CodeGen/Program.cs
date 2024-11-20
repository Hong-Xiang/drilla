using DualDrill.ApiGen.DMath;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Types;

var targetDirectory = new DirectoryInfo(args[0]);

var projectFile = Path.Combine(targetDirectory.FullName, "DualDrill.Mathematics.csproj");
if (!File.Exists(projectFile))
{
    throw new FileNotFoundException("DualDrill.Mathematics.csproj not found in the target directory.");
}



var config = CSharpProjectionConfiguration.Instance;
foreach (var t in ShaderType.GetVecTypes())
{
    var gen = new MathCodeGenerator(config);
    gen.Generate(t);
    var fn = $"{config.GetCSharpTypeName(t)}.gen.cs";
    var fpath = Path.Combine(targetDirectory.FullName, fn);
    File.WriteAllText(fpath, gen.GetCode());
}

{
    var gen = new MathCodeGenerator(config);
    gen.GenerateFunctions();
    var fn = $"DMath.gen.cs";
    var fpath = Path.Combine(targetDirectory.FullName, fn);
    File.WriteAllText(fpath, gen.GetCode());
}


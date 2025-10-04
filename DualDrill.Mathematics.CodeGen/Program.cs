using DualDrill.ApiGen.DMath;
using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.Types;
using System.Diagnostics;

var cwd = Directory.GetCurrentDirectory();
var targetDirectory = new DirectoryInfo(Path.Combine(cwd, "..", "..", "..", "..", "DualDrill.Mathematics"));
Debug.Assert(targetDirectory.Exists);
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


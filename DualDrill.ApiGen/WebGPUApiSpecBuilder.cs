using System.Xml.Linq;

namespace DualDrill.ApiGen;

public sealed record class WebGPUApiSpecBuilder(string XmlSpecContent)
{
    public WebGPUApiSpec Build()
    {
        var root = XElement.Parse(XmlSpecContent);
        var ns = root.Element("namespace");
        var enums = ns.Elements("enumeration")
                    .Select(el =>
                    {
                        var nativeName = el.Attribute("name").Value;
                        return new ApiEnumType(
                            Name: nativeName[1..],
                            NativeName: nativeName,
                            Values: el.Elements("enumerator")
                                        .Select(e =>
                                        {
                                            var valueNativeName = e.Attribute("name").Value;
                                            return new ApiEnumValue(Name: valueNativeName[(nativeName.Length + 1)..],
                                                                    NativeName: valueNativeName);
                                        })
                                        .Where(x => !x.Name.EndsWith("Force32")).ToArray(),
                            IsFlag: nativeName.EndsWith("Usage"));
                    });

        return new WebGPUApiSpec([.. enums], [], []);
    }
}

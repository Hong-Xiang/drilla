using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DualDrill.ApiGen.CLI;

internal sealed class WebGPUNativeXmlParser(XElement Root)
{
    XElement NameSpaceElement = Root.Element("namespace");

    public ApiMethod[] ParseMethods()
    {
        return NameSpaceElement.Element("class").Elements("function")
            .Select(e =>
            {
                var name = e.Attribute("name").Value;
                return new ApiMethod(name, name,
                    e.Elements("param").Select(p =>
                    {
                        var typeName = p.Element("type").Value;
                        return new Parameter(p.Attribute("name").Value,
                                            ParseTypeReference(p.Element("type")));
                    }).ToArray()
                );
            }).ToArray();
    }

    public IApiType[] ParseOpaqueHandles()
    {
        return NameSpaceElement.Elements("struct")
            .Where(e => e.Elements().Count() == 0)
            .Select(e =>
            {
                var nativeName = e.Attribute("name").Value;
                return new ApiOpaqueHandleType(nativeName, nativeName, []);
            })
            .ToArray();
    }

    public ApiTypeReference ParseTypeReference(XElement el)
    {
        var name = el.Value;
        var nativeName = el.Attribute("native")?.Value;
        return new(name, nativeName);
    }

    public ApiValueRecordType[] ParseValueRecordTypes()
    {
        return NameSpaceElement.Elements("struct")
            .Where(e => e.Elements("field").Any())
            .Select(e =>
            {
                var name = e.Attribute("name").Value;
                var fields = e.Elements("field").Select(fe =>
                {
                    return new Field(fe.Attribute("name").Value,
                           ParseTypeReference(fe.Element("type")));
                }).ToArray();
                return new ApiValueRecordType(name, fields);
            }).ToArray();
    }

    public IApiType[] ParseEnums()
    {
        var enums = NameSpaceElement.Elements("enumeration")
                        .Select(el =>
                    {
                        var nativeName = el.Attribute("name").Value;
                        return new ApiEnumType(
                            Name: nativeName,
                            NativeName: nativeName,
                            Values: el.Elements("enumerator")
                                        .Select(e =>
                                        {
                                            var valueName = e.Attribute("name").Value;
                                            return new ApiEnumValue(Name: valueName, NativeName: valueName);
                                        })
                                        .Where(x => !x.Name.EndsWith("Force32")).ToArray(),
                            IsFlag: false);
                    });
        return [.. enums];
    }
}

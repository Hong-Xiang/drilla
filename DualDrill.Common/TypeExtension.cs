using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.Common;

public static class TypeExtension
{
    public static string CSharpFullName(this Type type)
    {
        var ns = type.DeclaringType is { } t ? CSharpFullName(t) : type.Namespace;
        if (type.IsGenericType)
        {
            var tn = type.Name.Split('`')[0].Replace("+", ".");
            var gn = string.Join(",", type.GetGenericArguments().Select(t => t.CSharpFullName()));
            return $"{ns}.{tn}<{gn}>";
        }
        else
        {
            var tn = type.Name.Replace("+", ".");
            return $"{ns}.{tn}";
        }
    }
}

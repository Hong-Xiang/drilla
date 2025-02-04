﻿// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using DualDrill.Common;



Console.WriteLine(typeof(A.B.C<int>).CSharpFullName());
Console.WriteLine(typeof(A.B.C<A.B.C<float>>).CSharpFullName());
Console.WriteLine(typeof(A.B.D).CSharpFullName());

//var opcodes = typeof(OpCodes).GetMembers()
//                .OfType<FieldInfo>()
//                .Where(f => f.FieldType == typeof(OpCode))
//                .Select(f => (OpCode)f.GetValue(typeof(OpCodes)))
//                .ToArray();

//foreach (var op in opcodes)
//{
//    var ilOpCode = (ILOpCode)(op.Value);
//    //Console.WriteLine($"{ilOpCode} {op}");
//    Console.WriteLine($"ILOpCode.{ilOpCode} => throw new NotImplementedException(),");
//    //Console.WriteLine($"case ILOpCode.{ilOpCode}:");
//    //Console.WriteLine("    throw new NotImplementedException($\"{instruction}\");");
//}

namespace A.B
{
    struct C<T>
    {
    }
    struct D
    {
    }
}


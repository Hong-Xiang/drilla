using DotNext;
using LLVMSharp;
using LLVMSharp.Interop;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace DualDrill.CLSL.Test;



public sealed class LLVMSharpUsageTests(ITestOutputHelper Output)
{

    [Fact]
    public unsafe void TernaryVectorSwizzleTest()
    {
        using var module = LLVMModuleRef.CreateWithName(nameof(TernaryVectorSwizzleTest));


        var builder = module.Context.CreateBuilder();

        var vec3 = LLVMTypeRef.CreateVector(LLVMTypeRef.Float, 3);

        var vec3ptr = LLVMTypeRef.CreatePointer(vec3, vec3.PointerAddressSpace);
        var vec2 = LLVMTypeRef.CreateVector(LLVMTypeRef.Float, 2);
        var vec2ptr = LLVMTypeRef.CreatePointer(vec2, vec2.PointerAddressSpace);



        var fType = LLVMTypeRef.CreateFunction(
            LLVMTypeRef.Float,
            [vec3, LLVMTypeRef.Int1]
        );
        var f = module.AddFunction("swizzleTest", fType);
        var be = f.AppendBasicBlock("entry");
        var bf = f.AppendBasicBlock("if.false");
        var bt = f.AppendBasicBlock("if.true");
        var bm = f.AppendBasicBlock("if.merge");
        var br = f.AppendBasicBlock("return");

        var pv = f.GetParam(0);
        var pb = f.GetParam(1);
        pv.Name = "p";
        pb.Name = "cond";


        // Position builder at the entry block
        builder.PositionAtEnd(be);

        var pa = builder.BuildAlloca(vec3, "p_alloc");

        // Create allocas for all the required variables
        var l0 = builder.BuildAlloca(vec3ptr, "l0");
        var l1 = builder.BuildAlloca(vec3ptr, "l1");
        var l2 = builder.BuildAlloca(vec3ptr, "l2");
        var l3 = builder.BuildAlloca(vec3ptr, "l3");
        var l4 = builder.BuildAlloca(vec2, "l4");
        var l5 = builder.BuildAlloca(vec3ptr, "l5");
        var l6 = builder.BuildAlloca(vec2, "l6");
        var l7 = builder.BuildAlloca(vec3ptr, "l7");
        var l8 = builder.BuildAlloca(vec2, "l8");
        var l9 = builder.BuildAlloca(LLVMTypeRef.Float, "l9");

        // Store initial values to avoid undefined behavior
        builder.BuildStore(pv, pa);

        // Store pa address to l0 (initialize the pointer)
        builder.BuildStore(pa, l0);

        // Now load and store pointers properly
        var l0Val = builder.BuildLoad2(vec3, l0, "l0_val");
        builder.BuildStore(l0Val, l1);
        var l0Val2 = builder.BuildLoad2(vec3, l0, "l0_val_2");
        builder.BuildStore(l0Val2, l2);

        // Initialize vec2 allocas with zero vectors
        var floatZero = LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, 0.0);
        var vec2Zero = LLVMValueRef.CreateConstStruct([floatZero, floatZero], false);
        vec2Zero = builder.BuildInsertElement(vec2Zero, floatZero, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "");
        vec2Zero = builder.BuildInsertElement(vec2Zero, floatZero, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false), "");
        builder.BuildStore(vec2Zero, l4);
        builder.BuildStore(vec2Zero, l6);
        builder.BuildStore(vec2Zero, l8);

        // Initialize other pointer allocas
        builder.BuildStore(pa, l7);
        builder.BuildStore(pa, l5);

        // Branch based on condition
        builder.BuildCondBr(pb, bt, bf);

        // Build the if.false block - keep xz as is
        builder.PositionAtEnd(bf);
        builder.BuildStore(builder.BuildLoad2(vec3, l2), l3);
        var pFalse = builder.BuildLoad2(vec3, pa, "p_false");

        // Extract x and z components
        var pxF = builder.BuildExtractElement(pFalse, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "x_false");
        var pzF = builder.BuildExtractElement(pFalse, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false), "z_false");

        // Create xz vec2
        var l4v = builder.BuildLoad2(vec2, l4, "l4_val");
        l4v = builder.BuildInsertElement(l4v, pxF, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "xz_false_x");
        l4v = builder.BuildInsertElement(l4v, pzF, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false), "xz_false_z");
        builder.BuildStore(l4v, l4);

        var l3v = builder.BuildLoad2(vec3, l3);
        builder.BuildStore(l3v, l5);

        builder.BuildStore(builder.BuildLoad2(vec2, l4), l6);

        builder.BuildBr(bm);

        // Build the if.true block - swap xz to zx
        builder.PositionAtEnd(bt);
        var pTrue = builder.BuildLoad2(vec3, pa, "p_true");

        // Extract z and x components (swapped)
        var zTrue = builder.BuildExtractElement(pTrue, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false), "z_true");
        var xTrue = builder.BuildExtractElement(pTrue, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "x_true");

        // Create zx vec2 (swizzled)
        var l8v = builder.BuildLoad2(vec2, l8, "l8_val");
        l8v = builder.BuildInsertElement(l8v, zTrue, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "zx_true_z");
        l8v = builder.BuildInsertElement(l8v, xTrue, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false), "zx_true_x");
        builder.BuildStore(l8v, l8);

        builder.BuildStore(builder.BuildLoad2(vec3, l7), l5);
        builder.BuildStore(builder.BuildLoad2(vec2, l8), l6);

        builder.BuildBr(bm);

        // Build the merge block
        builder.PositionAtEnd(bm);

        var l5p = builder.BuildLoad2(vec3ptr, l5, "l5_ptr_val");
        var l5v = builder.BuildLoad2(vec3, l5p, "l5_val");
        var l6v = builder.BuildLoad2(vec2, l6, "l6_val");

        l5v = builder.BuildInsertElement(
            l5v,
            builder.BuildExtractElement(l6v, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "l6_x"),
            LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false),
            ""
        );
        l5v = builder.BuildInsertElement(
            l5v,
            builder.BuildExtractElement(l6v, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false), "l6_z"),
            LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false),
            ""
        );
        builder.BuildStore(l5v, l5p);

        var rx = builder.BuildExtractElement(builder.BuildLoad2(vec3, pa), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0, false), "result_x");
        var ry = builder.BuildExtractElement(builder.BuildLoad2(vec3, pa), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1, false), "result_y");
        var rz = builder.BuildExtractElement(builder.BuildLoad2(vec3, pa), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false), "result_z");
        builder.BuildStore(builder.BuildFAdd(builder.BuildFAdd(rx, ry), rz), l9);
        builder.BuildBr(br);

        // Build the return block
        builder.PositionAtEnd(br);
        builder.BuildRet(builder.BuildLoad2(LLVMTypeRef.Float, l9));

        module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);

        Output.WriteLine(module.ToString());

        var pm = LLVM.CreatePassManager();

        var pmb = LLVM.PassManagerBuilderCreate();
        LLVM.PassManagerBuilderSetOptLevel(pmb, 2);
        LLVM.PassManagerBuilderUseInlinerWithThreshold(pmb, 225);
        LLVM.PassManagerBuilderSetDisableUnrollLoops(pmb, 0);

        LLVM.PassManagerBuilderPopulateModulePassManager(pmb, pm);

        LLVM.RunPassManager(pm, module);


        Output.WriteLine(module.ToString());
    }

    [Fact]
    public unsafe void SimpleConditionalLLVMIRCreate()
    {
        using var module = LLVMModuleRef.CreateWithName(nameof(SimpleConditionalLLVMIRCreate));
        var builder = module.Context.CreateBuilder();

        var fType = LLVMTypeRef.CreateFunction(
            LLVMTypeRef.Int32,
            []
        );
        var f = module.AddFunction("ret42", fType);
        var entry = f.AppendBasicBlock("entry");
        builder.PositionAtEnd(entry);
        var v42 = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 42);
        var ret = builder.BuildRet(v42);

        Output.WriteLine(f.EntryBasicBlock.Handle.ToString());

        module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
        var passManager = LLVM.CreateFunctionPassManagerForModule(module);


        //var ctx = new LLVMContext();
        //var builder = new IRBuilder(ctx);
        //using var module = LLVMModuleRef.CreateWithName("test-module");
        //var f = module.AddFunction("ret42", LLVMTypeRef.CreateFunction(
        //    LLVMTypeRef.Int32,
        //    [LLVMTypeRef.Int32]));

        //builder.Handle.PositionAtEnd(f.EntryBasicBlock);



        //var val = LLVM.ConstInt(LLVMTypeRef.Int32, 42, 1);


        //builder.CreateRet(new LLVMSharp.Value(val));


        Output.WriteLine(module.ToString());
    }
}

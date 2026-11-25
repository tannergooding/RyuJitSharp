// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CallArgumentMorphTargetTests
{
    [Test]
    public static void MultiRegisterStructLoadsSpillMutableAddressesAndPreserveLoadEffects()
    {
        Assume.That(Configuration.TARGET_ARM64 || Configuration.TARGET_RISCV64);

        WithCompiler(compiler => {
            var pointerSize = Configuration.TARGET_POINTER_SIZE;
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x1234, true, unchecked((uint)(2 * pointerSize)),
                TYP_STRUCT, "MutableAddressStruct", "MutableAddressStruct");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            var layoutTableField = typeof(Compiler).GetField("_classLayoutTable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing class layout table.");
            layoutTableField.SetValue(compiler, layouts);

            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewIndir(TYP_BYREF, compiler.gtNewLclvNode(TYP_BYREF, 0), GTF_IND_NONFAULTING);
            var loadFlags = GTF_IND_VOLATILE | GTF_IND_UNALIGNED | GTF_IND_NONFAULTING;
            var load = compiler.gtNewBlkIndir(address, layout, loadFlags);
            load.HasOrderingSideEffect = true;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var argument = call.Args.PushBack(NewCallArg.CreateForStruct(load, TYP_STRUCT, layout));
            var abiInfo = new AbiPassingInformation(2);
            var registers = Globals.IntArgRegs;
            abiInfo.Segments[0] = AbiPassingSegment.InRegister(registers[0], 0, pointerSize);
            abiInfo.Segments[1] = AbiPassingSegment.InRegister(registers[1], pointerSize, pointerSize);
            argument.AbiInfo = abiInfo;

            Assert.That(TryMorphStructArg(compiler, argument), Is.True);

            var comma = argument.Node.AsOp();
            Assert.That(comma.Oper, Is.EqualTo(GT_COMMA));
            var addressStore = comma.Op1.AsLclVar();
            Assert.That(addressStore.Data.Oper, Is.EqualTo(GT_IND));
            var fields = comma.Op2.AsFieldList();
            var firstUse = fields.Uses.Head ?? throw new InvalidOperationException("Missing first struct slot.");
            var secondUse = firstUse.Next ?? throw new InvalidOperationException("Missing second struct slot.");
            Assert.That(secondUse.Next, Is.Null);

            var firstLoad = firstUse.Node.AsIndir();
            var secondLoad = secondUse.Node.AsIndir();
            Assert.That(firstLoad.Addr.AsLclVar().LclNum, Is.EqualTo(addressStore.LclNum));
            Assert.That(secondLoad.Addr.Oper, Is.EqualTo(GT_ADD));
            Assert.That(secondLoad.Addr.AsOp().Op1.AsLclVar().LclNum, Is.EqualTo(addressStore.LclNum));
            Assert.That(secondLoad.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)pointerSize));

            var expectedFlags = loadFlags & GTF_IND_COPYABLE_FLAGS;
            foreach (var slotLoad in new[] { firstLoad, secondLoad })
            {
                Assert.That(slotLoad.Flags & GTF_IND_COPYABLE_FLAGS, Is.EqualTo(expectedFlags));
                Assert.That(slotLoad.HasOrderingSideEffect, Is.True);
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(false);
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgTryMorphStructArg")]
    private static extern bool TryMorphStructArg(Compiler compiler, CallArg argument);
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64AddressLoweringTests
{
    [TestCase(TYP_INT, 2, true)]
    [TestCase(TYP_LONG, 3, true)]
    [TestCase(TYP_INT, 3, false)]
    [TestCase(TYP_LONG, 2, false)]
    public static void ScaledAddressUsesOnlyTheAccessWidth(var_types type, int shiftBy, bool folded)
    {
        WithLowering((compiler, lowering, block) => {
            var baseAddress = compiler.gtNewLclvNode(TYP_LONG, 0);
            var index = compiler.gtNewLclvNode(TYP_LONG, 1);
            var amount = compiler.gtNewIconNode(TYP_INT, shiftBy);
            var shift = new GenTreeOp(GT_LSH, TYP_LONG, index, amount);
            var original = new GenTreeOp(GT_ADD, TYP_LONG, baseAddress, shift);
            var indir = new GenTreeIndir(GT_IND, type, original) { IsUnusedValue = true };
            Append(block, baseAddress, index, amount, shift, original, indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.True);
            var mode = address.AsAddrMode();
            Assert.That(mode.BaseAddress, Is.SameAs(baseAddress));
            Assert.That(mode.Index, Is.SameAs(folded ? index : shift));
            Assert.That(mode.Scale, Is.EqualTo(folded ? 1 << shiftBy : 1));
            Assert.That(mode.Offset, Is.Zero);
            Assert.That(indir.Addr, Is.SameAs(mode));
            Assert.That(original.Next, Is.Null);
            Assert.That(shift.Next, folded ? Is.Null : Is.SameAs(mode));
#if DEBUG
            Assert.That(mode.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, -256, false)]
    [TestCase(false, 255, false)]
    [TestCase(true, -257, false)]
    [TestCase(true, -256, true)]
    [TestCase(true, 255, true)]
    [TestCase(true, 256, false)]
    public static void VolatileAddressRequiresRcpc2AndSignedNineBitOffset(bool rcpc2, int offset, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            if (rcpc2)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Rcpc2);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Rcpc2);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Rcpc2);
            }

            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var displacement = compiler.gtNewIconNode(TYP_LONG, offset);
            var original = new GenTreeOp(GT_ADD, TYP_LONG, local, displacement);
            var indir = new GenTreeIndir(GT_IND, TYP_LONG, original) { IsUnusedValue = true };
            indir.Flags |= GTF_IND_VOLATILE;
            Append(block, local, displacement, original, indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.EqualTo(expected));
            Assert.That(indir.Addr, Is.SameAs(address));
            if (expected)
            {
                Assert.That(address.AsAddrMode().Offset, Is.EqualTo(offset));
                Assert.That(address.AsAddrMode().Index, Is.Null);
                Assert.That(displacement.Next, Is.Null);
            }
            else
            {
                Assert.That(address, Is.SameAs(original));
                Assert.That(original.Prev, Is.SameAs(displacement));
            }
        });
    }

    [TestCase("load", 8, false, true)]
    [TestCase("store", 8, false, true)]
    [TestCase("write-barrier", 8, false, false)]
    [TestCase("null-ref", 8, false, true)]
    [TestCase("stack-ref", 8, false, true)]
    [TestCase("load", -257, false, false)]
    [TestCase("load", 256, false, false)]
    [TestCase("load", 8, true, false)]
    public static void Rcpc2IsReportedOnlyForUsableVolatileAddressModes(
        string operation, int offset, bool modified, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.notifyInstructionSetUsage =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&Notify;
            InstructionSetEE ee = new() { Interface = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compCompHnd = &ee.Interface;
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Rcpc2);
            compiler.lvaTable[0].Type = TYP_BYREF;
            var local = compiler.gtNewLclvNode(TYP_BYREF, 0);
            block.InsertAtEnd(local);
            if (modified)
            {
                compiler.lvaTable[1].Type = TYP_BYREF;
                var value = compiler.gtNewLclvNode(TYP_BYREF, 1);
                Append(block, value, compiler.gtNewStoreLclVarNode(0, value));
            }

            var displacement = compiler.gtNewIconNode(TYP_I_IMPL, offset);
            var original = new GenTreeOp(GT_ADD, TYP_BYREF, local, displacement);
            Append(block, displacement, original);
            GenTreeIndir parent;
            if (operation == "load")
            {
                parent = new GenTreeIndir(GT_IND, TYP_LONG, original) { IsUnusedValue = true };
            }
            else
            {
                var type = operation == "store" ? TYP_LONG : TYP_REF;
                compiler.lvaTable[1].Type = type;
                var value = operation == "null-ref"
                    ? compiler.gtNewZeroConNode(TYP_REF)
                    : compiler.gtNewLclvNode(type, 1);
                block.InsertAtEnd(value);
                parent = new GenTreeStoreInd(type, original, value);
                if (operation == "stack-ref")
                {
                    parent.Flags |= GTF_IND_TGT_NOT_HEAP;
                }
            }

            parent.Flags |= GTF_IND_VOLATILE;
            block.InsertAtEnd(parent);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, parent), Is.EqualTo(expected));
            Assert.That(ee.Count, Is.EqualTo(expected ? 1 : 0));
            Assert.That(compiler.opts.compSupportsISAReported.HasInstructionSet(InstructionSet_Rcpc2),
                Is.EqualTo(expected));
            Assert.That(parent.Addr, Is.SameAs(address));
            Assert.That(address.Oper, Is.EqualTo(expected ? GT_LEA : GT_ADD));
            if (expected)
            {
                Assert.That(ee.InstructionSet, Is.EqualTo(InstructionSet_Rcpc2));
            }
        });
    }

    private struct InstructionSetEE
    {
        public ICorJitInfo Interface;
        public CORINFO_InstructionSet InstructionSet;
        public int Count;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Notify(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported, byte preserve)
    {
        var ee = (InstructionSetEE*)self;
        ee->Count++;
        ee->InstructionSet = isa;

        return supported;
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public static void AddressReplacementRejectsModifiedLeavesAndOverflow(bool modified, bool overflow, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            block.InsertAtEnd(local);
            if (modified)
            {
                var value = compiler.gtNewIconNode(TYP_LONG, 24);
                Append(block, value, compiler.gtNewStoreLclVarNode(0, value));
            }

            var offset = compiler.gtNewIconNode(TYP_LONG, 8);
            var original = new GenTreeOp(GT_ADD, TYP_LONG, local, offset);
            original.Flags |= overflow ? GTF_OVERFLOW : GTF_EMPTY;
            var indir = new GenTreeIndir(GT_IND, TYP_LONG, original) { IsUnusedValue = true };
            Append(block, offset, original, indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.EqualTo(expected));
            Assert.That(indir.Addr, Is.SameAs(address));
            Assert.That(address.Oper, Is.EqualTo(expected ? GT_LEA : GT_ADD));
        });
    }

    [TestCase(TYP_BYTE, false, false, true)]
    [TestCase(TYP_BYTE, false, true, false)]
    [TestCase(TYP_INT, false, false, false)]
    [TestCase(TYP_INT, true, false, true)]
    [TestCase(TYP_INT, true, true, false)]
    [TestCase(TYP_LONG, true, false, false)]
    public static void ExtendedIndexContainmentRequiresWidthAndParentInvariance(
        var_types type, bool bfiz, bool modified, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            compiler.lvaTable[1].Type = TYP_INT;
            var baseAddress = compiler.gtNewLclvNode(TYP_LONG, 0);
            var localIndex = compiler.gtNewLclvNode(TYP_INT, 1);
            localIndex.IsContained = true;
            var cast = new GenTreeCast(TYP_LONG, localIndex, false, TYP_LONG);
            GenTree index = cast;
            Append(block, baseAddress, localIndex, cast);
            if (bfiz)
            {
                cast.IsContained = true;
                var shift = compiler.gtNewIconNode(TYP_INT, 2);
                index = new GenTreeOp(GT_BFIZ, TYP_LONG, cast, shift);
                Append(block, shift, index);
            }
            var original = new GenTreeOp(GT_ADD, TYP_LONG, baseAddress, index);
            block.InsertAtEnd(original);
            if (modified)
            {
                var value = compiler.gtNewIconNode(TYP_INT, 24);
                Append(block, value, compiler.gtNewStoreLclVarNode(1, value));
            }

            var indir = new GenTreeIndir(GT_IND, type, original) { IsUnusedValue = true };
            block.InsertAtEnd(indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.True);
            Assert.That(address.AsAddrMode().Index, Is.SameAs(index));
            Assert.That(index.IsContained, Is.EqualTo(expected));
            if (!bfiz)
            {
                Assert.That(localIndex.IsContained, Is.EqualTo(!expected));
            }
        });
    }

    [TestCase(TYP_SIMD, true, false)]
    [TestCase(TYP_MASK, true, false)]
    [TestCase(TYP_LONG, false, false)]
    [TestCase(TYP_LONG, true, true)]
    public static void AddressModesRespectScalableAndContainabilityRestrictions(
        var_types type, bool containable, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            var offset = compiler.gtNewIconNode(TYP_LONG, 8);
            var original = new GenTreeOp(GT_ADD, TYP_LONG, local, offset);
            var indir = new GenTreeIndir(GT_IND, type, original) { IsUnusedValue = true };
            Append(block, local, offset, original, indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, containable, indir), Is.EqualTo(expected));
            Assert.That(indir.Addr, Is.SameAs(address));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Rcpc2StillRejectsRegisterIndices(bool scaled)
    {
        WithLowering((compiler, lowering, block) => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Rcpc2);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Rcpc2);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Rcpc2);
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            GenTree index = compiler.gtNewLclvNode(TYP_LONG, 1);
            Append(block, local, index);
            if (scaled)
            {
                var amount = compiler.gtNewIconNode(TYP_INT, 3);
                index = new GenTreeOp(GT_LSH, TYP_LONG, index, amount);
                Append(block, amount, index);
            }

            var original = new GenTreeOp(GT_ADD, TYP_LONG, local, index);
            var indir = new GenTreeIndir(GT_IND, TYP_LONG, original) { IsUnusedValue = true };
            indir.Flags |= GTF_IND_VOLATILE;
            Append(block, original, indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.False);
            Assert.That(indir.Addr, Is.SameAs(original));
            Assert.That(address, Is.SameAs(original));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void LeaContainmentChecksAddressInterference(bool modified, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var local = compiler.gtNewLclvNode(TYP_LONG, 0);
            local.IsContained = true;
            var address = new GenTreeAddrMode(TYP_LONG, local, null, 1, 8);
            Append(block, local, address);
            if (modified)
            {
                var value = compiler.gtNewIconNode(TYP_LONG, 24);
                Append(block, value, compiler.gtNewStoreLclVarNode(0, value));
            }

            var indir = new GenTreeIndir(GT_IND, TYP_LONG, address) { IsUnusedValue = true };
            block.InsertAtEnd(indir);

            ContainCheckIndir(lowering, indir);

            Assert.That(address.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_LONG, GT_IND, false, true)]
    [TestCase(TYP_LONG, GT_NULLCHECK, false, false)]
    [TestCase(TYP_SIMD12, GT_IND, false, false)]
    [TestCase(TYP_LONG, GT_IND, true, true)]
    [TestCase(TYP_SIMD12, GT_IND, true, false)]
    public static void AddressContainmentPreservesStackProbeAndSimdRestrictions(
        var_types type, genTreeOps oper, bool tls, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            GenTree address = tls
                ? new GenTreeIntCon(TYP_LONG, 8) { Flags = GTF_ICON_TLS_HDL }
                : new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            var indir = new GenTreeIndir(oper, type, address) { IsUnusedValue = oper is GT_IND };
            Append(block, address, indir);

            ContainCheckIndir(lowering, indir);

            Assert.That(address.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    public static void StoresContainIntegerZeroAndAddressIndependently(int value, bool contained)
    {
        WithLowering((compiler, lowering, block) => {
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            var data = compiler.gtNewIconNode(TYP_LONG, value);
            var store = new GenTreeStoreInd(TYP_LONG, address, data);
            Append(block, address, data, store);

            ContainCheckStoreIndir(lowering, store);

            Assert.That(data.IsContained, Is.EqualTo(contained));
            Assert.That(address.IsContained, Is.True);
        });
    }

    private static void Append(BasicBlock block, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryCreateAddrMode")]
    private static extern bool TryCreateAddrMode(Lowering lowering, ref GenTree addr, bool isContainable, GenTree parent);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckIndir")]
    private static extern void ContainCheckIndir(Lowering lowering, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckStoreIndir")]
    private static extern void ContainCheckStoreIndir(Lowering lowering, GenTreeStoreInd indir);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_LONG;
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif

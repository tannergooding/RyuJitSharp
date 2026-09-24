// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AddressLoweringTests
{
    private static CorInfoReloc s_relocHint;

    [TestCase(false)]
    [TestCase(true)]
    public static void AddressModesReplaceOwnersAndRetainLogicalIdentity(bool includeBase)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var baseAddress = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var index = compiler.gtNewLclvNode(var_types.TYP_LONG, 1);
            var shiftAmount = compiler.gtNewIconNode(var_types.TYP_INT, 2);
            var shift = new GenTreeOp(genTreeOps.GT_LSH, var_types.TYP_LONG, index, shiftAmount);
            var inner = includeBase ? new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_LONG, baseAddress, shift) : shift;
            var displacement = compiler.gtNewIconNode(var_types.TYP_LONG, 16);
            var original = new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_LONG, inner, displacement);
            var indir = new GenTreeIndir(genTreeOps.GT_IND, var_types.TYP_INT, original) { IsUnusedValue = true };
            if (includeBase)
            {
                block.InsertAtEnd(baseAddress);
            }
            block.InsertAtEnd(index);
            block.InsertAtEnd(shiftAmount);
            block.InsertAtEnd(shift);
            if (includeBase)
            {
                block.InsertAtEnd(inner);
            }
            block.InsertAtEnd(displacement);
            block.InsertAtEnd(original);
            block.InsertAtEnd(indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.True);

            var mode = address.AsAddrMode();
            Assert.That(indir.Addr, Is.SameAs(mode));
            Assert.That(mode.BaseAddress, includeBase ? Is.SameAs(baseAddress) : Is.Null);
            Assert.That(mode.Index, Is.SameAs(index));
            Assert.That(mode.Scale, Is.EqualTo(4));
            Assert.That(mode.Offset, Is.EqualTo(16));
            Assert.That(mode.Prev, Is.SameAs(index));
            Assert.That(mode.Next, Is.SameAs(indir));
            Assert.That(indir.Prev, Is.SameAs(mode));
            Assert.That(original.Next, Is.Null);
            Assert.That(original.Prev, Is.Null);
            Assert.That(shift.Next, Is.Null);
            Assert.That(shiftAmount.Next, Is.Null);
            Assert.That(displacement.Next, Is.Null);
#if DEBUG
            Assert.That(mode.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public static void AddressFoldingRejectsInterferenceAndCheckedAdds(bool modifiedLocal, bool overflow, bool expected)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var local = compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            block.InsertAtEnd(local);
            if (modifiedLocal)
            {
                var value = compiler.gtNewIconNode(var_types.TYP_LONG, 24);
                var store = compiler.gtNewStoreLclVarNode(0, value);
                block.InsertAtEnd(value);
                block.InsertAtEnd(store);
            }
            var offset = compiler.gtNewIconNode(var_types.TYP_LONG, 4);
            var original = new GenTreeOp(genTreeOps.GT_ADD, var_types.TYP_LONG, local, offset);
            if (overflow)
            {
                original.Flags |= GenTreeFlags.GTF_OVERFLOW;
            }
            var indir = new GenTreeIndir(genTreeOps.GT_IND, var_types.TYP_INT, original) { IsUnusedValue = true };
            block.InsertAtEnd(offset);
            block.InsertAtEnd(original);
            block.InsertAtEnd(indir);
            GenTree address = original;

            Assert.That(TryCreateAddrMode(lowering, ref address, true, indir), Is.EqualTo(expected));
            Assert.That(indir.Addr, Is.SameAs(address));
            if (!expected)
            {
                Assert.That(address, Is.SameAs(original));
                Assert.That(original.Prev, Is.SameAs(offset));
                Assert.That(original.Next, Is.SameAs(indir));
            }
        });
    }

    [TestCase(1, 0)]
    [TestCase(3, 0)]
    [TestCase(3, 1)]
    [TestCase(3, 2)]
    public static void ReplacingUnusedLirNodesUpdatesRangeBoundaries(int count, int index)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var nodes = new GenTree[count];
            for (var i = 0; i < count; i++)
            {
                nodes[i] = compiler.gtNewIconNode(var_types.TYP_INT, i);
                nodes[i].IsUnusedValue = true;
                block.InsertAtEnd(nodes[i]);
            }
            var replacement = compiler.gtNewIconNode(var_types.TYP_INT, 42);
            replacement.IsUnusedValue = true;
            var original = nodes[index];

            block.ReplaceNode(original, replacement);
            nodes[index] = replacement;

            Assert.That(block.FirstNode, Is.SameAs(nodes[0]));
            Assert.That(block.LastNode, Is.SameAs(nodes[^1]));
            for (var i = 0; i < count; i++)
            {
                Assert.That(nodes[i].Prev, i == 0 ? Is.Null : Is.SameAs(nodes[i - 1]));
                Assert.That(nodes[i].Next, i == count - 1 ? Is.Null : Is.SameAs(nodes[i + 1]));
            }
            Assert.That(original.Prev, Is.Null);
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, false, CorInfoReloc.NONE, 2147483647L, true, false)]
    [TestCase(false, false, CorInfoReloc.NONE, 2147483648L, false, false)]
    [TestCase(false, false, CorInfoReloc.RELATIVE32, 2147483648L, true, true)]
    [TestCase(true, false, CorInfoReloc.RELATIVE32, 1L, false, false)]
    [TestCase(true, true, CorInfoReloc.RELATIVE32, 2147483648L, true, true)]
    [TestCase(true, true, CorInfoReloc.NONE, 1L, false, false)]
    public static void AbsoluteAddressEncodingRespectsRelocationHints(
        bool relocatable, bool handle, CorInfoReloc hint, long address, bool fits, bool needsRelocation)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            compiler.opts.compReloc = relocatable;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            s_relocHint = hint;
            var constant = compiler.gtNewIconNode(var_types.TYP_LONG, (nint)address);
            if (handle)
            {
                constant.Flags |= Globals.GTF_ICON_CONST_PTR;
            }

            Assert.That(constant.FitsInAddrBase(compiler), Is.EqualTo(fits));
            Assert.That(constant.AddrNeedsReloc(compiler), Is.EqualTo(needsRelocation));
            compiler.info.compMatchedVM = false;
            Assert.That(compiler.eeGetRelocTypeHint((void*)(nint)address), Is.EqualTo(CorInfoReloc.NONE));
        });
    }

    [TestCase(var_types.TYP_LONG, genTreeOps.GT_IND, false, var_types.TYP_INT)]
    [TestCase(var_types.TYP_LONG, genTreeOps.GT_NULLCHECK, true, var_types.TYP_INT)]
    [TestCase(var_types.TYP_UBYTE, genTreeOps.GT_IND, false, var_types.TYP_BYTE)]
    [TestCase(var_types.TYP_SIMD12, genTreeOps.GT_IND, false, var_types.TYP_BYTE)]
    public static void UnusedLoadsPreserveProbeWidthAndLirSuccessors(
        var_types type, genTreeOps oper, bool containedAddress, var_types probeType)
    {
        WithCompiler(compiler => {
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            GenTree address = containedAddress
                ? compiler.gtNewIconNode(var_types.TYP_LONG, 0x1234)
                : compiler.gtNewLclvNode(var_types.TYP_LONG, 0);
            var original = new GenTreeIndir(oper, type, address) { IsUnusedValue = oper is genTreeOps.GT_IND };
            original.Flags |= GenTreeFlags.GTF_DONT_EXTEND | GenTreeFlags.GTF_GLOB_REF;
            original.Flags |= containedAddress ? GenTreeFlags.GTF_IND_NONFAULTING : GenTreeFlags.GTF_EXCEPT;
            var successor = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            block.InsertAtEnd(address);
            block.InsertAtEnd(original);
            block.InsertAtEnd(successor);

            Assert.That(LowerIndir(lowering, original), Is.SameAs(successor));

            var result = address.Next ?? throw new InvalidOperationException("The lowered probe is missing.");
            Assert.That(result.Oper, Is.EqualTo(containedAddress ? genTreeOps.GT_IND : genTreeOps.GT_NULLCHECK));
            Assert.That(result.Type, Is.EqualTo(probeType));
            Assert.That(result.AsIndir().Addr, Is.SameAs(address));
            Assert.That(result.IsUnusedValue, Is.EqualTo(containedAddress));
            Assert.That(address.IsContained, Is.EqualTo(containedAddress));
            Assert.That(result.Flags & GenTreeFlags.GTF_DONT_EXTEND, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
            Assert.That(result.Flags & GenTreeFlags.GTF_IND_NONFAULTING,
                Is.EqualTo(containedAddress ? GenTreeFlags.GTF_IND_NONFAULTING : GenTreeFlags.GTF_EMPTY));
            Assert.That(result.Next, Is.SameAs(successor));
            Assert.That(successor.Prev, Is.SameAs(result));
            Assert.That(original.Prev, Is.Null);
            Assert.That(original.Next, Is.Null);
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* target) => s_relocHint;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerIndir")]
    private static extern GenTree? LowerIndir(Lowering lowering, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryCreateAddrMode")]
    private static extern bool TryCreateAddrMode(Lowering lowering, ref GenTree addr, bool isContainable, GenTree parent);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = var_types.TYP_LONG;
        compiler.lvaTable[1].Type = var_types.TYP_LONG;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LoongArchLinearScanIndirTests
{
    [TestCase(0, false, 0)]
    [TestCase(-2048, false, 0)]
    [TestCase(2047, false, 0)]
    [TestCase(2048, false, 1)]
    [TestCase(8, true, 1)]
    [TestCase(0, true, 0)]
    public static void ContainedAddressReservesTemporaryOnlyWhenLoongArchCannotEncodeIt(
        int offset, bool hasIndex, int expectedInternalDefinitions)
    {
        WithAllocator((compiler, allocator) => {
            var baseAddress = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, baseAddress, SRBM_NONE, 0);

            GenTree? index = null;
            if (hasIndex)
            {
                index = compiler.gtNewIconNode(TYP_I_IMPL, 3);
                ReferenceBuildLocation(allocator) = 4;
                _ = BuildDef(allocator, index, SRBM_NONE, 0);
            }

            var address = new GenTreeAddrMode(
                TYP_BYREF,
                baseAddress,
                index,
                hasIndex ? (byte)1 : (byte)0,
                offset)
            {
                IsContained = true,
            };
            var indirection = compiler.gtNewIndir(TYP_INT, address);
            ReferenceBuildLocation(allocator) = hasIndex ? 6u : 4u;

            Assert.That(BuildIndir(allocator, indirection), Is.EqualTo(hasIndex ? 2 : 1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(indirection));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == indirection) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Has.Count.EqualTo(expectedInternalDefinitions));
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void Simd12IndirectionReservesAnInternalRegister()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            ReferenceBuildLocation(allocator) = 2;
            _ = BuildDef(allocator, address, SRBM_NONE, 0);
            var indirection = compiler.gtNewIndir(TYP_SIMD12, address);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildIndir(allocator, indirection), Is.EqualTo(1));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == indirection) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Has.Count.EqualTo(1));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildIndir")]
    private static extern int BuildIndir(LinearScan allocator, GenTreeIndir indirection);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    private static void WithAllocator(Action<Compiler, LinearScan> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;

        var codeGen = new CodeGen(compiler);
        compiler.codeGen = codeGen;
        codeGen.IsFramePointerRequired = false;
        codeGen.IsFramePointerUsed = false;
        codeGen.IsFrameRequired = false;

        try
        {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif

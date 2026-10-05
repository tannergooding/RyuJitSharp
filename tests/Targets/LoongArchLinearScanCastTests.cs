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

internal static unsafe class LoongArchLinearScanCastTests
{
    [Test]
    public static void CastBuildsOnlyItsOperandUseAndDestinationDefinition()
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_LONG, 17);
            ReferenceBuildLocation(allocator) = 2;
            var sourceDefinition = BuildDef(allocator, source, SRBM_NONE, 0);
            var cast = new GenTreeCast(TYP_DOUBLE, source, true, TYP_DOUBLE);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildCast(allocator, cast), Is.EqualTo(1));
            Assert.That(sourceDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(cast));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.FindAll(reference =>
                (reference.treeNode == cast) &&
                (reference.refType is RefType.RefTypeDef) &&
                reference.getInterval().isInternal), Is.Empty);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildCast")]
    private static extern int BuildCast(LinearScan allocator, GenTreeCast cast);

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

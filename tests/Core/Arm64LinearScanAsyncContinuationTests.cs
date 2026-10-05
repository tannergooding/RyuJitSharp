// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm64LinearScanAsyncContinuationTests
{
    [Test]
    public static void AsyncContinuationUsesItsFixedReturnRegister()
    {
        Arm64LinearScanConstructionTests.WithCompiler(false, false, (compiler, _) => {
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            var continuation = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF);

            Assert.That(BuildNode(allocator, continuation), Is.Zero);

            var definition = allocator.refPositions.Find(position =>
                (position.treeNode == continuation) && (position.refType is RefType.RefTypeDef))
                ?? throw new AssertionException("The async continuation has no register definition.");
            Assert.That(definition.registerAssignment,
                Is.EqualTo(genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRiscVStackArgumentTests
{
    [Test]
    public static void StructBlockArgumentReservesTwoIntegerTempsAndConsumesItsAddress()
    {
        WithAllocator((compiler, allocator) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x2000);
            ReferenceBuildLocation(allocator) = 2;
            var addressDefinition = BuildDef(allocator, address, SRBM_NONE, 0);
            var block = new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(16)) { IsContained = true };
            var argument = new GenTreePutArgStk(TYP_VOID, block, null, 0, 16, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgStk(allocator, argument), Is.EqualTo(1));
            Assert.That(addressDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));

            var definitions = InternalDefinitions(allocator, argument);
            Assert.That(definitions, Has.Count.EqualTo(2));
            foreach (var definition in definitions)
            {
                Assert.That(definition.getInterval().registerType, Is.EqualTo(TYP_INT));
                Assert.That(definition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            }
        });
    }

    [Test]
    public static void FieldListArgumentConsumesEachFieldWithoutInternalTemps()
    {
        WithAllocator((compiler, allocator) =>
        {
            var first = compiler.gtNewIconNode(TYP_INT, 11);
            var second = compiler.gtNewIconNode(TYP_INT, 23);
            ReferenceBuildLocation(allocator) = 2;
            var firstDefinition = BuildDef(allocator, first, SRBM_NONE, 0);
            var secondDefinition = BuildDef(allocator, second, SRBM_NONE, 0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, TYP_INT);
            fields.AddFieldLIR(compiler, second, 8, TYP_INT);
            var argument = new GenTreePutArgStk(TYP_VOID, fields, null, 0, 16, false);
            ReferenceBuildLocation(allocator) = 4;

            Assert.That(BuildPutArgStk(allocator, argument), Is.EqualTo(2));
            Assert.That(firstDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(secondDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(InternalDefinitions(allocator, argument), Is.Empty);
        });
    }

    private static System.Collections.Generic.List<RefPosition> InternalDefinitions(
        LinearScan allocator, GenTree tree)
        => allocator.refPositions.FindAll(reference =>
            reference.treeNode == tree &&
            reference.refType is RefType.RefTypeDef &&
            reference.getInterval().isInternal);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildDef")]
    private static extern RefPosition BuildDef(LinearScan allocator, GenTree tree, regMask candidates, int multiRegIndex);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPutArgStk")]
    private static extern int BuildPutArgStk(LinearScan allocator, GenTreePutArgStk argument);

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

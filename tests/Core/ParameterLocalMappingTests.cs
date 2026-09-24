// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ParameterLocalMappingTests
{
    [TestCase(false, false, true)]
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    public static void OnlyIndependentRegisterParametersCreateMappings(bool promoted, bool dependent, bool register)
    {
        WithCompiler(compiler => {
            ref var parameter = ref compiler.lvaTable[0];
            parameter.lvPromoted = promoted;
            parameter.lvDoNotEnregister = dependent;
            compiler.lvaParameterPassingInfo[0] = AbiPassingInformation.FromSegment(compiler, false, register
                ? AbiPassingSegment.InRegister(REG_RCX, 0, 8)
                : AbiPassingSegment.OnStack(0, 0, 8));
            var existing = new ParameterRegisterLocalMapping(AbiPassingSegment.InRegister(REG_RDX, 0, 8), 2, 0);
            compiler._paramRegLocalMappings = [existing];
            var mappings = compiler._paramRegLocalMappings;

            MapParameters(new Lowering(compiler, new LinearScan(compiler)));

            var mapped = promoted && !dependent && register;
            Assert.That(compiler._paramRegLocalMappings, Is.SameAs(mappings));
            Assert.That(mappings.Count, Is.EqualTo(mapped ? 2 : 1));
            Assert.That(mappings[0].LclNum, Is.EqualTo(existing.LclNum));
            Assert.That(mappings[0].RegisterSegment.Register, Is.EqualTo(REG_RDX));
            Assert.That(compiler.lvaTable[1].lvIsParamRegTarget, Is.EqualTo(mapped));
            if (mapped)
            {
                Assert.That(mappings[1].LclNum, Is.EqualTo(1));
                Assert.That(mappings[1].Offset, Is.Zero);
                Assert.That(mappings[1].RegisterSegment.Register, Is.EqualTo(REG_RCX));
            }
        });
    }

    [Test]
    public static void SegmentOverlapPreservesOrderAndNativeUnsignedOffsets()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].lvPromoted = true;
            compiler.lvaTable[0].lvFieldCnt = 2;
            compiler.lvaTable[1].lvFldOffset = 8;
            compiler.lvaTable[2].Type = TYP_LONG;
            compiler.lvaTable[2].lvFldOffset = 16;
            var abiInfo = new AbiPassingInformation(4);
            abiInfo.Segments[0] = AbiPassingSegment.InRegister(REG_RCX, 0, 8);
            abiInfo.Segments[1] = AbiPassingSegment.InRegister(REG_RDX, 4, 8);
            abiInfo.Segments[2] = AbiPassingSegment.InRegister(REG_R8, 8, 8);
            abiInfo.Segments[3] = AbiPassingSegment.InRegister(REG_R9, 16, 8);
            compiler.lvaParameterPassingInfo[0] = abiInfo;

            MapParameters(new Lowering(compiler, new LinearScan(compiler)));

            var mappings = compiler._paramRegLocalMappings
                ?? throw new AssertionException("Parameter mappings were not initialized.");
            Assert.That(mappings.Count, Is.EqualTo(3));
            Assert.That(mappings[0].RegisterSegment.Register, Is.EqualTo(REG_RDX));
            Assert.That(mappings[0].LclNum, Is.EqualTo(1));
            Assert.That(mappings[0].Offset, Is.EqualTo(unchecked((uint)-4)));
            Assert.That(mappings[1].RegisterSegment.Register, Is.EqualTo(REG_R8));
            Assert.That(mappings[1].LclNum, Is.EqualTo(1));
            Assert.That(mappings[1].Offset, Is.Zero);
            Assert.That(mappings[2].RegisterSegment.Register, Is.EqualTo(REG_R9));
            Assert.That(mappings[2].LclNum, Is.EqualTo(2));
            Assert.That(mappings[2].Offset, Is.Zero);
            Assert.That(compiler.lvaTable[1].lvIsParamRegTarget, Is.True);
            Assert.That(compiler.lvaTable[2].lvIsParamRegTarget, Is.True);
        });
    }

    [Test]
    public static void NoParametersStillInitializeAnEmptyMappingList()
    {
        WithCompiler(compiler => {
            compiler.info.compArgsCount = 0;

            MapParameters(new Lowering(compiler, new LinearScan(compiler)));

            Assert.That(compiler._paramRegLocalMappings, Is.Not.Null.And.Empty);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MapParameterRegisterLocals")]
    private static extern void MapParameters(Lowering lowering);

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
        compiler.info.compArgsCount = 1;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_STRUCT, lvIsParam = true, lvFieldLclStart = 1, lvFieldCnt = 1 },
            new LclVarDsc { Type = TYP_LONG },
            new LclVarDsc { Type = TYP_LONG },
        ];
        compiler.lvaCount = compiler.lvaTable.Length;
        compiler.lvaParameterPassingInfo = new AbiPassingInformation[1];
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

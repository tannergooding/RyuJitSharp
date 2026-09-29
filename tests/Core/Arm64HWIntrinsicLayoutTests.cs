// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_HW_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64HWIntrinsicLayoutTests
{
    [TestCase(NI_AdvSimd_Arm64_LoadPairScalarVector64, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairScalarVector64NonTemporal, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector64, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector64NonTemporal, 16)]
    [TestCase(NI_AdvSimd_Load2xVector64AndUnzip, 16)]
    [TestCase(NI_AdvSimd_Load2xVector64, 16)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x2, 16)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x2, 16)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector128, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadPairVector128NonTemporal, 32)]
    [TestCase(NI_AdvSimd_Arm64_Load2xVector128AndUnzip, 32)]
    [TestCase(NI_AdvSimd_Arm64_Load2xVector128, 32)]
    [TestCase(NI_AdvSimd_Load4xVector64, 32)]
    [TestCase(NI_AdvSimd_Load4xVector64AndUnzip, 32)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x4, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x2, 32)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x2, 32)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x4, 32)]
    [TestCase(NI_AdvSimd_Load3xVector64AndUnzip, 24)]
    [TestCase(NI_AdvSimd_Load3xVector64, 24)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x3, 24)]
    [TestCase(NI_AdvSimd_LoadAndReplicateToVector64x3, 24)]
    [TestCase(NI_AdvSimd_Arm64_Load3xVector128AndUnzip, 48)]
    [TestCase(NI_AdvSimd_Arm64_Load3xVector128, 48)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x3, 48)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x3, 48)]
    [TestCase(NI_AdvSimd_Arm64_Load4xVector128AndUnzip, 64)]
    [TestCase(NI_AdvSimd_Arm64_Load4xVector128, 64)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4, 64)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndReplicateToVector128x4, 64)]
    public static void FixedAggregateLayoutsMatchNativeSizes(NamedIntrinsic intrinsic, int size)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            var node = NewIntrinsic(intrinsic);
            var expected = compiler.typGetBlkLayout(size);

            Assert.That(node.GetLayout(compiler), Is.SameAs(expected));
            Assert.That(((GenTree)node).GetLayout(compiler), Is.SameAs(expected));
            Assert.That(expected.Size, Is.EqualTo(size));
        });
    }

    [TestCase(NI_Sve_Load2xVectorAndUnzip, 2)]
    [TestCase(NI_Sve_Load3xVectorAndUnzip, 3)]
    [TestCase(NI_Sve_Load4xVectorAndUnzip, 4)]
    public static void SveAggregateLayoutsUseCompileTimeVectorLength(NamedIntrinsic intrinsic, int vectors)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT128);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_VectorT128);
            var node = NewIntrinsic(intrinsic);

            Assert.That(compiler.getRuntimeVectorTByteLength(), Is.EqualTo(16u));
            Assert.That(node.GetLayout(compiler).Size, Is.EqualTo((uint)(16 * vectors)));
        });
    }

    [Test]
    public static void RuntimeLengthUsesFixedArm64FallbackWithoutVectorIsa()
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT128);

            Assert.That(compiler.getRuntimeVectorTByteLength(), Is.EqualTo(16u));
        });
    }

#if DEBUG
    [TestCase(NI_Sve_Load2xVectorAndUnzip, 2, 32)]
    [TestCase(NI_Sve_Load3xVectorAndUnzip, 3, 48)]
    [TestCase(NI_Sve_Load4xVectorAndUnzip, 4, 64)]
    public static void SveAggregateLayoutsResolveRuntimeVectorLength(
        NamedIntrinsic intrinsic, int vectors, int byteLength)
    {
        CSELiveAcrossCallCostTests.WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getBuiltinClass = &GetVectorClass;
            vtable.Base.Base.getClassSize = &GetVectorClassSize;
            VectorLengthState state = new() {
                Info = new ICorJitInfo { lpVtbl = &vtable },
                ByteLength = byteLength,
            };
            compiler.info.compCompHnd = &state.Info;
            Arm64ScalableMaskValueNumTests.SetScalableConfiguration(true);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_VectorT);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_VectorT);
            var node = NewIntrinsic(intrinsic);

            Assert.That(node.GetLayout(compiler).Size, Is.EqualTo((uint)(byteLength * vectors)));
            Assert.That(state.BuiltinClassRequests, Is.EqualTo(1));
            Assert.That(state.ClassSizeRequests, Is.EqualTo(1));
        });
    }

    private struct VectorLengthState
    {
        public ICorJitInfo Info;
        public int ByteLength;
        public int BuiltinClassRequests;
        public int ClassSizeRequests;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetVectorClass(ICorJitInfo* info, CorInfoClassId id)
    {
        var state = (VectorLengthState*)info;
        state->BuiltinClassRequests++;
        return id is CorInfoClassId.CLASSID_NUMERICS_VECTORT ? (CORINFO_CLASS_STRUCT_*)0x1234 : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetVectorClassSize(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls)
    {
        var state = (VectorLengthState*)info;
        state->ClassSizeRequests++;
        return cls == (CORINFO_CLASS_STRUCT_*)0x1234 ? state->ByteLength : 0;
    }
#endif

    private static GenTreeHWIntrinsic NewIntrinsic(NamedIntrinsic intrinsic)
    {
        var operands = new GenTree[HWIntrinsicInfo.lookupNumArgs(intrinsic)];
        for (var index = 0; index < operands.Length; index++)
        {
            operands[index] = new GenTreeLclVar(TYP_INT, index);
        }

        return new GenTreeHWIntrinsic(TYP_STRUCT, intrinsic, TYP_INT, 0, operands);
    }
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.HWIntrinsicFlag;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class FeatureConfigurationTests
{
    [Test]
    public static void MultiRegisterReturnFeatureMatchesTheTargetArchitecture()
    {
        var supportsMultiRegRet = Configuration.TARGET_ARMARCH ||
            Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64 ||
            (Configuration.TARGET_XARCH && (!Configuration.TARGET_64BIT || Configuration.TARGET_UNIX));

        Assert.That(Configuration.FEATURE_MULTIREG_RET, Is.EqualTo(supportsMultiRegRet));
    }

    [Test]
    public static void LongReturnDescriptorUsesTheConfiguredRegisterWidth()
    {
        var descriptor = new ReturnTypeDesc();
        descriptor.InitializeLongReturnType();

        var returnsLongInMultipleRegisters = Configuration.FEATURE_MULTIREG_RET &&
            (Configuration.TARGET_X86 || Configuration.TARGET_ARM);

        Assert.That(descriptor.IsMultiRegRetType, Is.EqualTo(returnsLongInMultipleRegisters));
    }

    [Test]
    public static void ArgumentAndSwiftFeaturesMatchTheTargetArchitecture()
    {
        var supportsArgSplit = Configuration.TARGET_ARMARCH ||
            Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64;
        var supportsMultiRegArgs = supportsArgSplit ||
            (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX);
        var supportsFixedOutArgs = supportsArgSplit || Configuration.TARGET_AMD64;
        var supportsFastTailCall = supportsFixedOutArgs || Configuration.TARGET_WASM;
        var supportsTailCallOpt = supportsArgSplit || Configuration.TARGET_AMD64;
        var supportsSwift = Configuration.TARGET_ARM64 ||
            (Configuration.TARGET_AMD64 && Configuration.TARGET_UNIX);

        Assert.That(Configuration.FEATURE_ARG_SPLIT, Is.EqualTo(supportsArgSplit));
        Assert.That(Configuration.FEATURE_MULTIREG_ARGS, Is.EqualTo(supportsMultiRegArgs));
        Assert.That(Configuration.FEATURE_FIXED_OUT_ARGS, Is.EqualTo(supportsFixedOutArgs));
        Assert.That(Configuration.FEATURE_FASTTAILCALL, Is.EqualTo(supportsFastTailCall));
        Assert.That(Configuration.FEATURE_TAILCALL_OPT, Is.EqualTo(supportsTailCallOpt));
        Assert.That(Configuration.SWIFT_SUPPORT, Is.EqualTo(supportsSwift));
    }

    [Test]
    public static void CallTailCallMetadataMatchesTheTargetArchitecture()
    {
        WithCompiler(compiler =>
        {
            var call = new GenTreeCall(TYP_VOID)
            {
                _callMoreFlags =
                    GenTreeCallFlags.GTF_CALL_M_TAILCALL |
                    GenTreeCallFlags.GTF_CALL_M_IMPLICIT_TAILCALL |
                    GenTreeCallFlags.GTF_CALL_M_TAILCALL_TO_LOOP,
            };
            var supportsFastTailCall = Configuration.FEATURE_FASTTAILCALL || Configuration.TARGET_WASM;

            Assert.That(call.GetNonStandardAddedArgCount(compiler),
                Is.EqualTo(Configuration.TARGET_WASM ? 2 : 0));
            Assert.That(call.IsFastTailCall, Is.EqualTo(supportsFastTailCall));
            Assert.That(call.IsImplicitTailCall, Is.EqualTo(Configuration.FEATURE_TAILCALL_OPT));
            Assert.That(call.IsTailCallConvertibleToLoop, Is.EqualTo(Configuration.FEATURE_TAILCALL_OPT));
            Assert.That(call.IsTailCallViaJitHelper, Is.False);

            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL_VIA_JIT_HELPER;

            Assert.That(call.IsTailCallViaJitHelper, Is.EqualTo(Configuration.TARGET_X86));
            Assert.That(call.IsFastTailCall,
                Is.EqualTo(supportsFastTailCall && !Configuration.TARGET_X86));
        });
    }

    [Test]
    public static void CallSignatureMetadataSurvivesTreeCloning()
    {
        WithCompiler(compiler =>
        {
            var callWithoutSignature = new GenTreeCall(TYP_VOID);
            Assert.That(callWithoutSignature.CallSigInfo.Value.numArgs, Is.Zero);

            var callSig = new StrongBox<CORINFO_SIG_INFO>(default);
            var call = new GenTreeCall(TYP_VOID)
            {
                _callSig = callSig,
            };

            var clone = compiler.gtCloneExpr(call).AsCall();

            Assert.That(clone._callSig, Is.SameAs(callSig));
        });
    }

    [Test]
    public static void CustomCallArgumentRegistersMatchTheTargetArchitecture()
    {
        WithCompiler(compiler =>
        {
            Assert.That(CallArgs.GetCustomRegister(compiler, default, WellKnownArg.PInvokeFrame, out _),
                Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET &&
                    (Configuration.TARGET_X86 || Configuration.TARGET_ARM)));
            Assert.That(CallArgs.GetCustomRegister(compiler, default, WellKnownArg.ShiftLow, out _),
                Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET && Configuration.TARGET_X86));
            Assert.That(CallArgs.GetCustomRegister(compiler, default, WellKnownArg.R2RIndirectionCell, out _),
                Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET));
            Assert.That(CallArgs.GetCustomRegister(compiler, default, WellKnownArg.ValidateIndirectCallTarget, out _),
                Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET &&
                    (REG_VALIDATE_INDIRECT_CALL_ADDR != REG_ARG_0)));
            Assert.That(CallArgs.GetCustomRegister(compiler, default, WellKnownArg.DispatchIndirectCallTarget, out _),
                Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET &&
                    (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)));
            Assert.That(CallArgs.GetCustomRegister(compiler, CorInfoCallConvExtension.Swift,
                WellKnownArg.SwiftError, out _), Is.EqualTo(Configuration.HAS_FIXED_REGISTER_SET &&
                    Configuration.SWIFT_SUPPORT));
        });
    }

    [Test]
    public static void MaskedHardwareIntrinsicsMatchTheTargetArchitecture()
    {
        Assert.That(Configuration.FEATURE_MASKED_HW_INTRINSICS,
            Is.EqualTo(Configuration.TARGET_XARCH || Configuration.TARGET_ARM64));
    }

    [Test]
    public static void ReadyToRunFeatureIsAvailableOnEveryTarget()
    {
        Assert.That(Configuration.FEATURE_READYTORUN, Is.True);
    }

    [Test]
    public static unsafe void ReadyToRunIntrinsicEntryPointRoundTrips()
    {
        if (!Configuration.FEATURE_READYTORUN || !Configuration.FEATURE_HW_INTRINSICS)
        {
            return;
        }

        WithCompiler(compiler =>
        {
            var operand = compiler.gtNewIconNode(TYP_INT, 1);
            var entryPoint = new CORINFO_CONST_LOOKUP
            {
                accessType = InfoAccessType.IAT_PVALUE,
                addr = (void*)1,
            };
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, operand)
            {
                MethodHandle = default,
                EntryPoint = entryPoint,
            };
            Assert.That(node.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)node.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));

            var copy = compiler.gtCloneExpr(node).AsHWIntrinsic();
            Assert.That(copy.HWIntrinsicId, Is.EqualTo(node.HWIntrinsicId));
            Assert.That((nint)copy.MethodHandle, Is.EqualTo((nint)node.MethodHandle));
            Assert.That(copy.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)copy.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));
            Assert.That(copy.GetOp(1), Is.Not.SameAs(operand));
        });
    }

    [Test]
    public static unsafe void ReadyToRunTreeEntryPointMetadataRoundTrips()
    {
        if (!Configuration.FEATURE_READYTORUN)
        {
            return;
        }

        WithCompiler(compiler =>
        {
            var operand = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            var entryPoint = new CORINFO_CONST_LOOKUP
            {
                accessType = InfoAccessType.IAT_PVALUE,
                addr = (void*)1,
            };

            var intrinsic = new GenTreeIntrinsic(TYP_INT, operand, NI_Vector_Create, default)
            {
                EntryPoint = entryPoint,
            };
            Assert.That(intrinsic.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)intrinsic.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));
            var intrinsicCopy = compiler.gtCloneExpr(intrinsic).AsIntrinsic();
            Assert.That(intrinsicCopy.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)intrinsicCopy.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));

            var allocation = new GenTreeAllocObj(TYP_REF, operand, default, false, default)
            {
                EntryPoint = entryPoint,
            };
            Assert.That(allocation.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)allocation.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));
            var allocationCopy = compiler.gtCloneExpr(allocation).AsAllocObj();
            Assert.That(allocationCopy.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)allocationCopy.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));

            var functionPointer = new GenTreeFptrVal(TYP_I_IMPL, default)
            {
                EntryPoint = entryPoint,
            };
            Assert.That(functionPointer.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)functionPointer.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));
            var functionPointerCopy = compiler.gtCloneFtnAddr(functionPointer);
            Assert.That(functionPointerCopy.EntryPoint.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)functionPointerCopy.EntryPoint.addr, Is.EqualTo((nint)entryPoint.addr));

            var fieldAddress = new GenTreeFieldAddr(TYP_I_IMPL, null, default, 0)
            {
                FieldLookup = entryPoint,
            };
            Assert.That(fieldAddress.FieldLookup.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)fieldAddress.FieldLookup.addr, Is.EqualTo((nint)entryPoint.addr));
            Assert.That(fieldAddress.IsOffsetKnown, Is.False);
            var fieldAddressCopy = compiler.gtCloneFieldAddr(fieldAddress);
            Assert.That(fieldAddressCopy.FieldLookup.accessType, Is.EqualTo(entryPoint.accessType));
            Assert.That((nint)fieldAddressCopy.FieldLookup.addr, Is.EqualTo((nint)entryPoint.addr));

            var call = new GenTreeCall(TYP_VOID)
            {
                _entryPoint = entryPoint,
            };
            Assert.That(call.IsR2RRelativeIndir, Is.True);
            var callCopy = compiler.gtCloneExpr(call).AsCall();
            Assert.That(callCopy.IsR2RRelativeIndir, Is.True);

            if (Configuration.TARGET_ARMARCH || Configuration.TARGET_RISCV64 || Configuration.TARGET_LOONGARCH64)
            {
                Assert.That(call.IndirectionCellArgKind, Is.EqualTo(WellKnownArg.R2RIndirectionCell));
            }
            else if (Configuration.TARGET_XARCH)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
                var expectedArgumentKind = call.IsFastTailCall ? WellKnownArg.R2RIndirectionCell : WellKnownArg.None;
                Assert.That(call.IndirectionCellArgKind, Is.EqualTo(expectedArgumentKind));
            }
        });
    }

    [Test]
    public static void SimdConstantConstructionMatchesTheConfiguredFeature()
    {
        WithCompiler(compiler =>
        {
            if (Configuration.FEATURE_SIMD)
            {
                _ = new GenTreeVecCon(TYP_SIMD8);
            }
            else
            {
                var exception = Assert.Throws<FatalJitException>(() => new GenTreeVecCon(TYP_SIMD8));
                Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_IMPLLIMITATION));
            }
        });
    }

    [Test]
    public static void HardwareIntrinsicNodeConstructionRequiresHardwareIntrinsicSupport()
    {
        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            return;
        }

        WithCompiler(_ =>
        {
            var exception = Assert.Throws<PlatformNotSupportedException>(
                () => new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16));
            Assert.That(exception, Is.Not.Null);
        });
    }

    [Test]
    public static void HardwareIntrinsicImmediateLookupUsesTheRuntimeTarget()
    {
        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            return;
        }

        WithCompiler(compiler =>
        {
            if (Configuration.TARGET_WASM)
            {
                var vector = new GenTreeVecCon(TYP_SIMD16);
                var immediate = compiler.gtNewIconNode(TYP_INT, 1);
                var node = new GenTreeHWIntrinsic(TYP_INT, NI_PackedSimd_ExtractScalar, TYP_INT, 16, vector, immediate);
                Assert.That(node.GetImmOp(), Is.SameAs(immediate));
                return;
            }

            var scalar = compiler.gtNewIconNode(TYP_INT, 1);
            var scalarNode = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, scalar);
            var exception = Assert.Throws<PlatformNotSupportedException>(() => scalarNode.GetImmOp());
            Assert.That(exception, Is.Not.Null);
        });
    }

    [Test]
    public static void Arm64IntrinsicMetadataRejectsOtherTargets()
    {
        if (Configuration.TARGET_ARM64)
        {
            return;
        }

        var id = NI_Sve_ConditionalExtractAfterLastActiveElement;
        var scalarVariantException = Assert.Throws<PlatformNotSupportedException>(
            () => HWIntrinsicInfo.HasScalarInputVariant(id));
        Assert.That(scalarVariantException, Is.Not.Null);

        var tupleTypeException = Assert.Throws<PlatformNotSupportedException>(
            () => HWIntrinsicInfo.BaseTypeFromValueTupleArg(id));
        Assert.That(tupleTypeException, Is.Not.Null);

        var variantException = Assert.Throws<PlatformNotSupportedException>(
            () => HWIntrinsicInfo.GetScalarInputVariant(id));
        Assert.That(variantException, Is.Not.Null);
    }

    [Test]
    public static void HardwareIntrinsicFlagBitsDoNotCollideAcrossTargets()
    {
        var flags = Enum.GetValues<HWIntrinsicFlag>();

        for (var index = 0; index < flags.Length; index++)
        {
            var flag = flags[index];
            if (flag is HW_Flag_NoFlag or HW_Flag_SpecialSideEffectMask)
            {
                continue;
            }

            var value = (ulong)flag;
            Assert.That(value & (value - 1), Is.Zero, $"{flag} must be a single bit.");

            for (var otherIndex = index + 1; otherIndex < flags.Length; otherIndex++)
            {
                var otherFlag = flags[otherIndex];
                if (otherFlag is not (HW_Flag_NoFlag or HW_Flag_SpecialSideEffectMask))
                {
                    Assert.That(otherFlag, Is.Not.EqualTo(flag), $"{flag} collides with {otherFlag}.");
                }
            }
        }
    }

    [Test]
    public static void HardwareIntrinsicIdentityMapsRoundTripNativeRows()
    {
        var unionIdentityCount = (int)NI_HW_INTRINSIC_END - (int)NI_HW_INTRINSIC_START - 1;
        var unionValueNumberCount = (int)VNF_HWI_INTRINSIC_END - (int)VNF_HWI_INTRINSIC_START - 1;

        Assert.That(unionValueNumberCount, Is.EqualTo(unionIdentityCount));

        for (var unionIndex = 1; unionIndex <= unionIdentityCount; unionIndex++)
        {
            var id = (NamedIntrinsic)((int)NI_HW_INTRINSIC_START + unionIndex);
            var valueNumberFunctionName = $"VNF_HWI_{id.ToString()[3..]}";
            Assert.That(Enum.TryParse<VNFunc>(valueNumberFunctionName, out var valueNumberFunction), Is.True);
            Assert.That((int)valueNumberFunction - (int)VNF_HWI_INTRINSIC_START,
                Is.EqualTo((int)id - (int)NI_HW_INTRINSIC_START));
        }

        if (!Configuration.FEATURE_HW_INTRINSICS)
        {
            Assert.That(HWIntrinsicInfo.GetNativeTableEntryCount(), Is.Zero);
            return;
        }

        var nativeRowCount = HWIntrinsicInfo.GetNativeTableEntryCount();
        for (var tableIndex = 0; tableIndex < nativeRowCount; tableIndex++)
        {
            var id = HWIntrinsicInfo.GetIntrinsicIdForNativeTableIndex(tableIndex);
            Assert.That(HWIntrinsicInfo.GetNativeTableIndex(id), Is.EqualTo(tableIndex));
        }

        var unsupportedId = Configuration.TARGET_XARCH ? NI_AdvSimd_Add : NI_AVX2_Add;
        _ = Assert.Throws<PlatformNotSupportedException>(() => HWIntrinsicInfo.lookupFlags(unsupportedId));
    }

    [Test]
    public static void MaskTypeAndOperatorMetadataAreAvailableOnEveryTarget()
    {
        var maskOper = genTreeOps.GT_CNS_MSK;
        Assert.That(Globals.varTypeIsMask(var_types.TYP_MASK), Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsCnsMsk, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That(maskOper.IsConst, Is.EqualTo(Configuration.FEATURE_MASKED_HW_INTRINSICS));
        Assert.That((int)VNFunc.VNF_CNS_MSK, Is.EqualTo((int)genTreeOps.GT_CNS_MSK));
    }

    [Test]
    public static void MaskConstantConstructionRequiresMaskedHardwareIntrinsics()
    {
        WithCompiler(compiler =>
        {
            if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = new GenTreeMskCon(default);
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => new GenTreeMskCon(default));
            }
        });
    }

    [Test]
    public static void MaskValueNumberingMatchesTheConfiguredFeature()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNZeroForType(var_types.TYP_MASK));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                {
                    simdmask_t result = default;
                    var input = default(simdmask_t);
                    Globals.EvaluateUnaryMask(genTreeOps.GT_NOT, false, var_types.TYP_BYTE, 64, ref result, input);
                });
                return;
            }

            var value = simdmask_t.AllBitsSet(3);
            var vn = store.VNForSimdMaskCon(value);

            Assert.That(store.TypeOfVN(vn), Is.EqualTo(var_types.TYP_MASK));
            Assert.That(store.GetConstantSimdMask(vn), Is.EqualTo(value));
            Assert.That(store.VNForSimdMaskCon(value), Is.EqualTo(vn));
            Assert.That(store.GetConstantSimdMaskValue(vn).IsScalable, Is.False);

            simdmask_t result = default;
            var allBits = simdmask_t.AllBitsSet(64);
            Globals.EvaluateUnaryMask(genTreeOps.GT_NOT, false, var_types.TYP_BYTE, 64, ref result, allBits);
            Assert.That(result.RawBits, Is.Zero);
        });
    }

    [Test]
    public static void SimdConstantStorageMatchesTheConfiguredTarget()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_SIMD)
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNForSimd8Con(simd8_t.Zero));
                _ = Assert.Throws<PlatformNotSupportedException>(() => store.VNForSimdType(16, TYP_INT));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNBroadcastForSimdType(TYP_SIMD16, TYP_INT, store.VNForIntCon(0)));

                var input = new byte[16];
                var result = new byte[16];
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    Globals.EvaluateUnarySimd(genTreeOps.GT_NEG, false, TYP_INT, result, input));
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    Globals.EvaluateBinarySimd(genTreeOps.GT_ADD, false, TYP_INT, result, input, input, input.Length));
                return;
            }

            var fixedVector = simd8_t.AllBitsSet;
            var fixedVectorVN = store.VNForSimd8Con(fixedVector);
            Assert.That(store.GetConstantSimd8(fixedVectorVN), Is.EqualTo(fixedVector));

            if (Configuration.TARGET_XARCH)
            {
                var wideVector = simd64_t.AllBitsSet;
                var wideVectorVN = store.VNForSimd64Con(wideVector);
                Assert.That(store.GetConstantSimd64(wideVectorVN), Is.EqualTo(wideVector));
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNForSimd64Con(simd64_t.AllBitsSet));
            }

            if (Configuration.TARGET_ARM64)
            {
                var scalableVector = simdscalable_t.AllBitsSet;
                var scalableVectorVN = store.VNForSimdScalableCon(scalableVector);
                Assert.That(store.GetConstantSimdScalable(scalableVectorVN), Is.EqualTo(scalableVector));
            }
            else
            {
                _ = Assert.Throws<PlatformNotSupportedException>(() =>
                    store.VNForSimdScalableCon(simdscalable_t.AllBitsSet));
            }
        });
    }

    [Test]
    public static void NativeVariableTypeOrdinalsRemainTargetSpecific()
    {
        WithCompiler(compiler =>
        {
            var store = new ValueNumStore(compiler);
            if (!Configuration.FEATURE_SIMD)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(16));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_UNKNOWN, TYP_COUNT);
                return;
            }

            if (Configuration.TARGET_XARCH)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(22));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD32), Is.EqualTo(18));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD64), Is.EqualTo(19));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(20));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_SIMD32, TYP_SIMD64, TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }
            else if (Configuration.TARGET_ARM64)
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(21));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_SIMD_ARM64), Is.EqualTo(18));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(19));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_SIMD_ARM64, TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }
            else
            {
                Assert.That(var_typesExtensions.NativeCount, Is.EqualTo(20));
                Assert.That(var_typesExtensions.ToNativeOrdinal(TYP_MASK), Is.EqualTo(18));
                AssertNativeOrdinalRoundTrips(store, TYP_INT, TYP_SIMD8, TYP_SIMD12, TYP_SIMD16,
                    TYP_MASK, TYP_UNKNOWN, TYP_COUNT);
            }

            var structEncoding = store.EncodeBitCastType(TYP_STRUCT, new ValueSize(3));
            Assert.That(store.GetConstantInt32(structEncoding), Is.EqualTo(var_typesExtensions.NativeCount + 3));
            Assert.That(store.DecodeBitCastType(structEncoding, out var size), Is.EqualTo(TYP_STRUCT));
            Assert.That(size, Is.EqualTo(3U));

            var simdTypeVN = store.VNForSimdType(16, TYP_INT, TYP_MASK);
            var simdTypeApp = new VNFuncApp();
            Assert.That(store.GetVNFunc(simdTypeVN, ref simdTypeApp), Is.True);
            var encodedTypes = store.GetConstantInt32(simdTypeApp.GetArg(1));
            Assert.That(encodedTypes & 0xFF, Is.EqualTo(var_typesExtensions.ToNativeOrdinal(TYP_INT)));
            Assert.That((encodedTypes >> 8) - 1, Is.EqualTo(var_typesExtensions.ToNativeOrdinal(TYP_MASK)));
        });
    }

    private static void AssertNativeOrdinalRoundTrips(ValueNumStore store, params var_types[] types)
    {
        foreach (var type in types)
        {
            var ordinal = var_typesExtensions.ToNativeOrdinal(type);
            Assert.That(var_typesExtensions.FromNativeOrdinal(ordinal), Is.EqualTo(type));

            var castVN = store.VNForCastOper(type, false);
            store.GetCastOperFromVN(castVN, out var castType, out var isUnsigned);
            Assert.That(castType, Is.EqualTo(type));
            Assert.That(isUnsigned, Is.False);
        }
    }

    private static unsafe void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        JitConfig = new JitConfigValues();
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.opts.compCodeOpt = Compiler.BLENDED_CODE;
#if DEBUG
        compiler.info.compFullName = nameof(FeatureConfigurationTests);
#endif
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }
}

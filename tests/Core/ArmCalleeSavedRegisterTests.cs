// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ArmCalleeSavedRegisterTests
{
    [Test]
    public static void MinOptsStillLaysOutTheFrameBeforeReservingTheRegister()
    {
        WithFrame(0, (compiler, _) =>
        {
            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.True);
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.REGALLOC_FRAME_LAYOUT));
        }, minOpts: true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StackPointerReachabilityUsesTheStrictIntegerEncodingLimit(bool beyondLimit)
    {
        WithFrame(0, (compiler, codeGen) =>
        {
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;

            var frameSize = compiler.lvaFrameSize(Compiler.REGALLOC_FRAME_LAYOUT);
            var parameterStackSize = unchecked(0x1000u - frameSize + (beyondLimit ? 1u : 0u));
            compiler.lvaParameterStackSize = checked((int)parameterStackSize);

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.EqualTo(beyondLimit));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RequiredFramePointerReachabilityUsesTheStrictPositiveEncodingLimit(bool beyondLimit)
    {
        WithFrame(0, (compiler, codeGen) =>
        {
            codeGen.IsFramePointerRequired = true;
            codeGen.IsFramePointerUsed = true;

            var parameterStackSize = 0x1000u - (2 * (uint)REGSIZE_BYTES) + (beyondLimit ? 1u : 0u);
            compiler.lvaParameterStackSize = checked((int)parameterStackSize);

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.EqualTo(beyondLimit));
        });
    }

    [TestCase(0, false)]
    [TestCase(320, true)]
    public static void RequiredFramePointerMustReachTheNegativeLocalOffsetRange(int outgoingArgSpaceSize, bool expected)
    {
        WithFrame(outgoingArgSpaceSize, (compiler, codeGen) =>
        {
            codeGen.IsFramePointerRequired = true;
            codeGen.IsFramePointerUsed = true;

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.EqualTo(expected));
        });
    }

    [TestCase(4096, false)]
    [TestCase(4400, true)]
    public static void UsedFramePointerAndStackPointerShareReachabilityAcrossLocals(
        int outgoingArgSpaceSize, bool expected)
    {
        WithFrame(outgoingArgSpaceSize, (compiler, codeGen) =>
        {
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = true;

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.EqualTo(expected));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingPointFramesUseTheirSmallerStackOffsetEncodingLimit(bool beyondLimit)
    {
        WithFrame(0, (compiler, codeGen) =>
        {
            compiler.compFloatingPointUsed = true;
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;

            var frameSize = compiler.lvaFrameSize(Compiler.REGALLOC_FRAME_LAYOUT);
            var parameterStackSize = unchecked(0x3FDu - frameSize + (beyondLimit ? 1u : 0u));
            compiler.lvaParameterStackSize = checked((int)parameterStackSize);

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.EqualTo(beyondLimit));
        });
    }

    [TestCase(0u, SRBM_NONE, false, SRBM_NONE)]
    [TestCase(4u, SRBM_NONE, false, SRBM_R3)]
    [TestCase(8u, SRBM_NONE, false, SRBM_R2 | SRBM_R3)]
    [TestCase(12u, SRBM_NONE, false, SRBM_NONE)]
    [TestCase(16u, SRBM_NONE, false, SRBM_NONE)]
    [TestCase(3u, SRBM_NONE, false, SRBM_NONE)]
    [TestCase(uint.MaxValue, SRBM_NONE, false, SRBM_NONE)]
    [TestCase(4u, SRBM_F16 | SRBM_F17, false, SRBM_NONE)]
    [TestCase(8u, SRBM_F16 | SRBM_F17 | SRBM_F18 | SRBM_F19, false, SRBM_NONE)]
    [TestCase(4u, SRBM_NONE, true, SRBM_NONE)]
    [TestCase(8u, SRBM_NONE, true, SRBM_NONE)]
    [TestCase(8u, SRBM_F16 | SRBM_F17, true, SRBM_NONE)]
    public static void SmallFramesUseOnlyScratchWordsWithoutFloatsOrAsync(
        uint frameSize, regMask floats, bool async, regMask expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            if (async)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            }

            foreach (var phase in new[]
            {
                InsGroupFlags.Prolog,
                InsGroupFlags.FuncletProlog,
                InsGroupFlags.Epilog,
                InsGroupFlags.FuncletEpilog,
            })
            {
                SetPhase(codeGen, phase);
                var mask = StackAllocationMask(codeGen, frameSize, new regMaskTP(floats));

                Assert.That(mask, Is.EqualTo(new regMaskTP(expected)));
                Assert.That((mask & new regMaskTP(SRBM_R0 | SRBM_R1)).IsEmpty, Is.True);
                Assert.That(codeGen.RegSet.rsGetModifiedRegsMask().IsEmpty, Is.True);
                Assert.That(SavedMask(ref codeGen.RegSet).IsEmpty, Is.True);
            }
        });
    }

    [TestCase(false, SRBM_NONE, SRBM_NONE, true)]
    [TestCase(true, SRBM_NONE, SRBM_NONE, false)]
    [TestCase(false, SRBM_R0, SRBM_NONE, false)]
    [TestCase(false, SRBM_NONE, SRBM_R3, false)]
    [TestCase(false, SRBM_R0 | SRBM_R1, SRBM_R3, false)]
    [TestCase(true, SRBM_R0, SRBM_R3, false)]
    public static void PopReturnRequiresNeitherTailJumpNorPrespillsIncludingAlignment(
        bool jump, regMask arguments, regMask alignment, bool expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.RegSet.rsMaskPreSpillRegArg = new regMaskTP(arguments);
            codeGen.RegSet.rsMaskPreSpillAlign = new regMaskTP(alignment);
            foreach (var phase in new[] { InsGroupFlags.Epilog, InsGroupFlags.FuncletEpilog })
            {
                SetPhase(codeGen, phase);
                Assert.That(CanUsePopToReturn(codeGen, RBM_NONE, jump), Is.EqualTo(expected));
                Assert.That(CanUsePopToReturn(codeGen, new regMaskTP(SRBM_R4 | SRBM_FPBASE), jump),
                    Is.EqualTo(expected));
            }

            Assert.That(codeGen.RegSet.rsMaskPreSpillRegs(true),
                Is.EqualTo(new regMaskTP(arguments | alignment)));
        });
    }

    [TestCase(SRBM_NONE, false, 0, false)]
    [TestCase(SRBM_R4, false, 4, true)]
    [TestCase(SRBM_R4 | SRBM_R5, true, 8, false)]
    [TestCase(SRBM_R0 | SRBM_R4, true, 12, true)]
    [TestCase(SRBM_R4 | SRBM_F16 | SRBM_F17, false, 4, true)]
    [TestCase(SRBM_F16 | SRBM_F17 | SRBM_F18 | SRBM_F19, true, 8, false)]
    public static void PushRecordsActualSaveMaskAndInstructions(
        regMask modified, bool framePointer, int frameSize, bool zeroed)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            SetPhase(codeGen, InsGroupFlags.Prolog);
            codeGen.IsFramePointerUsed = framePointer;
            if (modified != SRBM_NONE)
            {
                codeGen.RegSet.rsSetRegsModified(new regMaskTP(modified));
            }
            var expected = codeGen.RegSet.rsGetModifiedCalleeSavedRegsMask() | new regMaskTP(SRBM_LR);
            if (framePointer)
            {
                expected |= new regMaskTP(SRBM_FPBASE);
            }
            compiler.compCalleeRegsPushed = BitOperations.PopCount(unchecked((ulong)expected.Lower));
            compiler.compLclFrameSize = frameSize;
            var initialZeroed = zeroed;

            RecordArm32Instructions(() => codeGen.genPushCalleeSavedRegisters(REG_R3, ref zeroed));

            Assert.That(SavedMask(ref codeGen.RegSet), Is.EqualTo(expected));
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(modified)));
            Assert.That(zeroed, Is.EqualTo(initialZeroed));
            Assert.That(codeGen.genUsedPopToReturn, Is.False);
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(frameSize));
            var floatMask = expected & new regMaskTP(SRBM_ALLFLOAT);
            var intMask = expected & ~floatMask;
            if (floatMask.IsEmpty)
            {
                intMask |= frameSize switch
                {
                    REGSIZE_BYTES => new regMaskTP(SRBM_R3),
                    2 * REGSIZE_BYTES => new regMaskTP(SRBM_R2 | SRBM_R3),
                    _ => RBM_NONE,
                };
            }

            var descriptors = Descriptors(codeGen);
            var expectedDescriptorCount = floatMask.IsNonEmpty ? 2 : 1;
            Assert.That(descriptors, Has.Count.EqualTo(expectedDescriptorCount));
            AssertArm32PushPopDescriptor(codeGen, descriptors[0], INS_push, intMask);
            if (floatMask.IsNonEmpty)
            {
                AssertFloatSaveDescriptor(codeGen, descriptors[1], INS_vpush, floatMask);
            }
        });
    }

    [TestCase(false, SRBM_NONE, SRBM_NONE, false, 0, true)]
    [TestCase(false, SRBM_NONE, SRBM_NONE, true, 4, true)]
    [TestCase(false, SRBM_R0, SRBM_NONE, true, 8, false)]
    [TestCase(false, SRBM_NONE, SRBM_R3, false, 4, false)]
    [TestCase(true, SRBM_NONE, SRBM_NONE, true, 8, false)]
    [TestCase(true, SRBM_R0, SRBM_R3, false, 12, false)]
    public static void IntegerPopRecordsPcOrLrAndRestoresRegisters(
        bool jump, regMask arguments, regMask alignment, bool framePointer, int frameSize, bool popReturn)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            SetPhase(codeGen, InsGroupFlags.Epilog);
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.RegSet.rsSetRegsModified(new regMaskTP(SRBM_R4 | SRBM_R5));
            codeGen.RegSet.rsMaskPreSpillRegArg = new regMaskTP(arguments);
            codeGen.RegSet.rsMaskPreSpillAlign = new regMaskTP(alignment);
            compiler.compLclFrameSize = frameSize;
            codeGen.genUsedPopToReturn = !popReturn;

            RecordArm32Instructions(() => codeGen.genPopCalleeSavedRegisters(jump));

            Assert.That(codeGen.genUsedPopToReturn, Is.EqualTo(popReturn));
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(new regMaskTP(SRBM_R4 | SRBM_R5)));
            Assert.That(codeGen.RegSet.rsMaskPreSpillRegs(true), Is.EqualTo(new regMaskTP(arguments | alignment)));
            Assert.That(SavedMask(ref codeGen.RegSet).IsEmpty, Is.True);
            Assert.That(compiler.compLclFrameSize, Is.EqualTo(frameSize));
            var popMask = new regMaskTP(SRBM_R4 | SRBM_R5);
            if (!jump)
            {
                popMask |= frameSize switch
                {
                    REGSIZE_BYTES => new regMaskTP(SRBM_R3),
                    2 * REGSIZE_BYTES => new regMaskTP(SRBM_R2 | SRBM_R3),
                    _ => RBM_NONE,
                };
            }

            if (framePointer)
            {
                popMask |= new regMaskTP(SRBM_FPBASE);
            }

            popMask |= new regMaskTP(popReturn ? SRBM_PC : SRBM_LR);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertArm32PushPopDescriptor(codeGen, descriptors[0], INS_pop, popMask);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatPopRecordsFloatAndIntegerRegisters(bool jump)
    {
        WithCodeGen((_, codeGen) =>
        {
            SetPhase(codeGen, InsGroupFlags.Epilog);
            var modified = new regMaskTP(SRBM_R4 | SRBM_F16 | SRBM_F17);
            codeGen.RegSet.rsSetRegsModified(modified);
            codeGen.IsFramePointerUsed = false;
            codeGen.genUsedPopToReturn = true;

            RecordArm32Instructions(() => codeGen.genPopCalleeSavedRegisters(jump));

            Assert.That(codeGen.genUsedPopToReturn, Is.EqualTo(!jump));
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(modified));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            AssertFloatSaveDescriptor(codeGen, descriptors[0], INS_vpop,
                new regMaskTP(SRBM_F16 | SRBM_F17));
            var returnRegister = jump ? SRBM_LR : SRBM_PC;
            AssertArm32PushPopDescriptor(codeGen, descriptors[1], INS_pop,
                new regMaskTP(SRBM_R4 | returnRegister));
        });
    }

    [TestCase(true, REG_F16, 2)]
    [TestCase(true, REG_F16, 4)]
    [TestCase(true, REG_F16, 16)]
    [TestCase(false, REG_F16, 2)]
    [TestCase(false, REG_F16, 16)]
    [TestCase(false, REG_F0, 2)]
    [TestCase(false, REG_F8, 4)]
    public static void FloatSavesUseContiguousSingleWordMasksForDoubleRegisterInstructions(
        bool push, regNumber first, int singleWords)
    {
        WithCodeGen((_, codeGen) =>
        {
            SetPhase(codeGen, push ? InsGroupFlags.Prolog : InsGroupFlags.Epilog);
            var mask = FloatMask(first, singleWords);

            Assert.That(BitOperations.PopCount(unchecked((ulong)mask.Lower)), Is.EqualTo(singleWords));
            Assert.That(floatRegCanHoldType(first, var_types.TYP_DOUBLE), Is.True);
            RecordArm32Instructions(() =>
            {
                if (push)
                {
                    PushFloats(codeGen, mask);
                }
                else
                {
                    PopFloats(codeGen, mask);
                }
            });
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertFloatSaveDescriptor(codeGen, descriptors[0], push ? INS_vpush : INS_vpop, mask);
            Assert.That(SavedMask(ref codeGen.RegSet).IsEmpty, Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void CalleeSavedMaskUnwindDependenciesRecordUnwindInfo(int operation)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var unwindInfo = new UnwindInfo();
            compiler.compFuncInfos =
            [
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_ROOT,
                    uwi = unwindInfo,
                },
            ];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            codeGen.Emitter.emitBegProlog();
            compiler.unwindBegProlog();

            var mask = new regMaskTP(operation == 1 ? SRBM_R4 | SRBM_LR : SRBM_F16 | SRBM_F17);
            WithUnwindSizeCheckSkipped(unwindInfo, () =>
            {
                switch (operation)
                {
                    case 0:
                    {
                        PushFloatUnwind(codeGen, mask);
                        break;
                    }

                    case 1:
                    {
                        PopIntUnwind(codeGen, mask);
                        break;
                    }

                    case 2:
                    {
                        PopFloatUnwind(codeGen, mask);
                        break;
                    }

                    default:
                    {
                        throw new AssertionException("Unknown unwind operation.");
                    }
                }
            });

            Assert.That(unwindInfo.GetCurrentEmitterLocation()?.IsCurrentLocation(codeGen.Emitter), Is.True);
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    // Recording-only fixtures lack the instruction descriptors needed by the debug size cross-check.
#if DEBUG
    internal static void WithUnwindSizeCheckSkipped(UnwindInfo unwindInfo, Action action)
    {
        var previous = AddingUnwindNop(unwindInfo);
        AddingUnwindNop(unwindInfo) = true;
        try
        {
            action();
        }
        finally
        {
            AddingUnwindNop(unwindInfo) = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "uwiAddingNOP")]
    private static extern ref bool AddingUnwindNop(UnwindInfo unwindInfo);

#else
    internal static void WithUnwindSizeCheckSkipped(UnwindInfo unwindInfo, Action action)
    {
        action();
    }
#endif

#if DEBUG
    [TestCase(TYP_REF, REG_R4, true, true, 1, true)]
    [TestCase(TYP_BYREF, REG_R5, true, true, 1, true)]
    [TestCase(TYP_REF, REG_R4, true, false, 0, false)]
    [TestCase(TYP_REF, REG_R0, true, true, 1, false)]
    [TestCase(TYP_REF, REG_R4, false, true, 0, false)]
    public static void PInvokeRequiresCalleeSavedGcRootsToBeSpilled(
        var_types type, regNumber reg, bool unmanaged, bool hasRoot, int expectedAssertions,
        bool expectedCalleeSavedAssertion)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
            compiler.info.compMatchedVM = false;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            compiler.compCurBB = block;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = false;
#endif

            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x4000,
                _directCallAddress = (void*)0x1234,
                _returnType = TYP_VOID,
            };
            if (unmanaged)
            {
                call.Flags |= GTF_CALL_UNMANAGED;
                call._callMoreFlags |= GTF_CALL_M_PINVOKE;
            }

            codeGen.InternalRegisters.Add(call, new regMaskTP(SRBM_R12));
            if (hasRoot)
            {
                codeGen.GCInfo.gcMarkRegPtrVal(reg, type);
            }

            if (expectedAssertions != 0)
            {
                AssertFailure(() => codeGen.genCall(call), message: null);
            }
            else
            {
                codeGen.genCall(call);
            }

            Assert.That(s_assertions, Has.Count.EqualTo(expectedAssertions));
            if (expectedAssertions != 0)
            {
                Assert.That(s_assertions[0].Contains("SRBM_INT_CALLEE_SAVED", StringComparison.Ordinal),
                    Is.EqualTo(expectedCalleeSavedAssertion));
            }
        }, captureAssertions: true);
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void FloatMaskGapsKeepTheNativeContiguityAssertion(bool push)
    {
        WithCodeGen((_, codeGen) =>
        {
            var mask = new regMaskTP(SRBM_F16 | SRBM_F18);
            AssertFailure(() =>
            {
                if (push)
                {
                    PushFloats(codeGen, mask);
                }
                else
                {
                    PopFloats(codeGen, mask);
                }
            }, message: null);
            Assert.That(s_assertions, Is.EqualTo(s_contiguousMaskAssertion));
        }, captureAssertions: true);
    }

    [Test]
    public static void PushFloatsRequiresF16ButPopFloatsDoesNot()
    {
        WithCodeGen((_, codeGen) =>
        {
            var mask = FloatMask(REG_F8, 2);
            AssertFailure(() => PushFloats(codeGen, mask),
                message: null);
            Assert.That(s_assertions, Is.EqualTo(s_lowRegisterAssertion));

            s_assertions.Clear();
            RecordArm32Instructions(() => PopFloats(codeGen, mask));
            Assert.That(s_assertions, Is.Empty);
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertFloatSaveDescriptor(codeGen, descriptors[0], INS_vpop, mask);
        }, captureAssertions: true);
    }

    [Test]
    public static void PushCountMismatchAssertsAfterSavingTheOwnerMask()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            SetPhase(codeGen, InsGroupFlags.Prolog);
            codeGen.IsFramePointerUsed = true;
            compiler.compCalleeRegsPushed = 1;
            var zeroed = true;

            AssertFailure(() => codeGen.genPushCalleeSavedRegisters(REG_R3, ref zeroed),
                message: null);

            Assert.That(s_assertions, Is.EqualTo(s_pushCountMismatchAssertion));
            Assert.That(SavedMask(ref codeGen.RegSet), Is.EqualTo(new regMaskTP(SRBM_FPBASE | SRBM_LR)));
            Assert.That(zeroed, Is.True);
        }, captureAssertions: true);
    }

    [Test]
    public static void NullCheckNodesRetainTheUnsupportedAssertion()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_BYREF, 7);
            var tree = new GenTreeIndir(GT_NULLCHECK, TYP_LONG, address);

            AssertFailure(() => codeGen.genCodeForNullCheck(tree), message: null);
            Assert.That(s_assertions, Is.EqualTo(s_unsupportedNullCheckAssertion));
        }, captureAssertions: true);
    }

    private static readonly List<string> s_assertions = [];
    private static readonly string[] s_contiguousMaskAssertion = ["genMaxOneBit(tmpMask)"];
    private static readonly string[] s_lowRegisterAssertion = ["lowReg == REG_F16"];
    private static readonly string[] s_pushCountMismatchAssertion = ["_compiler.compCalleeRegsPushed == count"];
    private static readonly string[] s_unsupportedNullCheckAssertion =
        ["!\"GT_NULLCHECK isn't supported for Arm32; use GT_IND.\""];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);
#endif

    private static regMaskTP FloatMask(regNumber first, int singleWords)
    {
        var mask = RBM_NONE;
        for (var index = 0; index < singleWords; index++)
        {
            var reg = (regNumber)((int)first + index);
            mask |= regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        }

        return mask;
    }

    private static void SetPhase(CodeGen codeGen, InsGroupFlags phase)
    {
        var group = CurrentGroup(codeGen.Emitter) ?? throw new AssertionException("Missing instruction group.");
        group.igFlags = phase;
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    private static void AssertFailure(TestDelegate action, string? message)
    {
        var failure = Assert.Throws<FatalJitException>(action) ??
            throw new AssertionException("Missing expected ARM target skip.");

        Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
        if (message is not null)
        {
            Assert.That(failure.Message, Is.EqualTo(message));
        }
    }

    private static void AssertFloatSaveDescriptor(
        CodeGen codeGen, Emitter.instrDesc descriptor, instruction ins, regMaskTP floatMask)
    {
        var firstRegister = (regNumber)BitOperations.TrailingZeroCount(unchecked((ulong)floatMask.Lower));
        var singleWords = BitOperations.PopCount(unchecked((ulong)floatMask.Lower));
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_T2_VLDST));
        Assert.That(descriptor.idReg1(), Is.EqualTo(firstRegister));
        var expectedImmediate = (ins == INS_vpush ? -1 : 1) * singleWords * sizeof(uint);
        Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)expectedImmediate));
    }

    private static void AssertArm32PushPopDescriptor(
        CodeGen codeGen, Emitter.instrDesc descriptor, instruction ins, regMaskTP mask)
    {
        var imm = unchecked((int)mask.Lower);
        var hasLr = (imm & (int)SRBM_LR) != 0;
        var hasPc = (imm & (int)SRBM_PC) != 0;
        var useThumb2 = (ins == INS_pop) && hasLr;
        var isSingleBit = (imm != 0) && ((unchecked(imm - 1) & imm) == 0);
        imm &= ~0xe000;

        var format = (((imm & 0x00ff) == imm) && !useThumb2)
            ? IF_T1_L1
            : !isSingleBit
                ? IF_T2_I1
                : throw new AssertionException("Unexpected single-register ARM push/pop encoding.");

        imm <<= 2;
        if (hasPc)
        {
            imm |= 2;
        }
        if (hasLr)
        {
            imm |= 1;
        }

        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idInsFmt(), Is.EqualTo(format));
        Assert.That(codeGen.Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)imm));
    }

    internal static void WithCodeGen(
        Action<Compiler, CodeGen> action, bool captureAssertions = false, bool minOpts = true)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(captureAssertions ? &ee : null);
        var previousConfig = JitConfig;
        if (captureAssertions)
        {
            JitConfig = new JitConfigValues();
            AltJitSkipOnAssert(ref JitConfig) = 1;
            s_assertions.Clear();
        }
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        var unwindInfo = new UnwindInfo();
        unwindInfo.InitUnwindInfo(compiler, null, null);
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = unwindInfo }];
        compiler.compFuncInfoCount = 1;
        compiler.compCurrFuncIdx = 0;
        compiler.fgFuncletsCreated = true;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
#if DEBUG
        compiler.info.compFullName = nameof(ArmCalleeSavedRegisterTests);
        if (captureAssertions)
        {
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        }
#endif
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.resetFramePointerUsedWritePhase();
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            unwindInfo.CaptureLocation();
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
#if DEBUG
            if (captureAssertions)
            {
                JitConfig = previousConfig;
            }
#endif
        }
    }

    private static void WithFrame(int outgoingArgSpaceSize, Action<Compiler, CodeGen> action, bool minOpts = false)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_INT, lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc
                {
                    Type = TYP_STRUCT,
                    lvOnFrame = true,
                    RegNum = REG_STK,
                    Layout = new ClassLayout(checked((uint)outgoingArgSpaceSize)),
                },
            ];
            compiler.lvaCount = 2;
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = outgoingArgSpaceSize;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaResumedIndicator = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.compLclFrameSize = 0;
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFrameRequired = false;
            codeGen.IsFramePointerUsed = false;
            action(compiler, codeGen);
        }, minOpts: minOpts);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_rsMaskCalleeSaved")]
    private static extern ref regMaskTP SavedMask(ref RegSet regSet);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genPushFltRegsArmCore")]
    private static extern void PushFloats(CodeGen codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genPopFltRegsArmCore")]
    private static extern void PopFloats(CodeGen codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genStackAllocRegisterMaskArmCore")]
    private static extern regMaskTP StackAllocationMask(CodeGen codeGen, uint size, regMaskTP floats);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCanUsePopToReturnArmCore")]
    private static extern bool CanUsePopToReturn(CodeGen codeGen, regMaskTP mask, bool jump);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unwindPushMaskFloatArmCore")]
    private static extern void PushFloatUnwind(CodeGen codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unwindPopMaskIntArmCore")]
    private static extern void PopIntUnwind(CodeGen codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "unwindPopMaskFloatArmCore")]
    private static extern void PopFloatUnwind(CodeGen codeGen, regMaskTP mask);
}
#endif

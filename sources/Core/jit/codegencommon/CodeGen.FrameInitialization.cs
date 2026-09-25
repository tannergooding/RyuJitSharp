// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public uint InitStkLclCnt { get; private set; }

    public bool UseBlockInit { get; private set; }

    public regMaskTP genGetParameterHomingTempRegisterCandidates()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Parameter homing register selection requires Windows AMD64.");
#else
        var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
        var regs = calleeTrash | _calleeRegArgMaskLiveIn | _regSet.rsGetModifiedRegsMask();
        // Reserved registers may be needed to address stack locals during homing.
        regs &= ~_regSet.rsMaskResvd;

        return regs;
#endif
    }

    public void genCheckUseBlockInit()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Prolog initialization planning requires Windows AMD64.");
#else
        assert(!Emitter.emitGeneratingPrologOrFuncletProlog());
        uint initStkLclCnt = 0;

        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            var counted = false;
            if (!varDsc.lvIsInReg && !varDsc.lvOnFrame)
            {
                noway_assert(varDsc.lvRefCnt() == 0);
                varDsc.lvMustInit = false;
                continue;
            }
            if (_compiler.lvaIsUnknownSizeLocal(varNum))
            {
                continue;
            }
            if (_compiler.fgVarIsNeverZeroInitializedInProlog(varNum))
            {
                varDsc.lvMustInit = false;
                continue;
            }
            if (_compiler.lvaIsFieldOfDependentlyPromotedStruct(in varDsc))
            {
                // The parent owns initialization of dependently promoted fields.
                varDsc.lvMustInit = false;
                continue;
            }
            if (varDsc.lvHasExplicitInit)
            {
                varDsc.lvMustInit = false;
                continue;
            }

            var isTemp = varDsc.lvIsTemp;
            var hasGCPtr = varDsc.HasGCPtr;
            var isTracked = varDsc.lvTracked;
            var isStruct = varTypeIsStruct(varDsc.Type);
            var compInitMem = _compiler.info.compInitMem;
            if (isTemp && !hasGCPtr)
            {
                varDsc.lvMustInit = false;
                continue;
            }

            if (compInitMem || hasGCPtr || varDsc.lvMustInit)
            {
                if (isTracked)
                {
                    assert(_compiler.fgFirstBB is not null);
                    if (varDsc.lvMustInit ||
                        VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, varDsc._varIndex))
                    {
                        varDsc.lvMustInit = true;
                        if (varDsc.lvOnFrame)
                        {
                            if (!varDsc.lvRegister)
                            {
                                if (!varDsc.lvIsInReg || varDsc.IsLiveInOutOfHandler)
                                {
                                    initStkLclCnt = unchecked(initStkLclCnt +
                                        (uint)(roundUp(_compiler.lvaLclStackHomeSize(varNum), TARGET_POINTER_SIZE) / sizeof(int)));
                                    counted = true;
                                }
                            }
                            else
                            {
                                noway_assert((varDsc.Type.Size > sizeof(int)) && (varDsc.OtherReg == REG_STK));
                                initStkLclCnt = unchecked(initStkLclCnt + (uint)TYP_INT.StSz);
                                counted = true;
                            }
                        }
                    }
                }

                if (varDsc.lvOnFrame)
                {
                    var mustInitThisVar = false;
                    if (hasGCPtr && !isTracked)
                    {
                        JITDUMP($"must init V{varNum:D2} because it has a GC ref\n");
                        mustInitThisVar = true;
                    }
                    else if (hasGCPtr && isStruct)
                    {
                        JITDUMP($"must init a tracked V{varNum:D2} because it a struct with a GC ref\n");
                        mustInitThisVar = true;
                    }
                    else if (!isTracked)
                    {
                        assert(!hasGCPtr && !isTemp);
                        if (compInitMem)
                        {
                            JITDUMP($"must init V{varNum:D2} because compInitMem is set and it is not a temp\n");
                            mustInitThisVar = true;
                        }
                    }

                    if (mustInitThisVar)
                    {
                        varDsc.lvMustInit = true;
                        if (!counted)
                        {
                            initStkLclCnt = unchecked(initStkLclCnt +
                                (uint)(roundUp(_compiler.lvaLclStackHomeSize(varNum), TARGET_POINTER_SIZE) / sizeof(int)));
                        }
                    }
                }
            }
        }

#if DEBUG
        assert(_regSet.tmpGetAllFree());
#endif
        for (var temp = _regSet.tmpListBeg(); temp is not null; temp = _regSet.tmpListNxt(temp))
        {
            if (varTypeIsGC(temp.tdTempType))
            {
                initStkLclCnt = unchecked(initStkLclCnt + 1);
            }
        }

        // Count four-byte slots. AMD64's aligned SIMD clearing uses the lower
        // native threshold also relied on by fgVarNeedsExplicitZeroInit.
        InitStkLclCnt = initStkLclCnt;
        UseBlockInit = InitStkLclCnt > 4;
#endif
    }
}

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
#if TARGET_WASM
        throw new FatalJitException(CORJIT_SKIPPED, "Parameter homing register selection requires AMD64.");
#else
#if HAS_MORE_THAN_64_REGISTERS
        var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH, SRBM_MSK_CALLEE_TRASH);
#elif TARGET_XARCH
        var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH | SRBM_MSK_CALLEE_TRASH);
#else
        var calleeTrash = new regMaskTP(SRBM_INT_CALLEE_TRASH | SRBM_FLT_CALLEE_TRASH);
#endif
        var regs = calleeTrash | _calleeRegArgMaskLiveIn | _regSet.rsGetModifiedRegsMask();
        // Reserved registers may be needed to address stack locals during homing.
        regs &= ~_regSet.rsMaskResvd;

        return regs;
#endif
    }

    public void genCheckUseBlockInit()
    {
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
                        // TODO-1stClassStructs: support precise liveness reporting for such structs.
                        JITDUMP($"must init a tracked V{varNum:D2} because it a struct with a GC ref\n");
                        mustInitThisVar = true;
                    }
#if TARGET_WASM
                    else if (hasGCPtr)
                    {
                        // On wasm all GC vars are reported as untracked, so the slot must be zero-inited.
                        JITDUMP($"must init V{varNum:D2} because wasm reports tracked GC vars as untracked\n");
                        mustInitThisVar = true;
                    }
#endif
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
                            counted = true;
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

        // Record number of 4 byte slots that need zeroing.
        InitStkLclCnt = initStkLclCnt;

        // Compiler.fgVarNeedsExplicitZeroInit relies on this logic to find
        // structs that are guaranteed to be block initialized.
#if TARGET_WASM
        // On WASM we always use a single memory.fill for prolog zeroing.
        UseBlockInit = InitStkLclCnt > 0;
#elif TARGET_64BIT && !TARGET_AMD64
        UseBlockInit = InitStkLclCnt > 8;
#else
        // AMD64 can clear using aligned SIMD so the threshold is lower,
        // and clears in order which is better for auto-prefetching.
        UseBlockInit = InitStkLclCnt > 4;
#endif

#if TARGET_ARM
        if (UseBlockInit)
        {
            // Force spill R4/R5/R6 so they can be used during block initialization.
            var maskCalleeRegArgMask = _calleeRegArgMaskLiveIn & RBM_ALLINT;
            var forceSpillRegCount = unchecked((int)(genCountBits(
                maskCalleeRegArgMask & ~genPrespilledUnmappedRegs()) - 1));
            if (forceSpillRegCount > 0)
            {
                _regSet.rsSetRegsModified(RBM_R4);
            }
            if (forceSpillRegCount > 1)
            {
                _regSet.rsSetRegsModified(RBM_R5);
            }
            if (forceSpillRegCount > 2)
            {
                _regSet.rsSetRegsModified(RBM_R6);
            }
        }
#endif
    }
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public interface ICodeGen
{
    Compiler Compiler { get; }

    ref Disassembler Disassembler { get; }

    string? siRegVarName(nuint offs, nuint size, uint reg);

    string? siStackVarName(nuint offs, nuint size, uint reg, uint stkOffs);

    Emitter Emitter { get; }

    ref GCInfo GCInfo { get; }

    ref regMaskTP CalleeRegArgMaskLiveIn { get; }

    int genCallerSPtoFPdelta { get; }

    int genCallerSPtoInitialSPdelta { get; }

    int genSPtoFPdelta { get; }

    int genTotalFrameSize { get; }

    ref NodeInternalRegisters InternalRegisters { get; }

    /// <summary>true if we've determined that the current method is to be fully interruptible.</summary>
    bool Interruptible { get; set; }

    /// <summary>Indicates whether the current method requires an explicit stack frame, and all arguments and locals to be accessible relative to the Frame Pointer.</summary>
    /// <remarks>Prohibits double alignment of the stack.</remarks>
    bool IsFramePointerRequired { get; set; }

    /// <summary>Indicates whether the current method sets up an explicit stack frame or not.</summary>
    bool IsFramePointerUsed { get; set; }

    bool IsSaveFpLrWithAllCalleeSavedRegisters { get; }

    bool IsDoubleAligned { get; set; }

    /// <summary>Indicates whether the current method requires an explicit frame.</summary>
    /// <remarks>Does not prohibit double alignment of the stack.</remarks>
    bool IsFrameRequired { get; set; }

    void SetSaveFpLrWithAllCalleeSavedRegisters(bool value);

    void SetFramePointerRequiredEH(bool value);

    void SetFramePointerRequiredGCInfo(bool value);

    //  The following will be set to true if we've determined that we need to
    //  generate a full-blown pointer register map for the current method.
    //  Currently it is equal to (GetInterruptible() || !IsFramePointerUsed)
    //  (i.e. We generate the full-blown map for EBP-less methods and
    //        for fully interruptible methods)
    bool IsFullPtrRegMapRequired { get; set; }

    regMask SRBM_ALLFLOAT => Compiler.SRBM_ALLFLOAT;

    regMask SRBM_ALLINT => Compiler.SRBM_ALLINT;

    regMask SRBM_FLT_CALLEE_TRASH => Compiler.SRBM_FLT_CALLEE_TRASH;

    regMask SRBM_INT_CALLEE_TRASH => Compiler.SRBM_INT_CALLEE_TRASH;

    regNumber REG_INT_LAST => Compiler.REG_INT_LAST;

    regMask SRBM_ALLMASK => Compiler.SRBM_ALLMASK;

    regMask SRBM_MSK_CALLEE_TRASH => Compiler.SRBM_MSK_CALLEE_TRASH;

    bool validImmForInstr(instruction ins, int imm, insFlags flags = INS_FLAGS_DONT_CARE);

    bool validImmForInstr(instruction ins, nint imm, insFlags flags = INS_FLAGS_DONT_CARE);

    bool IsEmbeddedBroadcastEnabled(instruction ins, GenTree operand);

    ref RegSet RegSet { get; }

    /// <summary>indicates whether to align loops.</summary>
    /// <remarks>Used to avoid effects of loop alignment when diagnosing perf issues.</remarks>
    bool ShouldAlignLoops { get; set; }

#if DEBUG
    /// <summary>used to make sure the value of 'GetInterruptible()' isn't changed after it's been used by any logic that depends on its value.</summary>
    bool IsGcTypeFixed { get; }

    bool Verbose { get; set; }
#endif

    /// <summary>Call this function after the equivalent fields in Compiler have been initialized.</summary>
    void CopyRegisterInfo();

    bool genCreateAddrMode(GenTreeOp addr, bool fold, int naturalMul, out bool rev, out GenTree? rv1, out GenTree? rv2, out int mul, out nint cns);

    bool validImmForBL(nint address);

    unsafe void genGenerateCode(out void* codePtr, out int nativeSizeOfCode);

    instruction ins_Store(var_types dstType, bool aligned = false);

    void inst_TT_RV(instruction ins, emitAttr size, GenTree tree, regNumber reg);

    void genSpillVar(GenTree tree);

    void genUpdateLife(GenTree tree);

    void genUpdateLife(nint[] newLife);

    void initializeVariableLiveKeeper();

    CodeGen.VariableLiveKeeper getVariableLiveKeeper();

    CodeGen.siVarLoc getSiVarLoc(in LclVarDsc varDsc, int offset, int stackLevel);

    uint getCurrentStackLevel();

#if DEBUG
    void dumpSiVarLoc(in CodeGen.siVarLoc varLoc);
#endif

    void genUpdateVarReg(ref LclVarDsc local, GenTree tree);

    void genUpdateVarReg(ref LclVarDsc local, GenTree tree, byte registerIndex);

    regMaskTP genGetRegMask(in LclVarDsc local);

    regMaskTP genGetRegMask(GenTree tree);

    void genUpdateRegLife(in LclVarDsc local, bool isBorn, bool isDying
#if DEBUG
        , GenTree? tree
#endif
    );

    bool genUseOptimizedWriteBarriers(GCInfo.WriteBarrierForm writeBarrierForm);

    CorInfoHelpFunc genWriteBarrierHelperForWriteBarrierForm(GCInfo.WriteBarrierForm writeBarrierForm);

    void ResetWritePhaseForFramePointerRequired();

    regNumber GetFramePointerReg(int funcletIndex);

    regNumber GetStackPointerReg(int funcletIndex);

    void SetFramePointerReg(int funcletIndex, regNumber reg);

    void SetStackPointerReg(int funcletIndex, regNumber reg);

    regMaskTP genGetGSCookieTempRegs(bool tailCall, GenTreeCall? tailCallNode);
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public sealed class CodeGen : ICodeGen
{
#if LATE_DISASM
    private Disassembler _cgDisasm;
#endif

    private Emitter _cgEmitter;

    private PhasedVar<bool> _cgFramePointerRequired;
    private PhasedVar<bool> _cgFramePointerUsed;
    private PhasedVar<bool> _cgFrameRequired;

    private bool _cgInterruptible;
    private bool _cgFullPtrRegMap;

    private Compiler _compiler;

    private bool _genAlignLoops;
    private bool _genInterruptibleUsed;

#if TARGET_AMD64
    private regMaskFlt _rbmAllFloat;
    private regMaskInt _rbmAllInt;
    private regMaskFlt _rbmFltCalleeTrash;
    private regMaskInt _rbmIntCalleeTrash;
    private regNumber _regIntLast;
#endif

#if TARGET_XARCH
    private regMaskMsk _rbmAllMask;
    private regMaskMsk _rbmMskCalleeTrash;
#endif

    private bool _verbose;

    private GCInfo _gcInfo;

    private RegSet _regSet;

    public CodeGen(Compiler compiler)
    {
        _compiler = compiler;
        _cgEmitter = new Emitter();
    }

    public Compiler Compiler => _compiler;

#if LATE_DISASM
    public Disassembler Disassembler => _cgDisasm;
#endif

    public Emitter Emitter => _cgEmitter;

    public ref GCInfo GCInfo => ref _gcInfo;

    /// <summary>return the offset from Caller-SP to the frame pointer.</summary>
    /// <remarks>
    ///   <para>This number is going to be negative, since the Caller-SP is at a higher address than the frame pointer.</para>
    ///   <para>There must be a frame pointer to call this property!</para>
    ///   <para>For AMD64, We can't compute this directly from the Caller-SP, since the frame pointer is based on a maximum delta from Initial-SP, so first we find SP, then compute the FP offset.</para>
    /// </remarks>
    public int genCallerSPtoFPdelta
    {
        get
        {
            assert(Debugger.IsAttached || IsFramePointerUsed);
            var callerSPtoFPdelta = 0;

#if TARGET_ARM
            // On ARM, we first push the prespill registers, then store LR, then R11 (FP), and point R11 at the saved R11.
            callerSPtoFPdelta -= (int.PopCount(regSet.rsMaskPreSpillRegs(true)) * REGSIZE_BYTES);
            callerSPtoFPdelta -= (2 * REGSIZE_BYTES);
#elif TARGET_X86
            // Thanks to ebp chaining, the difference between ebp-based addresses
            // and caller-SP-relative addresses is just the 2 pointers:
            //     return address
            //     pushed ebp
            callerSPtoFPdelta -= (2 * REGSIZE_BYTES);
#else
            callerSPtoFPdelta -= genCallerSPtoInitialSPdelta + genSPtoFPdelta;
#endif

            assert(callerSPtoFPdelta <= 0);
            return callerSPtoFPdelta;
        }
    }

    /// <summary>return the offset from Caller-SP to Initial SP.</summary>
    /// <remarks>This number will be negative.</remarks>
    public int genCallerSPtoInitialSPdelta
    {
        get
        {
            var callerSPtoSPdelta = -genTotalFrameSize;

#if TARGET_ARM
            callerSPtoSPdelta -= (int.PopCount(regSet.rsMaskPreSpillRegs(true)) * REGSIZE_BYTES);
#elif TARGET_XARCH
            // caller-pushed return address
            callerSPtoSPdelta -= REGSIZE_BYTES;

            // compCalleeRegsPushed does not account for the frame pointer
            // TODO-Cleanup: shouldn't this be part of genTotalFrameSize?
            if (IsFramePointerUsed)
            {
                callerSPtoSPdelta -= REGSIZE_BYTES;
            }
#endif

            assert(callerSPtoSPdelta <= 0);
            return callerSPtoSPdelta;
        }
    }

    /// <summary>return the offset from SP to the frame pointer.</summary>
    /// <remarks>
    ///   <para>This number is going to be positive, since SP must be at the lowest address.</para>
    ///   <para>There must be a frame pointer to call this property!</para>
    /// </remarks>
    public int genSPtoFPdelta
    {
        get
        {
            assert(Debugger.IsAttached || IsFramePointerUsed);
            int delta;

#if TARGET_X86 || TARGET_ARM
            delta = genCallerSPtoFPdelta - genCallerSPtoInitialSPdelta;
#elif UNIX_AMD64_ABI
            // We require frame chaining on Unix to support native tool unwinding (such as
            // unwinding by the native debugger). We have a CLR-only extension to the
            // unwind codes (UWOP_SET_FPREG_LARGE) to support SP->FP offsets larger than 240.
            // If Unix ever supports EnC, the RSP == RBP assumption will have to be reevaluated.
            delta = genTotalFrameSize;
#elif TARGET_AMD64
            // As per Amd64 ABI, RBP offset from initial RSP can be between 0 and 240 if
            // RBP needs to be reported in unwind codes.  This case would arise for methods
            // with localloc.

            if (_compiler.compLocallocUsed)
            {
                // We cannot base delta computation on compLclFrameSize since it changes from
                // tentative to final frame layout and hence there is a possibility of
                // under-estimating offset of vars from FP, which in turn results in under-
                // estimating instruction size.
                //
                // To be predictive and so as never to under-estimate offset of vars from FP
                // we will always position FP at min(240, outgoing arg area size).
                delta = int.Min(240, _compiler.lvaOutgoingArgSpaceSize.Value);
            }
            else if (!_compiler.opts.compDbgEnC)
            {
                // vm assumption on EnC methods is that rsp and rbp are equal
                delta = 0;
            }
            else
            {
                delta = genTotalFrameSize;
            }
#elif TARGET_ARM64
            if (IsSaveFpLrWithAllCalleeSavedRegisters)
            {
                // The saved frame pointer is at the top of the frame, just beneath the saved varargs register space and the saved LR.
                delta = genTotalFrameSize - (_compiler.info.compIsVarArgs ? (MAX_REG_ARG * REGSIZE_BYTES) : 0) - (2 * REGSIZE_BYTES);
            }
            else
            {
                // We place the saved frame pointer immediately above the outgoing argument space.
                delta = _compiler.lvaOutgoingArgSpaceSize;
            }
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
            assert(_compiler.compCalleeRegsPushed >= 2); // always FP/RA.
            delta = _compiler.compLclFrameSize;

            if ((_compiler.lvaMonAcquired != BAD_VAR_NUM) && !_compiler.opts.IsOSR)
            {
                delta -= TARGET_POINTER_SIZE;
            }
#endif

            assert(delta >= 0);
            return delta;
        }
    }

    /// <summary>return the "total" size of the stack frame, including local size and callee-saved register size.</summary>
    /// <remarks>
    ///   <para>There are a few things "missing" depending on the platform. genCallerSPtoInitialSPdelta includes those things.</para>
    ///   <para>For ARM, this doesn't include the prespilled registers.</para>
    ///   <para>For x86, this doesn't include the frame pointer if IsFramePointerUsed is true. It also doesn't include the pushed return address.</para>
    ///   <para>For AMD64, this does not include the caller-pushed return address.</para>
    /// </remarks>
    public int genTotalFrameSize
    {
        get
        {
            assert(Debugger.IsAttached || (_compiler.compCalleeRegsPushed >= 0));
            var totalFrameSize = (_compiler.compCalleeRegsPushed * REGSIZE_BYTES) + _compiler.compLclFrameSize;

#if TARGET_ARM64
            // For varargs functions, we home all the incoming register arguments. They are not
            // included in the compCalleeRegsPushed count. This is like prespill on ARM32, but
            // since we don't use "push" instructions to save them, we don't have to do the
            // save of these varargs register arguments as the first thing in the prolog.

            if (_compiler.info.compIsVarArgs)
            {
                totalFrameSize += (MAX_REG_ARG * REGSIZE_BYTES);
            }
#endif

            assert(totalFrameSize >= 0);
            return totalFrameSize;
        }
    }

    public bool Interruptible
    {
        get
        {
            return _cgInterruptible;
        }

        set
        {
            _cgInterruptible = value;
        }
    }

    public bool IsFramePointerRequired
    {
        get
        {
            return _cgFramePointerRequired.Value;
        }

        set
        {
            _cgFramePointerRequired.Value = value;
        }
    }

    public bool IsFramePointerUsed
    {
        get
        {
            return _cgFramePointerUsed.Value;
        }

        set
        {
            _cgFramePointerUsed.Value = value;
        }
    }

    public bool IsFrameRequired
    {
        get
        {
            return _cgFrameRequired.Value;
        }

        set
        {
            _cgFrameRequired.Value = value;
        }
    }

    public bool IsFullPtrRegMapRequired
    {
        get
        {
            return _cgFullPtrRegMap;
        }

        set
        {
            _cgFullPtrRegMap = value;
        }
    }

#if TARGET_AMD64
    public regMaskFlt RBM_ALLFLOAT => _rbmAllFloat;

    public regMaskInt RBM_ALLINT => _rbmAllInt;

    public regMaskFlt RBM_FLT_CALLEE_TRASH => _rbmFltCalleeTrash;

    public regMaskInt RBM_INT_CALLEE_TRASH => _rbmIntCalleeTrash;

    public regNumber REG_INT_LAST => _regIntLast;
#endif

#if TARGET_XARCH
    public regMaskMsk RBM_ALLMASK => _rbmAllMask;

    public regMaskMsk RBM_MSK_CALLEE_TRASH => _rbmMskCalleeTrash;
#endif

    public ref RegSet RegSet => ref _regSet;

    public bool ShouldAlignLoops
    {
        get
        {
            return _genAlignLoops;
        }

        set
        {
            _genAlignLoops = value;
        }
    }

#if DEBUG
    public bool IsGcTypeFixed => _genInterruptibleUsed;

    public bool Verbose
    {
        get
        {
            return _verbose;
        }

        set
        {
            _verbose = value;
        }
    }
#endif

#if TARGET_XARCH
    public void CopyRegisterInfo()
    {
#if TARGET_AMD64
        _rbmAllFloat = _compiler.RBM_ALLFLOAT;
        _rbmFltCalleeTrash = _compiler.RBM_FLT_CALLEE_TRASH;
        _rbmAllInt = _compiler.RBM_ALLINT;
        _rbmIntCalleeTrash = _compiler.RBM_INT_CALLEE_TRASH;
        _regIntLast = _compiler.REG_INT_LAST;
#endif

        _rbmAllMask = _compiler.RBM_ALLMASK;
        _rbmMskCalleeTrash = _compiler.RBM_MSK_CALLEE_TRASH;
    }
#endif

    public unsafe void genGenerateCode(out void* codePtr, out int nativeSizeOfCode)
    {
        // TODO: Port CodeGen.genGenerateCode
        codePtr = null;
        nativeSizeOfCode = 0;
    }

#if HAS_FIXED_REGISTER_SET
    public regNumber GetFramePointerReg(int funcletIndex) => REG_FPBASE;

    public regNumber GetStackPointerReg(int funcletIndex) => REG_SPBASE;
#else
    public regNumber GetFramePointerReg(int funcletIndex)
    {
        assert(funcletIndex < _compiler.compFuncInfoCount);
        return _compiler.compFuncInfos[funcletIndex].funFramePointerReg;
    }

    public regNumber GetStackPointerReg(int funcletIndex)
    {
        assert(funcletIndex < _compiler.compFuncInfoCount);
        return _compiler.compFuncInfos[funcletIndex].funStackPointerReg;
    }
#endif
}

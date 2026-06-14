// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
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

    private NodeInternalRegisters _internalRegisters;

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

    public ref NodeInternalRegisters InternalRegisters => ref _internalRegisters;

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

    /// <summary>Take an address expression and try to find the best set of components to form an address mode; returns true if this is successful.</summary>
    /// <param name="addr">Tree that potentially computes an address</param>
    /// <param name="fold">Secifies if it is OK to fold the array index which hangs off a GT_NOP node.</param>
    /// <param name="naturalMul">For arm64 specifies the natural multiplier for the address mode (i.e. the size of the parent indirection).</param>
    /// <param name="rev">True if rv2 is before rv1 in the evaluation order</param>
    /// <param name="rv1">Base operand</param>
    /// <param name="rv2">Optional operand</param>
    /// <param name="mul">Optional multiplier for rv2. If non-zero and naturalMul is non-zero, it must match naturalMul.</param>
    /// <param name="cns">Integer constant [optional]</param>
    /// <returns></returns>
    public bool genCreateAddrMode(GenTreeOp addr, bool fold, int naturalMul, out bool rev, out GenTree? rv1, out GenTree? rv2, out int mul, out nint cns)
    {
        rev = false;
        rv1 = null;
        rv2 = null;
        mul = 0;
        cns = 0;

#if TARGET_WASM
        // TODO-WASM: Prove whether a given addressing mode obeys the Wasm rules.
        // See https://github.com/dotnet/runtime/pull/122897#issuecomment-3721304477 for more details.

        return false;
#else
        // The following indirections are valid address modes on x86/x64:
        // 
        //     [                  icon]      * not handled here
        //     [reg                   ]
        //     [reg             + icon]
        //     [reg1 +     reg2       ]
        //     [reg1 +     reg2 + icon]
        //     [reg1 + 2 * reg2       ]
        //     [reg1 + 4 * reg2       ]
        //     [reg1 + 8 * reg2       ]
        //     [       2 * reg2 + icon]
        //     [       4 * reg2 + icon]
        //     [       8 * reg2 + icon]
        //     [reg1 + 2 * reg2 + icon]
        //     [reg1 + 4 * reg2 + icon]
        //     [reg1 + 8 * reg2 + icon]
        // 
        // The following indirections are valid address modes on arm64:
        // 
        //     [reg]
        //     [reg  + icon]
        //     [reg1 + reg2]
        //     [reg1 + reg2 * natural-scale]
        // 
        // The following indirections are valid address modes on riscv64:
        // 
        //     [reg]
        //     [reg  + icon]

        // All indirect address modes require the address to be an addition
        // Can't use indirect addressing mode as we need to check for overflow.
        // Also, can't use 'lea' as it doesn't set the flags.

        if ((addr.Oper is not GT_ADD) || addr.HasOverflowCheck)
        {
            return false;
        }

        // What order are the sub-operands to be evaluated

        var op1 = addr.Op1;
        var op2 = addr.Op2;

        if ((addr.Flags & GTF_REVERSE_OPS) is not 0)
        {
            (op1, op2) = (op2, op1);
        }

        // A complex address mode can combine the following operands:
        // 
        //     op1     ...     base address
        //     op2     ...     optional scaled index
        //     mul     ...     optional multiplier (2/4/8) for op2
        //     cns     ...     optional displacement
        // 
        // Here we try to find such a set of operands and arrange for these
        // to sit in registers.

        // We come back to 'AGAIN' if we have an add of a constant, and we are folding that
        // constant, or we have gone through a GT_NOP or GT_COMMA node. We never come back
        // here if we find a scaled index.
        var again = true;
        var foundAm = false;

        while (again)
        {
            again = false;
            assert(mul == 0);

            // Special case: keep constants as 'op2', but don't do this for constant handles
            // because they don't fit I32 that we're going to check for below anyway.

            if (op1.Oper.IsCnsIntOrI && !op1.AsIntCon().IsIconHandle())
            {
                // Presumably op2 is assumed to not be a constant (shouldn't happen if we've done constant folding)?
                (op1, op2) = (op2, op1);
            }

            // Check for an addition of a constant

            if (op2.Oper.IsCnsIntOrI)
            {
                var op2IntCon = op2.AsIntCon();
                var newCns = cns + op2IntCon.IconValue;

                if (op2IntCon.FitsInI32 && op2IntCon.ImmedValCanBeFolded(_compiler, addr.Oper) && (op2.Type is not TYP_REF) && FitsInI32(newCns))
                {
                    cns = newCns;

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
                    if (cns == 0)
#endif
                    {
                        // Inspect the operand the constant is being added to

                        switch (op1.Oper)
                        {
                            case GT_ADD:
                            {
                                if (op1.HasOverflowCheck)
                                {
                                    break;
                                }

                                var add = op1.AsOp();

                                op1 = add.Op1;
                                op2 = add.Op2;

                                again = true;
                                break;
                            }

                            case GT_MUL:
                            {
                                // TODO-ARM-CQ: For now we don't try to create a scaled index.

                                if (op1.HasOverflowCheck)
                                {
                                    // Need overflow check

                                    rv1 = null;
                                    rv2 = null;
                                    mul = 0;
                                    cns = 0;

                                    return false;
                                }

                                goto case GT_LSH;
                            }

                            case GT_LSH:
                            {
                                var mulCandidate = op1.ScaledIndex;

                                if (jitIsScaleIndexMul(mulCandidate, naturalMul))
                                {
                                    // We can use "[(mul * rv2) + icon]"

                                    mul = mulCandidate;

                                    rv1 = null;
                                    rv2 = op1.AsOp().Op1;

                                    foundAm = true;
                                }
                                break;
                            }

                            default:
                            {
                                break;
                            }
                        }
                    }

                    if (foundAm)
                    {
                        break;
                    }

                    if (again)
                    {
                        continue;
                    }

                    // The best we can do is "[rv1 + icon]"

                    rv1 = op1;
                    rv2 = null;

                    foundAm = true;
                    break;
                }
            }

            // op2 is not a constant. So keep on trying.

            // Neither op1 nor op2 are sitting in a register right now

            switch (op1.Oper)
            {
#if TARGET_XARCH || TARGET_RISCV64
                // TODO-ARM-CQ: For now we don't try to create a scaled index.
                case GT_ADD:
                {
                    if (op1.HasOverflowCheck)
                    {
                        break;
                    }

                    var add = op1.AsOp();

                    if (add.Op2.IsIntCnsFitsInI32)
                    {
                        var addConst = add.Op2.AsIntCon();
                        var newCns = cns + addConst.IconValue;

                        if (addConst.ImmedValCanBeFolded(_compiler, GT_ADD) && FitsInI32(newCns))
                        {
                            cns = newCns;
                            op1 = add.Op1;

                            again = true;
                        }
                    }
                    break;
                }
#endif

                case GT_MUL:
                {
                    if (op1.HasOverflowCheck)
                    {
                        break;
                    }
                    goto case GT_LSH;
                }

                case GT_LSH:
                {
                    var mulCandidate = op1.ScaledIndex;

                    if (jitIsScaleIndexMul(mulCandidate, naturalMul))
                    {
                        // 'op1' is a scaled value
                        mul = mulCandidate;

                        rv1 = op2;
                        rv2 = op1.AsOp().Op1;

                        int argScale;
                        while ((rv2.Oper is GT_MUL or GT_LSH) && ((argScale = rv2.ScaledIndex) is not 0))
                        {
                            if (jitIsScaleIndexMul(argScale * mul, naturalMul))
                            {
                                mul *= argScale;
                                rv2 = rv2.AsOp().Op1;
                            }
                            else
                            {
                                break;
                            }
                        }

                        noway_assert(rev is false);
                        rev = true;

                        foundAm = true;
                    }
                    break;
                }

                case GT_COMMA:
                {
                    op1 = op1.AsOp().Op2;
                    again = true;
                    break;
                }

                default:
                {
                    break;
                }
            }

            if (foundAm)
            {
                break;
            }

            if (again)
            {
                continue;
            }

            noway_assert(op2 is not null);

            switch (op2.Oper)
            {
#if TARGET_XARCH || TARGET_RISCV64
                // TODO-ARM64-CQ, TODO-ARM-CQ: For now we only handle MUL and LSH because
                // arm doesn't support both scale and offset at the same. Offset is handled
                // at the emitter as a peephole optimization.
                case GT_ADD:
                {
                    if (op2.HasOverflowCheck)
                    {
                        break;
                    }

                    var add = op2.AsOp();
                    var maybeIntCon = add.Op2;

                    if (maybeIntCon.Oper.IsCnsIntOrI)
                    {
                        var addConst = maybeIntCon.AsIntCon();
                        var newCns = cns + addConst.IconValue;

                        if (addConst.ImmedValCanBeFolded(_compiler, GT_ADD) && FitsInI32(newCns))
                        {
                            cns = newCns;
                            op2 = add.Op1;
                            again = true;
                        }
                    }
                    break;
                }
#endif

                case GT_MUL:
                {
                    if (op2.HasOverflowCheck)
                    {
                        break;
                    }
                    goto case GT_LSH;
                }

                case GT_LSH:
                {
                    var mulCandidate = op2.ScaledIndex;

                    if (jitIsScaleIndexMul(mulCandidate, naturalMul))
                    {
                        mul = mulCandidate;

                        // 'op2' is a scaled value...is it's argument also scaled?
                        rv2 = op2.AsOp().Op1;

                        while (rv2.Oper is GT_MUL or GT_LSH)
                        {
                            var argScale = rv2.ScaledIndex;

                            if (argScale is 0)
                            {
                                break;
                            }

                            if (jitIsScaleIndexMul(argScale * mul, naturalMul))
                            {
                                mul *= argScale;
                                rv2 = rv2.AsOp().Op1;
                            }
                            else
                            {
                                break;
                            }
                        }

                        rv1 = op1;
                        foundAm = true;
                    }
                    break;
                }

                case GT_COMMA:
                {
                    op2 = op2.AsOp().Op2;
                    again = true;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        if (!foundAm)
        {
            // The best we can do "[rv1 + rv2]" or "[rv1 + rv2 + cns]"

            rv1 = op1;
            rv2 = op2;

#if TARGET_ARM64
            assert(cns == 0);
#endif
        }

#if TARGET_RISCV64
        assert(mul == 0 || mul == 1);
#endif

        if (rv2 is not null)
        {
            // Make sure a GC address doesn't end up in 'rv2'
            if (varTypeIsGC(rv2.Type))
            {
                (rv1, rv2) = (rv2, rv1);
                rev = !rev;
            }

            // Special case: constant array index (that is range-checked)
            if (fold)
            {
                // By default, assume index is rv2 and indexScale is mul (or 1 if mul is zero)
                var index = rv2;
                var indexScale = (mul == 0) ? 1 : mul;

                if (rv2.Oper is GT_MUL or GT_LSH)
                {
                    var rv2Op = rv2.AsOp();
                    var maybeIntCon = rv2Op.Op2;

                    if (maybeIntCon.Oper.IsCnsIntOrI)
                    {
                        indexScale *= (int)(_compiler.optGetArrayRefScaleAndIndex(rv2Op, out index, bRngChk: false));
                    }
                }

                // "index * 0" means index is zero
                if (indexScale == 0)
                {
                    mul = 0;
                    rv2 = null;
                }
                else if (index.Oper.IsCnsIntOrI)
                {
                    var indexConst = index.AsIntCon();

                    if (!indexConst.ImmedValNeedsReloc(_compiler))
                    {
                        var constantIndex = indexConst.IconValue * indexScale;
                        var newCns = cns + constantIndex;

                        if (constantIndex == 0)
                        {
                            // while scale is a non-zero constant, the actual index is zero so drop it
                            mul = 0;
                            rv2 = null;
                        }
                        else if (FitsInI32(newCns))
                        {
                            // Add the constant index to the accumulated offset value and get rid of index
                            cns = newCns;
                            mul = 0;
                            rv2 = null;
                        }
                    }
                }
            }
        }

        // We shouldn't have [rv2*1 + cns] - this is equivalent to [rv1 + cns]
        noway_assert((rv1 is not null) || (mul is not 1));
        noway_assert(FitsInI32(cns));

        if ((rv1 is null) && (rv2 is null))
        {
            return false;
        }

        return true;
#endif
    }

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

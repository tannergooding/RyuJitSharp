// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    public unsafe void genGenerateCode(out void* codePtr, out uint nativeSizeOfCode)
    {
        // TODO: Port CodeGen.genGenerateCode
        codePtr = null;
        nativeSizeOfCode = 0;
    }
}

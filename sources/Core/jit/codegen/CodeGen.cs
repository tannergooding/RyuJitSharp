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

    private bool _genAlignLoops;

    public CodeGen(Compiler compiler)
    {
        // TODO: Port CodeGen.ctor
        _cgEmitter = new Emitter();
    }

#if LATE_DISASM
    public Disassembler Disassembler => _cgDisasm;
#endif

    public Emitter Emitter => _cgEmitter;

    /// <summary>Indicates whether the current method requires an explicit stack frame, and all arguments and locals to be accessible relative to the Frame Pointer.</summary>
    /// <remarks>Prohibits double alignment of the stack.</remarks>
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

    /// <summary>Indicates whether the current method requires an explicit frame.</summary>
    /// <remarks>Does not prohibit double alignment of the stack.</remarks>
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

    /// <summary>indicates whether to align loops.</summary>
    /// <remarks>Used to avoid effects of loop alignment when diagnosing perf issues.</remarks>
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
}

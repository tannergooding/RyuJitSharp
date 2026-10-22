// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal sealed class GcInfoEncoderWithLogging
{
    private readonly GcInfoEncoder _encoder;
    private readonly bool _log;

    public GcInfoEncoderWithLogging(GcInfoEncoder encoder, Compiler compiler)
    {
        _encoder = encoder;
        if (Configuration.DEBUG)
        {
            _log = compiler.verbose || compiler.dspGCtbls || (JitConfig.JitGCInfoLogging != 0);
        }
        else
        {
            if (Configuration.DUMP_GC_TABLES)
            {
                _log = compiler.dspGCtbls;
            }
            else
            {
                _log = false;
            }
        }
    }

    private static string GetStackBaseName(GcStackSlotBase stackBase)
    {
        switch (stackBase)
        {
            case GcStackSlotBase.GC_CALLER_SP_REL:
            {
                return "caller.sp";
            }

            case GcStackSlotBase.GC_SP_REL:
            {
                return "sp";
            }

            case GcStackSlotBase.GC_FRAMEREG_REL:
            {
                return "frame";
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidStackSlotBase);
                return default!;
            }
        }
    }

    private static string GetSlotFlagName(GcSlotFlags flags)
    {
        switch (((int)flags & 7))
        {
            case 0:
            {
                return "";
            }

            case 1:
            {
                return "(byref) ";
            }

            case 2:
            {
                return "(pinned) ";
            }

            case 3:
            {
                return "(byref, pinned) ";
            }

            case 4:
            {
                return "(untracked) ";
            }

            case 5:
            {
                return "(byref, untracked) ";
            }

            case 6:
            {
                return "(pinned, untracked) ";
            }

            case 7:
            {
                return "(byref, pinned, untracked) ";
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.InvalidGCSlotFlags);
                return default!;
            }
        }
    }

    public uint GetStackSlotId(int offset, GcSlotFlags flags, GcStackSlotBase stackBase = GcStackSlotBase.GC_CALLER_SP_REL)
    {
        var id = _encoder.GetStackSlotId(offset, flags, stackBase);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Stack slot id for offset {offset} ({(offset < 0 ? "-" : "")}0x{(offset < 0 ? -(long)offset : offset):x}) " +
                $"({GetStackBaseName(stackBase)}) {GetSlotFlagName(flags)}= {id}.\n");
        }
        return id;
    }

    public uint GetRegisterSlotId(uint regNum, GcSlotFlags flags)
    {
        var id = _encoder.GetRegisterSlotId(regNum, flags);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Register slot id for reg {((regNumber)regNum).Name} {GetSlotFlagName(flags)}= {id}.\n");
        }
        return id;
    }

    public void SetSlotState(uint offset, uint id, GcSlotState state)
    {
        _encoder.SetSlotState(offset, id, state);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set state of slot {id} at instr offset 0x{offset:x} to " +
                $"{(state == GcSlotState.GC_SLOT_LIVE ? "Live" : "Dead")}.\n");
        }
    }

    public unsafe void DefineCallSites(uint* offsets, byte* sizes, uint count)
    {
        _encoder.DefineCallSites(offsets, sizes, count);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Defining {count} call sites:\n");
            for (var index = 0u; index < count; index++)
            {
                jitprintf($"    Offset 0x{offsets[index]:x}, size {sizes[index]}.\n");
            }
        }
    }

    public void DefineInterruptibleRange(uint start, uint length)
    {
        _encoder.DefineInterruptibleRange(start, length);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Defining interruptible range: [0x{start:x}, 0x{unchecked(start + length):x}).\n");
        }
    }

    public void SetCodeLength(uint length)
    {
        _encoder.SetCodeLength(length);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set code length to {length}.\n");
        }
    }

    public void SetStackBaseRegister(uint reg)
    {
        _encoder.SetStackBaseRegister(reg);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set stack base register to {((regNumber)reg).Name}.\n");
        }
    }

    public void SetPrologSize(uint size)
    {
        _encoder.SetPrologSize(size);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set prolog size 0x{size:x}.\n");
        }
    }

    public void SetGSCookieStackSlot(int offset, uint start, uint end)
    {
        _encoder.SetGSCookieStackSlot(offset, start, end);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set GS Cookie stack slot to {offset}, valid from 0x{start:x} to 0x{end:x}.\n");
        }
    }

    public void SetGenericsInstContextStackSlot(int offset, GENERIC_CONTEXTPARAM_TYPE type)
    {
        _encoder.SetGenericsInstContextStackSlot(offset, type);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            var typeName = type switch {
                GENERIC_CONTEXTPARAM_TYPE.GENERIC_CONTEXTPARAM_THIS => "THIS",
                GENERIC_CONTEXTPARAM_TYPE.GENERIC_CONTEXTPARAM_MT => "MT",
                GENERIC_CONTEXTPARAM_TYPE.GENERIC_CONTEXTPARAM_MD => "MD",
                _ => "UNKNOWN!",
            };
            jitprintf($"Set generic instantiation context stack slot to {offset}, type is {typeName}.\n");
        }
    }

    public void SetIsVarArg()
    {
        _encoder.SetIsVarArg();
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf("SetIsVarArg.\n");
        }
    }

    public void SetWantsReportOnlyLeaf()
    {
        _encoder.SetWantsReportOnlyLeaf();
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf("Set WantsReportOnlyLeaf.\n");
        }
    }

    public void SetSizeOfStackOutgoingAndScratchArea(uint size)
    {
        _encoder.SetSizeOfStackOutgoingAndScratchArea(size);
        if ((Configuration.DEBUG || Configuration.DUMP_GC_TABLES) && _log)
        {
            jitprintf($"Set Outgoing stack arg area size to {size}.\n");
        }
    }
}

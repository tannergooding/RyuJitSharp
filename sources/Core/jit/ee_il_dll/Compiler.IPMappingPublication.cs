// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void eeSetLIcount(uint count)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "IP mapping allocation requires Windows AMD64.");
#else
        assert(opts.compDbgInfo);

        eeBoundariesCount = unchecked((int)count);
        eeBoundaries = count != 0
            ? (ICorDebugInfo.OffsetMapping*)info.compCompHnd->allocateArray(
                unchecked((nint)((nuint)count * (nuint)sizeof(ICorDebugInfo.OffsetMapping))))
            : null;
#endif
    }

    public unsafe void eeSetLIinfo(uint which, uint nativeOffset, IPmappingDscKind kind, in ILLocation loc)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "IP mapping recording requires Windows AMD64.");
#else
        assert(opts.compDbgInfo);
        assert((eeBoundariesCount > 0) && (eeBoundaries != null));
        assert(which < unchecked((uint)eeBoundariesCount));

        eeBoundaries[which].nativeOffset = unchecked((int)nativeOffset);
        eeBoundaries[which].source = 0;

        switch (kind)
        {
            case IPmappingDscKind.Normal:
            {
                eeBoundaries[which].ilOffset = loc.Offset;
                eeBoundaries[which].source = loc.SourceTypes;
                break;
            }

            case IPmappingDscKind.Prolog:
            {
                eeBoundaries[which].ilOffset = (int)ICorDebugInfo.MappingTypes.PROLOG;
                eeBoundaries[which].source = ICorDebugInfo.STACK_EMPTY;
                break;
            }

            case IPmappingDscKind.Epilog:
            {
                eeBoundaries[which].ilOffset = (int)ICorDebugInfo.MappingTypes.EPILOG;
                eeBoundaries[which].source = ICorDebugInfo.STACK_EMPTY;
                break;
            }

            case IPmappingDscKind.NoMapping:
            {
                eeBoundaries[which].ilOffset = (int)ICorDebugInfo.MappingTypes.NO_MAPPING;
                eeBoundaries[which].source = ICorDebugInfo.STACK_EMPTY;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_SKIPPED, "Unknown IP mapping kind.");
            }
        }
#endif
    }

    public unsafe void eeSetLIdone()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "IP mapping publication requires Windows AMD64.");
#else
        assert(opts.compDbgInfo);
#if DEBUG
        if (verbose || opts.dspDebugInfo)
        {
            eeDispLineInfos();
        }
#endif
        assert(sizeof(ICorDebugInfo.OffsetMapping) == 12);

        info.compCompHnd->setBoundaries(info.compMethodHnd, eeBoundariesCount, eeBoundaries);
        eeBoundaries = null;
#endif
    }

#if DEBUG
    public unsafe void eeDispLineInfos()
    {
        jitprintf($"IP mapping count : {eeBoundariesCount}\n");
        for (var index = 0; index < eeBoundariesCount; index++)
        {
            eeDispLineInfo(eeBoundaries + index);
        }
        jitprintf("\n");
    }

    public static unsafe void eeDispLineInfo(ICorDebugInfo.OffsetMapping* line)
    {
        jitprintf("IL offs ");
        switch (line->ilOffset)
        {
            case (int)ICorDebugInfo.MappingTypes.EPILOG:
            {
                jitprintf("EPILOG");
                break;
            }

            case (int)ICorDebugInfo.MappingTypes.PROLOG:
            {
                jitprintf("PROLOG");
                break;
            }

            case (int)ICorDebugInfo.MappingTypes.NO_MAPPING:
            {
                jitprintf("NO_MAP");
                break;
            }

            default:
            {
                eeDispILOffs(line->ilOffset);
                break;
            }
        }

        jitprintf($" : 0x{unchecked((uint)line->nativeOffset):X8}");
        if (line->source != 0)
        {
            jitprintf(" ( ");
            if ((line->source & ICorDebugInfo.STACK_EMPTY) != 0)
            {
                jitprintf("STACK_EMPTY ");
            }
            if ((line->source & ICorDebugInfo.CALL_INSTRUCTION) != 0)
            {
                jitprintf("CALL_INSTRUCTION ");
            }
            if ((line->source & ICorDebugInfo.CALL_SITE) != 0)
            {
                jitprintf("CALL_SITE ");
            }
            if ((line->source & ICorDebugInfo.ASYNC) != 0)
            {
                jitprintf("ASYNC ");
            }
            jitprintf(")");
        }
        jitprintf("\n");
        assert((line->source & ~(ICorDebugInfo.STACK_EMPTY | ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC)) == 0);
    }
#endif
}

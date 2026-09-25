// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genAddRichIPMappingHere(in DebugInfo di)
    {
        var mapping = new RichIPMapping
        {
            nativeLoc = new emitLocation(Emitter),
            debugInfo = di,
        };
        _ = _compiler.genRichIPmappings.AddLast(mapping);
    }

    public void genIPmappingAdd(IPmappingDscKind kind, in DebugInfo di, bool isLabel)
    {
        if (!_compiler.opts.compDbgInfo)
        {
            return;
        }
        assert((kind == IPmappingDscKind.Normal) == di.IsValid);

        switch (kind)
        {
            case IPmappingDscKind.Prolog:
            case IPmappingDscKind.Epilog:
            {
                break;
            }

            default:
            {
                if (kind == IPmappingDscKind.Normal)
                {
                    noway_assert(unchecked((uint)di.Location.Offset) <= unchecked((uint)_compiler.info.compILCodeSize));
                }

                var last = _compiler.genIPmappings.Last;
                if ((last is not null) && (kind == last.Value.ipmdKind) && (di.Location == last.Value.ipmdLoc))
                {
                    JITDUMP($"genIPmappingAdd: ignoring duplicate IL offset 0x{di.Location.Offset:x}\n");
                    return;
                }
                break;
            }
        }

        var mapping = new IPmappingDsc
        {
            ipmdNativeLoc = new emitLocation(Emitter),
            ipmdKind = kind,
            ipmdLoc = di.Location,
            ipmdIsLabel = isLabel,
        };
        assert((kind == IPmappingDscKind.Normal) == mapping.ipmdLoc.IsValid);
        _ = _compiler.genIPmappings.AddLast(mapping);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("Added IP mapping: ");
            genIPmappingDisp(uint.MaxValue, in mapping);
        }
#endif
    }

    public void genIPmappingAddToFront(IPmappingDscKind kind, in DebugInfo di, bool isLabel)
    {
        if (!_compiler.opts.compDbgInfo)
        {
            return;
        }

        noway_assert((kind != IPmappingDscKind.Normal) ||
            (di.IsValid && (unchecked((uint)di.Location.Offset) <= unchecked((uint)_compiler.info.compILCodeSize))));
        var mapping = new IPmappingDsc
        {
            ipmdNativeLoc = new emitLocation(Emitter),
            ipmdKind = kind,
            ipmdLoc = di.Location,
            ipmdIsLabel = isLabel,
        };
        _ = _compiler.genIPmappings.AddFirst(mapping);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("Added IP mapping to front: ");
            genIPmappingDisp(uint.MaxValue, in mapping);
        }
#endif
    }

    public void genEnsureCodeEmitted(in DebugInfo di)
    {
        if (!_compiler.opts.compDbgCode || !di.IsValid)
        {
            return;
        }

        var last = _compiler.genIPmappings.Last;
        if ((last is null) || (last.Value.ipmdLoc != di.Location))
        {
            return;
        }

        if (last.Value.ipmdNativeLoc.IsCurrentLocation(Emitter))
        {
            instGen(INS_nop);
        }
    }

#if DEBUG
    public void genIPmappingDisp(uint mappingNum, in IPmappingDsc mapping)
    {
        if (mappingNum != uint.MaxValue)
        {
            jitprintf($"{unchecked((int)mappingNum)}: ");
        }

        switch (mapping.ipmdKind)
        {
            case IPmappingDscKind.Prolog:
            {
                jitprintf("PROLOG");
                break;
            }

            case IPmappingDscKind.Epilog:
            {
                jitprintf("EPILOG");
                break;
            }

            case IPmappingDscKind.NoMapping:
            {
                jitprintf("NO_MAP");
                break;
            }

            case IPmappingDscKind.Normal:
            {
                var location = mapping.ipmdLoc;
                Compiler.eeDispILOffs(location.Offset);
                if ((location.SourceTypes & ICorDebugInfo.STACK_EMPTY) != 0)
                {
                    jitprintf(" STACK_EMPTY");
                }
                if (location.IsCallInstruction)
                {
                    jitprintf(" CALL_INSTRUCTION");
                }
                if (location.IsAsync)
                {
                    jitprintf(" ASYNC");
                }
                break;
            }
        }

        jitprintf(" ");
        mapping.ipmdNativeLoc.Print(_compiler.compMethodID);
        if (mapping.ipmdIsLabel)
        {
            jitprintf(" label");
        }
        jitprintf("\n");
    }

    public void genIPmappingListDisp()
    {
        uint mappingNum = 0;
        foreach (var mapping in _compiler.genIPmappings)
        {
            genIPmappingDisp(mappingNum, in mapping);
            mappingNum = unchecked(mappingNum + 1);
        }
    }
#endif
}

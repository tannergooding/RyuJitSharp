// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIPmappingGen()
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "IP mapping publication requires Windows AMD64.");
#else
        if (!_compiler.opts.compDbgInfo)
        {
            return;
        }

        JITDUMP("*************** In genIPmappingGen()\n");

        var mappings = _compiler.genIPmappings;
        if (mappings.Count == 0)
        {
            _compiler.eeSetLIcount(0);
            _compiler.eeSetLIdone();
            return;
        }

        var prevNativeOfs = uint.MaxValue;
        var current = mappings.First;
        while (current is not null)
        {
            var dscNativeOfs = current.Value.ipmdNativeLoc.CodeOffset(Emitter);
            if (dscNativeOfs != prevNativeOfs)
            {
                prevNativeOfs = dscNativeOfs;
                current = current.Next;
                continue;
            }

            assert(current != mappings.First);
            var prev = current.Previous
                ?? throw new FatalJitException(CORJIT_SKIPPED, "IP mapping has no predecessor at a duplicate native offset.");

            if (prev.Value.ipmdKind == IPmappingDscKind.NoMapping)
            {
                mappings.Remove(prev);
                current = current.Next;
                continue;
            }

            if (current.Value.ipmdKind == IPmappingDscKind.NoMapping)
            {
                var next = current.Next;
                mappings.Remove(current);
                current = next;
                continue;
            }

            // Keep the prolog and IL zero mapping even when they share a native offset.
            if ((prev.Value.ipmdKind == IPmappingDscKind.Prolog) &&
                (current.Value.ipmdKind == IPmappingDscKind.Normal) &&
                (current.Value.ipmdLoc.Offset == 0))
            {
                current = current.Next;
                continue;
            }

            // An empty return and its epilog remain separate stepper boundaries.
            if (current.Value.ipmdKind == IPmappingDscKind.Epilog)
            {
                current = current.Next;
                continue;
            }

            // Managed return values retain all call instruction mappings.
            if (((prev.Value.ipmdKind == IPmappingDscKind.Normal) && prev.Value.ipmdLoc.IsCallInstruction) ||
                ((current.Value.ipmdKind == IPmappingDscKind.Normal) && current.Value.ipmdLoc.IsCallInstruction))
            {
                current = current.Next;
                continue;
            }

            if (prev.Value.ipmdIsLabel)
            {
                var next = current.Next;
                mappings.Remove(current);
                current = next;
            }
            else
            {
                mappings.Remove(prev);
                current = current.Next;
            }
        }

        _compiler.eeSetLIcount(unchecked((uint)mappings.Count));

        uint mappingIdx = 0;
        foreach (var mapping in mappings)
        {
            _compiler.eeSetLIinfo(mappingIdx++, mapping.ipmdNativeLoc.CodeOffset(Emitter),
                mapping.ipmdKind, in mapping.ipmdLoc);
        }

        _compiler.eeSetLIdone();
#endif
    }
}

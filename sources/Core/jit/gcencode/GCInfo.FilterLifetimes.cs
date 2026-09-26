// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.CompilerServices;
#endif

namespace RyuJitSharp;

public partial struct GCInfo
{
    public void gcMarkFilterVarsPinned()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "GC filter lifetimes require Windows AMD64.");
#else
        var compiler = Compiler;
        assert(compiler.compHndBBtabCount > 0);
        foreach (ref var handler in new EHClauses(compiler))
        {
            if (!handler.HasFilter)
            {
                continue;
            }
            var filter = handler.ebdFilter
                ?? throw new FatalJitException(CORJIT_SKIPPED, "Filter has no starting block.");
            var filterBeg = compiler.ehCodeOffset(filter);
            var filterEnd = compiler.ehCodeOffset(handler.ebdHndBeg);

            for (var variable = gcVarPtrList; variable is not null; variable = variable.vpdNext)
            {
                var lowBits = variable.vpdVarNum & OffsetMask;
                var begin = variable.vpdBegOfs;
                var end = variable.vpdEndOfs;
                if ((end == begin) || (end <= filterBeg) || (begin >= filterEnd))
                {
                    continue;
                }
                assert((lowBits & PinnedOffsetFlag) == 0);

                if (begin < filterBeg)
                {
                    if (end > filterEnd)
                    {
#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf($"Splitting lifetime for filter: [{filterBeg:X4}, {filterEnd:X4}).\nOld: ");
                            gcDumpVarPtrDsc(variable);
                        }
#endif
                        var pinned = new varPtrDsc
                        {
                            vpdVarNum = variable.vpdVarNum | PinnedOffsetFlag,
                            vpdBegOfs = filterBeg,
                            vpdEndOfs = filterEnd,
                        };
                        var after = new varPtrDsc
                        {
                            vpdVarNum = variable.vpdVarNum,
                            vpdBegOfs = filterEnd,
                            vpdEndOfs = end,
                        };
                        variable.vpdEndOfs = filterBeg;
                        gcInsertVarPtrDscSplit(pinned, variable);
                        gcInsertVarPtrDscSplit(after, variable);
#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf("New (1 of 3): ");
                            gcDumpVarPtrDsc(variable);
                            jitprintf("New (2 of 3): ");
                            gcDumpVarPtrDsc(pinned);
                            jitprintf("New (3 of 3): ");
                            gcDumpVarPtrDsc(after);
                        }
#endif
                    }
                    else
                    {
#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf("Splitting lifetime for filter.\nOld: ");
                            gcDumpVarPtrDsc(variable);
                        }
#endif
                        var pinned = new varPtrDsc
                        {
                            vpdVarNum = variable.vpdVarNum | PinnedOffsetFlag,
                            vpdBegOfs = filterBeg,
                            vpdEndOfs = end,
                        };
                        variable.vpdEndOfs = filterBeg;
                        gcInsertVarPtrDscSplit(pinned, variable);
#if DEBUG
                        if (compiler.verbose)
                        {
                            jitprintf("New (1 of 2): ");
                            gcDumpVarPtrDsc(variable);
                            jitprintf("New (2 of 2): ");
                            gcDumpVarPtrDsc(pinned);
                        }
#endif
                    }
                }
                else if (end > filterEnd)
                {
#if DEBUG
                    if (compiler.verbose)
                    {
                        jitprintf("Splitting lifetime for filter.\nOld: ");
                        gcDumpVarPtrDsc(variable);
                    }
#endif
                    var pinned = new varPtrDsc
                    {
                        vpdVarNum = variable.vpdVarNum | PinnedOffsetFlag,
                        vpdBegOfs = begin,
                        vpdEndOfs = filterEnd,
                    };
                    variable.vpdBegOfs = filterEnd;
                    gcInsertVarPtrDscSplit(pinned, variable);
#if DEBUG
                    if (compiler.verbose)
                    {
                        jitprintf("New (1 of 2): ");
                        gcDumpVarPtrDsc(pinned);
                        jitprintf("New (2 of 2): ");
                        gcDumpVarPtrDsc(variable);
                    }
#endif
                }
                else
                {
#if DEBUG
                    if (compiler.verbose)
                    {
                        jitprintf("Pinning lifetime for filter.\nOld: ");
                        gcDumpVarPtrDsc(variable);
                    }
#endif
                    variable.vpdVarNum |= PinnedOffsetFlag;
#if DEBUG
                    if (compiler.verbose)
                    {
                        jitprintf("New : ");
                        gcDumpVarPtrDsc(variable);
                    }
#endif
                }
            }
        }
#endif
    }

    private void gcInsertVarPtrDscSplit(varPtrDsc descriptor, varPtrDsc begin)
    {
        assert(begin is not null);
        descriptor.vpdNext = gcVarPtrList;
        gcVarPtrList = descriptor;
    }

#if DEBUG
    private readonly unsafe void gcDumpVarPtrDsc(varPtrDsc descriptor)
    {
        var offset = unchecked((int)(descriptor.vpdVarNum & ~OffsetMask));
        var gcType = (descriptor.vpdVarNum & ByrefOffsetFlag) != 0 ? "byr" : "gcr";
        var pinned = (descriptor.vpdVarNum & PinnedOffsetFlag) != 0 ? "pinned-ptr" : "";
        var location = Compiler.IsFramePointerUsed ? STR_FPBASE : STR_SPBASE;
        var displacement = offset switch
        {
            < 0 => $"-0x{unchecked((uint)-offset):X2}",
            > 0 => $"+0x{offset:X2}",
            _ => "",
        };
        // The object address is used only for this dump; the GC may relocate it afterward.
        var address = Compiler.dspOffset(Unsafe.As<varPtrDsc, nint>(ref descriptor));
        jitprintf($"[{FMT_PTR((void*)address)}] {gcType}{pinned} var at [{location}{displacement}] " +
            $"live from {descriptor.vpdBegOfs:X4} to {descriptor.vpdEndOfs:X4}\n");
    }
#endif
}

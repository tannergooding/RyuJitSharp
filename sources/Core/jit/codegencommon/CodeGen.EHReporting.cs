// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct EHClauseInfo
{
    public CORINFO_EH_CLAUSE clause;

    // The native HBtab pointer is represented by its stable index in compHndBBtab.
    public ushort EHIndex;
}

public sealed partial class CodeGen
{
    public void genReportEH()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "EH reporting requires Windows AMD64.");
#else
        var count = _compiler.compHndBBtabCount;
        if (count == 0)
        {
            return;
        }

#if DEBUG
        if (_compiler.opts.dspEHTable)
        {
            jitprintf($"*************** EH table for {_compiler.info.compFullName}\n");
            jitprintf($"{count} EH table entries\n");
        }
#endif

        _compiler.eeSetEHcount(count);
        _compiler.Metrics.EHClauseCount = count;
        var clauses = new EHClauseInfo[count];
        var ehToVmOrder = _compiler.compEHTabOrderToVMClauseOrder
            ?? throw new FatalJitException(CORJIT_SKIPPED, "EH-to-VM clause order has not been initialized.");

        for (ushort XTnum = 0; XTnum < count; XTnum++)
        {
            ref var HBtab = ref _compiler.compHndBBtab[XTnum];
            var tryBeg = _compiler.ehCodeOffset(HBtab.ebdTryBeg);
            var hndBeg = _compiler.ehCodeOffset(HBtab.ebdHndBeg);
            var tryEnd = HBtab.ebdTryLast == _compiler.fgLastBB
                ? unchecked((uint)_compiler.info.compNativeCodeSize)
                : _compiler.ehCodeOffset(HBtab.ebdTryLast.Next
                    ?? throw new FatalJitException(CORJIT_SKIPPED, "EH try end has no successor."));
            var hndEnd = HBtab.ebdHndLast == _compiler.fgLastBB
                ? unchecked((uint)_compiler.info.compNativeCodeSize)
                : _compiler.ehCodeOffset(HBtab.ebdHndLast.Next
                    ?? throw new FatalJitException(CORJIT_SKIPPED, "EH handler end has no successor."));
            var hndTyp = HBtab.HasFilter
                ? unchecked((int)_compiler.ehCodeOffset(HBtab.ebdFilter
                    ?? throw new FatalJitException(CORJIT_SKIPPED, "EH filter has no start block.")))
                : (int)HBtab.ebdTyp;

            var clause = new CORINFO_EH_CLAUSE
            {
                Flags = ToCORINFO_EH_CLAUSE_FLAGS(HBtab.ebdHandlerType),
                TryOffset = unchecked((int)tryBeg),
                TryLength = unchecked((int)tryEnd),
                HandlerOffset = unchecked((int)hndBeg),
                HandlerLength = unchecked((int)hndEnd),
                ClassToken = hndTyp,
            };

            var vmIndex = ehToVmOrder[XTnum];
            clauses[vmIndex] = new EHClauseInfo { clause = clause, EHIndex = XTnum };
        }

        genReportEHClauses(clauses);
#endif
    }

    public unsafe void genReportEHClauses(EHClauseInfo[] clauses)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "EH clause reporting requires Windows AMD64.");
#else
#if DEBUG
        var lastFuncletIndex = 0u;
#endif
        var vmToEhOrder = _compiler.compVMClauseOrderToEHTabOrder
            ?? throw new FatalJitException(CORJIT_SKIPPED, "VM-to-EH clause order has not been initialized.");

        for (var vmIndex = 0; vmIndex < _compiler.compHndBBtabCount; vmIndex++)
        {
            ref var clause = ref clauses[vmIndex].clause;
            var XTnum = vmToEhOrder[vmIndex];
            ref var HBtab = ref _compiler.compHndBBtab[XTnum];

#if DEBUG
            if (HBtab.HasFilter)
            {
                assert(HBtab.ebdFuncIndex == lastFuncletIndex + 2);
            }
            else
            {
                assert(HBtab.ebdFuncIndex == lastFuncletIndex + 1);
            }
            lastFuncletIndex = HBtab.ebdFuncIndex;
#endif

            if (vmIndex > 0)
            {
                ref var previous = ref _compiler.compHndBBtab[clauses[vmIndex - 1].EHIndex];
                if (EHblkDsc.ebdIsSameTry(HBtab, previous))
                {
                    assert(HBtab.HasCatchHandler);
                    clause.Flags |= CORINFO_EH_CLAUSE_SAMETRY;
                }
            }

            fixed (CORINFO_EH_CLAUSE* clausePtr = &clause)
            {
                _compiler.eeSetEHinfo((uint)vmIndex, clausePtr);
            }
        }
#endif
    }

    private static CORINFO_EH_CLAUSE_FLAGS ToCORINFO_EH_CLAUSE_FLAGS(EHHandlerType type)
    {
        return type switch
        {
            EH_HANDLER_CATCH => CORINFO_EH_CLAUSE_NONE,
            EH_HANDLER_FILTER => CORINFO_EH_CLAUSE_FILTER,
            EH_HANDLER_FAULT or EH_HANDLER_FAULT_WAS_FINALLY => CORINFO_EH_CLAUSE_FAULT,
            EH_HANDLER_FINALLY => CORINFO_EH_CLAUSE_FINALLY,
            _ => throw new FatalJitException(CORJIT_SKIPPED, "Unknown EH handler type."),
        };
    }
}

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
        if (Configuration.TARGET_WASM)
        {
            var wasmClauses = _compiler.fgWasmEHInfo;
            if (wasmClauses is null)
            {
                return;
            }

            var wasmClauseCount = _compiler.compHndBBtabCount;
            assert(wasmClauses.Length == wasmClauseCount);
            _compiler.eeSetEHcount(wasmClauseCount);
            _compiler.Metrics.EHClauseCount = wasmClauseCount;

            genReportEHClauses(wasmClauses);
            return;
        }

        var count = _compiler.compHndBBtabCount;
        if (count == 0)
        {
            return;
        }

        if (Configuration.DEBUG && _compiler.dspEHTable)
        {
            jitprintf($"*************** EH table for {_compiler.info.compFullName}\n");
            jitprintf($"{count} EH table entries\n");
        }

        _compiler.eeSetEHcount(count);
        _compiler.Metrics.EHClauseCount = count;
        var clauses = new EHClauseInfo[count];
        var ehToVmOrder = _compiler.compEHTabOrderToVMClauseOrder;
        if (ehToVmOrder is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.EHToVMClauseOrderHasNotBeenInitialized);
            return;
        }

        for (ushort XTnum = 0; XTnum < count; XTnum++)
        {
            ref var HBtab = ref _compiler.compHndBBtab[XTnum];
            var tryBeg = _compiler.ehCodeOffset(HBtab.ebdTryBeg);
            var hndBeg = _compiler.ehCodeOffset(HBtab.ebdHndBeg);
            uint tryEnd;
            if (HBtab.ebdTryLast == _compiler.fgLastBB)
            {
                tryEnd = unchecked((uint)_compiler.info.compNativeCodeSize);
            }
            else
            {
                var tryLastNext = HBtab.ebdTryLast.Next;
                if (tryLastNext is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.EHTryEndHasNoSuccessor);
                    return;
                }

                tryEnd = _compiler.ehCodeOffset(tryLastNext);
            }

            uint hndEnd;
            if (HBtab.ebdHndLast == _compiler.fgLastBB)
            {
                hndEnd = unchecked((uint)_compiler.info.compNativeCodeSize);
            }
            else
            {
                var hndLastNext = HBtab.ebdHndLast.Next;
                if (hndLastNext is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.EHHandlerEndHasNoSuccessor);
                    return;
                }

                hndEnd = _compiler.ehCodeOffset(hndLastNext);
            }

            int hndTyp;
            if (HBtab.HasFilter)
            {
                var filter = HBtab.ebdFilter;
                if (filter is null)
                {
                    FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.EHFilterHasNoStartBlock);
                    return;
                }

                hndTyp = unchecked((int)_compiler.ehCodeOffset(filter));
            }
            else
            {
                hndTyp = (int)HBtab.ebdTyp;
            }

            var clause = new CORINFO_EH_CLAUSE {
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
    }

    public unsafe void genReportEHClauses(EHClauseInfo[] clauses)
    {
        var lastFuncletIndex = 0u;
        var vmToEhOrder = _compiler.compVMClauseOrderToEHTabOrder;
        if (vmToEhOrder is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.VMToEHClauseOrderHasNotBeenInitialized);
            return;
        }

        for (var vmIndex = 0; vmIndex < _compiler.compHndBBtabCount; vmIndex++)
        {
            ref var clause = ref clauses[vmIndex].clause;
            var XTnum = vmToEhOrder[vmIndex];
            ref var HBtab = ref _compiler.compHndBBtab[XTnum];

            if (Configuration.DEBUG)
            {
                if (HBtab.HasFilter)
                {
                    assert(HBtab.ebdFuncIndex == lastFuncletIndex + 2);
                }
                else
                {
                    assert(HBtab.ebdFuncIndex == lastFuncletIndex + 1);
                }
                lastFuncletIndex = HBtab.ebdFuncIndex;
            }

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
    }

    internal static CORINFO_EH_CLAUSE_FLAGS ToCORINFO_EH_CLAUSE_FLAGS(EHHandlerType type)
    {
        switch (type)
        {
            case EH_HANDLER_CATCH:
            {
                return CORINFO_EH_CLAUSE_NONE;
            }

            case EH_HANDLER_FILTER:
            {
                return CORINFO_EH_CLAUSE_FILTER;
            }

            case EH_HANDLER_FAULT or EH_HANDLER_FAULT_WAS_FINALLY:
            {
                return CORINFO_EH_CLAUSE_FAULT;
            }

            case EH_HANDLER_FINALLY:
            {
                return CORINFO_EH_CLAUSE_FINALLY;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.UnknownEHHandlerType);
                return default!;
            }
        }
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void eeSetEHcount(uint count)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"setEHcount(cEH={count})\n");
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->setEHcount(unchecked((int)count));
        }
    }

    public unsafe void eeSetEHinfo(uint EHnumber, CORINFO_EH_CLAUSE* clause)
    {
#if DEBUG
        if (opts.dspEHTable)
        {
            dispOutgoingEHClause(EHnumber, *clause);
        }
#endif
        if (info.compMatchedVM)
        {
            info.compCompHnd->setEHinfo(unchecked((int)EHnumber), clause);
        }
    }

#if DEBUG
    public void dispOutgoingEHClause(uint number, in CORINFO_EH_CLAUSE clause)
    {
        var emitter = codeGen?.Emitter
            ?? throw new FatalJitException(CORJIT_SKIPPED, "EH diagnostics require an emitter.");

        if (opts.dspDiffable)
        {
            jitprintf($"EH#{number}: try [{emitter.emitOffsetToLabel(unchecked((uint)clause.TryOffset))}.." +
                $"{emitter.emitOffsetToLabel(unchecked((uint)clause.TryLength))}) handled by " +
                $"[{emitter.emitOffsetToLabel(unchecked((uint)clause.HandlerOffset))}.." +
                $"{emitter.emitOffsetToLabel(unchecked((uint)clause.HandlerLength))}) ");
        }
        else
        {
            jitprintf($"EH#{number}: try [{unchecked((uint)dspOffset(clause.TryOffset)):X4}.." +
                $"{unchecked((uint)dspOffset(clause.TryLength)):X4}) handled by " +
                $"[{unchecked((uint)dspOffset(clause.HandlerOffset)):X4}.." +
                $"{unchecked((uint)dspOffset(clause.HandlerLength)):X4}) ");
        }

        // SAMETRY is orthogonal to the clause kind, which includes the zero-valued catch kind.
        const CORINFO_EH_CLAUSE_FLAGS typeMask = (CORINFO_EH_CLAUSE_FLAGS)0x7;
        switch (clause.Flags & typeMask)
        {
            case CORINFO_EH_CLAUSE_NONE:
            {
                jitprintf($"(class: {unchecked((uint)clause.ClassToken):X4})");
                break;
            }

            case CORINFO_EH_CLAUSE_FILTER:
            {
                if (opts.dspDiffable)
                {
                    jitprintf($"filter at [{emitter.emitOffsetToLabel(unchecked((uint)clause.FilterOffset))}.." +
                        $"{emitter.emitOffsetToLabel(unchecked((uint)clause.HandlerOffset))})");
                }
                else
                {
                    jitprintf($"filter at [{unchecked((uint)dspOffset(clause.FilterOffset)):X4}.." +
                        $"{unchecked((uint)dspOffset(clause.HandlerOffset)):X4})");
                }
                break;
            }

            case CORINFO_EH_CLAUSE_FINALLY:
            {
                jitprintf("(finally)");
                break;
            }

            case CORINFO_EH_CLAUSE_FAULT:
            {
                jitprintf("(fault)");
                break;
            }

            default:
            {
                jitprintf($"(UNKNOWN type {(uint)(clause.Flags & typeMask)}!)");
                throw new FatalJitException(CORJIT_SKIPPED, "Unknown outgoing EH clause type.");
            }
        }

        if ((clause.Flags & CORINFO_EH_CLAUSE_SAMETRY) != 0)
        {
            jitprintf(" same try");
        }
        jitprintf("\n");
    }
#endif
}

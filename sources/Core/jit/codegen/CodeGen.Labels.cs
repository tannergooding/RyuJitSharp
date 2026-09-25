// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if !TARGET_WASM
    // Keep region endpoints in sync with genReportEH. No blocks may be added or removed afterward.
    private void genMarkLabelsForCodegen()
    {
#if DEBUG
        assert(!_compiler.fgSafeBasicBlockCreation);
#endif
        JITDUMP("Mark labels for codegen\n");

#if DEBUG
        foreach (var block in _compiler.Blocks)
        {
            assert(!block.HasFlag(BBF_HAS_LABEL));
        }
#endif

        // The first label also establishes the initial GC state and switch-table base.
        assert(_compiler.fgFirstBB is not null);
        JITDUMP($"  {FMT_BB(_compiler.fgFirstBB.bbNum)} : first block\n");
        _compiler.fgFirstBB.SetFlags(BBF_HAS_LABEL);
        if (_compiler.fgHasSwitch)
        {
            JITDUMP($"  {FMT_BB(_compiler.fgFirstBB.bbNum)} : function has switch; mark first block\n");
            _compiler.fgFirstBB.SetFlags(BBF_HAS_LABEL);
        }

        foreach (var block in _compiler.Blocks)
        {
            switch (block.Kind)
            {
                case BBJ_ALWAYS:
                {
                    if (block.CanRemoveJumpToNext(_compiler))
                    {
                        break;
                    }
                    goto case BBJ_EHCATCHRET;
                }

                case BBJ_EHCATCHRET:
                {
                    JITDUMP($"  {FMT_BB(block.Target.bbNum)} : branch target\n");
                    block.Target.SetFlags(BBF_HAS_LABEL);
                    break;
                }

                case BBJ_COND:
                {
                    JITDUMP($"  {FMT_BB(block.TrueTarget.bbNum)} : branch target\n");
                    block.TrueTarget.SetFlags(BBF_HAS_LABEL);
                    if (!block.CanRemoveJumpToTarget(block.FalseTarget, _compiler))
                    {
                        JITDUMP($"  {FMT_BB(block.FalseTarget.bbNum)} : branch target\n");
                        block.FalseTarget.SetFlags(BBF_HAS_LABEL);
                    }
                    break;
                }

                case BBJ_SWITCH:
                {
                    foreach (var target in block.SwitchSuccs)
                    {
                        JITDUMP($"  {FMT_BB(target.bbNum)} : switch target\n");
                        target.SetFlags(BBF_HAS_LABEL);
                    }
                    break;
                }

                case BBJ_CALLFINALLY:
                {
                    // Duplicate-finally EH regions end after the whole call/continuation pair.
                    var next = block.Next;
                    if (block.isBBCallFinallyPair)
                    {
                        assert(next is not null);
                        next = next.Next;
                    }
                    if (next is not null)
                    {
                        JITDUMP($"  {FMT_BB(next.bbNum)} : callfinally thunk region end\n");
                        next.SetFlags(BBF_HAS_LABEL);
                    }
                    break;
                }

                case BBJ_CALLFINALLYRET:
                {
                    JITDUMP($"  {FMT_BB(block.Target.bbNum)} : finally continuation\n");
                    block.Target.SetFlags(BBF_HAS_LABEL);
                    break;
                }

                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                case BBJ_EHFILTERRET:
                case BBJ_RETURN:
                case BBJ_THROW:
                {
                    break;
                }

                default:
                {
                    noway_assert(false, "Unexpected bbKind");
                    break;
                }
            }
        }

        if (_compiler.fgHasAddCodeDscMap)
        {
            foreach (var add in _compiler.fgGetAddCodeDscMap().Values)
            {
                if (add.acdUsed)
                {
                    assert(add.acdDstBlk is not null);
                    JITDUMP($"  {FMT_BB(add.acdDstBlk.bbNum)} : throw helper block\n");
                    add.acdDstBlk.SetFlags(BBF_HAS_LABEL);
                }
            }
        }

        foreach (ref var clause in new EHClauses(_compiler))
        {
            clause.ebdTryBeg.SetFlags(BBF_HAS_LABEL);
            clause.ebdHndBeg.SetFlags(BBF_HAS_LABEL);
            JITDUMP($"  {FMT_BB(clause.ebdTryBeg.bbNum)} : try begin\n");
            JITDUMP($"  {FMT_BB(clause.ebdHndBeg.bbNum)} : hnd begin\n");

            if (!clause.ebdTryLast.IsLast)
            {
                clause.ebdTryLast.Next.SetFlags(BBF_HAS_LABEL);
                JITDUMP($"  {FMT_BB(clause.ebdTryLast.Next.bbNum)} : try end\n");
            }
            if (!clause.ebdHndLast.IsLast)
            {
                clause.ebdHndLast.Next.SetFlags(BBF_HAS_LABEL);
                JITDUMP($"  {FMT_BB(clause.ebdHndLast.Next.bbNum)} : hnd end\n");
            }
            if (clause.HasFilter)
            {
                clause.ebdFilter.SetFlags(BBF_HAS_LABEL);
                JITDUMP($"  {FMT_BB(clause.ebdFilter.bbNum)} : filter begin\n");
            }
        }

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("*************** After genMarkLabelsForCodegen()\n");
            _compiler.fgDispBasicBlocks();
        }
#endif
    }
#endif
}

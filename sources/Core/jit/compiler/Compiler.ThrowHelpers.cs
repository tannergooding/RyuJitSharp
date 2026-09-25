// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    internal bool fgUseThrowHelperBlocks() => !opts.compDbgCode;

    internal AddCodeDscMap fgGetAddCodeDscMap() => fgAddCodeDscMap ??= [];

    private AddCodeDsc fgCreateAddCodeDsc(BasicBlock source, SpecialCodeKind kind)
    {
        assert(!fgRngChkThrowAdded);

        var designator = AcdKeyDesignator.KD_NONE;
        var referenceData = kind is SCK_FAIL_FAST ? 0 : bbThrowIndex(source, out designator);
        var add = new AddCodeDsc {
            acdTryIndex = source.bbTryIndex,
            acdHndIndex = source.bbHndIndex,
            acdKeyDsg = designator,
            acdKind = kind,
        };
#if DEBUG
        add.acdNum = acdCount++;
#endif

        var map = fgGetAddCodeDscMap();
        var key = new AddCodeDscKey(add);
        assert(key.Data == referenceData);
        map[key] = add;

#if DEBUG
        JITDUMP($"{FMT_BB(source.bbNum)} requires throw helper block for {sckName(kind)}, created ACD{add.acdNum} with data 0x{unchecked((uint)key.Data):x8}\n");
        assert(map.TryGetValue(new AddCodeDscKey(kind, source, this), out var found) && (found == add));
#endif
        return add;
    }

    private void fgCreateThrowHelperBlock(AddCodeDsc add)
    {
        if (add.acdDstBlk is not null)
        {
            assert(add.acdDstBlk.HasFlag(BBF_THROW_HELPER));
            return;
        }

        ReadOnlySpan<BBKinds> jumpKinds = [
            BBJ_ALWAYS, BBJ_THROW, BBJ_THROW, BBJ_THROW,
            BBJ_THROW, BBJ_THROW, BBJ_THROW, BBJ_THROW,
        ];
        noway_assert(jumpKinds.Length == (int)SCK_COUNT);
        assert(add.acdKind is not SCK_NONE);

        var block = fgNewBBinRegion(jumpKinds[(int)add.acdKind], add.acdTryIndex, add.acdHndIndex,
            nearBlk: null, putInFilter: add.acdKeyDsg is AcdKeyDesignator.KD_FLT,
            runRarely: true, insertAtEnd: true);
        block.SetFlags(BBF_THROW_HELPER);
        add.acdDstBlk = block;
        fgSetThrowHelpBlockLiveness(block);

#if DEBUG
        if (verbose)
        {
            var where = add.acdKeyDsg switch {
                AcdKeyDesignator.KD_NONE => "non-EH region",
                AcdKeyDesignator.KD_HND => "handler",
                AcdKeyDesignator.KD_TRY => "try",
                AcdKeyDesignator.KD_FLT => "filter",
                _ => "? unexpected",
            };
            var reason = add.acdKind switch {
                SCK_RNGCHK_FAIL => " for RNGCHK_FAIL",
                SCK_DIV_BY_ZERO => " for DIV_BY_ZERO",
                SCK_OVERFLOW => " for OVERFLOW",
                SCK_ARG_EXCPN => " for ARG_EXCPN",
                SCK_ARG_RNG_EXCPN => " for ARG_RNG_EXCPN",
                SCK_FAIL_FAST => " for FAIL_FAST",
                SCK_NULL_CHECK => " for NULL_CHECK",
                _ => " for ??",
            };
            jitprintf($"\nAdding throw helper {FMT_BB(block.bbNum)} for ACD{add.acdNum} {sckName(add.acdKind)} in {where}{reason}\n");
        }
#endif
        block.SetFlags(BBF_IMPORTED | BBF_DONT_REMOVE);
    }

    private void fgSetThrowHelpBlockLiveness(BasicBlock block)
    {
        VarSetOps.ClearD(this, block.bbLiveOut);
        if (lvaKeepAliveAndReportThis() && lvaTable[info.compThisArg].lvTracked)
        {
            VarSetOps.AddElemD(this, block.bbLiveOut, lvaGetDesc(info.compThisArg)._varIndex);
        }

        if (block.HasPotentialEHSuccs(this))
        {
            _ = block.VisitEHSuccs(this, successor => {
                VarSetOps.UnionD(this, block.bbLiveOut, successor.bbLiveIn);
                return BasicBlockVisit.Continue;
            });
        }
        VarSetOps.Assign(this, ref block.bbLiveIn, block.bbLiveOut);
    }

    internal AddCodeDsc fgGetExcptnTarget(SpecialCodeKind kind, BasicBlock fromBlock, bool createIfNeeded = false)
    {
        assert(fgUseThrowHelperBlocks() || (kind is SCK_FAIL_FAST));
        var map = fgGetAddCodeDscMap();
        if (!map.TryGetValue(new AddCodeDscKey(kind, fromBlock, this), out var add))
        {
            add = fgCreateAddCodeDsc(fromBlock, kind);
            if (createIfNeeded)
            {
                fgCreateThrowHelperBlock(add);
            }
        }

        assert((add.acdDstBlk is null) || add.acdDstBlk.HasFlag(BBF_THROW_HELPER));
        return add;
    }

    internal void fgCreateThrowHelperBlockCode(AddCodeDsc add)
    {
        assert(add.acdUsed);
        var block = add.acdDstBlk;
        assert(block is not null);
        assert(block.IsEmpty);

        var helper = add.acdKind switch {
            SCK_RNGCHK_FAIL => CORINFO_HELP_RNGCHKFAIL,
            SCK_DIV_BY_ZERO => CORINFO_HELP_THROWDIVZERO,
            SCK_ARITH_EXCPN => CORINFO_HELP_OVERFLOW,
            SCK_ARG_EXCPN => CORINFO_HELP_THROW_ARGUMENTEXCEPTION,
            SCK_ARG_RNG_EXCPN => CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION,
            SCK_FAIL_FAST => CORINFO_HELP_FAIL_FAST,
            SCK_NULL_CHECK => CORINFO_HELP_THROWNULLREF,
            _ => CORINFO_HELP_UNDEF,
        };
        noway_assert(helper is not CORINFO_HELP_UNDEF);

        var tree = fgMorphArgs(gtNewHelperCallNode(TYP_VOID, helper));
        if (fgNodeThreading is not NodeThreading.LIR)
        {
            fgInsertStmtAtEnd(block, fgNewStmtFromTree(tree));
        }
        else
        {
            var range = LIR.SeqTree(this, tree);
            var first = range.FirstNode;
            var last = range.LastNode;
            block.InsertAtEnd(range);
            assert(_pLowering is not null);
            _pLowering.LowerRange(block, new LIR.ReadOnlyRange(first, last));
        }
    }

#if DEBUG
    internal static string sckName(SpecialCodeKind kind) => kind switch {
        SCK_RNGCHK_FAIL => nameof(SCK_RNGCHK_FAIL),
        SCK_ARG_EXCPN => nameof(SCK_ARG_EXCPN),
        SCK_ARG_RNG_EXCPN => nameof(SCK_ARG_RNG_EXCPN),
        SCK_DIV_BY_ZERO => nameof(SCK_DIV_BY_ZERO),
        SCK_ARITH_EXCPN => nameof(SCK_ARITH_EXCPN),
        SCK_FAIL_FAST => nameof(SCK_FAIL_FAST),
        SCK_NULL_CHECK => nameof(SCK_NULL_CHECK),
        _ => "SCK_UNKNOWN",
    };
#endif
}

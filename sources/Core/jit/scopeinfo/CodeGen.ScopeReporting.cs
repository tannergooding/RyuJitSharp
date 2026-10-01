// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.ICorDebugInfo.VarLocType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if DEBUG
    private TrnslLocalVarInfo[] genTrnslLocalVarInfo = [];

    private struct TrnslLocalVarInfo
    {
        public int tlviVarNum;
        public int tlviLVnum;
        public VarName? tlviName;
        public uint tlviStartPC;
        public nuint tlviLength;
        public bool tlviAvailable;
        public siVarLoc tlviVarLoc;
    }
#endif

    [Conditional("DEBUG")]
    public static void checkICodeDebugInfo()
    {
        // siVarLoc uses ICorDebugInfo's enum and union directly: the native enum
        // identity checks are guaranteed by the managed field/property types.
        assert(Unsafe.SizeOf<siVarLoc>() == Unsafe.SizeOf<ICorDebugInfo.VarLoc>());
    }

    public unsafe void genSetScopeInfo()
    {
        if (!_compiler.opts.compScopeInfo)
        {
            return;
        }

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("*************** In genSetScopeInfo()\n");
        }
#endif
        var keeper = getVariableLiveKeeper();
        assert(emittedCallReturnInfo is not null);
        var varsLocationsCount = unchecked((uint)(keeper.getLiveRangesCount() + (nuint)emittedCallReturnInfo.Count));

        if (varsLocationsCount == 0)
        {
            _compiler.eeAllocateLVs(0);
            _compiler.eeSetLVdone();
            return;
        }

        noway_assert((_compiler.opts.compScopeInfo && (_compiler.info.compVarScopesCount > 0)) ||
            (keeper.getLiveRangesCount() == 0));
        _compiler.eeAllocateLVs(varsLocationsCount);

#if DEBUG
        _genTrnslLocalVarCount = unchecked((int)varsLocationsCount);
        genTrnslLocalVarInfo = new TrnslLocalVarInfo[varsLocationsCount];
#endif
        if (keeper.getLiveRangesCount() > 0)
        {
            genSetScopeInfoUsingVariableRanges();
        }

        foreach (var callReturnInfo in emittedCallReturnInfo)
        {
            if (callReturnInfo.returnValueLoc.vlType == VLT_INVALID)
            {
                continue;
            }

            var retOffset = callReturnInfo.returnLocation.CodeOffset(Emitter);
            var which = unchecked(_compiler.eeVarsCount++);
            _compiler.eeSetLVinfo(unchecked((uint)which), retOffset, unchecked(retOffset + 1),
                unchecked((uint)callReturnInfo.callILOffset), ICorDebugInfo.CALL_RETURN_ILNUM,
                in callReturnInfo.returnValueLoc);
        }

        _compiler.eeSetLVdone();
    }

    public unsafe void genSetScopeInfoUsingVariableRanges()
    {
        var liveRangeIndex = 0u;
        var keeper = getVariableLiveKeeper();

        for (var varNum = 0; varNum < _compiler.info.compLocalsCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            if (_compiler.compMap2ILvarNum(varNum) == ICorDebugInfo.UNKNOWN_ILNUM)
            {
                continue;
            }

            var isParam = varDsc.lvIsParam;

            void ReportRange(ref siVarLoc loc, uint start, uint end)
            {
                if (loc.vlType == VLT_INVALID)
                {
                    return;
                }

                // Empty argument ranges must remain inspectable on method entry.
                if (isParam && (start == end))
                {
                    end = unchecked(end + 1);
                }

                if (start < end)
                {
                    genSetScopeInfo(liveRangeIndex, start, end - start, varNum, varNum, true, ref loc);
                    liveRangeIndex = unchecked(liveRangeIndex + 1);
                }
            }

            VariableLiveKeeper.VariableLiveRange? curRange = null;
            var curStart = 0u;
            var curEnd = 0u;

            for (var rangeIndex = 0; rangeIndex < 2; rangeIndex++)
            {
                var liveRanges = rangeIndex == 0
                    ? keeper.getLiveRangesForVarForProlog(varNum)
                    : keeper.getLiveRangesForVarForBody(varNum);

                foreach (var liveRange in liveRanges)
                {
                    var startOffs = liveRange.m_StartEmitLocation.CodeOffset(Emitter);
                    var endOffs = liveRange.m_EndEmitLocation.CodeOffset(Emitter);
                    assert(startOffs <= endOffs);
                    assert(startOffs >= curEnd);

                    if ((curRange is not null) && (startOffs == curEnd) &&
                        siVarLoc.Equals(curRange.m_VarLocation, liveRange.m_VarLocation))
                    {
                        curEnd = endOffs;
                        continue;
                    }

                    if (curRange is not null)
                    {
                        ReportRange(ref curRange.m_VarLocation, curStart, curEnd);
                    }

                    curRange = liveRange;
                    curStart = startOffs;
                    curEnd = endOffs;
                }
            }

            if (curRange is not null)
            {
                ReportRange(ref curRange.m_VarLocation, curStart, curEnd);
            }
        }

        _compiler.eeVarsCount = unchecked((int)liveRangeIndex);
    }

    public unsafe void genSetScopeInfo(uint which, uint startOffs, uint length, int varNum,
        int LVnum, bool avail, ref siVarLoc varLoc)
    {
        var ilVarNum = _compiler.compMap2ILvarNum(varNum);
        noway_assert(ilVarNum != ICorDebugInfo.UNKNOWN_ILNUM);

#if TARGET_X86
        // Non-x86 targets access arguments directly. Stack varargs instead use
        // a cookie-relative location, replacing the original live-range home.
        if (_compiler.info.compIsVarArgs && (varNum != _compiler.lvaVarargsHandleArg) &&
            (varNum < _compiler.info.compArgsCount) && !_compiler.lvaGetDesc(varNum).lvIsRegArg)
        {
            noway_assert(varLoc.vlType is VLT_STK or VLT_STK2);

            assert(_compiler.lvaVarargsHandleArg < _compiler.info.compArgsCount);
            if (!_compiler.lvaGetDesc(_compiler.lvaVarargsHandleArg).lvOnFrame)
            {
                noway_assert(!_compiler.opts.compDbgCode);
                return;
            }

            // Varargs stack arguments need not have lvOnFrame set: their
            // locations are deliberately excluded from GC reporting.
            noway_assert(!_compiler.lvaGetDesc(varNum).lvRegister);
            var cookieOffset = unchecked((uint)_compiler.lvaGetDesc(_compiler.lvaVarargsHandleArg).StackOffset);
            var varOffset = unchecked((uint)_compiler.lvaGetDesc(varNum).StackOffset);

            noway_assert(cookieOffset < varOffset);
            var offset = unchecked(varOffset - cookieOffset);
            var stackArgSize = unchecked((uint)_compiler.lvaParameterStackSize);
            noway_assert(offset < stackArgSize);
            offset = unchecked(stackArgSize - offset);

            varLoc.vlType = VLT_FIXED_VA;
            varLoc.vlFixedVarArg.vlfvOffset = unchecked((int)offset);
        }
#endif

#if DEBUG
        VarName? name = null;
        for (var scopeNum = 0; scopeNum < _compiler.info.compVarScopesCount; scopeNum++)
        {
            if (LVnum == _compiler.info.compVarScopes[scopeNum].vsdLVnum)
            {
                name = _compiler.info.compVarScopes[scopeNum].vsdName;
            }
        }

        ref var tlvi = ref genTrnslLocalVarInfo[which];
        tlvi.tlviVarNum = ilVarNum;
        tlvi.tlviLVnum = LVnum;
        tlvi.tlviName = name;
        tlvi.tlviStartPC = startOffs;
        tlvi.tlviLength = length;
        tlvi.tlviAvailable = avail;
        tlvi.tlviVarLoc = varLoc;
#endif
        _compiler.eeSetLVinfo(which, startOffs, unchecked(startOffs + length), 0, ilVarNum, in varLoc);
    }
}

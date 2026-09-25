// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private VariableLiveKeeper? varLiveKeeper;

    public void initializeVariableLiveKeeper()
    {
        var amountTrackedVariables = _compiler.opts.compDbgInfo ? _compiler.info.compLocalsCount : 0;
        var amountTrackedArgs = _compiler.opts.compDbgInfo ? _compiler.info.compArgsCount : 0;
        varLiveKeeper = new VariableLiveKeeper(amountTrackedVariables, amountTrackedArgs, this);
    }

    public VariableLiveKeeper getVariableLiveKeeper()
    {
        return varLiveKeeper ?? throw new FatalJitException("Variable live ranges have not been initialized.");
    }

    public sealed partial class VariableLiveKeeper
    {
        private readonly int m_LiveDscCount;
        private readonly int m_LiveArgsCount;
        private readonly Compiler m_compiler;
        private readonly CodeGen m_codeGen;
        private readonly VariableLiveDescriptor[] m_vlrLiveDsc;
        private readonly VariableLiveDescriptor[] m_vlrLiveDscForProlog;
        private bool m_LastBasicBlockHasBeenEmitted;

        public VariableLiveKeeper(int totalLocalCount, int argsCount, CodeGen codeGen)
        {
#if !TARGET_AMD64
            throw new FatalJitException(CORJIT_SKIPPED, "Variable live ranges outside AMD64 are not implemented.");
#else
            m_LiveDscCount = totalLocalCount;
            m_LiveArgsCount = argsCount;
            m_compiler = codeGen.Compiler;
            m_codeGen = codeGen;
            m_LastBasicBlockHasBeenEmitted = false;
            m_vlrLiveDsc = new VariableLiveDescriptor[totalLocalCount];
            m_vlrLiveDscForProlog = new VariableLiveDescriptor[totalLocalCount];

            for (var varNum = 0; varNum < totalLocalCount; varNum++)
            {
                m_vlrLiveDsc[varNum] = new VariableLiveDescriptor(
#if DEBUG
                    m_compiler, varNum
#endif
                    );
                m_vlrLiveDscForProlog[varNum] = new VariableLiveDescriptor(
#if DEBUG
                    m_compiler, varNum
#endif
                    );
            }
#endif
        }

        public void siStartOrCloseVariableLiveRange(in LclVarDsc varDsc, int varNum, bool isBorn, bool isDying)
        {
            if (m_compiler.opts.compDbgInfo && (unchecked((uint)varNum) < (uint)m_LiveDscCount))
            {
                if (isBorn && !isDying)
                {
                    siStartVariableLiveRange(in varDsc, varNum);
                }
                if (isDying && !isBorn)
                {
                    siEndVariableLiveRange(varNum);
                }
            }
        }

        public void siStartOrCloseVariableLiveRanges(ReadOnlySpan<nint> varsIndexSet, bool isBorn, bool isDying)
        {
            if (m_compiler.opts.compDbgInfo)
            {
                _ = VarSetOps.VisitBits(m_compiler, varsIndexSet, varIndex =>
                {
                    var trackedToVarNum = m_compiler.lvaTrackedToVarNum ??
                        throw new FatalJitException("Tracked local mapping has not been initialized.");
                    var varNum = trackedToVarNum[varIndex];
                    ref var varDsc = ref m_compiler.lvaGetDesc(varNum);
                    siStartOrCloseVariableLiveRange(in varDsc, varNum, isBorn, isDying);
                    return true;
                });
            }
        }

        public void siStartVariableLiveRange(in LclVarDsc varDsc, int varNum)
        {
            if (m_compiler.opts.compDbgInfo && (unchecked((uint)varNum) < (uint)m_LiveDscCount) &&
                (varDsc.lvIsInReg || varDsc.lvOnFrame) && varDsc.lvValueSize.IsExact)
            {
                var varLocation = m_codeGen.getSiVarLoc(in varDsc, 0, unchecked((int)m_codeGen.getCurrentStackLevel()));
                m_vlrLiveDsc[varNum].startLiveRangeFromEmitter(varLocation, m_codeGen.Emitter);
            }
        }

        public void siEndVariableLiveRange(int varNum)
        {
            if (m_compiler.opts.compDbgInfo && (unchecked((uint)varNum) < (uint)m_LiveDscCount) &&
                !m_LastBasicBlockHasBeenEmitted && m_vlrLiveDsc[varNum].hasVariableLiveRangeOpen())
            {
                m_vlrLiveDsc[varNum].endLiveRangeAtEmitter(m_codeGen.Emitter);
            }
        }

        public void siUpdateVariableLiveRange(in LclVarDsc varDsc, int varNum)
        {
            if (m_compiler.opts.compDbgInfo && (unchecked((uint)varNum) < (uint)m_LiveDscCount) &&
                !m_LastBasicBlockHasBeenEmitted)
            {
                var varLocation = m_codeGen.getSiVarLoc(in varDsc, 0, unchecked((int)m_codeGen.getCurrentStackLevel()));
                m_vlrLiveDsc[varNum].updateLiveRangeAtEmitter(varLocation, m_codeGen.Emitter);
            }
        }

        public void siEndAllVariableLiveRange(ReadOnlySpan<nint> varsToClose)
        {
            if (m_compiler.opts.compDbgInfo)
            {
                if ((m_compiler.lvaTrackedCount > 0) || !m_compiler.opts.OptimizationDisabled)
                {
                    _ = VarSetOps.VisitBits(m_compiler, varsToClose, varIndex =>
                    {
                        var trackedToVarNum = m_compiler.lvaTrackedToVarNum ??
                            throw new FatalJitException("Tracked local mapping has not been initialized.");
                        var varNum = trackedToVarNum[varIndex];
                        siEndVariableLiveRange(varNum);
                        return true;
                    });
                }
                else
                {
                    siEndAllVariableLiveRange();
                }
            }

            m_LastBasicBlockHasBeenEmitted = true;
        }

        public void siEndAllVariableLiveRange()
        {
            for (var varNum = 0; varNum < m_LiveDscCount; varNum++)
            {
                if (m_vlrLiveDsc[varNum].hasVariableLiveRangeOpen())
                {
                    siEndVariableLiveRange(varNum);
                }
            }
        }

        public IReadOnlyList<VariableLiveRange> getLiveRangesForVarForBody(int varNum)
        {
            noway_assert(unchecked((uint)varNum) < (uint)m_LiveDscCount);
            return m_vlrLiveDsc[varNum].getLiveRanges();
        }

        public IReadOnlyList<VariableLiveRange> getLiveRangesForVarForProlog(int varNum)
        {
            noway_assert(unchecked((uint)varNum) < (uint)m_LiveDscCount);
            return m_vlrLiveDscForProlog[varNum].getLiveRanges();
        }

        public unsafe nuint getLiveRangesCount()
        {
            nuint liveRangesCount = 0;

            if (m_compiler.opts.compDbgInfo)
            {
                for (var varNum = 0; varNum < m_LiveDscCount; varNum++)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        var varLiveDsc = (i == 0 ? m_vlrLiveDscForProlog : m_vlrLiveDsc)[varNum];
                        if (m_compiler.compMap2ILvarNum(varNum) != ICorDebugInfo.UNKNOWN_ILNUM)
                        {
                            liveRangesCount += (nuint)varLiveDsc.getLiveRanges().Count;
                        }
                    }
                }
            }

            return liveRangesCount;
        }

        public void psiStartVariableLiveRange(siVarLoc varLocation, int varNum)
        {
            noway_assert(unchecked((uint)varNum) < (uint)m_LiveArgsCount);
            m_vlrLiveDscForProlog[varNum].startLiveRangeFromEmitter(varLocation, m_codeGen.Emitter);
        }

        public void psiClosePrologVariableRanges()
        {
            noway_assert(m_LiveArgsCount <= m_LiveDscCount);

            for (var varNum = 0; varNum < m_LiveArgsCount; varNum++)
            {
                var varLiveDsc = m_vlrLiveDscForProlog[varNum];
                if (varLiveDsc.hasVariableLiveRangeOpen())
                {
                    varLiveDsc.endLiveRangeAtEmitter(m_codeGen.Emitter);
                }
            }
        }
    }
}

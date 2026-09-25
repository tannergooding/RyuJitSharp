// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;
using static RyuJitSharp.ICorDebugInfo.VarLocType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void dumpSiVarLoc(in siVarLoc varLoc)
    {
#if TARGET_AMD64
        switch (varLoc.vlType)
        {
            case VLT_REG:
            case VLT_REG_BYREF:
            {
                jitprintf(((regNumber)varLoc.vlReg.vlrReg).Name);
                if (varLoc.vlType == VLT_REG_BYREF)
                {
                    jitprintf(" byref");
                }
                break;
            }
            case VLT_REG_FP:
            {
                var reg = (regNumber)((int)REG_FP_FIRST + (int)varLoc.vlReg.vlrReg -
                    (int)ICorDebugInfo.RegNum.REGNUM_FP_FIRST);
                jitprintf(reg.Name);
                break;
            }
            case VLT_STK:
            case VLT_STK_BYREF:
            {
                if (varLoc.vlStk.vlsBaseReg != ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP)
                {
                    jitprintf($"{((regNumber)varLoc.vlStk.vlsBaseReg).Name}[{varLoc.vlStk.vlsOffset}] (1 slot)");
                }
                else
                {
                    jitprintf($"{STR_SPBASE}'[{varLoc.vlStk.vlsOffset}] (1 slot)");
                }
                if (varLoc.vlType == VLT_STK_BYREF)
                {
                    jitprintf(" byref");
                }
                break;
            }
            case VLT_REG_REG:
            {
                var reg1 = ToJitReg(varLoc.vlRegReg.vlrrReg1);
                var reg2 = ToJitReg(varLoc.vlRegReg.vlrrReg2);
                jitprintf($"{reg1.Name}-{reg2.Name}");
                break;
            }
            default:
            {
                throw new FatalJitException("Invalid variable location kind for AMD64 diagnostics.");
            }
        }

        static regNumber ToJitReg(ICorDebugInfo.RegNum reg)
        {
            if (reg >= ICorDebugInfo.RegNum.REGNUM_FP_FIRST)
            {
                return (regNumber)((int)REG_FP_FIRST + (int)reg - (int)ICorDebugInfo.RegNum.REGNUM_FP_FIRST);
            }

            return (regNumber)reg;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Variable location diagnostics outside AMD64 are not implemented.");
#endif
    }

    public sealed partial class VariableLiveKeeper
    {
        public sealed partial class VariableLiveRange
        {
            public void dumpVariableLiveRange(CodeGen codeGen)
            {
                codeGen.dumpSiVarLoc(in m_VarLocation);
                jitprintf(" [");
                m_StartEmitLocation.Print(codeGen.Compiler.compMethodID);
                jitprintf(", ");
                if (m_EndEmitLocation.Valid())
                {
                    m_EndEmitLocation.Print(codeGen.Compiler.compMethodID);
                }
                else
                {
                    jitprintf("...");
                }
                jitprintf("]");
            }

            public void dumpVariableLiveRange(Emitter emit, CodeGen codeGen)
            {
                codeGen.dumpSiVarLoc(in m_VarLocation);
                jitprintf($" [{m_StartEmitLocation.CodeOffset(emit):X}, {m_EndEmitLocation.CodeOffset(emit):X})");
            }
        }

        private sealed class LiveRangeDumper
        {
            // Append-only range storage makes this index equivalent to the native stable list iterator.
            private int m_startingLiveRange;
            private bool m_hasLiveRangesToDump;

            public void resetDumper(List<VariableLiveRange> liveRanges)
            {
                assert(m_hasLiveRangesToDump);
                if (liveRanges[^1].m_EndEmitLocation.Valid())
                {
                    m_hasLiveRangesToDump = false;
                }
                else
                {
                    m_startingLiveRange = liveRanges.Count - 1;
                }
            }

            public void setDumperStartAt(int liveRangeIndex)
            {
                m_hasLiveRangesToDump = true;
                m_startingLiveRange = liveRangeIndex;
            }

            public int getStartForDump() => m_startingLiveRange;

            public bool hasLiveRangesToDump() => m_hasLiveRangesToDump;
        }

        private sealed partial class VariableLiveDescriptor
        {
            public void dumpAllRegisterLiveRangesForBlock(Emitter emit, CodeGen codeGen)
            {
                var first = true;
                foreach (var range in m_VariableLiveRanges)
                {
                    if (!first)
                    {
                        jitprintf("; ");
                    }
                    range.dumpVariableLiveRange(emit, codeGen);
                    first = false;
                }
            }

            public void dumpRegisterLiveRangesForBlockBeforeCodeGenerated(CodeGen codeGen)
            {
                var first = true;
                for (var index = m_VariableLifeBarrier.getStartForDump(); index < m_VariableLiveRanges.Count; index++)
                {
                    if (!first)
                    {
                        jitprintf("; ");
                    }
                    m_VariableLiveRanges[index].dumpVariableLiveRange(codeGen);
                    first = false;
                }
            }

            public bool hasVarLiveRangesToDump() => m_VariableLiveRanges.Count != 0;

            public bool hasVarLiveRangesFromLastBlockToDump() => m_VariableLifeBarrier.hasLiveRangesToDump();

            public void endBlockLiveRanges()
            {
                m_VariableLifeBarrier.resetDumper(m_VariableLiveRanges);
            }
        }

        public void dumpBlockVariableLiveRanges(BasicBlock block)
        {
            var hasDumpedHistory = false;
            jitprintf($"\nVariable Live Range History Dump for {FMT_BB(block.bbNum)}\n");

            if (m_compiler.opts.compDbgInfo)
            {
                for (var varNum = 0; varNum < m_LiveDscCount; varNum++)
                {
                    var varLiveDsc = m_vlrLiveDsc[varNum];
                    if (varLiveDsc.hasVarLiveRangesFromLastBlockToDump())
                    {
                        hasDumpedHistory = true;
                        m_compiler.gtDispLclVar(varNum, false);
                        jitprintf(": ");
                        varLiveDsc.dumpRegisterLiveRangesForBlockBeforeCodeGenerated(m_codeGen);
                        varLiveDsc.endBlockLiveRanges();
                        jitprintf("\n");
                    }
                }
            }

            if (!hasDumpedHistory)
            {
                jitprintf("..None..\n");
            }
        }

        public void dumpLvaVariableLiveRanges()
        {
            var hasDumpedHistory = false;
            jitprintf("VARIABLE LIVE RANGES:\n");

            if (m_compiler.opts.compDbgInfo)
            {
                for (var varNum = 0; varNum < m_LiveDscCount; varNum++)
                {
                    var varLiveDsc = m_vlrLiveDsc[varNum];
                    if (varLiveDsc.hasVarLiveRangesToDump())
                    {
                        hasDumpedHistory = true;
                        m_compiler.gtDispLclVar(varNum, false);
                        jitprintf(": ");
                        varLiveDsc.dumpAllRegisterLiveRangesForBlock(m_codeGen.Emitter, m_codeGen);
                        jitprintf("\n");
                    }
                }
            }

            if (!hasDumpedHistory)
            {
                jitprintf("..None..\n");
            }
        }
    }
}
#endif

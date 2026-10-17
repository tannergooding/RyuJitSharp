// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using static RyuJitSharp.ICorDebugInfo.VarLocType;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void dumpSiVarLoc(in siVarLoc varLoc)
    {
        if (!Configuration.DEBUG)
        {
            return;
        }

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
                var reg = Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64
                    ? (regNumber)((int)TargetRuntimeMetadata.FloatingRegisterFirst + (int)varLoc.vlReg.vlrReg -
                        (int)ICorDebugInfo.REGNUM_FP_FIRST)
                    : (regNumber)((int)TargetRuntimeMetadata.FloatingRegisterFirst + (int)varLoc.vlReg.vlrReg);
                jitprintf(reg.Name);
                break;
            }
            case VLT_STK:
            case VLT_STK_BYREF:
            {
                if (varLoc.vlStk.vlsBaseReg != ICorDebugInfo.REGNUM_AMBIENT_SP)
                {
                    jitprintf($"{((regNumber)varLoc.vlStk.vlsBaseReg).Name}[{varLoc.vlStk.vlsOffset}] (1 slot)");
                }
                else
                {
                    jitprintf($"{TargetRuntimeMetadata.StackPointerBaseName}'[{varLoc.vlStk.vlsOffset}] (1 slot)");
                }
                if (varLoc.vlType == VLT_STK_BYREF)
                {
                    jitprintf(" byref");
                }
                break;
            }
            case VLT_REG_REG:
            {
                var reg1 = Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64
                    ? ToJitReg(varLoc.vlRegReg.vlrrReg1)
                    : (regNumber)varLoc.vlRegReg.vlrrReg1;
                var reg2 = Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64
                    ? ToJitReg(varLoc.vlRegReg.vlrrReg2)
                    : (regNumber)varLoc.vlRegReg.vlrrReg2;
                jitprintf($"{reg1.Name}-{reg2.Name}");
                break;
            }
            case VLT_REG_STK:
            {
                if (Configuration.TARGET_AMD64)
                {
                    unreached();
                    break;
                }

                var reg = (regNumber)varLoc.vlRegStk.vlrsReg;
                var baseReg = varLoc.vlRegStk.vlrsStk.vlrssBaseReg;
                var offset = varLoc.vlRegStk.vlrsStk.vlrssOffset;
                if (baseReg != ICorDebugInfo.REGNUM_AMBIENT_SP)
                {
                    jitprintf($"{reg.Name}-{((regNumber)baseReg).Name}[{offset}]");
                }
                else
                {
                    jitprintf($"{reg.Name}-{TargetRuntimeMetadata.StackPointerBaseName}'[{offset}]");
                }
                break;
            }
            case VLT_STK_REG:
            {
                if (Configuration.TARGET_AMD64)
                {
                    unreached();
                    break;
                }

                unreached();
                break;
            }
            case VLT_STK2:
            {
                if (Configuration.TARGET_AMD64)
                {
                    unreached();
                    break;
                }

                if (varLoc.vlStk2.vls2BaseReg != ICorDebugInfo.REGNUM_AMBIENT_SP)
                {
                    jitprintf($"{((regNumber)varLoc.vlStk2.vls2BaseReg).Name}[{varLoc.vlStk2.vls2Offset}] (2 slots)");
                }
                else
                {
                    jitprintf($"{TargetRuntimeMetadata.StackPointerBaseName}'[{varLoc.vlStk2.vls2Offset}] (2 slots)");
                }
                break;
            }
            case VLT_FPSTK:
            {
                if (Configuration.TARGET_AMD64)
                {
                    unreached();
                    break;
                }

                jitprintf($"ST(L-{varLoc.vlFPstk.vlfReg})");
                break;
            }
            case VLT_FIXED_VA:
            {
                if (Configuration.TARGET_AMD64)
                {
                    unreached();
                    break;
                }

                jitprintf($"fxd_va[{varLoc.vlFixedVarArg.vlfvOffset}]");
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        static regNumber ToJitReg(ICorDebugInfo.RegNum reg)
        {
            if (reg >= ICorDebugInfo.REGNUM_FP_FIRST)
            {
                return (regNumber)((int)TargetRuntimeMetadata.FloatingRegisterFirst + (int)reg -
                    (int)ICorDebugInfo.REGNUM_FP_FIRST);
            }

            return (regNumber)reg;
        }
    }

    public sealed partial class VariableLiveKeeper
    {
        public sealed partial class VariableLiveRange
        {
            [System.Diagnostics.Conditional("DEBUG")]
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

            [System.Diagnostics.Conditional("DEBUG")]
            public void dumpVariableLiveRange(Emitter emit, CodeGen codeGen)
            {
                codeGen.dumpSiVarLoc(in m_VarLocation);
                jitprintf($" [{m_StartEmitLocation.CodeOffset(emit):X}, {m_EndEmitLocation.CodeOffset(emit):X})");
            }
        }

        private sealed class LiveRangeDumper
        {
            // Append-only range storage makes this index equivalent to the native stable list iterator.
            private readonly Compiler m_compiler;
            private readonly int m_varNum;
            private int m_startingLiveRange;
            private bool m_hasLiveRangesToDump;

            public LiveRangeDumper(Compiler compiler, int varNum)
            {
                m_compiler = compiler;
                m_varNum = varNum;
            }

            public Compiler Compiler => m_compiler;

            public int VarNum => m_varNum;

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
            [System.Diagnostics.Conditional("DEBUG")]
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

            [System.Diagnostics.Conditional("DEBUG")]
            public void dumpRegisterLiveRangesForBlockBeforeCodeGenerated(CodeGen codeGen)
            {
                var first = true;
                for (var index = getLiveRangeDumper().getStartForDump(); index < m_VariableLiveRanges.Count; index++)
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

            public bool hasVarLiveRangesFromLastBlockToDump() =>
                Configuration.DEBUG && getLiveRangeDumper().hasLiveRangesToDump();

            [System.Diagnostics.Conditional("DEBUG")]
            public void endBlockLiveRanges()
            {
                getLiveRangeDumper().resetDumper(m_VariableLiveRanges);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
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

        [System.Diagnostics.Conditional("DEBUG")]
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

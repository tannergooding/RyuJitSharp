// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public sealed partial class VariableLiveKeeper
    {
        public sealed partial class VariableLiveRange
        {
            public emitLocation m_StartEmitLocation;
            public emitLocation m_EndEmitLocation;
            public siVarLoc m_VarLocation;

            public VariableLiveRange(siVarLoc varLocation, emitLocation startEmitLocation, emitLocation endEmitLocation)
            {
                m_StartEmitLocation = startEmitLocation;
                m_EndEmitLocation = endEmitLocation;
                m_VarLocation = varLocation;
            }
        }

        private sealed partial class VariableLiveDescriptor
        {
            private readonly List<VariableLiveRange> m_VariableLiveRanges = [];
#if DEBUG
            private readonly LiveRangeDumper m_VariableLifeBarrier = new();
            private readonly int m_varNum;
            private readonly Compiler m_compiler;
#endif

            public VariableLiveDescriptor(
#if DEBUG
                Compiler compiler, int varNum
#endif
                )
            {
#if DEBUG
                m_varNum = varNum;
                m_compiler = compiler;
#endif
            }

            public bool hasVariableLiveRangeOpen()
            {
                return (m_VariableLiveRanges.Count != 0) && !m_VariableLiveRanges[^1].m_EndEmitLocation.Valid();
            }

            public List<VariableLiveRange> getLiveRanges() => m_VariableLiveRanges;

            public void startLiveRangeFromEmitter(siVarLoc varLocation, Emitter emit)
            {
                noway_assert((m_VariableLiveRanges.Count == 0) || m_VariableLiveRanges[^1].m_EndEmitLocation.Valid());

                if ((m_VariableLiveRanges.Count != 0) &&
                    siVarLoc.Equals(varLocation, m_VariableLiveRanges[^1].m_VarLocation) &&
                    m_VariableLiveRanges[^1].m_EndEmitLocation.IsPreviousInsNum(emit))
                {
#if DEBUG
                    if (m_compiler.verbose)
                    {
                        jitprintf($"Debug: Extending V{m_varNum:D2} debug range...\n");
                    }
#endif
                    m_VariableLiveRanges[^1].m_EndEmitLocation.Init();
                }
                else
                {
#if DEBUG
                    if (m_compiler.verbose)
                    {
                        var reason = m_VariableLiveRanges.Count == 0 ? "first" :
                            siVarLoc.Equals(varLocation, m_VariableLiveRanges[^1].m_VarLocation) ?
                                "new var or location" : "not adjacent";
                        jitprintf($"Debug: New V{m_varNum:D2} debug range: {reason}\n");
                    }
#endif
                    var liveRange = new VariableLiveRange(varLocation, default, default);
                    m_VariableLiveRanges.Add(liveRange);
                    liveRange.m_StartEmitLocation.CaptureLocation(emit);
                }

#if DEBUG
                if (!m_VariableLifeBarrier.hasLiveRangesToDump())
                {
                    m_VariableLifeBarrier.setDumperStartAt(m_VariableLiveRanges.Count - 1);
                }
#endif
                noway_assert(m_VariableLiveRanges[^1].m_StartEmitLocation.Valid());
                noway_assert(!m_VariableLiveRanges[^1].m_EndEmitLocation.Valid());
            }

            public void endLiveRangeAtEmitter(Emitter emit)
            {
                noway_assert(hasVariableLiveRangeOpen());
                m_VariableLiveRanges[^1].m_EndEmitLocation.CaptureLocation(emit);

#if DEBUG
                if (m_compiler.verbose)
                {
                    jitprintf($"Debug: Closing V{m_varNum:D2} debug range.\n");
                }
#endif
                noway_assert(m_VariableLiveRanges[^1].m_EndEmitLocation.Valid());
            }

            public void updateLiveRangeAtEmitter(siVarLoc varLocation, Emitter emit)
            {
                noway_assert(m_VariableLiveRanges.Count != 0);
                noway_assert(!m_VariableLiveRanges[^1].m_EndEmitLocation.Valid());

                endLiveRangeAtEmitter(emit);
                startLiveRangeFromEmitter(varLocation, emit);
            }
        }
    }
}

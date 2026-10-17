// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
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
            private readonly LiveRangeDumper? m_VariableLifeBarrier;

            public VariableLiveDescriptor(Compiler compiler, int varNum)
            {
                if (Configuration.DEBUG)
                {
                    m_VariableLifeBarrier = new LiveRangeDumper(compiler, varNum);
                }
            }

            private LiveRangeDumper getLiveRangeDumper() => m_VariableLifeBarrier
                ?? throw new InvalidOperationException("Live range diagnostics are only available in Debug builds.");

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
                    if (Configuration.DEBUG)
                    {
                        var liveRangeDumper = getLiveRangeDumper();
                        if (liveRangeDumper.Compiler.verbose)
                        {
                            jitprintf($"Debug: Extending V{liveRangeDumper.VarNum:D2} debug range...\n");
                        }
                    }
                    m_VariableLiveRanges[^1].m_EndEmitLocation.Init();
                }
                else
                {
                    if (Configuration.DEBUG)
                    {
                        var liveRangeDumper = getLiveRangeDumper();
                        if (liveRangeDumper.Compiler.verbose)
                        {
                            var reason = m_VariableLiveRanges.Count == 0 ? "first" :
                                siVarLoc.Equals(varLocation, m_VariableLiveRanges[^1].m_VarLocation) ?
                                    "new var or location" : "not adjacent";
                            jitprintf($"Debug: New V{liveRangeDumper.VarNum:D2} debug range: {reason}\n");
                        }
                    }
                    var liveRange = new VariableLiveRange(varLocation, default, default);
                    m_VariableLiveRanges.Add(liveRange);
                    liveRange.m_StartEmitLocation.CaptureLocation(emit);
                }

                if (Configuration.DEBUG)
                {
                    var liveRangeDumper = getLiveRangeDumper();
                    if (!liveRangeDumper.hasLiveRangesToDump())
                    {
                        liveRangeDumper.setDumperStartAt(m_VariableLiveRanges.Count - 1);
                    }
                }
                noway_assert(m_VariableLiveRanges[^1].m_StartEmitLocation.Valid());
                noway_assert(!m_VariableLiveRanges[^1].m_EndEmitLocation.Valid());
            }

            public void endLiveRangeAtEmitter(Emitter emit)
            {
                noway_assert(hasVariableLiveRangeOpen());
                m_VariableLiveRanges[^1].m_EndEmitLocation.CaptureLocation(emit);

                if (Configuration.DEBUG)
                {
                    var liveRangeDumper = getLiveRangeDumper();
                    if (liveRangeDumper.Compiler.verbose)
                    {
                        jitprintf($"Debug: Closing V{liveRangeDumper.VarNum:D2} debug range.\n");
                    }
                }
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

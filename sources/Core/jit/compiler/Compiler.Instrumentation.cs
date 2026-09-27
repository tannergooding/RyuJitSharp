// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static RyuJitSharp.CorInfoFlag;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp;

public partial class Compiler
{
    protected unsafe PhaseStatus fgPrepareToInstrumentMethod()
    {
        if (compIsForInlining && JitConfig.JitInstrumentInlinees == 0)
        {
            JITDUMP("Inlinee instrumentation disabled by config\n");
            return PhaseStatus.MODIFIED_NOTHING;
        }

        var edgesEnabled = JitConfig.JitEdgeProfiling > 0;
        var prejit = IsAot;
        var useEdgeProfiles = edgesEnabled && !prejit;
        var minimalProfiling = prejit
            ? JitConfig.JitMinimalPrejitProfiling > 0 : JitConfig.JitMinimalJitProfiling > 0;

        if (minimalProfiling && (info.compFlags & CORINFO_FLG_INTRINSIC) != 0)
        {
            var intrinsic = lookupNamedIntrinsic(info.compMethodHnd);
            string? className = null;
            if (intrinsic is NI_System_Numerics_Intrinsic)
            {
                className = getClassNameFromMetadata(info.compClassHnd, out _);
            }

            if (!ShouldInstrumentMinimalIntrinsic(intrinsic, className))
            {
                JITDUMP("Not instrumenting intrinsic excluded by minimal profiling\n");
                fgCountInstrumentor = new NonInstrumentor(this);
                fgHistogramInstrumentor = new NonInstrumentor(this);
                fgValueInstrumentor = new NonInstrumentor(this);
                return PhaseStatus.MODIFIED_NOTHING;
            }
        }

        if (minimalProfiling && fgBBcount < 2)
        {
            JITDUMP("Not using any block profiling (fgBBcount < 2)\n");
            fgCountInstrumentor = new NonInstrumentor(this);
        }
        else if (useEdgeProfiles)
        {
            JITDUMP("Using edge profiling\n");
            fgCountInstrumentor = new EfficientEdgeCountInstrumentor(this, minimalProfiling);
        }
        else
        {
            JITDUMP($"Using block profiling, because {(prejit ? "prejitting" : "edge profiling disabled")}\n");
            fgCountInstrumentor = new BlockCountInstrumentor(this);
        }

        var useClassProfiles = JitConfig.JitClassProfiling > 0;
        var useDelegateProfiles = JitConfig.JitDelegateProfiling > 0;
        var useVTableProfiles = JitConfig.JitVTableProfiling > 0;
        if (!prejit && (useClassProfiles || useDelegateProfiles || useVTableProfiles))
        {
            fgHistogramInstrumentor = new HandleHistogramProbeInstrumentor(this);
        }
        else
        {
            JITDUMP($"Not doing class/method profiling, because {(prejit ? "prejit" : "class/method profiles disabled")}\n");
            fgHistogramInstrumentor = new NonInstrumentor(this);
        }

        if (!prejit && JitConfig.JitProfileValues != 0)
        {
            fgValueInstrumentor = new ValueInstrumentor(this);
        }
        else
        {
            JITDUMP($"Not doing generic profiling, because {(prejit ? "prejit" : "DOTNET_JitProfileValues=0")}\n");
            fgValueInstrumentor = new NonInstrumentor(this);
        }

        fgCountInstrumentor.Prepare(preImport: true);
        fgHistogramInstrumentor.Prepare(preImport: true);
        fgValueInstrumentor.Prepare(preImport: true);
        return PhaseStatus.MODIFIED_NOTHING;
    }

    internal static bool ShouldInstrumentMinimalIntrinsic(NamedIntrinsic intrinsic, string? className)
    {
        switch (intrinsic)
        {
            case NI_System_Runtime_Intrinsics_Intrinsic:
            case NI_System_Runtime_Intrinsics_PlatformIntrinsic:
            case NI_IsSupported:
            case NI_IsHardwareAccelerated:
            case NI_IsSupported_Type:
            case NI_Vector_GetCount:
            case NI_System_GC_KeepAlive:
            case NI_System_Threading_Thread_FastPollGC:
            case NI_System_Threading_Interlocked_MemoryBarrier:
            case NI_System_Threading_Volatile_ReadBarrier:
            case NI_System_Threading_Volatile_WriteBarrier:
            case NI_System_StubHelpers_NextCallReturnAddress:
            case NI_System_Activator_AllocatorOf:
            case NI_System_Activator_DefaultConstructorOf:
            case NI_Internal_Runtime_MethodTable_Of:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsRuntimeAsync:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_IsReferenceOrContainsReferences:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_GetMethodTable:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_WriteBarrier:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallGenericContext:
            case NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallAsyncContinuation:
            case NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncSuspend:
            case NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncCallContinuation:
            case NI_System_Runtime_CompilerServices_AsyncHelpers_TailAwait:
            case NI_System_Runtime_CompilerServices_StaticsHelpers_VolatileReadAsByref:
            {
                return false;
            }

            case NI_System_Numerics_Intrinsic:
            {
                return className is not ("Vector" or "Vector`1");
            }

            default:
            {
                assert(intrinsic is not NI_Throw_PlatformNotSupportedException);
#if FEATURE_HW_INTRINSICS
                if (intrinsic is > NI_HW_INTRINSIC_START and < NI_HW_INTRINSIC_END)
                {
                    return false;
                }
#endif
                return intrinsic is not (> NI_SRCS_UNSAFE_START and < NI_SRCS_UNSAFE_END);
            }
        }
    }

    protected PhaseStatus fgInstrumentMethod()
    {
        BasicBlock? returnBlock = null;
        Statement? temporaryReturn = null;
        if (compIsForInlining)
        {
            if (JitConfig.JitInstrumentInlinees == 0)
            {
                JITDUMP("Inlinee instrumentation disabled by config\n");
                return PhaseStatus.MODIFIED_NOTHING;
            }

            var returnExpression = impInlineInfo.inlineCandidateInfo.retExpr;
            if (returnExpression?.SubstBB is BasicBlock block)
            {
                assert(returnExpression.SubstExpr is not null);
                returnBlock = block;
                temporaryReturn = gtNewStmt(returnExpression.SubstExpr);
                fgInsertStmtAtEnd(block, temporaryReturn);
#if DEBUG
                JITDUMP($"Temporarily adding ret expr [{returnExpression.SubstExpr.TreeId:D6}] " +
                    $"to {FMT_BB(block.bbNum)}\n");
#endif
            }
        }

        var status = fgInstrumentMethodCore();
        if (temporaryReturn is not null)
        {
            assert(returnBlock is not null);
            fgRemoveStmt(returnBlock, temporaryReturn);
        }

        return status;
    }

    protected unsafe PhaseStatus fgInstrumentMethodCore()
    {
        assert(fgCountInstrumentor is not null);
        assert(fgHistogramInstrumentor is not null);
        assert(fgValueInstrumentor is not null);
        var count = fgCountInstrumentor;
        var histogram = fgHistogramInstrumentor;
        var value = fgValueInstrumentor;

        count.Prepare(preImport: false);
        histogram.Prepare(preImport: false);
        value.Prepare(preImport: false);

        var schema = new List<ICorJitInfo.PgoInstrumentationSchema>();
        foreach (var block in Blocks)
        {
            if (count.ShouldProcess(block))
            {
                count.BuildSchemaElements(block, schema);
            }

            if (histogram.ShouldProcess(block))
            {
                histogram.BuildSchemaElements(block, schema);
            }

            if (value.ShouldProcess(block))
            {
                value.BuildSchemaElements(block, schema);
            }
        }

        var anticipatoryChanges = count.ModifiedFlow || histogram.ModifiedFlow || value.ModifiedFlow;
        var earlyExit = anticipatoryChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
        var minimalMode = IsAot ? JitConfig.JitMinimalPrejitProfiling > 0 : JitConfig.JitMinimalJitProfiling > 0;
        if (minimalMode && count.SchemaCount == 1 && histogram.SchemaCount == 0 && value.SchemaCount == 0)
        {
            JITDUMP("Not instrumenting method: minimal probing enabled, and method has only one counter and no class and no value probes\n");
            return earlyExit;
        }

        if (schema.Count == 0)
        {
            JITDUMP("Not instrumenting method: no schemas were created\n");
            return earlyExit;
        }

        JITDUMP($"Instrumenting method: {count.SchemaCount} count probes, " +
            $"{histogram.SchemaCount} class probes and {value.SchemaCount} value probes\n");
        byte* profileMemory = null;
        int result;
        // The callback writes schema offsets; only the callback needs the managed schema pinned.
        // Keep the EE-owned profile buffer pointer, not a managed copy, after the pin ends.
        fixed (ICorJitInfo.PgoInstrumentationSchema* entries = CollectionsMarshal.AsSpan(schema))
        {
            result = info.compCompHnd->allocPgoInstrumentationBySchema(
                info.compMethodHnd, entries, schema.Count, &profileMemory);
        }

        if (result < 0)
        {
            JITDUMP($"Unable to instrument: schema allocation failed: 0x{result:x}\n");
            if (result != unchecked((int)0x80004001))
            {
                noway_assert(false, "Error: unexpected hresult from allocPgoInstrumentationBySchema");
            }

            return earlyExit;
        }

        JITDUMP($"Instrumentation data base address is {FMT_DBG_ADDR(profileMemory)}\n");
        foreach (var block in Blocks)
        {
            if (count.ShouldInstrument(block))
            {
                count.Instrument(block, schema, profileMemory);
            }

            if (histogram.ShouldInstrument(block))
            {
                histogram.Instrument(block, schema, profileMemory);
            }

            if (value.ShouldInstrument(block))
            {
                value.Instrument(block, schema, profileMemory);
            }
        }

        assert(count.InstrCount <= count.SchemaCount);
        assert(histogram.InstrCount == info.compHandleHistogramProbeCount);
        return PhaseStatus.MODIFIED_EVERYTHING;
    }
}

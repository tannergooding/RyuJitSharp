// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class CSE_HeuristicRLHook
{
    public override unsafe void ConsiderCandidates()
    {
        EnsureDebugDiagnosticsAvailable();
        if (JitConfig.JitRLHookCSEDecisions is null)
        {
            return;
        }

        ConfigIntArray config = default;
        config.EnsureInit(JitConfig.JitRLHookCSEDecisions);
        var decisions = config.GetData();
        var count = m_compiler.CseCandidateCount;
        foreach (var index in decisions)
        {
            if ((index < 0) || (index >= count))
            {
                JITDUMP($"Invalid candidate number {unchecked(index + 1)}\n");
                continue;
            }

            var descriptor = m_compiler.CseCandidateTable[index];
            if (descriptor is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RLHookCSECandidateIsMissingItsDescriptor);
                return;
            }
            if (!descriptor.IsViable())
            {
                JITDUMP($"Abandoned {FMT_CSE(descriptor.csdIndex)} -- not viable\n");
                continue;
            }

            _ = m_compiler.NextCseAttempt();
            var candidate = new CSE_Candidate(this, descriptor);
            JITDUMP($"\nRLHook attempting {FMT_CSE(candidate.CseIndex())}\n");
            JITDUMP("CSE Expression : \n");
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
            JITDUMP("\n");

            PerformCSE(candidate);
            madeChanges = true;
        }
    }

    public override unsafe void DumpMetrics()
    {
        EnsureDebugDiagnosticsAvailable();
        string[] __jitExceptionResult539;
        var __jitExceptionCoalesceLeft540 = s_featureNameAndType;
        if (__jitExceptionCoalesceLeft540 != null)
        {
            __jitExceptionResult539 = __jitExceptionCoalesceLeft540;
        }
        else
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.RLHookCSEFeatureNamesHaveNotBeenInitialized);
            __jitExceptionResult539 = default;
        }
        var featureNameAndType = __jitExceptionResult539;
        if (JitConfig.JitRLHookEmitFeatureNames > 0)
        {
            jitprintf(" featureNames ");
            for (var index = 0; index < MaxFeatures; index++)
            {
                jitprintf($"{(index == 0 ? "" : ",")}{featureNameAndType[index]}");
            }
        }

        for (var index = 0; index < m_compiler.CseCandidateCount; index++)
        {
            var descriptor = m_compiler.CseCandidateTable[index];
            if (descriptor is null)
            {
                FatalJitException.ThrowFatalJitException(FatalJitErrorCode.RLHookCSECandidateIsMissingItsDescriptor);
                return;
            }
            var features = new int[MaxFeatures];
            GetFeatures(descriptor, features);

            jitprintf($" features #{descriptor.csdIndex}");
            for (var feature = 0; feature < MaxFeatures; feature++)
            {
                jitprintf($",{features[feature]}");
            }
        }

        if (JitConfig.JitRLHookCSEDecisions is not null)
        {
            ConfigIntArray config = default;
            config.EnsureInit(JitConfig.JitRLHookCSEDecisions);
            var decisions = config.GetData();
            if (decisions.Length > 0)
            {
                jitprintf(" seq ");
                for (var index = 0; index < decisions.Length; index++)
                {
                    jitprintf($"{(index == 0 ? "" : ",")}{decisions[index]}");
                }
            }
        }
    }
}

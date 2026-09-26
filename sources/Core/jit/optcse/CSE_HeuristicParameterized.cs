// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class CSE_HeuristicParameterized : CSE_HeuristicCommon
{
    public sealed class Choice
    {
        public Choice(CSEdsc? descriptor, double preference)
        {
            Descriptor = descriptor;
            Preference = preference;
        }

        public CSEdsc? Descriptor { get; }
        public double Preference { get; set; }
        public double Softmax { get; set; }
        public bool Performed { get; set; }
    }

    protected const int NumParameters = 25;
    protected const int BooleanScale = 5;
    protected const int MaxSteps = 65;

    private static readonly double[] s_defaultParameters =
    [
        0.2425, 0.2479, 0.1089, -0.2363, 0.2472, -0.0559, -0.8418, -0.0585, -0.2773,
        0.0000, 0.0213, -0.4116, 0.0000, -0.0922, 0.2593, -0.0315, -0.0745, 0.2607,
        0.3475, -0.0590, -0.3177, -0.6883, -0.4998, -0.3220, -0.2268,
    ];

    protected readonly double[] m_parameters;
    protected uint m_registerPressure;
    protected List<double>? m_localWeights;
    protected bool m_verbose;

#if DEBUG
    protected readonly List<double> m_likelihoods = [];
    protected List<double>? m_baseLikelihoods;
    protected List<string>? m_features;
#endif

    public CSE_HeuristicParameterized(Compiler compiler) : base(compiler)
    {
        m_parameters = (double[])s_defaultParameters.Clone();
        m_registerPressure = unchecked((uint)(compiler.CNT_CALLEE_TRASH_INT + CNT_CALLEE_SAVED));
        m_verbose = JitConfig.JitRLCSEVerbose > 0;
#if DEBUG
        m_verbose |= compiler.verbose;
#endif
    }

    public override string Name() => "Parameterized CSE Heuristic";

    public override bool ConsiderTree(GenTree tree, bool isReturn)
        => CanConsiderTree(tree, isReturn);

    public override void ConsiderCandidates()
    {
        var count = m_compiler.CseCandidateCount;
        sortTab = new CSEdsc?[count];
        sortSiz = unchecked((nuint)(count * IntPtr.Size));
        Array.Copy(m_compiler.CseCandidateTable, sortTab, count);

        CaptureLocalWeights();
        GreedyPolicy();
    }

    public void CaptureLocalWeights()
    {
        JITDUMP("Local weight table...\n");
        m_localWeights = [];
        var trackedToLocal = m_compiler.lvaTrackedToVarNum;

        for (var index = 0; index < m_compiler.lvaTrackedCount; index++)
        {
            var lclNum = (trackedToLocal ??
                throw new InvalidOperationException("Tracked local map is not available."))[index];
            ref var descriptor = ref m_compiler.lvaGetDesc(lclNum);
            if ((descriptor.lvRefCnt() == 0) || descriptor.lvDoNotEnregister ||
                varTypeIsFloating(descriptor.Type) || varTypeIsMask(descriptor.Type))
            {
                continue;
            }

            JITDUMP($"V{lclNum:D2},{FMT_WT(descriptor.lvRefCntWtd())}\n");
            m_localWeights.Add(descriptor.lvRefCntWtd() / BB_UNITY_WEIGHT);
        }
    }

    public void GreedyPolicy()
    {
#if DEBUG
        if (m_verbose)
        {
            logf("RL using greedy policy\n");
        }
#endif
        List<Choice> choices = [with(m_compiler.CseCandidateCount + 1)];
        var numUnmarked = m_compiler.CseUnmarks;
        var recomputeFeatures = true;

        while (true)
        {
            var choice = ChooseGreedy(choices, recomputeFeatures);
            var descriptor = choice.Descriptor;
#if DEBUG
            m_likelihoods.Add(choice.Softmax);
#endif
            if (descriptor is null)
            {
                break;
            }

            assert(sortTab is not null);
            assert(sortTab[descriptor.csdIndex - 1] == descriptor);
            sortTab[descriptor.csdIndex - 1] = null;
            assert(descriptor.IsViable());

            var candidate = new CSE_Candidate(this, descriptor);
            if (m_verbose)
            {
                jitprintf($"\nRL attempting {FMT_CSE(candidate.CseIndex())}\n");
            }

            JITDUMP("CSE Expression : \n");
#if DEBUG
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
#endif
            JITDUMP("\n");

            PerformCSE(candidate);
            madeChanges = true;
            choice.Performed = true;

            var newNumUnmarked = m_compiler.CseUnmarks;
            assert(newNumUnmarked >= numUnmarked);
            recomputeFeatures = numUnmarked != newNumUnmarked;
            numUnmarked = newNumUnmarked;
        }
    }

    public void BuildChoices(List<Choice> choices)
    {
        JITDUMP("Building choice array...\n");
        assert(sortTab is not null);
        for (var index = 0; index < m_compiler.CseCandidateCount; index++)
        {
            var descriptor = sortTab[index];
            if ((descriptor is null) || !descriptor.IsViable())
            {
                continue;
            }

            choices.Add(new Choice(descriptor, Preference(descriptor)));
        }

        choices.Add(new Choice(null, StoppingPreference()));
    }

    public Choice ChooseGreedy(List<Choice> choices, bool recompute)
    {
        if (recompute)
        {
            choices.Clear();
            BuildChoices(choices);
        }
        else
        {
            var stopping = choices[^1];
            assert(stopping.Descriptor is null);
            stopping.Preference = StoppingPreference();
        }

        // ArrayStack's TopRef(0) is the last choice: stopping.
        var choiceNum = 0;
        for (var index = 1; index < choices.Count; index++)
        {
            var choice = choices[choices.Count - 1 - index];
            if (choice.Performed)
            {
                continue;
            }

            var bestChoice = choices[choices.Count - 1 - choiceNum];
            var delta = choice.Preference - bestChoice.Preference;
            var update = (delta > 0) || ((delta == 0) &&
                ((choice.Descriptor is null) ||
                 ((bestChoice.Descriptor is not null) &&
                  (choice.Descriptor.csdIndex < bestChoice.Descriptor.csdIndex))));
            if (update)
            {
                choiceNum = index;
            }
        }

#if DEBUG
        if (m_verbose)
        {
            logf("Greedy candidate evaluation\n");
            DumpChoices(choices, choiceNum);
        }
#endif
        return choices[choices.Count - 1 - choiceNum];
    }
}

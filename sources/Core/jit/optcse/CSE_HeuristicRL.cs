// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public unsafe class CSE_HeuristicRL : CSE_HeuristicParameterized
{
    private readonly double[] m_rewards = new double[MaxSteps];
    private readonly CLRRandom m_cseRng;
    private readonly bool m_updateParameters;
    private readonly bool m_greedy;
    private readonly double m_alpha;

    public CSE_HeuristicRL(Compiler compiler) : base(compiler)
    {
        m_cseRng = new CLRRandom(compiler.info.compMethodHash() ^ JitConfig.JitRandomCSE);

        var parameters = new ConfigDoubleArray();
        parameters.EnsureInit(JitConfig.JitRLCSE);
        var initial = parameters.GetData();
        for (var index = 0; index < Math.Min(initial.Length, NumParameters); index++)
        {
            m_parameters[index] = initial[index];
        }
        if (initial.Length < NumParameters)
        {
            JITDUMP($"Too few parameters (expected {NumParameters}), trailing will be zero\n");
            Array.Clear(m_parameters, initial.Length, NumParameters - initial.Length);
        }
        else if (initial.Length > NumParameters)
        {
            JITDUMP($"Too many parameters (expected {NumParameters}), trailing will be ignored\n");
        }

        if ((JitConfig.JitReplayCSE is not null) && (JitConfig.JitReplayCSEReward is not null))
        {
            m_updateParameters = true;
            var rewards = new ConfigDoubleArray();
            rewards.EnsureInit(JitConfig.JitReplayCSEReward);
            var values = rewards.GetData();
            for (var index = 0; index < Math.Min(values.Length, MaxSteps); index++)
            {
                m_rewards[index] = values[index];
            }

            if (JitConfig.JitRLCSEAlpha is not null)
            {
                var alpha = new ConfigDoubleArray();
                alpha.EnsureInit(JitConfig.JitRLCSEAlpha);
                m_alpha = alpha.GetData()[0];
            }
            else
            {
                m_alpha = 0.001;
            }
        }
        else if (JitConfig.JitRLCSEGreedy > 0)
        {
            m_greedy = true;
        }

        m_baseLikelihoods = [];
        m_features = [];
    }

    public override string Name() => m_updateParameters
        ? "RL Policy Gradient Update"
        : "RL Policy Gradient Stochastic";

    public override bool ConsiderTree(GenTree tree, bool isReturn)
        => CanConsiderTree(tree, isReturn);

    public override void Announce()
    {
        JITDUMP($"{Name()} salt {JitConfig.JitRandomCSE} parameters ");
        for (var index = 0; index < NumParameters; index++)
        {
            JITDUMP($"{(index == 0 ? "" : ",")}{formatFloat(m_parameters[index], "F6")}");
        }
        JITDUMP("\n");

        if (m_updateParameters)
        {
            var sequence = Marshal.PtrToStringUTF8((nint)JitConfig.JitReplayCSE);
            var rewards = Marshal.PtrToStringUTF8((nint)JitConfig.JitReplayCSEReward);
            JITDUMP($"Operating in update mode with sequence {sequence}, rewards {rewards}, " +
                $"and alpha {formatFloat(m_alpha, "F6")}\n");
        }
    }

    public override void DumpMetrics()
    {
        base.DumpMetrics();
        if (m_updateParameters)
        {
            var features = m_features
                ?? throw new InvalidOperationException("RL CSE feature metrics have not been initialized.");
            jitprintf(" updatedparams ");
            for (var index = 0; index < NumParameters; index++)
            {
                jitprintf($"{(index == 0 ? "" : ",")}{formatFloat(m_parameters[index], "F6")}");
            }

            if (JitConfig.JitRLCSECandidateFeatures > 0)
            {
                jitprintf(", features ");
                var first = true;
                foreach (var feature in features)
                {
                    jitprintf($"{(first ? "" : ",")}{feature}");
                    first = false;
                }
            }
        }
        else if (!m_greedy)
        {
            var baseLikelihoods = m_baseLikelihoods
                ?? throw new InvalidOperationException("RL CSE base likelihoods have not been initialized.");
            jitprintf(" likelihoods ");
            var first = true;
            foreach (var likelihood in m_likelihoods)
            {
                jitprintf($"{(first ? "" : ",")}{formatFloat(likelihood, "F3")}");
                first = false;
            }

            jitprintf(" baseLikelihoods ");
            first = true;
            foreach (var likelihood in baseLikelihoods)
            {
                jitprintf($"{(first ? "" : ",")}{formatFloat(likelihood, "F3")}");
                first = false;
            }
        }
    }

    public override void ConsiderCandidates()
    {
        var count = m_compiler.CseCandidateCount;
        sortTab = new CSEdsc?[count];
        sortSiz = unchecked((nuint)(count * IntPtr.Size));
        Array.Copy(m_compiler.CseCandidateTable, sortTab, count);

        CaptureLocalWeights();
        if (m_updateParameters)
        {
            UpdateParameters();
        }
        else if (m_greedy)
        {
            GreedyPolicy();
        }
        else
        {
            SoftmaxPolicy();
        }
    }

    private void SoftmaxPolicy()
    {
        var baseLikelihoods = m_baseLikelihoods
            ?? throw new InvalidOperationException("RL CSE base likelihoods have not been initialized.");
        if (m_verbose)
        {
            jitprintf("RL using softmax policy\n");
        }

        List<Choice> choices = [];
        choices.Capacity = m_compiler.CseCandidateCount + 1;
        var first = true;
        while (true)
        {
            var choice = ChooseSoftmax(choices);
            if (first)
            {
                for (var index = choices.Count - 1; index >= 0; index--)
                {
                    var option = choices[index];
                    baseLikelihoods.Add(option.Descriptor?.csdIndex ?? 0);
                    baseLikelihoods.Add(option.Softmax);
                }
                first = false;
            }

            var descriptor = choice.Descriptor;
            if (descriptor is null)
            {
                m_likelihoods.Add(choice.Softmax);
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
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
            JITDUMP("\n");

            PerformCSE(candidate);
            madeChanges = true;
            m_likelihoods.Add(choice.Softmax);
        }
    }

    private Choice ChooseSoftmax(List<Choice> choices)
    {
        choices.Clear();
        BuildChoices(choices);
        Softmax(choices);

        var randomFactor = m_cseRng.NextDouble();
        double sum = 0;
        var choiceNum = 0;
        for (var index = 0; index < choices.Count; index++)
        {
            sum += choices[choices.Count - 1 - index].Softmax;
            if (randomFactor < sum)
            {
                choiceNum = index;
                break;
            }
        }

        if (m_verbose)
        {
            jitprintf($"Current candidate evaluation, rng is {formatFloat(randomFactor, "F6")}\n");
            DumpChoices(choices, choiceNum);
        }

        return choices[choices.Count - 1 - choiceNum];
    }

    private static void Softmax(List<Choice> choices)
    {
        double sum = 0;
        for (var index = choices.Count - 1; index >= 0; index--)
        {
            var choice = choices[index];
            choice.Softmax = Math.Exp(choice.Preference);
            sum += choice.Softmax;
        }

        for (var index = choices.Count - 1; index >= 0; index--)
        {
            choices[index].Softmax /= sum;
        }
    }

    private void UpdateParameters()
    {
        var count = m_compiler.CseCandidateCount;
        if (count == 0)
        {
            return;
        }

        List<Choice> choices = [];
        ConfigIntArray configuration = default;
        configuration.EnsureInit(JitConfig.JitReplayCSE);
        var sequence = configuration.GetData();

        if (m_verbose)
        {
            jitprintf("Updating parameters with sequence ");
            configuration.Dump();
            jitprintf($" alpha {FMT_WT(m_alpha)} and rewards ");
            for (var index = 0; index < sequence.Length; index++)
            {
                jitprintf((index == 0 ? "" : ",") +
                    formatFloat(m_rewards[RewardIndex(index)], "F4").PadLeft(7));
            }
            jitprintf("\n");
        }

        var delta = new double[NumParameters];
        var step = 0;
        for (; step < sequence.Length; step++)
        {
            var number = sequence[step];
            if (number == 0)
            {
                break;
            }

            var index = unchecked(number - 1);
            if ((index < 0) || (index >= count))
            {
                JITDUMP($"Invalid candidate number {unchecked(index + 1)}\n");
                continue;
            }

            choices.Clear();
            BuildChoices(choices);
            Softmax(choices);

            _ = m_compiler.NextCseAttempt();
            var descriptor = sortTab![index]
                ?? throw new FatalJitException("RL CSE update candidate was previously consumed.");
            assert(sortTab[descriptor.csdIndex - 1] == descriptor);
            sortTab[descriptor.csdIndex - 1] = null;
            if (!descriptor.IsViable())
            {
                continue;
            }

            var candidate = new CSE_Candidate(this, descriptor);
            if (m_verbose)
            {
                jitprintf($"\nRL Update attempting {FMT_CSE(candidate.CseIndex())}\n");
            }
            JITDUMP("CSE Expression : \n");
            if (m_compiler.verbose)
            {
                m_compiler.gtDispTree(candidate.Expr());
            }
            JITDUMP("\n");

            UpdateParametersStep(descriptor, choices, m_rewards[RewardIndex(step)], delta);
            PerformCSE(candidate);
            madeChanges = true;
        }

        choices.Clear();
        BuildChoices(choices);
        var undone = choices.Count - 1;
        if (undone > 0)
        {
            if (m_verbose)
            {
                jitprintf($"\nRL Update stopping early ({step} CSEs done, {undone} CSEs left undone)\n");
            }
            Softmax(choices);
            UpdateParametersStep(null, choices, m_rewards[RewardIndex(step)], delta);
        }

        for (var index = 0; index < NumParameters; index++)
        {
            m_parameters[index] += delta[index];
        }
    }

    private static int RewardIndex(int step)
    {
        if ((uint)step >= MaxSteps)
        {
            throw new FatalJitException("RL CSE update sequence exceeds the native reward capacity.");
        }
        return step;
    }

    private void UpdateParametersStep(CSEdsc? descriptor, List<Choice> choices, double reward, double[] delta)
    {
        assert(FindChoice(descriptor, choices) is not null);
        if (m_verbose)
        {
            DumpChoices(choices, descriptor);
            jitprintf("Reward: " + formatFloat(reward, "F4").PadLeft(7) + "\n");
        }

        var currentFeatures = new double[NumParameters];
        GetFeatures(descriptor, currentFeatures);
        var adjustment = new double[NumParameters];
        for (var choiceIndex = choices.Count - 1; choiceIndex >= 0; choiceIndex--)
        {
            var choice = choices[choiceIndex];
            var features = new double[NumParameters];
            GetFeatures(choice.Descriptor, features);
            for (var index = 0; index < NumParameters; index++)
            {
                adjustment[index] += choice.Softmax * features[index];
            }
        }

        if (m_verbose)
        {
            jitprintf("Feat   OldDelta     Feature  Adjustment    Gradient   StepDelta   NewDelta\n");
        }
        for (var index = 0; index < NumParameters; index++)
        {
            var gradient = currentFeatures[index] - adjustment[index];
            var newDelta = m_alpha * reward * gradient;
            if (m_verbose)
            {
                jitprintf($"{index,4}  {formatFloat(delta[index], "F7"),10}  " +
                    $"{formatFloat(currentFeatures[index], "F7"),10}  " +
                    $"{formatFloat(adjustment[index], "F7"),10}  " +
                    $"{formatFloat(gradient, "F7"),10}  " +
                    $"{formatFloat(newDelta, "F7"),10} {formatFloat(newDelta + delta[index], "F7"),10}\n");
            }
            delta[index] += newDelta;
        }
    }

    private static Choice? FindChoice(CSEdsc? descriptor, List<Choice> choices)
    {
        for (var index = choices.Count - 1; index >= 0; index--)
        {
            if (choices[index].Descriptor == descriptor)
            {
                return choices[index];
            }
        }
        return null;
    }
}
#endif

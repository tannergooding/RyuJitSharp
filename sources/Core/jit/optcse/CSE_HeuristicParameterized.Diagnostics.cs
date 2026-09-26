// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Collections.Generic;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class CSE_HeuristicParameterized
{
    public override void Announce()
    {
        JITDUMP($"{Name()} parameters ");
        for (var index = 0; index < NumParameters; index++)
        {
            JITDUMP($"{(index == 0 ? "" : ",")}{formatFloat(m_parameters[index], "F6")}");
        }
        JITDUMP("\n");
    }

    public override void DumpMetrics()
    {
        base.DumpMetrics();
        jitprintf(" params ");
        for (var index = 0; index < NumParameters; index++)
        {
            jitprintf($"{(index == 0 ? "" : ",")}{formatFloat(m_parameters[index], "F6")}");
        }
    }

    public void DumpFeatures(CSEdsc? descriptor, double[] features)
    {
        jitprintf($"features,{m_compiler.info.compMethodSpmiIndex},{FMT_CSE(descriptor?.csdIndex ?? 0)}");
        for (var index = 0; index < NumParameters; index++)
        {
            jitprintf($",{formatFloat(features[index], "F6")}");
        }
        jitprintf("\n");
    }

    public void DumpChoices(List<Choice> choices, int highlight = -1)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[choices.Count - 1 - index];
            if (choice.Performed)
            {
                continue;
            }

            var marker = index == highlight ? "=>" : "  ";
            var preference = formatFloat(choice.Preference, "F7").PadLeft(10);
            var likelihood = formatFloat(choice.Softmax, "F7").PadLeft(10);
            if (choice.Descriptor is CSEdsc descriptor)
            {
                jitprintf($"{marker}{index,2}: {FMT_CSE(descriptor.csdIndex)} preference {preference} " +
                    $"likelihood {likelihood}\n");
            }
            else
            {
                jitprintf($"{marker}{index,2}: QUIT    preference {preference} likelihood {likelihood}\n");
            }
        }
    }

    public void DumpChoices(List<Choice> choices, CSEdsc? highlight)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[choices.Count - 1 - index];
            if (choice.Performed)
            {
                continue;
            }

            var marker = choice.Descriptor == highlight ? "=>" : "  ";
            var preference = formatFloat(choice.Preference, "F7").PadLeft(10);
            var likelihood = formatFloat(choice.Softmax, "F7").PadLeft(10);
            if (choice.Descriptor is CSEdsc descriptor)
            {
                jitprintf($"{marker}{index,2}: {FMT_CSE(descriptor.csdIndex)} preference {preference} " +
                    $"likelihood {likelihood}\n");
            }
            else
            {
                jitprintf($"{marker}{index,2}: QUIT    preference {preference} likelihood {likelihood}\n");
            }
        }
    }
}
#endif

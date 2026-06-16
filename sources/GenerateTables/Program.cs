// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RyuJitSharp;

internal static class Program
{
    public static void Main()
    {
        HandleInstructionSetDesc();
    }

    private static void HandleInstructionSetDesc()
    {
        var lines = File.ReadAllLines(@"Inputs\InstructionSetDesc.txt");

        var insSetBuilder = new StringBuilder();
        var insSetFlagsBuilder = new StringBuilder();
        var ensureValidBuilder = new StringBuilder();
        var insSetToStringBuilder = new StringBuilder();
        var fromR2RInsSetBuilder = new StringBuilder();

        var previousTargetArchInsSet = "";
        var previousTargetArchInsSetFlags = "";
        var previousTargetArchEnsureValid = "";
        var previousTargetArchInsSetToString = "";
        var previousTargetArchFromR2RInsSet = "";

        var addedInsSet = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';'))
            {
                continue;
            }

            var parts = trimmedLine.Split(',');
            var kind = parts[0].Trim();

            if (kind.Equals("instructionset", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 7)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var targetArch = parts[1].Trim().ToUpper(CultureInfo.InvariantCulture);

                if (targetArch.Equals("X86", StringComparison.OrdinalIgnoreCase))
                {
                    targetArch = "XARCH";
                }

                var name = parts[5].Trim();

                if (!addedInsSet.TryGetValue(targetArch, out var addedInsSetForTargetArch))
                {
                    addedInsSetForTargetArch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    addedInsSet[targetArch] = addedInsSetForTargetArch;
                }

                if (addedInsSetForTargetArch.Add(name))
                {
                    _ = AppendIfDefWhenRequired(insSetBuilder, ref previousTargetArchInsSet, targetArch, allowElif: true);
                    _ = insSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"    InstructionSet_{name},");

                    _ = AppendIfDefWhenRequired(insSetToStringBuilder, ref previousTargetArchInsSetToString, targetArch);
                    _ = insSetToStringBuilder.AppendLine(CultureInfo.InvariantCulture, $"            InstructionSet_{name} => \"{name}\",");
                }

                var r2rName = parts[2].Trim();

                if (string.IsNullOrEmpty(r2rName))
                {
                    r2rName = parts[3].Trim();
                }

                if (!string.IsNullOrEmpty(r2rName))
                {
                    _ = AppendIfDefWhenRequired(fromR2RInsSetBuilder, ref previousTargetArchFromR2RInsSet, targetArch, allowElif: true);
                    _ = fromR2RInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"            READYTORUN_INSTRUCTION_{r2rName} => InstructionSet_{name},");
                }
                continue;
            }

            if (kind.Equals("instructionset64bit", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 3)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var targetArchSuffix = parts[1].Trim();
                var targetArch = targetArchSuffix.ToUpper(CultureInfo.InvariantCulture);

                if (targetArch.Equals("X86", StringComparison.OrdinalIgnoreCase))
                {
                    targetArch = "AMD64";
                    targetArchSuffix = "X64";
                }
                else if (targetArch.Equals("ARM64", StringComparison.OrdinalIgnoreCase))
                {
                    targetArchSuffix = "Arm64";
                }

                var name = parts[2].Trim();

                _ = insSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"    InstructionSet_{name}_{targetArchSuffix},");

                if (!AppendIfDefWhenRequired(insSetFlagsBuilder, ref previousTargetArchInsSetFlags, targetArch, allowElif: true))
                {
                    _ = insSetFlagsBuilder.AppendLine();
                }
                _ = insSetFlagsBuilder.AppendLine(CultureInfo.InvariantCulture, $$"""
        if (HasInstructionSet(InstructionSet_{{name}}))                    
        {
            AddInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}});
        }
""");

                if (!AppendIfDefWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, targetArch))
                {
                    _ = ensureValidBuilder.AppendLine();
                }
                _ = ensureValidBuilder.AppendLine(CultureInfo.InvariantCulture, $$"""
            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}) && !resultFlags.HasInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}}))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}});
            }

            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}}) && !resultFlags.HasInstructionSet(InstructionSet_{{name}}))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}});
            }
""");

                _ = AppendIfDefWhenRequired(insSetToStringBuilder, ref previousTargetArchInsSetToString, targetArch);
                _ = insSetToStringBuilder.AppendLine(CultureInfo.InvariantCulture, $"            InstructionSet_{name}_{targetArchSuffix} => \"{name}_{targetArchSuffix}\",");

                continue;
            }

            if (kind.Equals("implication", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 4)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var targetArch = parts[1].Trim().ToUpper(CultureInfo.InvariantCulture);

                if (targetArch.Equals("X86", StringComparison.OrdinalIgnoreCase))
                {
                    targetArch = "XARCH";
                }

                var name = parts[2].Trim();
                var dependency = parts[3].Trim();

                if (!AppendIfDefWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, targetArch))
                {
                    _ = ensureValidBuilder.AppendLine();
                }
                _ = ensureValidBuilder.AppendLine(CultureInfo.InvariantCulture, $$"""
            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}) && !resultFlags.HasInstructionSet(InstructionSet_{{dependency}}))        
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}});
            }
""");

                continue;
            }
        }

        _ = AppendIfDefWhenRequired(insSetBuilder, ref previousTargetArchInsSet, "");
        _ = AppendIfDefWhenRequired(insSetFlagsBuilder, ref previousTargetArchInsSetFlags, "");
        _ = AppendIfDefWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, "");
        _ = AppendIfDefWhenRequired(insSetToStringBuilder, ref previousTargetArchInsSetToString, "");
        _ = AppendIfDefWhenRequired(fromR2RInsSetBuilder, ref previousTargetArchFromR2RInsSet, "");

        _ = Directory.CreateDirectory(@"Outputs\inc\corinfoinstructionset");

        File.WriteAllText(@"Outputs\inc\corinfoinstructionset\CORINFO_InstructionSet.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CORINFO_InstructionSet;

namespace RyuJitSharp;

public enum CORINFO_InstructionSet
{
    InstructionSet_ILLEGAL = 0,

{{insSetBuilder}}

    InstructionSet_NONE = 127,
}
""");
            
        File.WriteAllText(@"Outputs\inc\corinfoinstructionset\CORINFO_InstructionSetFlags.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct CORINFO_InstructionSetFlags
{
    public void Set64BitInstructionSetVariants()
    {
{{insSetFlagsBuilder}}
    }
}
""");
        File.WriteAllText(@"Outputs\inc\corinfoinstructionset\Globals.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_InstructionSetFlags EnsureInstructionSetFlagsAreValid(CORINFO_InstructionSetFlags input)
    {
        CORINFO_InstructionSetFlags oldFlags;
        var resultFlags = input;

        do
        {
            oldFlags = resultFlags;

{{ensureValidBuilder}}
        }
        while (!oldFlags.Equals(resultFlags));

        return resultFlags;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InstructionSetToString(CORINFO_InstructionSet instructionSet)
    {
        return instructionSet switch {
{{insSetToStringBuilder}}

            _ => "UnknownInstructionSet",
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CORINFO_InstructionSet InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet r2rSet)
    {
        return r2rSet switch {
{{fromR2RInsSetBuilder}}
            _ => InstructionSet_ILLEGAL,
        };
    }
}
""");

        static bool AppendIfDefWhenRequired(StringBuilder builder, ref string previousTargetArch, string targetArch, bool allowElif = false)
        {
            var appended = false;

            if (!targetArch.Equals(previousTargetArch, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(previousTargetArch))
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"#if TARGET_{targetArch}");
                }
                else if (string.IsNullOrEmpty(targetArch))
                {
                    _ = builder.Append("#endif");
                }
                else if (allowElif)
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"#elif TARGET_{targetArch}");
                }
                else
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $$"""
#endif
                        
#if TARGET_{{targetArch}}
""");
                }

                previousTargetArch = targetArch;
                appended = true;
            }
            return appended;
        }
    }
}

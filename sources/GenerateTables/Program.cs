// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace RyuJitSharp;

internal static class Program
{
    public static void Main()
    {
        if (Directory.Exists(@"Outputs"))
        {
            Directory.Delete(@"Outputs", recursive: true);
        }

        GenerateApiICorJitInfoNames();
        GenerateApiICorJitInfoNamesExtensions();

        GenerateCodeSeqSM();
        GenerateCompiler();

        GenerateGenTree();
        GenerateGenTreeGlobals();
        GenerateGenTreeOps();
        GenerateGenTreeOpsExtensions();

        GenerateHandleKindIndex();
        GenerateHWIntrinsicInfo();

        GenerateInlineObservation();
        GenerateInlineObservationExtensions();
        GenerateInstruction();

        GenerateJitConfigValues();
        GenerateJitMetadata();
        GenerateJitMetrics();

        GenerateLogGlobals();

        GenerateNamedIntrinsic();

        GenerateOpcode();
        GenerateOpcodeExtensions();

        GeneratePhases();
        GeneratePhasesExtensions();

        GenerateRegMask();
        GenerateRegNumber();
        GenerateRegNumberExtensions();

        GenerateSmOpcode();
        GenerateSmOpcodeExtensions();

        GenerateTargetGlobals();

        GenerateVarTypes();
        GenerateVarTypesExtensions();

        GenerateVNFunc();
        GenerateVNFuncExtensions();

        ProcessInstructionSetDesc();
    }

    private static void GenerateApiICorJitInfoNames()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\ICorJitInfo_names_generated.h", "DEF_CLR_API(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 1)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    API_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");

        File.WriteAllText(@"Outputs\jit\compiler\API_ICorJitInfo_Names.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.API_ICorJitInfo_Names;

namespace RyuJitSharp;

public enum API_ICorJitInfo_Names
{
{{builder}}    API_COUNT,
}
""");
    }

    private static void GenerateApiICorJitInfoNamesExtensions()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\ICorJitInfo_names_generated.h", "DEF_CLR_API(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 1)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        \"{name}\",");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");

        File.WriteAllText(@"Outputs\jit\compiler\API_ICorJitInfo_NamesExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class API_ICorJitInfo_NamesExtensions
{
    private static readonly string[] s_name = [
{{builder}}    ];
}
""");
    }

    private static void GenerateCodeSeqSM()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\smopcodemap.def", "OPCODEMAP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var opcode = parts[0].AsSpan().Trim();
            var name = parts[2].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {name}, // {opcode}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\sm");

        File.WriteAllText(@"Outputs\jit\sm\CodeSeqSM.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class CodeSeqSM
{
    private static ReadOnlySpan<SM_OPCODE> s_opcodeMap => [
{{builder}}    ];
}
""");
    }

    private static void GenerateCompiler()
    {
        var gtDispIconHandleFlagBuilder = ProcessMacroBasedFile(@"Inputs\handlekinds.h", "HANDLE_KIND(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var description = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $$"""

            case {{name}}:
            {
                jitprintf($" {{{description}}}");
                break;
            }
""");
        });

        var compInitVarTypeCalleeTrashRegMasksBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var calleeTrashRegs = parts[11].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        varTypeCalleeTrashRegMasks[(int)(TYP_{name})] = S{calleeTrashRegs};");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");

        File.WriteAllText(@"Outputs\jit\compiler\Compiler.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private void compInitVarTypeCalleeTrashRegMasks()
    {
{{compInitVarTypeCalleeTrashRegMasksBuilder}}    }

    private void gtDispIconHandleFlag(GenTreeIntCon intCon)
    {
        switch (intCon.IconHandleFlag)
        {
            case GTF_EMPTY:
            {
                break;
            }
{{gtDispIconHandleFlagBuilder}}
            default:
            {
                jitprintf(" ILLEGAL");
                break;
            }
        }
    }
}
""");
    }

    private static void GenerateGenTree()
    {
        var handleKindFlagsBuilder = ProcessMacroBasedFile(@"Inputs\handlekinds.h", "HANDLE_KIND(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var flags = parts[2].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {flags}, // {name}");
        });

        ReadOnlySpan<string> prefixes = [
            "GTSTRUCT_0(",
            "GTSTRUCT_1(",
            "GTSTRUCT_2(",
            "GTSTRUCT_3(",
            "GTSTRUCT_4(",
            "GTSTRUCT_N(",
            "GTSTRUCT_2_SPECIAL(",
            "GTSTRUCT_3_SPECIAL(",
        ];

        var asNodeBuilder = ProcessMacroBasedFile(@"Inputs\gtstructs.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (parts.Length is < 2)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var operCheck = new StringBuilder("_oper");

            if (name.Equals("UnOp", StringComparison.Ordinal))
            {
                Debug.Assert(parts.Length is 2);
                Debug.Assert(parts[1].AsSpan().Trim().Equals("GT_OP", StringComparison.Ordinal));
                _ = operCheck.Append(".IsSimple");
            }
            else if (name.Equals("Op", StringComparison.Ordinal))
            {
                Debug.Assert(parts.Length is 2);
                Debug.Assert(parts[1].AsSpan().Trim().Equals("GT_OP", StringComparison.Ordinal));
                _ = operCheck.Append(".IsBinary");
            }
            else
            {
                var firstOper = parts[1].AsSpan().Trim();
                _ = operCheck.Append(CultureInfo.InvariantCulture, $" is {firstOper}");

                var remainingOpers = parts[2..];

                foreach (var remainingOper in remainingOpers)
                {
                    var trimmedOper = remainingOper.AsSpan().Trim();

                    _ = operCheck.Append(CultureInfo.InvariantCulture, $" or {trimmedOper}");
                }
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $$"""
    public GenTree{{name}} As{{name}}()
    {
        assert({{operCheck}});
        assert(this is GenTree{{name}});
        return Unsafe.As<GenTree{{name}}>(this);
    }
""");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\gentree");

        File.WriteAllText(@"Outputs\jit\gentree\GenTree.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class GenTree
{
    private static ReadOnlySpan<HandleKindFlag> s_handleKindFlags => [
{{handleKindFlagsBuilder}}    ];

{{asNodeBuilder}}}
""");
    }

    private static void GenerateGenTreeGlobals()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\handlekinds.h", "HANDLE_KIND(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public const GenTreeFlags {name} = (GenTreeFlags)((int)(HandleKindIndex.{name} + 1) << HANDLE_KIND_INDEX_SHIFT);");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\gentree");

        File.WriteAllText(@"Outputs\jit\gentree\Globals.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
{{builder}}}
""");
    }

    private static void GenerateGenTreeOps()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    GT_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\gentreeopsdef");

        File.WriteAllText(@"Outputs\jit\gentreeopsdef\genTreeOps.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

public enum genTreeOps : byte
{
{{builder}}    GT_COUNT,

#if TARGET_64BIT
    // GT_CNS_NATIVELONG is the gtOper symbol for GT_CNS_LNG or GT_CNS_INT, depending on the target.
    // For the 64-bit targets we will only use GT_CNS_INT as it used to represent all the possible sizes
    GT_CNS_NATIVELONG = GT_CNS_INT,
#else
    // For the 32-bit targets we use a GT_CNS_LNG to hold a 64-bit integer constant and GT_CNS_INT for all others.
    // In the future when we retarget the JIT for x86 we should consider eliminating GT_CNS_LNG
    GT_CNS_NATIVELONG = GT_CNS_LNG,
#endif
}
""");
    }

    private static void GenerateGenTreeOpsExtensions()
    {
        var kindsBuilder = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var commutative = parts[2].AsSpan().Trim();

            var flags = parts[4].AsSpan().Trim();

            if ((flags[0] == '(') && (flags[^1] == ')'))
            {
                flags = flags[1..^1].Trim();
            }

            Debug.Assert(commutative.Equals("0", StringComparison.Ordinal) || commutative.Equals("1", StringComparison.Ordinal));

            var separator = "        ";
            var count = 0;

            foreach (var flagRange in flags.Split('|'))
            {
                var flag = flags[flagRange].Trim();

                if (flag.StartsWith("GTK_", StringComparison.Ordinal))
                {
                    _ = builder.Append(CultureInfo.InvariantCulture, $"{separator}{flag}");
                    separator = " | ";
                    count++;
                }
                else
                {
                    Debug.Assert(flag.StartsWith("DBK_"));
                }
            }
            Debug.Assert(count != 0);

            if (commutative.Equals("1", StringComparison.Ordinal))
            {
                _ = builder.Append(" | GTK_COMMUTE");
            }
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $", // GT_{name}");
        });

        var debugKindsBuilder = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (Action<StringBuilder, string, string, string, string[]>)((builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            var flags = parts[4].AsSpan().Trim();

            if ((flags[0] == '(') && (flags[^1] == ')'))
            {
                flags = flags[1..^1].Trim();
            }

            var separator = "        ";
            var count = 0;

            foreach (var flagRange in flags.Split('|'))
            {
                var flag = flags[flagRange].Trim();

                if (MemoryExtensions.StartsWith(flag, "DBK_", StringComparison.Ordinal))
                {
                    _ = builder.Append(CultureInfo.InvariantCulture, $"{separator}{flag}");
                    separator = " | ";
                    count++;
                }
                else
                {
                    Debug.Assert(MemoryExtensions.StartsWith<char>(flag, (ReadOnlySpan<char>)"GTK_"));
                }
            }

            if (count is 0)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{separator}DBK_NONE, // GT_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $", // GT_{name}");
            }
        }));

        var namesBuilder = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        \"{name}\", // GT_{name}");
        });

        var structTypesBuilder = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            var structType = parts[1].Trim();
            var flags = parts[4].AsSpan().Trim();

            if ((flags[0] == '(') && (flags[^1] == ')'))
            {
                flags = flags[1..^1].Trim();
            }

            var kind = 0;

            foreach (var flagRange in flags.Split('|'))
            {
                var flag = flags[flagRange].Trim();

                if (flag.StartsWith("GTK_", StringComparison.Ordinal))
                {
                    flag = flag[4..].Trim();

                    if (flag.Equals("SPECIAL", StringComparison.Ordinal))
                    {
                        Debug.Assert(kind == 0);
                        kind |= 0;
                    }
                    else if (flag.Equals("LEAF", StringComparison.Ordinal))
                    {
                        Debug.Assert(kind == 0);
                        kind |= 1;
                    }
                    else if (flag.Equals("UNOP", StringComparison.Ordinal))
                    {
                        Debug.Assert(kind == 0);
                        kind |= 2;
                    }
                    else if (flag.Equals("BINOP", StringComparison.Ordinal))
                    {
                        Debug.Assert(kind == 0);
                        kind |= 3;
                    }
                    else if (!flag.Equals("EXOP", StringComparison.Ordinal) &&
                             !flag.Equals("NOVALUE", StringComparison.Ordinal) &&
                             !flag.Equals("STORE", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }
                }
                else
                {
                    Debug.Assert(flag.StartsWith("DBK_"));
                }
            }

            if (kind is 2)
            {
                if (structType.Equals("GenTree", StringComparison.Ordinal) ||
                    structType.Equals("GenTreeOp", StringComparison.Ordinal))
                {
                    structType = "GenTreeUnOp";
                }
            }
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        typeof({structType}), // GT_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\gentreeopsdef");

        File.WriteAllText(@"Outputs\jit\gentreeopsdef\genTreeOpsExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class genTreeOpsExtensions
{
    private static ReadOnlySpan<GenTreeOperKind> s_kinds => [
{{kindsBuilder}}    ];

#if DEBUG
    private static ReadOnlySpan<GenTreeDebugOperKind> s_debugKinds => [
{{debugKindsBuilder}}    ];

    private static readonly string[] s_names = [
{{namesBuilder}}    ];
#endif

#if DEBUG
    private static readonly Type[] s_structTypes = [
{{structTypesBuilder}}    ];
#endif
}
""");
    }

    private static void GenerateHandleKindIndex()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\handlekinds.h", "HANDLE_KIND(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\gentree");

        File.WriteAllText(@"Outputs\jit\gentree\HandleKindIndex.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum HandleKindIndex
{
{{builder}}    COUNT,
}
""");
    }

    private static void GenerateHWIntrinsicInfo()
    {
        var isaBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        InstructionSet_{isa}, // NI_{isa}_{name}");
        });

        var nameBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        \"{name}\", // NI_{isa}_{name}");
        });

        var simdSizeBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();
            var simdSize = parts[2].AsSpan().Trim();

            if (simdSize.Equals("-1", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        unchecked((byte)({simdSize})), // NI_{isa}_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {simdSize}, // NI_{isa}_{name}");
            }
        });

        var numArgsBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();
            var numArgs = parts[3].AsSpan().Trim();

            if (numArgs.Equals("-1", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        unchecked((byte)({numArgs})), // NI_{isa}_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {numArgs}, // NI_{isa}_{name}");
            }
        });

        var insBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            var insT1 = parts[4].AsSpan().Trim();
            var insT2 = parts[5].AsSpan().Trim();
            var insT3 = parts[6].AsSpan().Trim();
            var insT4 = parts[7].AsSpan().Trim();
            var insT5 = parts[8].AsSpan().Trim();
            var insT6 = parts[9].AsSpan().Trim();
            var insT7 = parts[10].AsSpan().Trim();
            var insT8 = parts[11].AsSpan().Trim();
            var insT9 = parts[12].AsSpan().Trim();
            var insT10 = parts[13].AsSpan().Trim();

            Debug.Assert(insT1[0] == '{');
            Debug.Assert(insT10[^1] == '}');

            insT1 = insT1[1..];
            insT10 = insT10[..^1];

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {insT1}, {insT2}, {insT3}, {insT4}, {insT5}, {insT6}, {insT7}, {insT8}, {insT9}, {insT10}, // NI_{isa}_{name}");
        });

        var intCostBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            var intCost = (parts.Length is 18) ? parts[14].AsSpan().Trim() : "-1";

            if (intCost.Equals("-1", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        unchecked((byte)({intCost})), // NI_{isa}_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {intCost}, // NI_{isa}_{name}");
            }
        });

        var fltCostBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            var fltCost = (parts.Length is 18) ? parts[15].AsSpan().Trim() : "-1";

            if (fltCost.Equals("-1", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        unchecked((byte)({fltCost})), // NI_{isa}_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {fltCost}, // NI_{isa}_{name}");
            }
        });

        var categoryBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            var category = parts[(parts.Length is 18) ? 16 : 14].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {category}, // NI_{isa}_{name}");
        });

        var flagsBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            var flags = parts[(parts.Length is 18) ? 17 : 15].AsSpan().Trim();

            if ((flags[0] == '(') && (flags[^1] == ')'))
            {
                flags = flags[1..^1].Trim();
            }

            var separator = "        ";
            var count = 0;

            foreach (var flagRange in flags.Split('|'))
            {
                var flag = flags[flagRange].Trim();

                _ = builder.Append(CultureInfo.InvariantCulture, $"{separator}{flag}");
                separator = " | ";
                count++;
            }
            Debug.Assert(count is not 0);

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $", // NI_{isa}_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\hwintrinsic");

        File.WriteAllText(@"Outputs\jit\hwintrinsic\HWIntrinsicInfo.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct HWIntrinsicInfo
{
    private static ReadOnlySpan<HWIntrinsicCategory> s_categories => [
{{categoryBuilder}}
    ];

    private static ReadOnlySpan<HWIntrinsicFlag> s_flags => [
{{flagsBuilder}}
    ];

    private static ReadOnlySpan<byte> s_fltCosts => [
{{fltCostBuilder}}
    ];

    private static ReadOnlySpan<instruction> s_instructions => [
{{insBuilder}}
    ];

    private static ReadOnlySpan<CORINFO_InstructionSet> s_instructionSets => [
{{isaBuilder}}
    ];

    private static ReadOnlySpan<byte> s_intCosts => [
{{intCostBuilder}}
    ];

    private static readonly string[] s_names = [
{{nameBuilder}}
    ];

    private static ReadOnlySpan<byte> s_numArgs => [
{{numArgsBuilder}}
    ];

    private static ReadOnlySpan<byte> s_simdSizes => [
{{simdSizeBuilder}}
    ];
}
""");
    }

    private static void GenerateInlineObservation()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\inline.def", "INLINE_OBSERVATION(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var scope = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {scope}_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\inline");

        File.WriteAllText(@"Outputs\jit\inline\InlineObservation.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public enum InlineObservation
{
{{builder}}}
""");
    }

    private static void GenerateInlineObservationExtensions()
    {
        var descriptionsBuilder = ProcessMacroBasedFile(@"Inputs\inline.def", "INLINE_OBSERVATION(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var description = parts[2].AsSpan().Trim();
            var scope = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {description}, // {scope}_{name}");
        });

        var targetsBuilder = ProcessMacroBasedFile(@"Inputs\inline.def", "INLINE_OBSERVATION(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var scope = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        InlineTarget.{scope}, // {scope}_{name}");
        });

        var impactsBuilder = ProcessMacroBasedFile(@"Inputs\inline.def", "INLINE_OBSERVATION(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var impact = parts[3].AsSpan().Trim();
            var scope = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        InlineImpact.{impact}, // {scope}_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\inline");

        File.WriteAllText(@"Outputs\jit\inline\InlineObservationExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class InlineObservationExtensions
{
    private static readonly string[] s_descriptions = [
{{descriptionsBuilder}}    ];

    private static ReadOnlySpan<InlineImpact> s_impacts => [
{{impactsBuilder}}    ];

    private static ReadOnlySpan<InlineTarget> s_targets => [
{{targetsBuilder}}    ];
}
""");
    }

    private static void GenerateInstruction()
    {
        var builder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            var id = parts[0].AsSpan().Trim();

            if (inputFile.Equals("Inputs\\instrsarm64sve.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    INS_sve_{id},");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    INS_{id},");
            }
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\instr");

        File.WriteAllText(@"Outputs\jit\instr\instruction.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public enum instruction
{
{{builder}}
#if TARGET_ARM || TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    INS_lea,
#endif

    INS_none,
    INS_count = INS_none,
}
""");
    }

    private static void GenerateJitConfigValues()
    {
        const int FLAG_NONE = 0;

        const int FLAG_INTEGER = 1;
        const int FLAG_STRING = 2;
        const int FLAG_METHODSET = 3;
        const int FLAG_KIND_MASK = 0b11;

        const int FLAG_DEBUG = 4;
        const int FLAG_OPT = 8;
        const int FLAG_RELEASE = 12;
        const int FLAG_DEFINE_MASK = 0b1100;

        ReadOnlySpan<string> prefixes = [
            "CONFIG_INTEGER(", "CONFIG_STRING(", "CONFIG_METHODSET(",
            "OPT_CONFIG_INTEGER(", "OPT_CONFIG_STRING(", "OPT_CONFIG_METHODSET(",
            "RELEASE_CONFIG_INTEGER(", "RELEASE_CONFIG_STRING(", "RELEASE_CONFIG_METHODSET("
        ];

        var lastFlags = FLAG_NONE;

        var fieldBuilder = ProcessMacroBasedFile(@"Inputs\jitconfigvalues.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            var expectedPartCount = prefix.EndsWith("CONFIG_INTEGER(", StringComparison.Ordinal) ? 3 : 2;

            if (parts.Length != expectedPartCount)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var flags = GetFlagsForPrefix(prefix);
            var type = GetTypeForFlags(flags);

            EmitIfdefWhereRequired(builder, flags, lastFlags);

            var name = GetFieldName(parts[0].AsSpan().Trim());
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private {type} _{name};");

            lastFlags = flags;
        });

        EmitIfdefWhereRequired(fieldBuilder, FLAG_NONE, lastFlags);
        lastFlags = FLAG_NONE;

        var propertyBuilder = ProcessMacroBasedFile(@"Inputs\jitconfigvalues.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            var expectedPartCount = prefix.EndsWith("CONFIG_INTEGER(", StringComparison.Ordinal) ? 3 : 2;

            if (parts.Length != expectedPartCount)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var flags = GetFlagsForPrefix(prefix);
            var type = GetTypeForFlags(flags);

            EmitIfdefWhereRequired(builder, flags, lastFlags);

            var name = parts[0].AsSpan().Trim();
            var fieldName = GetFieldName(name);

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public {type} {name} => _{fieldName};");

            lastFlags = flags;
        });

        EmitIfdefWhereRequired(propertyBuilder, FLAG_NONE, lastFlags);
        lastFlags = FLAG_NONE;

        var destroyBuilder = ProcessMacroBasedFile(@"Inputs\jitconfigvalues.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            var expectedPartCount = prefix.EndsWith("CONFIG_INTEGER(", StringComparison.Ordinal) ? 3 : 2;

            if (parts.Length != expectedPartCount)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var flags = GetFlagsForPrefix(prefix);
            var kind = flags & FLAG_KIND_MASK;

            EmitIfdefWhereRequired(builder, flags, lastFlags);

            var name = GetFieldName(parts[0].AsSpan().Trim());

            if (kind == FLAG_INTEGER)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        // _{name} = unchecked((int)(0xCDCDCDCD));");
            }
            else if (kind == FLAG_STRING)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        jitHost->freeStringConfigValue(_{name});");
            }
            else
            {
                Debug.Assert(kind == FLAG_METHODSET);
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        _{name}.destroy(jitHost);");
            }
            lastFlags = flags;
        });

        EmitIfdefWhereRequired(destroyBuilder, FLAG_NONE, lastFlags);
        lastFlags = FLAG_NONE;

        var initializeBuilder = ProcessMacroBasedFile(@"Inputs\jitconfigvalues.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            var expectedPartCount = prefix.EndsWith("CONFIG_INTEGER(", StringComparison.Ordinal) ? 3 : 2;

            if (parts.Length != expectedPartCount)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var flags = GetFlagsForPrefix(prefix);
            var kind = flags & FLAG_KIND_MASK;

            EmitIfdefWhereRequired(builder, flags, lastFlags);

            var name = GetFieldName(parts[0].AsSpan().Trim());
            var key = parts[1].AsSpan().Trim();

            if (kind == FLAG_INTEGER)
            {
                var value = parts[2].AsSpan().Trim();

                if (value.Equals("0xffffffff", StringComparison.OrdinalIgnoreCase))
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        _{name} = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference({key}u8))), unchecked((int)({value})));");
                }
                else
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        _{name} = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference({key}u8))), {value});");
                }
            }
            else if (kind == FLAG_STRING)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        _{name} = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference({key}u8))));");
            }
            else
            {
                Debug.Assert(kind == FLAG_METHODSET);
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        var {name}Value = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference({key}u8))));");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        _{name} = new MethodSet({name}Value, jitHost);");
            }
            lastFlags = flags;
        });

        EmitIfdefWhereRequired(initializeBuilder, FLAG_NONE, lastFlags);
        lastFlags = FLAG_NONE;

        _ = Directory.CreateDirectory(@"Outputs\jit\jitconfig");

        File.WriteAllText(@"Outputs\jit\jitconfig\JitConfigValues.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
{{fieldBuilder}}
{{propertyBuilder}}
    public unsafe void destroy(ICorJitHost* jitHost)
    {
        if (!_isInitialized)
        {
            return;
        }

{{destroyBuilder}}        _isInitialized = false;
    }

    public unsafe void initialize(ICorJitHost* jitHost)
    {
        assert(!_isInitialized);

{{initializeBuilder}}        _isInitialized = true;
    }
}
""");

        static void EmitIfdefWhereRequired(StringBuilder builder, int flags, int lastFlags)
        {
            var define = flags & FLAG_DEFINE_MASK;
            var lastDefine = lastFlags & FLAG_DEFINE_MASK;

            if (define != lastDefine)
            {
                if (lastDefine is not FLAG_NONE and not FLAG_RELEASE)
                {
                    _ = builder.AppendLine("#endif");
                }

                if (define == FLAG_DEBUG)
                {
                    _ = builder.AppendLine("#if DEBUG");
                }
                else if (define == FLAG_OPT)
                {
                    _ = builder.AppendLine("#if OPT_CONFIG");
                }
                else
                {
                    Debug.Assert(define is FLAG_NONE or FLAG_RELEASE);
                }
            }
        }

        static string GetFieldName(ReadOnlySpan<char> name)
        {
            return $"{char.ToLower(name[0], CultureInfo.InvariantCulture)}{name[1..]}";
        }

        static int GetFlagsForPrefix(string prefix)
        {
            var kind = FLAG_NONE;

            if (prefix.EndsWith("CONFIG_INTEGER(", StringComparison.Ordinal))
            {
                kind |= FLAG_INTEGER;
            }
            else if (prefix.EndsWith("CONFIG_STRING(", StringComparison.Ordinal))
            {
                kind |= FLAG_STRING;
            }
            else if (prefix.EndsWith("CONFIG_METHODSET(", StringComparison.Ordinal))
            {
                kind |= FLAG_METHODSET;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected prefix: '{prefix}'");
            }

            if (prefix.StartsWith("CONFIG_", StringComparison.Ordinal))
            {
                kind |= FLAG_DEBUG;
            }
            else if (prefix.StartsWith("OPT_CONFIG_", StringComparison.Ordinal))
            {
                kind |= FLAG_OPT;
            }
            else if (prefix.StartsWith("RELEASE_CONFIG_", StringComparison.Ordinal))
            {
                kind |= FLAG_RELEASE;
            }
            else
            {
                throw new InvalidOperationException($"Unexpected prefix: '{prefix}'");
            }

            return kind;
        }

        static string GetTypeForFlags(int flags)
        {
            var kind = flags & FLAG_KIND_MASK;

            return (kind == FLAG_INTEGER) ? "int"
                 : (kind == FLAG_STRING) ? "unsafe byte*"
                 : (kind == FLAG_METHODSET) ? "MethodSet"
                 : throw new InvalidOperationException($"Unexpected kind: '{kind}'");
        }
    }

    private static void GenerateJitMetadata()
    {
        ReadOnlySpan<string> prefixes = [
            "JITMETADATAINFO(",
            "JITMETADATAMETRIC(",
        ];

        var builder = ProcessMacroBasedFile(@"Inputs\jitmetadatalist.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (!prefix.Equals("JITMETADATAINFO(", StringComparison.Ordinal))
            {
                return;
            }

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public const string {name} = nameof({name});");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\jitmetadata");

        File.WriteAllText(@"Outputs\jit\jitmetadata\JitMetrics.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class JitMetadata
{
{{builder}}
}
""");
    }

    private static void GenerateJitMetrics()
    {
        ReadOnlySpan<string> prefixes = [
            "JITMETADATAINFO(",
            "JITMETADATAMETRIC(",
        ];

        var fieldBuilder = ProcessMacroBasedFile(@"Inputs\jitmetadatalist.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (!prefix.Equals("JITMETADATAMETRIC(", StringComparison.Ordinal))
            {
                return;
            }

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var type = parts[1].AsSpan().Trim();

            if (type.Equals("int64_t", StringComparison.Ordinal))
            {
                type = "long";
            }
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public {type} {name};");
        });

        var reportBuilder = ProcessMacroBasedFile(@"Inputs\jitmetadatalist.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (!prefix.Equals("JITMETADATAMETRIC(", StringComparison.Ordinal))
            {
                return;
            }

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        JitMetadata.report(compiler, nameof({name}), {name});");
        });

        var nameMaxWidth = 0;

        var dumpBuilder = ProcessMacroBasedFile(@"Inputs\jitmetadatalist.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (!prefix.Equals("JITMETADATAMETRIC(", StringComparison.Ordinal))
            {
                return;
            }

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        jitprintf($\"{{nameof({name})}}{{new string(' ', NameMaxWidth + 5 - {name.Length})}}: {{{name}}}\\n\");");
            nameMaxWidth = int.Max(nameMaxWidth, name.Length);
        });

        var mergeToRootBuilder = ProcessMacroBasedFile(@"Inputs\jitmetadatalist.h", prefixes, (builder, inputFile, line, prefix, parts) => {
            if (!prefix.Equals("JITMETADATAMETRIC(", StringComparison.Ordinal))
            {
                return;
            }

            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        root.Metrics.{name} += inlineeCompiler.Metrics.{name};");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\jitmetadata");

        File.WriteAllText(@"Outputs\jit\jitmetadata\JitMetrics.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct JitMetrics
{
{{fieldBuilder}}
    /// <summary>Report all metrics and their values back to the EE.</summary>
    /// <param name="compiler">Compiler instance</param>
    public readonly void report(Compiler compiler)
    {
{{reportBuilder}}    }

#if DEBUG
    public readonly void dump()
    {
        const int NameMaxWidth = {{nameMaxWidth}};

{{dumpBuilder}}    }

    /// <summary>Merge inlinee compiler metrics to root compiler instance</summary>
    /// <param name="inlineeCompiler">inlinee compiler instance</param>
    public readonly void mergeToRoot(Compiler inlineeCompiler)
    {
        var root = inlineeCompiler.impInlineRoot;

{{mergeToRootBuilder}}    }
#endif
}
""");
    }

    private static void GenerateLogGlobals()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\loglf.h", "DEFINE_LOG_FACILITY(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var value = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public const int {name} = {value};");
        });

        _ = Directory.CreateDirectory(@"Outputs\inc\log");

        File.WriteAllText(@"Outputs\inc\log\Globals.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
{{builder}}}
""");
    }

    private static void GenerateNamedIntrinsic()
    {
        var builder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    NI_{isa}_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\namedintrinsiclist");

        File.WriteAllText(@"Outputs\jit\namedintrinsiclist\NamedIntrinsic.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.NamedIntrinsic;

namespace RyuJitSharp;

// When adding a new intrinsic that will use the GT_INTRINSIC node and can throw, make sure
// to update the "OperMayThrow" and "fgValueNumberAddExceptionSet" methods to account for that.

public enum NamedIntrinsic : ushort
{
    NI_Illegal = 0,

    NI_System_ArgumentNullException_ThrowIfNull,

    NI_System_Enum_Equals,
    NI_System_Enum_HasFlag,

    NI_System_BitConverter_DoubleToInt64Bits,
    NI_System_BitConverter_Int32BitsToSingle,
    NI_System_BitConverter_Int64BitsToDouble,
    NI_System_BitConverter_SingleToInt32Bits,

    NI_System_SpanHelpers_Memmove,

    NI_System_Half_op_Explicit,

    NI_SYSTEM_MATH_START,
    NI_System_Math_Abs,
    NI_System_Math_Acos,
    NI_System_Math_Acosh,
    NI_System_Math_Asin,
    NI_System_Math_Asinh,
    NI_System_Math_Atan,
    NI_System_Math_Atanh,
    NI_System_Math_Atan2,
    NI_System_Math_Cbrt,
    NI_System_Math_Ceiling,
    NI_System_Math_Cos,
    NI_System_Math_Cosh,
    NI_System_Math_Exp,
    NI_System_Math_Floor,
    NI_System_Math_FusedMultiplyAdd,
    NI_System_Math_ILogB,
    NI_System_Math_Log,
    NI_System_Math_Log2,
    NI_System_Math_Log10,
    NI_System_Math_Max,
    NI_System_Math_MaxMagnitude,
    NI_System_Math_MaxMagnitudeNumber,
    NI_System_Math_MaxNative,
    NI_System_Math_MaxNumber,
    NI_System_Math_MaxUnsigned,
    NI_System_Math_Min,
    NI_System_Math_MinMagnitude,
    NI_System_Math_MinMagnitudeNumber,
    NI_System_Math_MinNative,
    NI_System_Math_MinNumber,
    NI_System_Math_MinUnsigned,
    NI_System_Math_MultiplyAddEstimate,
    NI_System_Math_Pow,
    NI_System_Math_ReciprocalEstimate,
    NI_System_Math_ReciprocalSqrtEstimate,
    NI_System_Math_Round,
    NI_System_Math_Sin,
    NI_System_Math_Sinh,
    NI_System_Math_Sqrt,
    NI_System_Math_Tan,
    NI_System_Math_Tanh,
    NI_System_Math_Truncate,
    NI_SYSTEM_MATH_END,

    NI_System_Collections_Generic_Comparer_get_Default,
    NI_System_Collections_Generic_EqualityComparer_get_Default,
    NI_System_Buffers_Binary_BinaryPrimitives_ReverseEndianness,

    NI_System_GC_KeepAlive,

    NI_System_Text_UTF8Encoding_UTF8EncodingSealed_ReadUtf8,

    NI_System_Threading_Thread_get_CurrentThread,
    NI_System_Threading_Thread_get_ManagedThreadId,
    NI_System_Threading_Thread_FastPollGC,
    NI_System_Threading_Volatile_Read,
    NI_System_Threading_Volatile_Write,
    NI_System_Threading_Volatile_ReadBarrier,
    NI_System_Threading_Volatile_WriteBarrier,
    NI_System_Type_get_IsEnum,
    NI_System_Type_GetEnumUnderlyingType,
    NI_System_Type_get_IsValueType,
    NI_System_Type_get_IsPrimitive,
    NI_System_Type_get_IsByRefLike,
    NI_System_Type_get_TypeHandle,
    NI_System_Type_get_IsGenericType,
    NI_System_Type_IsAssignableFrom,
    NI_System_Type_IsAssignableTo,
    NI_System_Type_op_Equality,
    NI_System_Type_op_Inequality,
    NI_System_Type_GetTypeFromHandle,
    NI_System_Type_GetGenericTypeDefinition,
    NI_System_Array_Clone,
    NI_System_Array_GetLength,
    NI_System_Array_GetLowerBound,
    NI_System_Array_GetUpperBound,
    NI_System_Object_MemberwiseClone,
    NI_System_Object_GetType,
    NI_System_RuntimeTypeHandle_ToIntPtr,
    NI_System_RuntimeType_get_TypeHandle,
    NI_System_StubHelpers_GetStubContext,
    NI_System_StubHelpers_NextCallReturnAddress,

    NI_Array_Address,
    NI_Array_Get,
    NI_Array_Set,

    NI_System_Activator_AllocatorOf,
    NI_System_Activator_DefaultConstructorOf,

    NI_Internal_Runtime_MethodTable_Of,

    NI_System_Runtime_CompilerServices_RuntimeHelpers_CreateSpan,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_IsKnownConstant,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_IsReferenceOrContainsReferences,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_GetMethodTable,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_WriteBarrier,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallGenericContext,
    NI_System_Runtime_CompilerServices_RuntimeHelpers_SetNextCallAsyncContinuation,

    NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncSuspend,
    NI_System_Runtime_CompilerServices_AsyncHelpers_Await,
    NI_System_Runtime_CompilerServices_AsyncHelpers_AsyncCallContinuation,
    NI_System_Runtime_CompilerServices_AsyncHelpers_TailAwait,

    NI_System_Runtime_CompilerServices_StaticsHelpers_VolatileReadAsByref,

    NI_System_Runtime_InteropService_MemoryMarshal_GetArrayDataReference,

    NI_System_String_Equals,
    NI_System_String_FastAllocateString,
    NI_System_String_get_Chars,
    NI_System_String_get_Length,
    NI_System_String_op_Implicit,
    NI_System_String_StartsWith,
    NI_System_String_EndsWith,
    NI_System_Span_get_Item,
    NI_System_Span_get_Length,
    NI_System_SpanHelpers_ClearWithoutReferences,
    NI_System_SpanHelpers_Fill,
    NI_System_SpanHelpers_SequenceEqual,
    NI_System_ReadOnlySpan_get_Item,
    NI_System_ReadOnlySpan_get_Length,

    NI_System_MemoryExtensions_AsSpan,
    NI_System_MemoryExtensions_Equals,
    NI_System_MemoryExtensions_SequenceEqual,
    NI_System_MemoryExtensions_StartsWith,
    NI_System_MemoryExtensions_EndsWith,

    NI_System_Threading_Interlocked_And,
    NI_System_Threading_Interlocked_Or,
    NI_System_Threading_Interlocked_CompareExchange,
    NI_System_Threading_Interlocked_Exchange,
    NI_System_Threading_Interlocked_ExchangeAdd,
    NI_System_Threading_Interlocked_MemoryBarrier,

    NI_System_Threading_Tasks_Task_ConfigureAwait,

    // These two are special marker IDs so that we still get the inlining profitability boost
    NI_System_Numerics_Intrinsic,
    NI_System_Runtime_Intrinsics_Intrinsic,

#if FEATURE_HW_INTRINSICS
    NI_HW_INTRINSIC_START,
{{builder}}
    NI_HW_INTRINSIC_END,
#endif

#if FEATURE_SIMD
    NI_SIMD_UpperRestore,
    NI_SIMD_UpperSave,
#endif

    //
    // Special Import Intrinsics
    //

    NI_SPECIAL_IMPORT_START,

    // These are used by HWIntrinsics but are defined more generally
    // to allow dead code optimization and handle the recursion case

    NI_IsSupported_True,
    NI_IsSupported_False,
    NI_IsSupported_Dynamic,
    NI_IsSupported_Type,
    NI_Throw_PlatformNotSupportedException,
    NI_Vector_GetCount,

    NI_SPECIAL_IMPORT_END,

    //
    // System.Runtime.CompilerServices.Unsafe Intrinsics
    //

    NI_SRCS_UNSAFE_START,

    NI_SRCS_UNSAFE_Add,
    NI_SRCS_UNSAFE_AddByteOffset,
    NI_SRCS_UNSAFE_AreSame,
    NI_SRCS_UNSAFE_As,
    NI_SRCS_UNSAFE_AsPointer,
    NI_SRCS_UNSAFE_AsRef,
    NI_SRCS_UNSAFE_BitCast,
    NI_SRCS_UNSAFE_ByteOffset,
    NI_SRCS_UNSAFE_Copy,
    NI_SRCS_UNSAFE_CopyBlock,
    NI_SRCS_UNSAFE_CopyBlockUnaligned,
    NI_SRCS_UNSAFE_InitBlock,
    NI_SRCS_UNSAFE_InitBlockUnaligned,
    NI_SRCS_UNSAFE_IsAddressGreaterThan,
    NI_SRCS_UNSAFE_IsAddressGreaterThanOrEqualTo,
    NI_SRCS_UNSAFE_IsAddressLessThan,
    NI_SRCS_UNSAFE_IsAddressLessThanOrEqualTo,
    NI_SRCS_UNSAFE_IsNullRef,
    NI_SRCS_UNSAFE_NullRef,
    NI_SRCS_UNSAFE_Read,
    NI_SRCS_UNSAFE_ReadUnaligned,
    NI_SRCS_UNSAFE_SizeOf,
    NI_SRCS_UNSAFE_SkipInit,
    NI_SRCS_UNSAFE_Subtract,
    NI_SRCS_UNSAFE_SubtractByteOffset,
    NI_SRCS_UNSAFE_Unbox,
    NI_SRCS_UNSAFE_Write,
    NI_SRCS_UNSAFE_WriteUnaligned,

    NI_SRCS_UNSAFE_END,

    //
    // Primitive Intrinsics
    //

    NI_PRIMITIVE_START,

    NI_PRIMITIVE_ConvertToInteger,
    NI_PRIMITIVE_ConvertToIntegerNative,
    NI_PRIMITIVE_Crc32C,
    NI_PRIMITIVE_LeadingZeroCount,
    NI_PRIMITIVE_Log2,
    NI_PRIMITIVE_PopCount,
    NI_PRIMITIVE_RotateLeft,
    NI_PRIMITIVE_RotateRight,
    NI_PRIMITIVE_TrailingZeroCount,

    NI_PRIMITIVE_END,

    //
    // Enumeration Intrinsics
    //
    NI_System_SZArrayHelper_GetEnumerator,
    NI_System_Array_T_GetEnumerator,
    NI_System_Collections_Generic_IEnumerable_GetEnumerator,
}
""");
    }

    private static void GenerateOpcode()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\opcode.def", "OPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 10)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\inc\openum");

        File.WriteAllText(@"Outputs\inc\openum\OPCODE.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.OPCODE;

namespace RyuJitSharp;

public enum OPCODE
{
{{builder}}    CEE_COUNT,
}
""");
    }

    private static void GenerateOpcodeExtensions()
    {
        var flowKindsBuilder = ProcessMacroBasedFile(@"Inputs\opcode.def", "OPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 10)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var ctrl = parts[9].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        FLOW_{ctrl}, // {name}");
        });

        var sizesBuilder = ProcessMacroBasedFile(@"Inputs\opcode.def", "OPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 10)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var type = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {type}_size, // {name}");
        });

        var namesBuilder = ProcessMacroBasedFile(@"Inputs\opcode.def", "OPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 10)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var nameString = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {nameString}, // {name}");
        });

        var argKindsBuilder = ProcessMacroBasedFile(@"Inputs\opcode.def", "OPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 10)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var type = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {type}, // {name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\inc\openum");

        File.WriteAllText(@"Outputs\inc\openum\OPCODEExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class OPCODEExtensions
{
    private const byte InlineNone_size = 0;
    private const byte ShortInlineVar_size = 1;
    private const byte InlineVar_size = 2;
    private const byte ShortInlineI_size = 1;
    private const byte InlineI_size = 4;
    private const byte InlineI8_size = 8;
    private const byte ShortInlineR_size = 4;
    private const byte InlineR_size = 8;
    private const byte ShortInlineBrTarget_size = 1;
    private const byte InlineBrTarget_size = 4;
    private const byte InlineMethod_size = 4;
    private const byte InlineField_size = 4;
    private const byte InlineType_size = 4;
    private const byte InlineString_size = 4;
    private const byte InlineSig_size = 4;
    private const byte InlineRVA_size = 4;
    private const byte InlineTok_size = 4;
    private const byte InlineSwitch_size = 0;
    private const byte InlinePhi_size = 0;
    private const byte InlineVarTok_size = 0;

#if DEBUG
    private static ReadOnlySpan<OPCODE_FORMAT> s_argKinds => [
{{argKindsBuilder}}    ];

    private static ReadOnlySpan<OpFlow> s_flowKinds => [
{{flowKindsBuilder}}    ];

    private static readonly string[] s_names = [
{{namesBuilder}}    ];
#endif

    private static ReadOnlySpan<byte> s_sizes => [
{{sizesBuilder}}    ];
}
""");
    }

    private static void GeneratePhases()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\compphases.h", "CompPhaseNameMacro(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var enumName = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {enumName},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");

        File.WriteAllText(@"Outputs\jit\compiler\Phases.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.Phases;

namespace RyuJitSharp;

public enum Phases
{
{{builder}}    PHASE_NUMBER_OF,
}
""");
    }

    private static void GeneratePhasesExtensions()
    {
        var namesBuilder = ProcessMacroBasedFile(@"Inputs\compphases.h", "CompPhaseNameMacro(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var enumName = parts[0].AsSpan().Trim();
            var stringName = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {stringName}, // {enumName}");
        });

        var hasChildrenBuilder = ProcessMacroBasedFile(@"Inputs\compphases.h", "CompPhaseNameMacro(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var enumName = parts[0].AsSpan().Trim();
            var hasChildren = parts[2].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {hasChildren}, // {enumName}");
        });

        var parentsBuilder = ProcessMacroBasedFile(@"Inputs\compphases.h", "CompPhaseNameMacro(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var enumName = parts[0].AsSpan().Trim();
            var parent = parts[3].AsSpan().Trim();

            if (parent.Equals("-1", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        (Phases)(-1), // {enumName}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {parent}, // {enumName}");
            }
        });

        var reportsIRSizeBuilder = ProcessMacroBasedFile(@"Inputs\compphases.h", "CompPhaseNameMacro(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var enumName = parts[0].AsSpan().Trim();
            var measureIR = parts[4].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {measureIR}, // {enumName}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");

        File.WriteAllText(@"Outputs\jit\compiler\PhasesExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class PhasesExtensions
{
#if FEATURE_JIT_METHOD_PERF || DUMP_FLOWGRAPHS
    private static readonly string[] s_names = [
{{namesBuilder}}    ];
#endif

#if FEATURE_JIT_METHOD_PERF
    private static ReadOnlySpan<bool> s_hasChildren => [
{{hasChildrenBuilder}}    ];

    private static ReadOnlySpan<Phases> s_parents => [
{{parentsBuilder}}    ];

    private static ReadOnlySpan<bool> s_reportsIRSize => [
{{reportsIRSizeBuilder}}    ];
#endif
}
""");
    }

    private static void GenerateRegMask()
    {
        var builder = ProcessRegister((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 2 and not 4 and not 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();

            if (parts.Length is 2)
            {
                var realName = parts[1].AsSpan().Trim();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    SRBM_{name} = SRBM_{realName},");
            }
            else
            {
                var mask = parts[2].AsSpan().Trim();
                _ = builder.Append(CultureInfo.InvariantCulture, $"    SRBM_{name} = ");

                if (inputFile.Equals(@"Inputs\registerx86.h", StringComparison.Ordinal))
                {
                    if (mask.StartsWith("XMMMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1 << ({mask[8..^1]} + XMMBASE)");
                    }
                    else if (mask.StartsWith("KMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1 << ({mask[6..^1]} + KBASE)");
                    }
                    else
                    {
                        Debug.Assert(!mask.Contains('('));
                        _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                    }
                }
                else if (inputFile.Equals(@"Inputs\registeramd64.h", StringComparison.Ordinal))
                {
                    if (mask.StartsWith("GPRMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << {mask[8..^1]}");
                    }
                    else if (mask.StartsWith("XMMMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << ({mask[8..^1]} + XMMBASE)");
                    }
                    else if (mask.StartsWith("KMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << {mask[6..^1]}");
                    }
                    else
                    {
                        Debug.Assert(!mask.Contains('('));
                        _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                    }
                }
                else if (inputFile.Equals(@"Inputs\registerarm.h", StringComparison.Ordinal))
                {
                    if (mask.StartsWith("VFPMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << ({mask[8..^1]} + FPBASE)");
                    }
                    else
                    {
                        Debug.Assert(!mask.Contains('('));
                        _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                    }
                }
                else if (inputFile.Equals(@"Inputs\registerarm64.h", StringComparison.Ordinal))
                {
                    if (mask.StartsWith("VMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << ({mask[6..^1]} + VBASE)");
                    }
                    else if (mask.StartsWith("RMASK(", StringComparison.Ordinal) ||
                             mask.StartsWith("PMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << {mask[6..^1]}");
                    }
                    else
                    {
                        Debug.Assert(!mask.Contains('('));
                        _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                    }
                }
                else if (inputFile.Equals(@"Inputs\registerloongarch64.h", StringComparison.Ordinal) ||
                         inputFile.Equals(@"Inputs\registerriscv64.h", StringComparison.Ordinal))
                {
                    if (mask.StartsWith("FMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << ({mask[6..^1]} + FBASE)");
                    }
                    else if (mask.StartsWith("RMASK(", StringComparison.Ordinal))
                    {
                        _ = builder.Append(CultureInfo.InvariantCulture, $"1L << {mask[6..^1]}");
                    }
                    else
                    {
                        Debug.Assert(!mask.Contains('('));
                        _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                    }
                }
                else
                {
                    Debug.Assert(inputFile.Equals(@"Inputs\registerwasm.h", StringComparison.Ordinal));
                    Debug.Assert(!mask.Contains('('));
                    _ = builder.Append(CultureInfo.InvariantCulture, $"{mask}");
                }
                _ = builder.AppendLine(",");
            }
        }, includeRegAlias: true);

        _ = Directory.CreateDirectory(@"Outputs\jit\target");

        File.WriteAllText(@"Outputs\jit\target\regMask.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.regMask;

namespace RyuJitSharp;

#if TARGET_X86
public enum regMask
#else
public enum regMask : long
#endif
{
    SRBM_NONE = 0,
{{builder}}}
""");
    }

    private static void GenerateRegNumber()
    {
        var builder = ProcessRegister((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 2 and not 4 and not 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();

            if (parts.Length is 2)
            {
                var realName = parts[1].AsSpan().Trim();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    REG_{name} = REG_{realName},");
            }
            else
            {
                var rnum = parts[1].AsSpan().Trim();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    REG_{name} = {rnum},");
            }
        }, includeRegAlias: true);

        _ = Directory.CreateDirectory(@"Outputs\jit\target");

        File.WriteAllText(@"Outputs\jit\target\regNumber.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public enum regNumber : byte
{
{{builder}}    REG_COUNT,

    REG_NA = REG_COUNT,

    // everything but REG_STK (only real regs)
    ACTUAL_REG_COUNT = REG_COUNT - 1,
}
""");
    }

    private static void GenerateRegNumberExtensions()
    {
        var builder = ProcessRegister((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 4 and not 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var sname = parts[3].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {sname}, // REG_{name}");
        }, includeRegAlias: false);

        _ = Directory.CreateDirectory(@"Outputs\jit\target");

        File.WriteAllText(@"Outputs\jit\target\regNumberExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class regNumberExtensions
{
    private static readonly string[] s_names = [
{{builder}}    ];
}
""");
    }

    private static void GenerateSmOpcode()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\smopcode.def", "SMOPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\smopenum");

        File.WriteAllText(@"Outputs\jit\smopenum\SM_OPCODE.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.SM_OPCODE;

namespace RyuJitSharp;

public enum SM_OPCODE
{
{{builder}}    SM_COUNT,
}
""");
    }

    private static void GenerateSmOpcodeExtensions()
    {
        var namesBuilder = ProcessMacroBasedFile(@"Inputs\smopcode.def", "SMOPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var nameString = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {nameString}, // {name}");
        });

        var codeSeqsBuilder = ProcessMacroBasedFile(@"Inputs\smopcode.def", "SMOPDEF(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {name}, CODE_SEQUENCE_END,");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\smopenum");

        File.WriteAllText(@"Outputs\jit\smopenum\SM_OPCODEExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG || SMGEN_COMPILE
using System;

namespace RyuJitSharp;

public static partial class SM_OPCODEExtensions
{
    private static ReadOnlySpan<SM_OPCODE> s_codeSeqs => [
        // ==== Single opcode states ====
{{codeSeqsBuilder}}
        // ==== Legel prefixed opcode sequences ====
        SM_CONSTRAINED, SM_CALLVIRT, CODE_SEQUENCE_END,
        
        // ==== Interesting patterns ====
        
        // Fetching of object field
        SM_LDARG_0, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_1, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_2, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_3, SM_LDFLD, CODE_SEQUENCE_END,
        
        // Fetching of struct field
        SM_LDARGA_S, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDLOCA_S, SM_LDFLD, CODE_SEQUENCE_END,
        
        // Fetching of struct field from a normed struct
        SM_LDARGA_S_NORMED, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDLOCA_S_NORMED, SM_LDFLD, CODE_SEQUENCE_END,
        
        // stloc/ldloc --> dup
        SM_STLOC_0, SM_LDLOC_0, CODE_SEQUENCE_END,
        SM_STLOC_1, SM_LDLOC_1, CODE_SEQUENCE_END,
        SM_STLOC_2, SM_LDLOC_2, CODE_SEQUENCE_END,
        SM_STLOC_3, SM_LDLOC_3, CODE_SEQUENCE_END,
        
        // FPU operations
        SM_LDC_R4, SM_ADD, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_SUB, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_MUL, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_DIV, CODE_SEQUENCE_END,
        
        SM_LDC_R8, SM_ADD, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_SUB, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_MUL, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_DIV, CODE_SEQUENCE_END,
        
        SM_CONV_R4, SM_ADD, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_SUB, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_MUL, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_DIV, CODE_SEQUENCE_END,
        
        // {SM_CONV_R8,       SM_ADD,        CODE_SEQUENCE_END},  // Removed since it collides with ldelem.r8 in
        // Math.InternalRound
        // {SM_CONV_R8,       SM_SUB,        CODE_SEQUENCE_END},  // Just remove the SM_SUB as well.
        SM_CONV_R8, SM_MUL, CODE_SEQUENCE_END,
        SM_CONV_R8, SM_DIV, CODE_SEQUENCE_END,
        
        // Constant init constructor:
        //  L_0006: ldarg.0
        //  L_0007: ldc.r8 0
        //  L_0010: stfld float64 raytracer.Vec::x

        SM_LDARG_0, SM_LDC_I4_0, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDC_R4, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDC_R8, SM_STFLD, CODE_SEQUENCE_END,
        
        // Copy constructor:
        //  L_0006: ldarg.0
        //  L_0007: ldarg.1
        //  L_0008: ldfld float64 raytracer.Vec::x
        //  L_000d: stfld float64 raytracer.Vec::x

        SM_LDARG_0, SM_LDARG_1, SM_LDFLD, SM_STFLD, CODE_SEQUENCE_END,
        
        // Field setter:
        //  [DebuggerNonUserCode]
        //  private void CtorClosed(object target, IntPtr methodPtr)
        //  {
        //      if (target == null)
        //      {
        //          this.ThrowNullThisInDelegateToInstance();
        //      }
        //      base._target = target;
        //      base._methodPtr = methodPtr;
        //  }
        //
        //
        //  .method private hidebysig instance void CtorClosed(object target, native int methodPtr) cil managed
        //  {
        //      .custom instance void System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor()
        //      .maxstack 8
        //      L_0000: ldarg.1
        //      L_0001: brtrue.s L_0009
        //      L_0003: ldarg.0
        //      L_0004: call instance void System.MulticastDelegate::ThrowNullThisInDelegateToInstance()
        //
        //      L_0009: ldarg.0
        //      L_000a: ldarg.1
        //      L_000b: stfld object System.Delegate::_target
        //
        //      L_0010: ldarg.0
        //      L_0011: ldarg.2
        //      L_0012: stfld native int System.Delegate::_methodPtr
        //
        //      L_0017: ret
        //  }
        
        SM_LDARG_0, SM_LDARG_1, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDARG_2, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDARG_3, SM_STFLD, CODE_SEQUENCE_END,
        
        // Scale operator:
        //  L_0000: ldarg.0
        //  L_0001: dup
        //  L_0002: ldfld float64 raytracer.Vec::x
        //  L_0007: ldarg.1
        //  L_0008: mul
        //  L_0009: stfld float64 raytracer.Vec::x
        
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_ADD, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_SUB, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_MUL, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_DIV, SM_STFLD, CODE_SEQUENCE_END,
        
        // Add operator
        //  L_0000: ldarg.0
        //  L_0001: ldfld float64 raytracer.Vec::x
        //  L_0006: ldarg.1
        //  L_0007: ldfld float64 raytracer.Vec::x
        //  L_000c: add
        
        SM_LDARG_0, SM_LDFLD, SM_LDARG_1, SM_LDFLD, SM_ADD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDFLD, SM_LDARG_1, SM_LDFLD, SM_SUB, CODE_SEQUENCE_END,
        // No need for mul and div since there is no mathemetical meaning of it.
        
        SM_LDARGA_S, SM_LDFLD, SM_LDARGA_S, SM_LDFLD, SM_ADD, CODE_SEQUENCE_END,
        SM_LDARGA_S, SM_LDFLD, SM_LDARGA_S, SM_LDFLD, SM_SUB, CODE_SEQUENCE_END,
        // No need for mul and div since there is no mathemetical meaning of it.

        // The end:
        CODE_SEQUENCE_END,
    ];

    private static readonly string[] s_names = [
{{namesBuilder}}    ];
}
#endif
""");
    }

    private static void GenerateTargetGlobals()
    {
        var builder = ProcessRegister((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 2 and not 4 and not 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();

            if (parts.Length is 2)
            {
                var realName = parts[1].AsSpan().Trim();
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public static regMaskTP RBM_{name} => RBM_{realName};");
            }
            else
            {
                var rnum = parts[1].AsSpan().Trim();
                var mask = parts[2].AsSpan().Trim();

                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public static regMaskTP RBM_{name} => regMaskTP.CreateFromRegNum((regNumber)({rnum}), (regMask)({mask}));");
            }
        }, includeRegAlias: true);

        _ = Directory.CreateDirectory(@"Outputs\jit\target");

        File.WriteAllText(@"Outputs\jit\target\Globals.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static regMaskTP RBM_NONE => default;
{{builder}}
}
""");
    }

    private static void GenerateVarTypes()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    TYP_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\vartypesdef");

        File.WriteAllText(@"Outputs\jit\vartypesdef\var_types.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public enum var_types : byte
{
{{builder}}    TYP_COUNT,
}
""");
    }

    private static void GenerateVarTypesExtensions()
    {
        var varTypeNameBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var nameString = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {nameString}, // TYP_{name}");
        });

        var genActualTypesBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var actualType = parts[2].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {actualType}, // TYP_{name}");
        });

        var genTypeSizesBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var size = parts[3].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {size}, // TYP_{name}");
        });

        var emitTypeSizesBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var size = parts[4].AsSpan().Trim();

            if (int.TryParse(size, out _))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        (emitAttr)({size}), // TYP_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {size}, // TYP_{name}");
            }
        });

        var emitTypeActSzBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var actualSize = parts[5].AsSpan().Trim();

            if (int.TryParse(actualSize, out _))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        (emitAttr)({actualSize}), // TYP_{name}");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {actualSize}, // TYP_{name}");
            }
        });

        var genTypeStSzsBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var stSz = parts[6].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {stSz}, // TYP_{name}");
        });

        var genTypeAlignmentsBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var alignment = parts[7].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {alignment}, // TYP_{name}");
        });

        var varTypeRegisterBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var register = parts[8].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {register}, // TYP_{name}");
        });

        var varTypeClassificationBuilder = ProcessMacroBasedFile(@"Inputs\typelist.h", "DEF_TP(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 13)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var classification = parts[12].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {classification}, // TYP_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\vartypesdef");

        File.WriteAllText(@"Outputs\jit\vartypesdef\var_typesExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class var_typesExtensions
{
    private static ReadOnlySpan<var_types> s_actualTypes => [
{{genActualTypesBuilder}}    ];

    private static ReadOnlySpan<byte> s_alignments => [
{{genTypeAlignmentsBuilder}}    ];

    private static ReadOnlySpan<var_types_classification> s_classifications => [
{{varTypeClassificationBuilder}}    ];

    private static ReadOnlySpan<emitAttr> s_emitActualSizes => [
{{emitTypeActSzBuilder}}    ];

    private static ReadOnlySpan<emitAttr> s_emitSizes => [
{{emitTypeSizesBuilder}}    ];

#if DEBUG
    private static readonly string[] s_names = [
{{varTypeNameBuilder}}    ];
#endif

    private static ReadOnlySpan<var_types_register> s_registers => [
{{varTypeRegisterBuilder}}    ];

    private static ReadOnlySpan<byte> s_sizes => [
{{genTypeSizesBuilder}}    ];

    private static ReadOnlySpan<byte> s_stSzs => [
{{genTypeStSzsBuilder}}    ];
}
""");
    }

    private static void GenerateVNFunc()
    {
        var builder1 = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_{name},");
        });

        var builder2 = ProcessMacroBasedFile(@"Inputs\valuenumfuncs.h", "ValueNumFuncDef(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclistxarch.h\"", StringComparison.Ordinal))
            {
                var xarchBuilder = ProcessHWIntrinsicListXarch((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length != 18)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();

                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_HWI_{isa}_{name},");
                });

                _ = builder.Append(xarchBuilder);
                return;
            }
            else if (line.Equals("#include \"hwintrinsiclistarm64.h\"", StringComparison.Ordinal))
            {
                var arm64Builder = ProcessHWIntrinsicListArm64((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length != 16)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();

                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_HWI_{isa}_{name},");
                });

                _ = builder.Append(arm64Builder);
                return;
            }

            if (parts.Length != 4)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_{name},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\valuenum");

        File.WriteAllText(@"Outputs\jit\valuenum\VNFunc.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.VNFunc;

namespace RyuJitSharp;

public enum VNFunc
{
{{builder1}}    VNF_Boundary = GT_COUNT,

{{builder2}}    VNF_COUNT,
}
""");
    }

    private static void GenerateVNFuncExtensions()
    {
        var builder1 = ProcessMacroBasedFile(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var commute = parts[2].AsSpan().Trim();
            var illegalAsVNFunc = parts[3].AsSpan().Trim();
            var flags = parts[4].AsSpan().Trim();

            Debug.Assert(commute.Equals("0", StringComparison.Ordinal) || commute.Equals("1", StringComparison.Ordinal));
            Debug.Assert(illegalAsVNFunc.Equals("0", StringComparison.Ordinal) || illegalAsVNFunc.Equals("1", StringComparison.Ordinal));

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        ValueNumStore.GetOpAttribsForGenTree(GT_{name}, commute: {(commute.Equals("1", StringComparison.Ordinal) ? "true" : "false")}, illegalAsVNFunc: {(illegalAsVNFunc.Equals("1", StringComparison.Ordinal) ? "true" : "false")}, GT_{name}.Kind), // VNF_{name}");
        });

        var builder2 = ProcessMacroBasedFile(@"Inputs\valuenumfuncs.h", "ValueNumFuncDef(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclistxarch.h\"", StringComparison.Ordinal))
            {
                var xarchBuilder = ProcessHWIntrinsicListXarch((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length != 18)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();
                    var numArgs = parts[3].AsSpan().Trim();
                    var flags = parts[17].AsSpan().Trim();

                    var arity = numArgs.Equals("-1", StringComparison.Ordinal) ? "-1" : $"{numArgs} + 1";
                    var commute = flags.Contains("HW_Flag_Commutative", StringComparison.Ordinal) ? "true" : "false";

                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        ValueNumStore.GetOpAttribsForFunc(arity: {arity}, commute: {commute}, knownNonNull: false), // VNF_HWI_{isa}_{name}");
                });

                _ = builder.Append(xarchBuilder);
                return;
            }
            else if (line.Equals("#include \"hwintrinsiclistarm64.h\"", StringComparison.Ordinal))
            {
                var arm64Builder = ProcessHWIntrinsicListArm64((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length != 16)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();
                    var numArgs = parts[3].AsSpan().Trim();
                    var flags = parts[15].AsSpan().Trim();

                    var arity = numArgs.Equals("-1", StringComparison.Ordinal) ? "-1" : $"{numArgs} + 1";
                    var commute = flags.Contains("HW_Flag_Commutative", StringComparison.Ordinal) ? "true" : "false";

                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        ValueNumStore.GetOpAttribsForFunc(arity: {arity}, commute: {commute}, knownNonNull: false), // VNF_HWI_{isa}_{name}");
                });

                _ = builder.Append(arm64Builder);
                return;
            }

            if (parts.Length != 4)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            var arity = parts[1].AsSpan().Trim();
            var commute = parts[2].AsSpan().Trim();
            var knownNonNull = parts[3].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        ValueNumStore.GetOpAttribsForFunc(arity: {arity}, commute: {commute}, knownNonNull: {knownNonNull}), // VNF_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\valuenum");

        File.WriteAllText(@"Outputs\jit\valuenum\VNFuncExtensions.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class VNFuncExtensions
{
    private static readonly ValueNumStore.VNFOpAttrib[] s_attribs = [
{{builder1}}        0, // VNF_Boundary
{{builder2}}    ];
}
""");
    }

    private static void ProcessInstructionSetDesc()
    {
        var lines = File.ReadAllLines(@"Inputs\InstructionSetDesc.txt");

        var insSetBuilder = new StringBuilder();
        var insSetFlagsBuilder = new StringBuilder();
        var ensureValidBuilder = new StringBuilder();
        var insSetToStringBuilder = new StringBuilder();
        var fromR2RInsSetBuilder = new StringBuilder();
        var r2rInsSetBuilder = new StringBuilder();

        var previousTargetArchInsSet = "";
        var previousTargetArchInsSetFlags = "";
        var previousTargetArchEnsureValid = "";
        var previousTargetArchInsSetToString = "";
        var previousTargetArchFromR2RInsSet = "";
        var previousTargetArchR2RInsSet = "";

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

                var r2rValue = parts[4].Trim();

                if (!string.IsNullOrEmpty(r2rName))
                {
                    _ = AppendIfDefWhenRequired(fromR2RInsSetBuilder, ref previousTargetArchFromR2RInsSet, targetArch, allowElif: true);
                    _ = fromR2RInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"            READYTORUN_INSTRUCTION_{r2rName} => InstructionSet_{name},");

                    _ = AppendIfDefWhenRequired(r2rInsSetBuilder, ref previousTargetArchR2RInsSet, targetArch, allowElif: true);
                    _ = r2rInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"            READYTORUN_INSTRUCTION_{r2rName} = {r2rValue},");
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

    private static StringBuilder ProcessHWIntrinsicList(Action<StringBuilder, string, string, string, string[]> callback)
    {
        var xarchBuilder = ProcessHWIntrinsicListXarch(callback);
        var arm64Builder = ProcessHWIntrinsicListArm64(callback);

        return new StringBuilder($"""
#if TARGET_XARCH
{xarchBuilder}#elif TARGET_ARM64
{arm64Builder}#endif
""");
    }

    private static StringBuilder ProcessInstrs(Action<StringBuilder, string, string, string, string[]> callback) => ProcessMacroBasedFile(@"Inputs\instrs.h", "HARDWARE_INTRINSIC(", (builder, inputFile, line, prefix, parts) => {
        if (line.Equals("#include \"instrsxarch.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST0(",
                "INST1(",
                "INST2(",
                "INST3(", "INSTMUL(",
                "INST4(",
                "INST5(",
            ];

            var xarchBuilder = ProcessMacroBasedFile(@"Inputs\instrsxarch.h", prefixes, callback);
            _ = builder.Append(xarchBuilder);
        }
        else if (line.Equals("#include \"instrsarm.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST1(",
                "INST2(",
                "INST3(",
                "INST4(",
                "INST5(",
                "INST6(",
                "INST8(",
                "INST9(",
            ];

            var armBuilder = ProcessMacroBasedFile(@"Inputs\instrsarm.h", prefixes, callback);
            _ = builder.Append(armBuilder);
        }
        else if (line.Equals("#include \"instrsarm64.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST1(",
                "INST2(",
                "INST3(",
                "INST4(",
                "INST5(",
                "INST6(",
                "INST9(",
            ];

            var arm64Builder = ProcessMacroBasedFile(@"Inputs\instrsarm64.h", prefixes, callback);
            _ = builder.Append(arm64Builder);

            prefixes = [
                "INST1(",
                "INST2(",
                "INST3(",
                "INST4(",
                "INST5(",
                "INST6(",
                "INST7(",
                "INST8(",
                "INST9(",
                "INST11(",
                "INST13(",
            ];

            var arm64SveBuilder = ProcessMacroBasedFile(@"Inputs\instrsarm64sve.h", prefixes, callback);
            _ = builder.Append(arm64SveBuilder);
        }
        else if (line.Equals("#include \"instrsloongarch64.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST(",
            ];

            var loongarch64Builder = ProcessMacroBasedFile(@"Inputs\instrsloongarch64.h", prefixes, callback);
            _ = builder.Append(loongarch64Builder);
        }
        else if (line.Equals("#include \"instrsriscv64.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST(",
            ];

            var riscv64Builder = ProcessMacroBasedFile(@"Inputs\instrsriscv64.h", prefixes, callback);
            _ = builder.Append(riscv64Builder);
        }
        else if (line.Equals("#include \"instrswasm.h\"", StringComparison.Ordinal))
        {
            ReadOnlySpan<string> prefixes = [
                "INST(",
                "INST2(",
            ];

            var wasmBuilder = ProcessMacroBasedFile(@"Inputs\instrswasm.h", prefixes, callback);
            _ = builder.Append(wasmBuilder);
        }
        else
        {
            callback(builder, inputFile, line, prefix, parts);
        }
    });

    private static StringBuilder ProcessHWIntrinsicListXarch(Action<StringBuilder, string, string, string, string[]> callback)
        => ProcessMacroBasedFile(@"Inputs\hwintrinsiclistxarch.h", "HARDWARE_INTRINSIC(", callback);

    private static StringBuilder ProcessHWIntrinsicListArm64(Action<StringBuilder, string, string, string, string[]> callback) => ProcessMacroBasedFile(@"Inputs\hwintrinsiclistarm64.h", "HARDWARE_INTRINSIC(", (builder, inputFile, line, prefix, parts) => {
        if (line.Equals("#include \"hwintrinsiclistarm64sve.h\"", StringComparison.Ordinal))
        {
            var sveBuilder = ProcessMacroBasedFile(@"Inputs\hwintrinsiclistarm64sve.h", "HARDWARE_INTRINSIC(", callback);
            _ = builder.Append(sveBuilder);
        }
        else
        {
            callback(builder, inputFile, line, prefix, parts);
        }
    });

    private static StringBuilder ProcessMacroBasedFile(string inputFile, string prefix, Action<StringBuilder, string, string, string, string[]> callback)
        => ProcessMacroBasedFile(inputFile, [prefix], callback);

    private static StringBuilder ProcessMacroBasedFile(string inputFile, ReadOnlySpan<string> prefixes, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var lines = File.ReadAllLines(inputFile);
        return ProcessMacroBasedFile(inputFile, lines, prefixes, callback);
    }

    private static StringBuilder ProcessMacroBasedFile(string inputFile, ReadOnlySpan<string> lines, ReadOnlySpan<string> prefixes, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var builder = new StringBuilder();

        var skipUntilEndif = 0;
        var skipLineContinuation = false;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmedLine = line.AsSpan().Trim();

            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmedLine.StartsWith("/*", StringComparison.Ordinal))
            {
                if (trimmedLine.Contains("*/", StringComparison.Ordinal))
                {
                    continue;
                }

                var nextLineIndex = lineIndex + 1;

                while (nextLineIndex < lines.Length)
                {
                    var nextLine = lines[nextLineIndex];

                    if (nextLine.Contains("*/", StringComparison.Ordinal))
                    {
                        lineIndex = nextLineIndex;
                        break;
                    }
                    nextLineIndex++;
                }

                Debug.Assert(lineIndex == nextLineIndex);
                continue;
            }

            if (skipLineContinuation)
            {
                skipLineContinuation = trimmedLine[^1] is '\\';
                continue;
            }

            if (trimmedLine.StartsWith('#'))
            {
                trimmedLine = trimmedLine[1..].Trim();

                var directive = ExtractDirective(trimmedLine, out var bytesConsumed);

                if (string.IsNullOrEmpty(directive))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }
                trimmedLine = trimmedLine[bytesConsumed..].Trim();

                if (skipUntilEndif != 0)
                {
                    if (directive.StartsWith("#if ", StringComparison.OrdinalIgnoreCase))
                    {
                        skipUntilEndif++;
                    }
                    else if (directive.Equals("#endif", StringComparison.Ordinal))
                    {
                        skipUntilEndif--;
                    }
                    continue;
                }

                if (directive.Equals("#define ", StringComparison.Ordinal))
                {
                    if (trimmedLine[^1] == '\\')
                    {
                        skipLineContinuation = true;
                    }
                    continue;
                }
                else if (directive.Equals("#include ", StringComparison.Ordinal))
                {
                    callback(builder, inputFile, line, "", []);
                    continue;
                }
                else if (directive.Equals("#undef ", StringComparison.Ordinal))
                {
                    continue;
                }

                Debug.Assert((trimmedLine.Length == 0) || (trimmedLine[^1] != '\\'));
                Debug.Assert(skipUntilEndif == 0);

                var commentStart = trimmedLine.IndexOf("//", StringComparison.Ordinal);

                if (commentStart is not -1)
                {
                    trimmedLine = trimmedLine[..commentStart];
                }

                if (trimmedLine.Length is not 0)
                {
                    if (trimmedLine.Contains("defined(", StringComparison.Ordinal))
                    {
                        trimmedLine = trimmedLine.ToString().Replace("defined(", "(", StringComparison.Ordinal);
                    }

                    if (trimmedLine.Contains("defined (", StringComparison.Ordinal))
                    {
                        trimmedLine = trimmedLine.ToString().Replace("defined (", "(", StringComparison.Ordinal);
                    }
                }
                trimmedLine = trimmedLine.Trim();

                if (trimmedLine.Length is not 0)
                {
                    if (directive.Equals("#error ", StringComparison.Ordinal))
                    {
                        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{directive}{trimmedLine}");
                        continue;
                    }

                    var regionStart = lineIndex;
                    var regionEnd = -1;

                    if (directive.StartsWith("#if ", StringComparison.Ordinal) ||
                        directive.Equals("#elif ", StringComparison.Ordinal) ||
                        directive.Equals("#else", StringComparison.Ordinal))
                    {
                        var nextLineIndex = lineIndex + 1;
                        var depth = 1;
                        var regionContainsOnlyDefines = true;

                        while (nextLineIndex < lines.Length)
                        {
                            var nextLine = lines[nextLineIndex].AsSpan().Trim();

                            if (nextLine.StartsWith('#'))
                            {
                                nextLine = nextLine[1..].Trim();

                                var nextDirective = ExtractDirective(nextLine, out _);

                                if (nextDirective.StartsWith("#if ", StringComparison.Ordinal))
                                {
                                    depth++;
                                }
                                else
                                {
                                    var isEndif = nextDirective.Equals("#endif", StringComparison.Ordinal);

                                    if (isEndif ||
                                        nextDirective.Equals("#elif ", StringComparison.Ordinal) ||
                                        nextDirective.Equals("#else", StringComparison.Ordinal))
                                    {
                                        if (depth == 1)
                                        {
                                            regionEnd = nextLineIndex;

                                            if (!regionContainsOnlyDefines || isEndif)
                                            {
                                                break;
                                            }
                                        }
                                        else if (isEndif)
                                        {
                                            depth--;
                                        }
                                    }
                                    else if (!nextDirective.Equals("#define ", StringComparison.Ordinal) &&
                                             !nextDirective.Equals("#undef ", StringComparison.Ordinal))
                                    {
                                        regionContainsOnlyDefines = false;

                                        if (regionEnd != -1)
                                        {
                                            throw new InvalidDataException($"Invalid line format: '{line}'");
                                        }
                                    }
                                }
                            }
                            else if (nextLine.Length is not 0)
                            {
                                regionContainsOnlyDefines = false;

                                if (regionEnd != -1)
                                {
                                    throw new InvalidDataException($"Invalid line format: '{line}'");
                                }
                            }
                            nextLineIndex++;
                        }
                        Debug.Assert(regionEnd != -1);

                        if (regionContainsOnlyDefines)
                        {
                            lineIndex = regionEnd;
                            continue;
                        }
                    }

                    var separator = "";
                    var conditionBuilder = new StringBuilder();

                    foreach (var conditionRange in trimmedLine.Split(' '))
                    {
                        var condition = trimmedLine[conditionRange].Trim();

                        if ((condition[0] == '(') && (condition[^1] == ')'))
                        {
                            condition = condition[1..^1];
                        }
                        else if ((condition[0] == '!') && (condition[1] == '(') && (condition[^1] == ')'))
                        {
                            condition = condition[2..^1];

                            if (string.IsNullOrEmpty(separator))
                            {
                                directive += '!';
                            }
                            else
                            {
                                condition = $"!{condition}";
                            }
                        }

                        if (directive.Equals("#if !", StringComparison.Ordinal))
                        {
                            foreach (var prefix in prefixes)
                            {
                                if (condition.Equals(prefix.AsSpan(0, prefix.Length - 1), StringComparison.OrdinalIgnoreCase))
                                {
                                    skipUntilEndif = 1;
                                    break;
                                }
                            }

                            if (skipUntilEndif != 0)
                            {
                                break;
                            }
                        }

                        _ = conditionBuilder.Append(CultureInfo.InvariantCulture, $"{separator}{condition}");
                        separator = " ";
                    }

                    if (skipUntilEndif != 0)
                    {
                        continue;
                    }
                    

                    if (regionEnd != -1)
                    {
                        var regionBuilder = ProcessMacroBasedFile(inputFile, lines[(regionStart + 1)..regionEnd], prefixes, callback);

                        if (regionBuilder.Length >= 6 &&
                            regionBuilder[0] == '#' &&
                            regionBuilder[1] == 'e' &&
                            regionBuilder[2] == 'n' &&
                            regionBuilder[3] == 'd' &&
                            regionBuilder[4] == 'i' &&
                            regionBuilder[5] == 'f')
                        {
                            var length = 6;

                            if (regionBuilder[length] == '\r')
                            {
                                length++;
                            }
                            if (regionBuilder[length] == '\n')
                            {
                                length++;
                            }

                            _ = regionBuilder.Remove(0, length);
                            _ = builder.AppendLine("#endif");
                        }

                        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{directive}{conditionBuilder}");
                        _ = builder.Append(regionBuilder);

                        lineIndex = regionEnd - 1;
                        continue;
                    }
                    else
                    {
                        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"{directive}{conditionBuilder}");
                    }
                }
                else
                {
                    _ = builder.AppendLine(directive);
                }
                continue;
            }

            if (skipUntilEndif != 0)
            {
                continue;
            }

            foreach (var prefix in prefixes)
            {
                if (!TryExtractMacroContents(lines, ref lineIndex, line, prefix, out var parts))
                {
                    continue;
                }
                callback(builder, inputFile, line, prefix, parts);
            }
        }
        return builder;

        static string ExtractDirective(ReadOnlySpan<char> trimmedLine, out int bytesConsumed)
        {
            var directive = "";

            if (trimmedLine.StartsWith("if ", StringComparison.Ordinal))
            {
                directive = "#if ";
                bytesConsumed = 3;
            }
            else if (trimmedLine.StartsWith("ifdef ", StringComparison.Ordinal))
            {
                directive = "#if ";
                bytesConsumed = 6;
            }
            else if (trimmedLine.StartsWith("ifndef ", StringComparison.Ordinal))
            {
                directive = "#if !";
                bytesConsumed = 7;
            }
            else if (trimmedLine.StartsWith("elif ", StringComparison.Ordinal))
            {
                directive = "#elif ";
                bytesConsumed = 5;
            }
            else if (trimmedLine.StartsWith("else", StringComparison.Ordinal))
            {
                directive = "#else";
                bytesConsumed = trimmedLine.Length;
            }
            else if (trimmedLine.StartsWith("endif", StringComparison.Ordinal))
            {
                directive = "#endif";
                bytesConsumed = trimmedLine.Length;
            }
            else if (trimmedLine.StartsWith("error ", StringComparison.Ordinal))
            {
                directive = "#error ";
                bytesConsumed = 6;
            }
            else if (trimmedLine.StartsWith("define ", StringComparison.Ordinal))
            {
                directive = "#define ";
                bytesConsumed = 7;
            }
            else if (trimmedLine.StartsWith("include ", StringComparison.Ordinal))
            {
                directive = "#include ";
                bytesConsumed = 8;
            }
            else if (trimmedLine.StartsWith("undef ", StringComparison.Ordinal))
            {
                directive = "#undef ";
                bytesConsumed = 6;
            }
            else
            {
                bytesConsumed = 0;
            }
            return directive;
        }
    }

    private static StringBuilder ProcessRegister(Action<StringBuilder, string, string, string, string[]> callback, bool includeRegAlias) => ProcessMacroBasedFile(@"Inputs\register.h", "REGDEF(", (builder, inputFile, line, prefix, parts) => {
        ReadOnlySpan<string> prefixes = [
            "REGDEF(",
            "REGALIAS(",
        ];

        if (!includeRegAlias)
        {
            prefixes = prefixes[..1];
        }

        if (line.Equals("#include \"registerx86.h\"", StringComparison.Ordinal))
        {
            var x86Builder = ProcessMacroBasedFile(@"Inputs\registerx86.h", prefixes, callback);
            _ = builder.Append(x86Builder);
        }
        else if (line.Equals("#include \"registeramd64.h\"", StringComparison.Ordinal))
        {
            var amd64Builder = ProcessMacroBasedFile(@"Inputs\registeramd64.h", prefixes, callback);
            _ = builder.Append(amd64Builder);
        }
        else if (line.Equals("#include \"registerarm.h\"", StringComparison.Ordinal))
        {
            var armBuilder = ProcessMacroBasedFile(@"Inputs\registerarm.h", prefixes, callback);
            _ = builder.Append(armBuilder);
        }
        else if (line.Equals("#include \"registerarm64.h\"", StringComparison.Ordinal))
        {
            var arm64Builder = ProcessMacroBasedFile(@"Inputs\registerarm64.h", prefixes, callback);
            _ = builder.Append(arm64Builder);
        }
        else if (line.Equals("#include \"registerloongarch64.h\"", StringComparison.Ordinal))
        {
            var loongarch64Builder = ProcessMacroBasedFile(@"Inputs\registerloongarch64.h", prefixes, callback);
            _ = builder.Append(loongarch64Builder);
        }
        else if (line.Equals("#include \"registerriscv64.h\"", StringComparison.Ordinal))
        {
            var riscv64Builder = ProcessMacroBasedFile(@"Inputs\registerriscv64.h", prefixes, callback);
            _ = builder.Append(riscv64Builder);
        }
        else if (line.Equals("#include \"registerwasm.h\"", StringComparison.Ordinal))
        {
            var wasmBuilder = ProcessMacroBasedFile(@"Inputs\registerwasm.h", prefixes, callback);
            _ = builder.Append(wasmBuilder);
        }
        else
        {
            callback(builder, inputFile, line, prefix, parts);
        }
    });

    private static bool TryExtractMacroContents(ReadOnlySpan<string> lines, ref int lineIndex, string line, ReadOnlySpan<char> prefix, out string[] parts)
    {
        var macro = line.AsSpan().Trim();

        if (!macro.StartsWith(prefix, StringComparison.Ordinal))
        {
            parts = [];
            return false;
        }

        macro = macro[prefix.Length..];
        var commentStart = macro.IndexOf("//", StringComparison.Ordinal);

        if (commentStart != -1)
        {
            macro = macro[..commentStart];
        }

        var macroEnd = macro.LastIndexOf(')');
        var additionalLinesConsumed = 0;

        while (macroEnd == -1)
        {
            additionalLinesConsumed++;
            var nextLineIndex = lineIndex + additionalLinesConsumed;

            if (nextLineIndex >= lines.Length)
            {
                break;
            }

            var nextLine = lines[nextLineIndex].AsSpan().Trim();
            var nextCommentStart = nextLine.IndexOf("//", StringComparison.Ordinal);

            if (nextCommentStart != -1)
            {
                nextLine = nextLine[..nextCommentStart];
            }

            macro = string.Concat(macro, nextLine);
            macroEnd = macro.LastIndexOf(')');
        }

        if (macroEnd == -1)
        {
            throw new InvalidDataException($"Invalid line format: '{line}'");
        }

        if (additionalLinesConsumed is not 0)
        {
            lineIndex += additionalLinesConsumed;
        }

        macro = macro[..macroEnd];

        var partsBuilder = ImmutableArray.CreateBuilder<string>(initialCapacity: 32);
        var builder = null as StringBuilder;

        foreach (var partRange in macro.Split(','))
        {
            var part = macro[partRange];
            var trimmedPart = part.Trim();

            if (builder is null)
            {
                if (trimmedPart.StartsWith('"') && !trimmedPart.EndsWith('"'))
                {
                    builder = new StringBuilder();
                    _ = builder.Append(part.TrimStart());
                }
                else
                {
                    partsBuilder.Add(trimmedPart.ToString());
                }
            }
            else if (trimmedPart.EndsWith('"'))
            {
                _ = builder.Append(CultureInfo.InvariantCulture, $",{part.TrimEnd()}");
                partsBuilder.Add(builder.ToString());
                builder = null;
            }
            else
            {
                _ = builder.Append(CultureInfo.InvariantCulture, $",{part}");
            }
        }

        parts = partsBuilder.ToArray();
        return true;
    }
}

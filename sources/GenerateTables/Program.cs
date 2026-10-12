// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RyuJitSharp;

internal static class Program
{
    private static readonly (string Target, string InputFile)[] s_registerTargetHeaders = [
        ("X86", @"Inputs\registerx86.h"),
        ("AMD64", @"Inputs\registeramd64.h"),
        ("ARM", @"Inputs\registerarm.h"),
        ("ARM64", @"Inputs\registerarm64.h"),
        ("LOONGARCH64", @"Inputs\registerloongarch64.h"),
        ("RISCV64", @"Inputs\registerriscv64.h"),
        ("WASM", @"Inputs\registerwasm.h"),
    ];

    public static void Main()
    {
        var workingDirectory = Environment.CurrentDirectory;
        if (!File.Exists(Path.Combine(workingDirectory, "GenerateTables.csproj"))
            || !Directory.Exists(Path.Combine(workingDirectory, "Inputs")))
        {
            throw new InvalidOperationException(
                "GenerateTables must run from its project directory, which contains GenerateTables.csproj and Inputs.");
        }

        ValidateUniversalVarTypeEntries();

        var outputDirectory = Path.Combine(workingDirectory, "Outputs");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        GenerateApiICorJitInfoNames();
        GenerateApiICorJitInfoNamesExtensions();
        GenerateWrapICorJitInfo();

        GenerateCodeSeqSM();
        GenerateCompiler();
        GenerateEmitJumpKind();

        GenerateGenTree();
        GenerateGenTreeGlobals();
        GenerateGenTreeOps();
        GenerateGenTreeOpsExtensions();

        GenerateHandleKindIndex();
        ProcessInstructionSetDesc();
        GenerateHWIntrinsicInfo();
        GenerateTernaryLogicInfo();

        GenerateInlineObservation();
        GenerateInlineObservationExtensions();
        GenerateInstruction();
        GenerateInstructionFormats();
        GenerateInstructionOpcodes();
        GenerateLoongArchInstructionOpcodes();
        GenerateWasmInstructionOpcodes();

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
    }

    private static void ValidateUniversalVarTypeEntries()
    {
        var requiredTypes = new HashSet<string>(StringComparer.Ordinal) {
            "SIMD8",
            "SIMD12",
            "SIMD16",
            "SIMD32",
            "SIMD64",
            "SIMD_ARM64",
            "MASK",
        };
        var conditionalDepth = 0;

        foreach (var sourceLine in File.ReadLines(@"Inputs\typelist.h"))
        {
            var line = sourceLine.AsSpan().Trim();
            if (line.StartsWith("DEF_TP(", StringComparison.Ordinal))
            {
                var nameEnd = line.IndexOf(',');
                if (nameEnd is -1)
                {
                    throw new InvalidDataException($"Invalid variable type entry: '{sourceLine}'");
                }

                var name = line["DEF_TP(".Length..nameEnd].Trim().ToString();
                if (requiredTypes.Contains(name))
                {
                    if (conditionalDepth != 0)
                    {
                        throw new InvalidDataException(
                            $"Universal SIMD type '{name}' must not be guarded by a target or feature condition.");
                    }

                    _ = requiredTypes.Remove(name);
                }
            }
            else if (line.StartsWith("#if", StringComparison.Ordinal))
            {
                conditionalDepth++;
            }
            else if (line.StartsWith("#endif", StringComparison.Ordinal))
            {
                conditionalDepth--;
            }
        }

        if (requiredTypes.Count != 0)
        {
            throw new InvalidDataException(
                $"The universal variable type list is missing: {string.Join(", ", requiredTypes)}.");
        }
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

    private static void GenerateWrapICorJitInfo()
    {
        // Use the managed ABI declarations for signatures and the native API list
        // for slot order. The generated proxy uses the existing EE vtable layout.
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        var vtablePaths = new Dictionary<string, string>(StringComparer.Ordinal);
        var thunks = new StringBuilder();
        var vtableAssignments = new StringBuilder();
        var apiCount = 0;

        AddVtablePaths(@"..\Core\inc\corjit\ICorJitInfo.cs", "vtable->");
        AddVtablePaths(@"..\Core\inc\corinfo\ICorDynamicInfo.cs", "vtable->Base.");
        AddVtablePaths(@"..\Core\inc\corinfo\ICorStaticInfo.cs", "vtable->Base.Base.");

        void AddVtablePaths(string path, string prefix)
        {
            var inVtable = false;

            foreach (var line in File.ReadLines(path))
            {
                if (line.Contains("public struct Vtbl<TSelf>", StringComparison.Ordinal))
                {
                    inVtable = true;
                    continue;
                }

                if (inVtable && line.Trim() is "}")
                {
                    inVtable = false;
                    continue;
                }

                if (!inVtable)
                {
                    continue;
                }

                var match = Regex.Match(line, @"^\s*public delegate\* unmanaged\[MemberFunction\]<.*>\s+(?<name>\w+);");
                if (match.Success)
                {
                    var name = match.Groups["name"].Value;
                    vtablePaths.Add(name, $"{prefix}{name}");
                }
            }
        }

        foreach (var line in File.ReadLines(@"..\Core\inc\corjit\ICorJitInfo.cs"))
        {
            var match = Regex.Match(line, @"^    public (?!static)(?<signature>.+? (?<name>\w+)\(.*\)) => ");

            if (match.Success)
            {
                signatures.Add(match.Groups["name"].Value, match.Groups["signature"].Value);
            }
        }

        var builder = ProcessMacroBasedFile(@"Inputs\ICorJitInfo_names_generated.h", "DEF_CLR_API(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 1)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();

            if (!signatures.Remove(name, out var signature) || !vtablePaths.TryGetValue(name, out var vtablePath))
            {
                throw new InvalidDataException($"Missing or duplicate ICorJitInfo signature or vtable slot: '{name}'");
            }

            var openParen = signature.IndexOf('(', StringComparison.Ordinal);
            var parameters = signature.AsSpan(openParen + 1, signature.Length - openParen - 2);
            var arguments = new List<string>();
            var parameterDeclarations = new List<string>();

            foreach (var parameter in parameters.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var declaration = parameter.Split('=')[0].Trim();
                var nameIndex = declaration.LastIndexOf(' ', StringComparison.Ordinal);
                var parameterType = declaration[..nameIndex].Trim();
                var parameterName = declaration[(nameIndex + 1)..];
                arguments.Add(parameterName);
                parameterDeclarations.Add($"{parameterType} {parameterName}");
            }

            var returnType = signature[..(openParen - name.Length - 1)];
            var unmanagedReturnType = returnType == "bool" ? "byte" : returnType;
            var call = $"wrapHnd->{name}({string.Join(", ", arguments)})";
            var thunkParameterDeclarations = parameterDeclarations.ToArray();
            var thunkArguments = arguments.ToArray();

            if (name.Equals("notifyInstructionSetUsage", StringComparison.Ordinal))
            {
                thunkParameterDeclarations[0] = "CORINFO_InstructionSet nativeInstructionSet";
                thunkArguments[0] = "instructionSet";
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public {signature}");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        wrapComp.CLR_API_Enter(API_{name});");

            if (returnType == "void")
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {call};");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        wrapComp.CLR_API_Leave(API_{name});");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {returnType} temp = {call};");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        wrapComp.CLR_API_Leave(API_{name});");
                _ = builder.AppendLine();
                _ = builder.AppendLine("        return temp;");
            }

            _ = builder.AppendLine("    }");
            _ = builder.AppendLine();

            _ = thunks.AppendLine("    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]");
            _ = thunks.AppendLine(CultureInfo.InvariantCulture,
                $"    private static {unmanagedReturnType} Thunk_{name}(ICorJitInfo* self{(thunkParameterDeclarations.Length is 0 ? "" : $", {string.Join(", ", thunkParameterDeclarations)}")})");
            _ = thunks.AppendLine("    {");
            _ = thunks.AppendLine("        try");
            _ = thunks.AppendLine("        {");

            if (name.Equals("notifyInstructionSetUsage", StringComparison.Ordinal))
            {
                _ = thunks.AppendLine("            if (!Globals.TryGetManagedInstructionSet((int)nativeInstructionSet, out var instructionSet))");
                _ = thunks.AppendLine("            {");
                _ = thunks.AppendLine("                throw new PlatformNotSupportedException($\"Native instruction-set ordinal '{(int)nativeInstructionSet}' is unavailable for this target.\");");
                _ = thunks.AppendLine("            }");
                _ = thunks.AppendLine();
            }

            if (returnType == "void")
            {
                _ = thunks.AppendLine(CultureInfo.InvariantCulture,
                    $"            GetWrapper(self).{name}({string.Join(", ", thunkArguments)});");
            }
            else if (returnType == "bool")
            {
                _ = thunks.AppendLine(CultureInfo.InvariantCulture,
                    $"            return GetWrapper(self).{name}({string.Join(", ", thunkArguments)}) ? (byte)1 : (byte)0;");
            }
            else
            {
                _ = thunks.AppendLine(CultureInfo.InvariantCulture,
                    $"            return GetWrapper(self).{name}({string.Join(", ", thunkArguments)});");
            }

            _ = thunks.AppendLine("        }");
            _ = thunks.AppendLine("        catch (Exception exception)");
            _ = thunks.AppendLine("        {");
            _ = thunks.AppendLine("            FailFastCallback(exception);");
            _ = thunks.AppendLine("            throw;");
            _ = thunks.AppendLine("        }");
            _ = thunks.AppendLine("    }");
            _ = thunks.AppendLine();

            _ = vtableAssignments.AppendLine(CultureInfo.InvariantCulture,
                $"        {vtablePath} = &Thunk_{name};");
            apiCount++;
        });

        if (signatures.Count != 0)
        {
            throw new InvalidDataException($"ICorJitInfo APIs missing timing indices: {string.Join(", ", signatures.Keys)}");
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\compiler");
        File.WriteAllText(@"Outputs\jit\compiler\WrapICorJitInfo.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
using System.Runtime.CompilerServices;
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp;

public sealed unsafe partial class WrapICorJitInfo
{
{{builder}}}
#endif
""");

        File.WriteAllText(@"Outputs\jit\compiler\WrapICorJitInfo.Vtbl.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp;

public sealed unsafe partial class WrapICorJitInfo
{
    private static readonly ICorJitInfo.Vtbl<ICorJitInfo>* s_vtbl = CreateVtbl();

    private static ICorJitInfo.Vtbl<ICorJitInfo>* CreateVtbl()
    {
        var vtable = (ICorJitInfo.Vtbl<ICorJitInfo>*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(WrapICorJitInfo), sizeof(ICorJitInfo.Vtbl<ICorJitInfo>));

        if (sizeof(ICorJitInfo.Vtbl<ICorJitInfo>) != {{apiCount}} * sizeof(nint))
        {
            throw new InvalidOperationException("ICorJitInfo vtable layout does not match the generated API order.");
        }

{{vtableAssignments}}        return vtable;
    }

    private static WrapICorJitInfo GetWrapper(ICorJitInfo* self)
    {
        var proxy = (ProxyContext*)self;
        var handle = GCHandle.FromIntPtr(proxy->WrapperHandle);

        return handle.Target is WrapICorJitInfo wrapper
            ? wrapper
            : throw new InvalidOperationException("ICorJitInfo proxy context is no longer valid.");
    }

    [DoesNotReturn]
    private static void FailFastCallback(Exception exception)
    {
        // The native JIT cannot continue safely with a fabricated EE result.
        Environment.FailFast("An exception escaped an ICorJitInfo timing proxy callback.", exception);
        throw new InvalidOperationException("Environment.FailFast returned unexpectedly.", exception);
    }

{{thunks}}}
#endif
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
#if TARGET_XARCH
    private void compInitVarTypeCalleeTrashRegMasks()
    {
{{compInitVarTypeCalleeTrashRegMasksBuilder}}    }
#endif

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

        var asNodeBuilder = ProcessMacroBasedFileUnion(@"Inputs\gtstructs.h", prefixes, (builder, inputFile, line, prefix, parts) => {
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
        var builder = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
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
}
""");
    }

    private static StringBuilder ProcessGenTreeOpDefinitions(Action<StringBuilder, string, string, string, string[]> callback)
    {
        var definitions = new Dictionary<string, string[]>(StringComparer.Ordinal);
        return ProcessMacroBasedFileUnion(@"Inputs\gtlist.h", "GTNODE(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            if (definitions.TryGetValue(name, out var previous))
            {
                for (var index = 0; index < parts.Length; index++)
                {
                    var currentPart = parts[index].Trim();
                    var previousPart = previous[index].Trim();
                    if (currentPart.Equals(previousPart, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var isMulLongStructVariant = (name is "MUL_LONG") && (index is 1) &&
                        ((currentPart is "GenTreeMultiRegOp" && previousPart is "GenTreeOp") ||
                         (currentPart is "GenTreeOp" && previousPart is "GenTreeMultiRegOp"));
                    if (!isMulLongStructVariant)
                    {
                        throw new InvalidDataException($"Conflicting GTNODE definitions for '{name}'.");
                    }
                }
                return;
            }

            definitions.Add(name, parts);
            callback(builder, inputFile, line, prefix, parts);
        });
    }

    private static void GenerateGenTreeOpsExtensions()
    {
        var kindsBuilder = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
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

        var debugKindsBuilder = ProcessGenTreeOpDefinitions((Action<StringBuilder, string, string, string, string[]>)((builder, inputFile, line, prefix, parts) => {
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

        var namesBuilder = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        \"{name}\", // GT_{name}");
        });

        var structTypesBuilder = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].Trim();
            var structType = parts[1].Trim();
            var flags = parts[4].AsSpan().Trim();

            // Native gtlist.h names the allocation base for these nodes, but the
            // managed constructor/type contract requires gtNewAtomicNode's concrete type.
            if ((name is "LOCKADD" or "XAND" or "XORR" or "XADD" or "XCHG") &&
                !structType.Equals("GenTreeIndir", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"GT_{name} must use the concrete GenTreeIndir type.");
            }

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
            var structTypeExpression = name.Equals("MUL_LONG", StringComparison.Ordinal)
                ? "Configuration.TARGET_ARM64 ? typeof(GenTreeOp) : typeof(GenTreeMultiRegOp)"
                : $"typeof({structType})";
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {structTypeExpression}, // GT_{name}");
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
#endif

#if DEBUG || CALL_ARG_STATS || COUNT_BASIC_BLOCKS || EMITTER_STATS || MEASURE_MEM_ALLOC || NODEBASH_STATS || MEASURE_NODE_SIZE || COUNT_AST_OPERS || DUMP_FLOWGRAPHS
    private static readonly string[] s_names = [
{{namesBuilder}}    ];
#endif

#if DEBUG || MEASURE_NODE_SIZE
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

    private sealed record HWIntrinsicTableData(
        StringBuilder Categories,
        StringBuilder Flags,
        StringBuilder FloatCosts,
        StringBuilder Instructions,
        StringBuilder InstructionSets,
        StringBuilder IntegerCosts,
        StringBuilder NameData,
        StringBuilder NameRanges,
        StringBuilder ArgumentCounts,
        StringBuilder SimdSizes);

    private static void GenerateHWIntrinsicInfo()
    {
        var isaBuilder = ProcessHWIntrinsicList((builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        InstructionSet_{isa}, // NI_{isa}_{name}");
        });
        var isaRangeBoundsBuilder = GenerateIsaRangeBounds(isaBuilder.ToString());
        var instructionOrdinals = GenerateTargetInstructionOrdinals();
        var xarchData = GenerateHWIntrinsicTableData("XARCH", instructionOrdinals["X64"]);
        var arm64Data = GenerateHWIntrinsicTableData("ARM64", instructionOrdinals["ARM64"]);
        var wasmData = GenerateHWIntrinsicTableData("WASM", instructionOrdinals["WASM"]);
        var x86Instructions = GenerateTargetHWIntrinsicInstructionOrdinals("XARCH", instructionOrdinals["X86"]);

        var arraysBuilder = new StringBuilder();
        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicCategory", "s_XarchCategories", xarchData.Categories);
        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicFlag", "s_XarchFlags", xarchData.Flags);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_XarchFltCosts", xarchData.FloatCosts);
        AppendHWIntrinsicArray(arraysBuilder, "ushort", "s_X64Instructions", xarchData.Instructions);
        AppendHWIntrinsicArray(arraysBuilder, "ManagedInstructionSet", "s_XarchInstructionSets", xarchData.InstructionSets);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_XarchIntCosts", xarchData.IntegerCosts);
        AppendHWIntrinsicArray(arraysBuilder, "char", "s_XarchNameData", xarchData.NameData);
        AppendHWIntrinsicArray(arraysBuilder, "int", "s_XarchNameRanges", xarchData.NameRanges);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_XarchNumArgs", xarchData.ArgumentCounts);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_XarchSimdSizes", xarchData.SimdSizes);

        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicCategory", "s_Arm64Categories", arm64Data.Categories);
        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicFlag", "s_Arm64Flags", arm64Data.Flags);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_Arm64FltCosts", arm64Data.FloatCosts);
        AppendHWIntrinsicArray(arraysBuilder, "ushort", "s_Arm64Instructions", arm64Data.Instructions);
        AppendHWIntrinsicArray(arraysBuilder, "ManagedInstructionSet", "s_Arm64InstructionSets", arm64Data.InstructionSets);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_Arm64IntCosts", arm64Data.IntegerCosts);
        AppendHWIntrinsicArray(arraysBuilder, "char", "s_Arm64NameData", arm64Data.NameData);
        AppendHWIntrinsicArray(arraysBuilder, "int", "s_Arm64NameRanges", arm64Data.NameRanges);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_Arm64NumArgs", arm64Data.ArgumentCounts);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_Arm64SimdSizes", arm64Data.SimdSizes);

        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicCategory", "s_WasmCategories", wasmData.Categories);
        AppendHWIntrinsicArray(arraysBuilder, "HWIntrinsicFlag", "s_WasmFlags", wasmData.Flags);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_WasmFltCosts", wasmData.FloatCosts);
        AppendHWIntrinsicArray(arraysBuilder, "ushort", "s_WasmInstructions", wasmData.Instructions);
        AppendHWIntrinsicArray(arraysBuilder, "ManagedInstructionSet", "s_WasmInstructionSets", wasmData.InstructionSets);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_WasmIntCosts", wasmData.IntegerCosts);
        AppendHWIntrinsicArray(arraysBuilder, "char", "s_WasmNameData", wasmData.NameData);
        AppendHWIntrinsicArray(arraysBuilder, "int", "s_WasmNameRanges", wasmData.NameRanges);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_WasmNumArgs", wasmData.ArgumentCounts);
        AppendHWIntrinsicArray(arraysBuilder, "byte", "s_WasmSimdSizes", wasmData.SimdSizes);
        AppendHWIntrinsicArray(arraysBuilder, "ushort", "s_X86Instructions", x86Instructions);

        var propertiesBuilder = new StringBuilder();
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "HWIntrinsicCategory", "s_categories",
            "s_XarchCategories", "s_Arm64Categories", "s_WasmCategories");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "HWIntrinsicFlag", "s_flags",
            "s_XarchFlags", "s_Arm64Flags", "s_WasmFlags");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "byte", "s_fltCosts",
            "s_XarchFltCosts", "s_Arm64FltCosts", "s_WasmFltCosts");
        AppendTargetHWIntrinsicInstructionProperty(propertiesBuilder);
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "ManagedInstructionSet", "s_instructionSets",
            "s_XarchInstructionSets", "s_Arm64InstructionSets", "s_WasmInstructionSets");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "byte", "s_intCosts",
            "s_XarchIntCosts", "s_Arm64IntCosts", "s_WasmIntCosts");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "char", "s_nameData",
            "s_XarchNameData", "s_Arm64NameData", "s_WasmNameData");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "int", "s_nameRanges",
            "s_XarchNameRanges", "s_Arm64NameRanges", "s_WasmNameRanges");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "byte", "s_numArgs",
            "s_XarchNumArgs", "s_Arm64NumArgs", "s_WasmNumArgs");
        AppendTargetHWIntrinsicProperty(propertiesBuilder, "byte", "s_simdSizes",
            "s_XarchSimdSizes", "s_Arm64SimdSizes", "s_WasmSimdSizes");

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
{{arraysBuilder}}
{{isaRangeBoundsBuilder}}
{{propertiesBuilder}}
}
""");

        GenerateHWIntrinsicIndexMaps();
    }

    private static HWIntrinsicTableData GenerateHWIntrinsicTableData(
        string target, Dictionary<string, ushort> instructionOrdinals)
    {
        var categories = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var category = parts[(parts.Length is 18) ? 16 : 14].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {category}, // NI_{isa}_{intrinsicName}");
        });

        var flags = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var rowFlags = parts[(parts.Length is 18) ? 17 : 15].AsSpan().Trim();

            if ((rowFlags[0] == '(') && (rowFlags[^1] == ')'))
            {
                rowFlags = rowFlags[1..^1].Trim();
            }

            var separator = "        ";
            var count = 0;

            foreach (var flagRange in rowFlags.Split('|'))
            {
                var flag = rowFlags[flagRange].Trim();

                if ((target is not "XARCH") &&
                    (flag.Equals("HW_Flag_AvxOnlyCompatible", StringComparison.Ordinal) ||
                     flag.Equals("HW_Flag_NoContainment", StringComparison.Ordinal)))
                {
                    continue;
                }

                _ = builder.Append(CultureInfo.InvariantCulture, $"{separator}{flag}");
                separator = " | ";
                count++;
            }

            if (count is 0)
            {
                _ = builder.Append("        HW_Flag_NoFlag");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $", // NI_{isa}_{intrinsicName}");
        });

        var floatCosts = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var cost = (parts.Length is 18) ? parts[15].AsSpan().Trim() : "-1";

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {FormatHardwareIntrinsicByte(cost)}, // NI_{isa}_{intrinsicName}");
        });

        var instructions = GenerateTargetHWIntrinsicInstructionOrdinals(target, instructionOrdinals);

        var instructionSets = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        InstructionSet_{isa}, // NI_{isa}_{intrinsicName}");
        });

        var integerCosts = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var cost = (parts.Length is 18) ? parts[14].AsSpan().Trim() : "-1";

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {FormatHardwareIntrinsicByte(cost)}, // NI_{isa}_{intrinsicName}");
        });

        var nameData = new StringBuilder();
        var nameOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var nameDataLength = 0;
        var nameRanges = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var nameValue = intrinsicName.ToString();

            if (!nameOffsets.TryGetValue(nameValue, out var characterIndex))
            {
                characterIndex = nameDataLength;
                nameOffsets.Add(nameValue, characterIndex);

                foreach (var character in intrinsicName)
                {
                    AppendCharacterLiteral(nameData, character);
                    _ = nameData.Append(", ");
                }

                _ = nameData.Append("'\\0',");
                _ = nameData.AppendLine(CultureInfo.InvariantCulture, $" // NI_{isa}_{intrinsicName}");
                nameDataLength += intrinsicName.Length + 1;
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {characterIndex}, {intrinsicName.Length}, // NI_{isa}_{intrinsicName}");
        });

        var argumentCounts = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var count = parts[3].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {FormatHardwareIntrinsicByte(count)}, // NI_{isa}_{intrinsicName}");
        });

        var simdSizes = ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var size = parts[2].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {FormatHardwareIntrinsicByte(size)}, // NI_{isa}_{intrinsicName}");
        });

        return new HWIntrinsicTableData(
            categories,
            flags,
            floatCosts,
            instructions,
            instructionSets,
            integerCosts,
            nameData,
            nameRanges,
            argumentCounts,
            simdSizes);
    }

    private static StringBuilder ProcessUniqueTargetHardwareIntrinsicList(
        string target, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        return ProcessTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var identity = GetHardwareIntrinsicIdentity(parts);
            if (!seen.Add(identity))
            {
                throw new InvalidDataException($"Duplicate hardware intrinsic table row '{identity}' for {target}.");
            }

            callback(builder, inputFile, line, prefix, parts);
        });
    }

    private static StringBuilder GenerateTargetHWIntrinsicInstructionOrdinals(
        string target, Dictionary<string, ushort> instructionOrdinals)
    {
        return ProcessUniqueTargetHardwareIntrinsicList(target, (builder, inputFile, line, prefix, parts) => {
            var isa = parts[0].AsSpan().Trim();
            var intrinsicName = parts[1].AsSpan().Trim();
            var instructionNames = new string[10];
            var ordinals = new ushort[10];

            for (var instructionIndex = 0; instructionIndex < instructionNames.Length; instructionIndex++)
            {
                instructionNames[instructionIndex] = parts[instructionIndex + 4].Trim();
            }

            var hasOpeningBrace = instructionNames[0][0] is '{';
            var hasClosingBrace = instructionNames[^1][^1] is '}';

            if (hasOpeningBrace != hasClosingBrace)
            {
                throw new InvalidDataException($"Invalid instruction mapping format: '{line}'");
            }

            if (hasOpeningBrace)
            {
                instructionNames[0] = instructionNames[0][1..];
                instructionNames[^1] = instructionNames[^1][..^1];
            }

            for (var instructionIndex = 0; instructionIndex < instructionNames.Length; instructionIndex++)
            {
                if (!instructionOrdinals.TryGetValue(instructionNames[instructionIndex], out ordinals[instructionIndex]))
                {
                    throw new InvalidDataException(
                        $"Instruction '{instructionNames[instructionIndex]}' for NI_{isa}_{intrinsicName} is not present in the {target} instruction enum.");
                }
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        {string.Join(", ", ordinals)}, // NI_{isa}_{intrinsicName}");
        });
    }

    private static Dictionary<string, Dictionary<string, ushort>> GenerateTargetInstructionOrdinals()
    {
        var instructionBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            var instructionName = parts[0].AsSpan().Trim();

            if (inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    INS_sve_{instructionName},");
            }
            else
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    INS_{instructionName},");
            }
        }).ToString();

        var profiles = new (string Name, string[] DefinedSymbols)[]
        {
            ("X86", ["TARGET_XARCH", "TARGET_X86", "TARGET_32BIT", "FEATURE_SIMD"]),
            ("X64", ["TARGET_XARCH", "TARGET_AMD64", "TARGET_64BIT", "FEATURE_SIMD"]),
            ("ARM", ["TARGET_ARM", "TARGET_ARMARCH", "TARGET_32BIT"]),
            ("ARM64", ["TARGET_ARM64", "TARGET_ARMARCH", "TARGET_64BIT", "FEATURE_SIMD"]),
            ("LOONGARCH64", ["TARGET_LOONGARCH64", "TARGET_64BIT"]),
            ("RISCV64", ["TARGET_RISCV64", "TARGET_64BIT"]),
            ("WASM", ["TARGET_WASM", "TARGET_WASM32", "TARGET_32BIT", "FEATURE_SIMD"]),
        };
        var result = new Dictionary<string, Dictionary<string, ushort>>(StringComparer.Ordinal);

        foreach (var (name, definedSymbols) in profiles)
        {
            var activeSource = GetActivePreprocessorLines(instructionBuilder, new HashSet<string>(definedSymbols, StringComparer.Ordinal));
            var ordinals = new Dictionary<string, ushort>(StringComparer.Ordinal);
            var ordinal = 0;

            foreach (var line in activeSource.Split('\n'))
            {
                var match = Regex.Match(line, @"^\s*(INS_[A-Za-z0-9_]+)\s*,", RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    continue;
                }

                AddOrdinal(match.Groups[1].Value);
            }

            if (name is "ARM" or "ARM64" or "LOONGARCH64" or "RISCV64")
            {
                AddOrdinal("INS_lea");
            }

            AddOrdinal("INS_none");
            result.Add(name, ordinals);

            void AddOrdinal(string instructionName)
            {
                if (!ordinals.TryAdd(instructionName, checked((ushort)ordinal)))
                {
                    throw new InvalidDataException($"Duplicate instruction '{instructionName}' for {name}.");
                }

                ordinal++;
            }
        }

        return result;
    }

    private static void GenerateInstructionOrdinalMaps(
        List<string> instructionNames, Dictionary<string, Dictionary<string, ushort>> targetOrdinals)
    {
        var targetNames = new (string ProfileName, string FieldName)[]
        {
            ("X86", "x86"),
            ("X64", "x64"),
            ("ARM", "arm"),
            ("ARM64", "arm64"),
            ("LOONGARCH64", "loongarch64"),
            ("RISCV64", "riscv64"),
            ("WASM", "wasm"),
        };
        var builder = new StringBuilder();

        _ = builder.AppendLine("""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

internal static class InstructionOrdinalMaps
{
    // Ordinal tables use 65535 for instruction identities unavailable on a target.
""");

        foreach (var (profileName, fieldName) in targetNames)
        {
            var ordinals = targetOrdinals[profileName];
            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<ushort> s_{fieldName}Ordinals => [");

            for (var instructionIndex = 0; instructionIndex < instructionNames.Count; instructionIndex++)
            {
                if ((instructionIndex % 12) is 0)
                {
                    _ = builder.Append("        ");
                }

                var instructionName = instructionNames[instructionIndex];
                var value = ordinals.TryGetValue(instructionName, out var ordinal)
                    ? ordinal.ToString(CultureInfo.InvariantCulture)
                    : "65535";
                _ = builder.Append(value);

                var isLastInstruction = instructionIndex == instructionNames.Count - 1;

                if (!isLastInstruction)
                {
                    _ = builder.Append(',');
                }

                if ((instructionIndex % 12) is 11 || instructionIndex == instructionNames.Count - 1)
                {
                    _ = builder.AppendLine();
                }
                else
                {
                    _ = builder.Append(' ');
                }
            }

            _ = builder.AppendLine("    ];");
            _ = builder.AppendLine();

            var nativeInstructionNames = new string[ordinals["INS_none"] + 1];
            foreach (var (instructionName, ordinal) in ordinals)
            {
                nativeInstructionNames[ordinal] = instructionName;
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<instruction> s_{fieldName}InstructionsByNativeOrdinal => [");

            foreach (var instructionName in nativeInstructionNames)
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {instructionName},");
            }

            _ = builder.AppendLine("    ];");
            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("""
    private static ReadOnlySpan<ushort> GetCurrentTargetOrdinals()
    {
        if (Configuration.TARGET_XARCH)
        {
            return Configuration.TARGET_64BIT ? s_x64Ordinals : s_x86Ordinals;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_arm64Ordinals;
        }

        if (Configuration.TARGET_ARM)
        {
            return s_armOrdinals;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return s_loongarch64Ordinals;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return s_riscv64Ordinals;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_wasmOrdinals;
        }

        throw new PlatformNotSupportedException("Instruction ordinals are unsupported on this target.");
    }

    private static ReadOnlySpan<instruction> GetCurrentTargetInstructionsByNativeOrdinal()
    {
        if (Configuration.TARGET_XARCH)
        {
            return Configuration.TARGET_64BIT ? s_x64InstructionsByNativeOrdinal : s_x86InstructionsByNativeOrdinal;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_arm64InstructionsByNativeOrdinal;
        }

        if (Configuration.TARGET_ARM)
        {
            return s_armInstructionsByNativeOrdinal;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            return s_loongarch64InstructionsByNativeOrdinal;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return s_riscv64InstructionsByNativeOrdinal;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_wasmInstructionsByNativeOrdinal;
        }

        throw new PlatformNotSupportedException("Instruction ordinals are unsupported on this target.");
    }

    internal static bool TryGetNativeInstructionOrdinal(instruction ins, out int ordinal)
    {
        var ordinals = GetCurrentTargetOrdinals();
        var instructionIndex = (int)ins;

        if (instructionIndex < 0 || instructionIndex >= ordinals.Length)
        {
            ordinal = 0;
            return false;
        }

        var nativeOrdinal = Globals.GetRvaSpanEntry(ordinals, instructionIndex);
        if (nativeOrdinal is ushort.MaxValue)
        {
            ordinal = 0;
            return false;
        }

        ordinal = nativeOrdinal;
        return true;
    }

    internal static int GetNativeInstructionOrdinal(instruction ins)
    {
        if (!TryGetNativeInstructionOrdinal(ins, out var ordinal))
        {
            throw new PlatformNotSupportedException($"Instruction '{ins}' is unsupported on this target.");
        }

        return ordinal;
    }

    internal static bool IsNativeInstructionOrdinalInRange(instruction ins, instruction first, instruction last)
    {
        var instructionOrdinal = GetNativeInstructionOrdinal(ins);
        return instructionOrdinal >= GetNativeInstructionOrdinal(first) &&
               instructionOrdinal <= GetNativeInstructionOrdinal(last);
    }

    internal static bool IsNativeInstructionOrdinalLessThan(instruction ins, instruction upperBound)
    {
        return GetNativeInstructionOrdinal(ins) < GetNativeInstructionOrdinal(upperBound);
    }

    internal static bool IsNativeInstructionOrdinalLessThanOrEqualTo(instruction ins, instruction upperBound)
    {
        return GetNativeInstructionOrdinal(ins) <= GetNativeInstructionOrdinal(upperBound);
    }

    internal static int GetNativeInstructionCount()
    {
        return GetNativeInstructionOrdinal(INS_none);
    }

    internal static instruction GetInstructionFromNativeOrdinal(int ordinal)
    {
        var instructions = GetCurrentTargetInstructionsByNativeOrdinal();

        if (ordinal < 0 || ordinal >= instructions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        return Globals.GetRvaSpanEntry(instructions, ordinal);
    }
}
""");

        File.WriteAllText(@"Outputs\jit\instr\instruction.Ordinals.generated.cs", builder.ToString());
    }

    private static string FormatHardwareIntrinsicByte(ReadOnlySpan<char> value) =>
        value.Equals("-1", StringComparison.Ordinal) ? "unchecked((byte)(-1))" : value.ToString();

    private static void AppendHWIntrinsicArray(
        StringBuilder builder, string elementType, string name, StringBuilder values)
    {
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<{elementType}> {name} => [");
        _ = builder.Append(values);
        _ = builder.AppendLine("    ];");
        _ = builder.AppendLine();
    }

    private static void AppendTargetHWIntrinsicProperty(
        StringBuilder builder, string elementType, string propertyName,
        string xarchTable, string arm64Table, string wasmTable)
    {
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<{elementType}> {propertyName}");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        get");
        _ = builder.AppendLine("        {");
        _ = builder.AppendLine("            if (!Configuration.FEATURE_HW_INTRINSICS)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                throw new PlatformNotSupportedException(\"Hardware-intrinsic metadata is unsupported on this target.\");");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_XARCH)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                return {xarchTable};");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_ARM64)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                return {arm64Table};");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_WASM)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                return {wasmTable};");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            throw new PlatformNotSupportedException(\"Hardware-intrinsic tables are unavailable for this target.\");");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
    }

    private static void AppendTargetHWIntrinsicInstructionProperty(StringBuilder builder)
    {
        _ = builder.AppendLine("    private static ReadOnlySpan<ushort> s_instructions");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        get");
        _ = builder.AppendLine("        {");
        _ = builder.AppendLine("            if (!Configuration.FEATURE_HW_INTRINSICS)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                throw new PlatformNotSupportedException(\"Hardware-intrinsic metadata is unsupported on this target.\");");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_XARCH)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                return Configuration.TARGET_64BIT ? s_X64Instructions : s_X86Instructions;");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_ARM64)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                return s_Arm64Instructions;");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            if (Configuration.TARGET_WASM)");
        _ = builder.AppendLine("            {");
        _ = builder.AppendLine("                return s_WasmInstructions;");
        _ = builder.AppendLine("            }");
        _ = builder.AppendLine();
        _ = builder.AppendLine("            throw new PlatformNotSupportedException(\"Hardware-intrinsic tables are unavailable for this target.\");");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");
        _ = builder.AppendLine();
    }

    private static void GenerateHWIntrinsicIndexMaps()
    {
        var unionNames = GetHardwareIntrinsicNames();
        var xarchNames = GetHardwareIntrinsicNamesForTarget("XARCH");
        var arm64Names = GetHardwareIntrinsicNamesForTarget("ARM64");
        var wasmNames = GetHardwareIntrinsicNamesForTarget("WASM");
        var builder = new StringBuilder();

        AppendHWIntrinsicIndexMaps(builder, "Xarch", unionNames, xarchNames);
        AppendHWIntrinsicIndexMaps(builder, "Arm64", unionNames, arm64Names);
        AppendHWIntrinsicIndexMaps(builder, "Wasm", unionNames, wasmNames);

        File.WriteAllText(@"Outputs\jit\hwintrinsic\HWIntrinsicInfo.IndexMaps.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct HWIntrinsicInfo
{
{{builder}}}
""");
    }

    private static void AppendHWIntrinsicIndexMaps(
        StringBuilder builder, string target, List<string> unionNames, List<string> targetNames)
    {
        var targetIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var tableIndex = 0; tableIndex < targetNames.Count; tableIndex++)
        {
            if (!targetIndexes.TryAdd(targetNames[tableIndex], tableIndex))
            {
                throw new InvalidDataException($"Duplicate hardware intrinsic table row '{targetNames[tableIndex]}' for {target}.");
            }
        }

        var unionIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var unionIndex = 0; unionIndex < unionNames.Count; unionIndex++)
        {
            if (!unionIndexes.TryAdd(unionNames[unionIndex], unionIndex))
            {
                throw new InvalidDataException($"Duplicate hardware intrinsic identity '{unionNames[unionIndex]}'.");
            }
        }

        if (targetNames.Count >= ushort.MaxValue || unionNames.Count >= ushort.MaxValue)
        {
            throw new InvalidDataException("Hardware intrinsic index maps exceed their ushort encoding.");
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<ushort> s_{target}TableIndexByUnionIndex => [");

        foreach (var intrinsicName in unionNames)
        {
            var tableIndex = targetIndexes.TryGetValue(intrinsicName, out var index)
                ? index.ToString(CultureInfo.InvariantCulture)
                : "ushort.MaxValue";
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {tableIndex}, // NI_{intrinsicName}");
        }

        _ = builder.AppendLine("    ];");
        _ = builder.AppendLine();
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<ushort> s_{target}UnionIndexByTableIndex => [");

        foreach (var intrinsicName in targetNames)
        {
            var unionIndex = unionIndexes[intrinsicName];
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {unionIndex}, // NI_{intrinsicName}");
        }

        _ = builder.AppendLine("    ];");
        _ = builder.AppendLine();
    }

    private static List<string> GetHardwareIntrinsicNames()
    {
        var names = new List<string>();

        _ = ProcessHWIntrinsicListUnion((builder, inputFile, line, prefix, parts) =>
            names.Add(GetHardwareIntrinsicIdentity(parts)));

        return names;
    }

    private static List<string> GetHardwareIntrinsicNamesForTarget(string target)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddIntrinsic(StringBuilder builder, string inputFile, string line, string prefix, string[] parts)
        {
            if (parts.Length is not 16 and not 18)
            {
                return;
            }

            var identity = GetHardwareIntrinsicIdentity(parts);
            if (seen.Add(identity))
            {
                names.Add(identity);
            }
        }

        _ = ProcessTargetHardwareIntrinsicList(target, AddIntrinsic);

        return names;
    }

    private static StringBuilder ProcessTargetHardwareIntrinsicList(
        string target, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var builder = new StringBuilder();
        _ = builder.Append(ProcessTargetIntrinsicRows(
            @"Inputs\hwintrinsiclist.h", target, "HARDWARE_INTRINSIC(", callback));

        var targetInput = target switch
        {
            "XARCH" => @"Inputs\hwintrinsiclistxarch.h",
            "ARM64" => @"Inputs\hwintrinsiclistarm64.h",
            "WASM" => @"Inputs\hwintrinsiclistwasm.h",
            _ => throw new InvalidDataException($"Unsupported target hardware intrinsic table: {target}."),
        };
        _ = builder.Append(ProcessTargetIntrinsicRows(targetInput, target, "HARDWARE_INTRINSIC(", callback));

        if (target is "ARM64")
        {
            _ = builder.Append(ProcessTargetIntrinsicRows(
                @"Inputs\hwintrinsiclistarm64sve.h", target, "HARDWARE_INTRINSIC(", callback));
        }

        return builder;
    }

    private static string GetHardwareIntrinsicIdentity(string[] parts)
    {
        if (parts.Length is not 16 and not 18)
        {
            throw new InvalidDataException("Invalid hardware intrinsic row.");
        }

        return $"{parts[0].Trim()}_{parts[1].Trim()}";
    }

    private static StringBuilder ProcessTargetIntrinsicRows(
        string inputFile, string target, string prefix, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var lines = File.ReadAllLines(inputFile);
        var definedSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "FEATURE_HW_INTRINSICS",
            "HARDWARE_INTRINSIC",
        };

        switch (target)
        {
            case "XARCH":
            {
                _ = definedSymbols.Add("TARGET_XARCH");
                _ = definedSymbols.Add("TARGET_64BIT");
                break;
            }

            case "ARM64":
            {
                _ = definedSymbols.Add("TARGET_ARM64");
                _ = definedSymbols.Add("TARGET_64BIT");
                break;
            }

            case "WASM":
            {
                _ = definedSymbols.Add("TARGET_WASM");
                break;
            }

            default:
            {
                throw new InvalidDataException($"Unsupported target hardware intrinsic table: {target}.");
            }
        }

        var builder = new StringBuilder();
        var branches = new List<(bool ParentActive, bool BranchTaken, bool Active)>();
        var isActive = true;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmedLine = line.AsSpan().Trim();

            if (trimmedLine.StartsWith('#'))
            {
                var directiveLine = trimmedLine[1..].Trim();

                if (directiveLine.StartsWith("if ", StringComparison.Ordinal) ||
                    directiveLine.StartsWith("ifdef ", StringComparison.Ordinal) ||
                    directiveLine.StartsWith("ifndef ", StringComparison.Ordinal))
                {
                    var conditionStart = directiveLine.IndexOf(' ');
                    var directive = directiveLine[..conditionStart];
                    var condition = directiveLine[(conditionStart + 1)..].Trim().ToString();
                    var isConditionMet = directive.SequenceEqual("if")
                        ? EvaluateTargetIntrinsicCondition(condition, definedSymbols)
                        : directive.SequenceEqual("ifdef")
                            ? definedSymbols.Contains(condition)
                            : !definedSymbols.Contains(condition);
                    var parentActive = isActive;
                    isActive = parentActive && isConditionMet;
                    branches.Add((parentActive, isConditionMet, isActive));
                }
                else if (directiveLine.StartsWith("elif ", StringComparison.Ordinal))
                {
                    var condition = directiveLine[5..].Trim().ToString();
                    var branch = branches[^1];
                    var isConditionMet = !branch.BranchTaken && EvaluateTargetIntrinsicCondition(condition, definedSymbols);
                    branch.Active = branch.ParentActive && isConditionMet;
                    branch.BranchTaken |= isConditionMet;
                    branches[^1] = branch;
                    isActive = branch.Active;
                }
                else if (directiveLine.StartsWith("else", StringComparison.Ordinal))
                {
                    var branch = branches[^1];
                    branch.Active = branch.ParentActive && !branch.BranchTaken;
                    branch.BranchTaken = true;
                    branches[^1] = branch;
                    isActive = branch.Active;
                }
                else if (directiveLine.StartsWith("endif", StringComparison.Ordinal))
                {
                    branches.RemoveAt(branches.Count - 1);
                    isActive = branches.Count == 0 || branches[^1].Active;
                }

                continue;
            }

            if (isActive && TryExtractMacroContents(lines, ref lineIndex, line, prefix, out var parts))
            {
                callback(builder, inputFile, line, prefix, parts);
            }
        }

        if (branches.Count is not 0)
        {
            throw new InvalidDataException($"Unbalanced preprocessor conditionals in '{inputFile}'.");
        }

        return builder;
    }

    private static bool EvaluateTargetIntrinsicCondition(string condition, HashSet<string> definedSymbols)
    {
        var normalized = Regex.Replace(condition, @"defined\s*\(\s*(\w+)\s*\)", match =>
            definedSymbols.Contains(match.Groups[1].Value) ? "1" : "0");
        var tokens = new List<string>();

        foreach (Match match in Regex.Matches(normalized, @"&&|\|\||!|\(|\)|1|0"))
        {
            tokens.Add(match.Value);
        }

        var tokenIndex = 0;
        var result = ParseOr();

        if (tokenIndex != tokens.Count)
        {
            throw new InvalidDataException($"Unsupported intrinsic-list condition: '{condition}'.");
        }

        return result;

        bool ParseOr()
        {
            var value = ParseAnd();

            while (tokenIndex < tokens.Count && tokens[tokenIndex] is "||")
            {
                tokenIndex++;
                var right = ParseAnd();
                value |= right;
            }

            return value;
        }

        bool ParseAnd()
        {
            var value = ParseUnary();

            while (tokenIndex < tokens.Count && tokens[tokenIndex] is "&&")
            {
                tokenIndex++;
                var right = ParseUnary();
                value &= right;
            }

            return value;
        }

        bool ParseUnary()
        {
            if (tokenIndex >= tokens.Count)
            {
                throw new InvalidDataException($"Incomplete intrinsic-list condition: '{condition}'.");
            }

            var token = tokens[tokenIndex++];

            if (token is "!")
            {
                return !ParseUnary();
            }

            if (token is "(")
            {
                var value = ParseOr();

                if (tokenIndex >= tokens.Count || tokens[tokenIndex++] is not ")")
                {
                    throw new InvalidDataException($"Unbalanced intrinsic-list condition: '{condition}'.");
                }

                return value;
            }

            if (token is "1" or "0")
            {
                return token is "1";
            }

            throw new InvalidDataException($"Unsupported intrinsic-list condition token '{token}'.");
        }
    }

    private static StringBuilder GenerateIsaRangeBounds(string instructionSetBuilder)
    {
        var instructionSetSource = File.ReadAllText(@"Outputs\inc\corinfoinstructionset\CORINFO_InstructionSet.generated.cs");
        var builder = new StringBuilder();

        AppendTargetIsaRangeBounds(builder, instructionSetSource, instructionSetBuilder, "xarch", "xarch", ["FEATURE_HW_INTRINSICS", "TARGET_XARCH"]);
        AppendTargetIsaRangeBounds(builder, instructionSetSource, instructionSetBuilder, "arm64", "arm64", ["FEATURE_HW_INTRINSICS", "TARGET_ARM64"]);
        AppendTargetIsaRangeBounds(builder, instructionSetSource, instructionSetBuilder, "wasm", "wasm", ["FEATURE_HW_INTRINSICS", "TARGET_WASM", "TARGET_WASM32"]);

        return builder;
    }

    private static void AppendTargetIsaRangeBounds(
        StringBuilder builder,
        string instructionSetSource,
        string instructionSetBuilder,
        string name,
        string target,
        HashSet<string> definedSymbols)
    {
        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<ushort> s_{name}IsaRangeBounds => [");
        AppendIsaRangeBounds(builder, instructionSetSource, instructionSetBuilder, target, definedSymbols);
        _ = builder.AppendLine("    ];");
        _ = builder.AppendLine();
    }

    private static void AppendIsaRangeBounds(StringBuilder builder, string instructionSetSource, string instructionSetBuilder, string target, HashSet<string> definedSymbols)
    {
        var instructionSetValues = ParseInstructionSetValues(instructionSetSource, definedSymbols);
        var instructionSets = ParseInstructionSetRows(instructionSetBuilder, definedSymbols);

        var vectorInstructionSetValue = instructionSetValues["InstructionSet_Vector"];
        var noneInstructionSetValue = instructionSetValues["InstructionSet_NONE"];
        var maximumInstructionSetValue = 0;

        foreach (var (name, value) in instructionSetValues)
        {
            if (name is not "InstructionSet_Vector" and not "InstructionSet_NONE")
            {
                maximumInstructionSetValue = Math.Max(maximumInstructionSetValue, value);
            }
        }

        var isaRangeBounds = new ushort[(maximumInstructionSetValue + 1) * 2];
        Array.Fill(isaRangeBounds, ushort.MaxValue);
        ushort[] vectorRangeBounds = [ushort.MaxValue, ushort.MaxValue];

        for (var index = 0; index < instructionSets.Count; index++)
        {
            // Keep ushort.MaxValue available as the sentinel for an ISA without a lookup range.
            if ((uint)index >= ushort.MaxValue)
            {
                throw new InvalidDataException($"The intrinsic table index exceeds the ISA range encoding: {index}.");
            }

            if (!instructionSetValues.TryGetValue(instructionSets[index], out var instructionSetValue) ||
                (instructionSetValue == noneInstructionSetValue))
            {
                throw new InvalidDataException($"Intrinsic ISA '{instructionSets[index]}' is not present in the target instruction-set enum.");
            }

            var isVectorInstructionSet = instructionSetValue == vectorInstructionSetValue;
            var targetRangeBounds = isVectorInstructionSet ? vectorRangeBounds : isaRangeBounds;
            var boundsIndex = isVectorInstructionSet ? 0 : instructionSetValue * 2;

            if (targetRangeBounds[boundsIndex] is ushort.MaxValue)
            {
                targetRangeBounds[boundsIndex] = checked((ushort)index);
            }
            else if (targetRangeBounds[boundsIndex + 1] != index - 1)
            {
                // Internal codegen-only intrinsics follow the ISA's sorted lookup rows.
                continue;
            }

            targetRangeBounds[boundsIndex + 1] = checked((ushort)index);
        }

        // All portable vector widths share the Vector intrinsic IDs and names.
        CopyIsaRange(isaRangeBounds, instructionSetValues, "InstructionSet_Vector128", vectorRangeBounds[0], vectorRangeBounds[1]);

        if (target.Equals("xarch", StringComparison.Ordinal))
        {
            CopyIsaRange(isaRangeBounds, instructionSetValues, "InstructionSet_Vector256", vectorRangeBounds[0], vectorRangeBounds[1]);
            CopyIsaRange(isaRangeBounds, instructionSetValues, "InstructionSet_Vector512", vectorRangeBounds[0], vectorRangeBounds[1]);
        }
        else if (target.Equals("arm64", StringComparison.Ordinal))
        {
            CopyIsaRange(isaRangeBounds, instructionSetValues, "InstructionSet_Vector64", vectorRangeBounds[0], vectorRangeBounds[1]);
        }

        var instructionSetNames = new string[isaRangeBounds.Length / 2];
        foreach (var (name, value) in instructionSetValues)
        {
            if (name is not "InstructionSet_Vector" and not "InstructionSet_NONE")
            {
                instructionSetNames[value] = name;
            }
        }

        for (var index = 0; index < isaRangeBounds.Length; index += 2)
        {
            var instructionSetName = instructionSetNames[index / 2];
            var comment = instructionSetName is null ? "" : $" // {instructionSetName}";
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {isaRangeBounds[index]}, {isaRangeBounds[index + 1]},{comment}");
        }
    }

    private static Dictionary<string, int> ParseInstructionSetValues(string instructionSetSource, HashSet<string> definedSymbols)
    {
        var instructionSetValues = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextValue = 0;
        const string declaration = "public enum ManagedInstructionSet";
        var declarationIndex = instructionSetSource.IndexOf(declaration, StringComparison.Ordinal);

        if (declarationIndex < 0)
        {
            throw new InvalidDataException("The generated instruction-set source does not define ManagedInstructionSet.");
        }

        var bodyStart = instructionSetSource.IndexOf('{', declarationIndex, StringComparison.Ordinal);

        if (bodyStart < 0)
        {
            throw new InvalidDataException("The generated ManagedInstructionSet enum is malformed.");
        }

        var bodyEnd = instructionSetSource.IndexOf('}', bodyStart, StringComparison.Ordinal);

        if (bodyEnd < 0)
        {
            throw new InvalidDataException("The generated ManagedInstructionSet enum is malformed.");
        }

        var managedInstructionSetSource = instructionSetSource[bodyStart..(bodyEnd + 1)];
        var activeSource = GetActivePreprocessorLines(managedInstructionSetSource, definedSymbols);

        foreach (var line in activeSource.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(InstructionSet_[A-Za-z0-9_]+)(?:\s*=\s*(\d+))?\s*,?\s*$", RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                continue;
            }

            var name = match.Groups[1].Value;
            var value = match.Groups[2].Success
                ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                : nextValue;

            if (!instructionSetValues.TryAdd(name, value))
            {
                throw new InvalidDataException($"Instruction set is defined more than once for the target: '{name}'.");
            }

            nextValue = checked(value + 1);
        }

        if (!instructionSetValues.ContainsKey("InstructionSet_NONE"))
        {
            throw new InvalidDataException("The generated instruction-set enum does not define InstructionSet_NONE.");
        }

        return instructionSetValues;
    }

    private static List<string> ParseInstructionSetRows(string instructionSetBuilder, HashSet<string> definedSymbols)
    {
        var instructionSets = new List<string>();
        var activeSource = GetActivePreprocessorLines(instructionSetBuilder, definedSymbols);

        foreach (var line in activeSource.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(InstructionSet_[A-Za-z0-9_]+)\s*,", RegexOptions.CultureInvariant);

            if (match.Success)
            {
                instructionSets.Add(match.Groups[1].Value);
            }
        }

        return instructionSets;
    }

    private static string GetActivePreprocessorLines(string source, HashSet<string> definedSymbols)
    {
        var builder = new StringBuilder();
        var conditionalStack = new Stack<(bool ParentIncluded, bool BranchTaken)>();
        var isIncluded = true;

        using var reader = new StringReader(source);

        while (reader.ReadLine() is string line)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("#if ", StringComparison.Ordinal))
            {
                var condition = EvaluatePreprocessorCondition(trimmedLine[4..], definedSymbols);
                conditionalStack.Push((isIncluded, condition));
                isIncluded &= condition;
            }
            else if (trimmedLine.StartsWith("#elif ", StringComparison.Ordinal))
            {
                if (conditionalStack.Count is 0)
                {
                    throw new InvalidDataException($"Unexpected preprocessor #elif: '{line}'.");
                }

                var (parentIncluded, branchTaken) = conditionalStack.Pop();
                var condition = EvaluatePreprocessorCondition(trimmedLine[6..], definedSymbols);
                var branchIncluded = !branchTaken && condition;
                conditionalStack.Push((parentIncluded, branchTaken || condition));
                isIncluded = parentIncluded && branchIncluded;
            }
            else if (trimmedLine.Equals("#else", StringComparison.Ordinal))
            {
                if (conditionalStack.Count is 0)
                {
                    throw new InvalidDataException($"Unexpected preprocessor #else: '{line}'.");
                }

                var (parentIncluded, branchTaken) = conditionalStack.Pop();
                isIncluded = parentIncluded && !branchTaken;
                conditionalStack.Push((parentIncluded, true));
            }
            else if (trimmedLine.Equals("#endif", StringComparison.Ordinal))
            {
                if (conditionalStack.Count is 0)
                {
                    throw new InvalidDataException($"Unexpected preprocessor #endif: '{line}'.");
                }

                isIncluded = conditionalStack.Pop().ParentIncluded;
            }
            else if (trimmedLine.StartsWith("#error ", StringComparison.Ordinal))
            {
                if (isIncluded)
                {
                    throw new InvalidDataException($"Active #error in intrinsic lookup table: '{line}'.");
                }
            }
            else if (trimmedLine.Length > 0 && trimmedLine[0] is '#')
            {
                throw new InvalidDataException($"Unsupported preprocessor directive in intrinsic lookup table: '{line}'.");
            }
            else if (isIncluded)
            {
                _ = builder.AppendLine(line);
            }
        }

        if (conditionalStack.Count is not 0)
        {
            throw new InvalidDataException("Unterminated conditional region in intrinsic lookup table.");
        }

        return builder.ToString();
    }

    private static bool EvaluatePreprocessorCondition(string condition, HashSet<string> definedSymbols)
    {
        foreach (var disjunction in condition.Split("||", StringSplitOptions.TrimEntries))
        {
            var isConjunctionTrue = true;

            foreach (var term in disjunction.Split("&&", StringSplitOptions.TrimEntries))
            {
                var symbol = term.Trim('(', ')', ' ');
                var isNegated = symbol.Length > 0 && symbol[0] is '!';

                if (isNegated)
                {
                    symbol = symbol[1..].Trim('(', ')', ' ');
                }

                if (symbol.Length is 0 || !Regex.IsMatch(symbol, @"^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
                {
                    throw new InvalidDataException($"Unsupported preprocessor condition in intrinsic lookup table: '{condition}'.");
                }

                var isDefined = definedSymbols.Contains(symbol);

                if (!isDefined && symbol is not "FEATURE_HW_INTRINSICS" and not "FEATURE_SIMD" and
                    not "FEATURE_ITINSTRUCTION" and not "FEATURE_PLI_INSTRUCTION" and
                    not "TARGET_32BIT" and not "TARGET_64BIT" and not "TARGET_AMD64" and not "TARGET_ARM" and
                    not "TARGET_ARM64" and not "TARGET_ARMARCH" and not "TARGET_LOONGARCH64" and
                    not "TARGET_RISCV64" and not "TARGET_WASM" and not "TARGET_WASM32" and
                    not "TARGET_X86" and not "TARGET_XARCH")
                {
                    throw new InvalidDataException($"Unknown preprocessor symbol in intrinsic lookup table: '{symbol}'.");
                }

                isConjunctionTrue &= isNegated ? !isDefined : isDefined;
            }

            if (isConjunctionTrue)
            {
                return true;
            }
        }

        return false;
    }

    private static void CopyIsaRange(ushort[] isaRangeBounds, Dictionary<string, int> instructionSetValues, string instructionSet, ushort lowerBound, ushort upperBound)
    {
        if (!instructionSetValues.TryGetValue(instructionSet, out var instructionSetValue))
        {
            throw new InvalidDataException($"The generated instruction-set enum does not define '{instructionSet}'.");
        }

        var boundsIndex = instructionSetValue * 2;
        isaRangeBounds[boundsIndex] = lowerBound;
        isaRangeBounds[boundsIndex + 1] = upperBound;
    }

    private static void GenerateTernaryLogicInfo()
    {
        string[] kinds = [
            "None", "Select", "True", "False", "Not", "And", "Nand", "Or",
            "Nor", "Xor", "Xnor", "Cond", "Major", "Minor",
        ];
        string[] uses = ["None", "A", "B", "AB", "C", "AC", "BC", "ABC"];
        var entries = new StringBuilder();
        var count = 0;

        foreach (var line in File.ReadLines(@"Inputs\ternarylogic.inc"))
        {
            var tokens = Regex.Matches(line, @"TernaryLogic(?:OperKind|UseFlags)::([A-Za-z]+)");
            if (tokens.Count == 0)
            {
                continue;
            }
            if (tokens.Count != 6)
            {
                throw new InvalidDataException($"Malformed ternary-logic entry: {line}");
            }

            uint packed = 0;
            for (var index = 0; index < 3; index++)
            {
                var kind = Array.IndexOf(kinds, tokens[2 * index].Groups[1].Value);
                var use = Array.IndexOf(uses, tokens[(2 * index) + 1].Groups[1].Value);
                if ((kind < 0) || (use < 0))
                {
                    throw new InvalidDataException($"Unknown ternary-logic operation: {line}");
                }

                var entry = (uint)kind | ((uint)use << 4);
                packed |= entry << (7 * index);
            }

            _ = entries.AppendLine(CultureInfo.InvariantCulture, $"        0x{packed:X6}u, // 0x{count:X2}");
            count++;
        }

        if (count != 256)
        {
            throw new InvalidDataException($"Expected 256 native ternary-logic entries; found {count}.");
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\hwintrinsic");
        File.WriteAllText(@"Outputs\jit\hwintrinsic\TernaryLogicInfo.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public readonly partial struct TernaryLogicInfo
{
    private static System.ReadOnlySpan<uint> PackedEntries => [
{{entries}}    ];
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

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        InlineObservation.{scope}_{name} => {description},");
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
    private static string GetDescription(InlineObservation observation) => observation switch
    {
{{descriptionsBuilder}}        _ => throw new FatalJitException("Invalid inline observation."),
    };

    private static ReadOnlySpan<InlineImpact> s_impacts => [
{{impactsBuilder}}    ];

    private static ReadOnlySpan<InlineTarget> s_targets => [
{{targetsBuilder}}    ];
}
""");
    }

    private static void GenerateEmitJumpKind()
    {
        var builder = ProcessMacroBasedFile(@"Inputs\emitjmps.h", "JMP_SMALL(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid jump mapping format: '{line}'");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    EJ_{parts[0].Trim()},");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\instr");
        File.WriteAllText(@"Outputs\jit\instr\emitJumpKind.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.emitJumpKind;

namespace RyuJitSharp;

public enum emitJumpKind
{
    EJ_NONE,
{{builder}}
    EJ_COUNT,
}
""");

        var instructionBuilder = ProcessMacroBasedFile(@"Inputs\emitjmps.h", "JMP_SMALL(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid jump mapping format: '{line}'");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        INS_{parts[2].Trim()}, // EJ_{parts[0].Trim()}");
        });

        var reverseBuilder = ProcessMacroBasedFile(@"Inputs\emitjmps.h", "JMP_SMALL(", (builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"Invalid jump mapping format: '{line}'");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        EJ_{parts[1].Trim()}, // EJ_{parts[0].Trim()}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\emitxarch");
        File.WriteAllText(@"Outputs\jit\emitxarch\Emitter.JumpInstructions.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH || TARGET_ARM64 || TARGET_RISCV64
    private static ReadOnlySpan<instruction> emitJumpKindInstructions => [
        INS_nop, // EJ_NONE
{{instructionBuilder}}
#if TARGET_XARCH
        INS_call, // EJ_COUNT
#endif
    ];

    private static ReadOnlySpan<emitJumpKind> emitReverseJumpKinds => [
        EJ_NONE,
{{reverseBuilder}}    ];
#endif
}
""");
    }

    private static void AppendCharacterLiteral(StringBuilder builder, char value)
    {
        _ = builder.Append('\'');
        _ = builder.Append(value switch
        {
            '\0' => "\\0",
            '\t' => "\\t",
            '\n' => "\\n",
            '\r' => "\\r",
            '\\' => "\\\\",
            '\'' => "\\'",
            _ => value.ToString(),
        });
        _ = builder.Append('\'');
    }

    private static void GenerateInstruction()
    {
        var instructionNames = new List<string>();
        var instructionNameSet = new HashSet<string>(StringComparer.Ordinal);
        _ = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is 0)
            {
                return;
            }

            var id = parts[0].AsSpan().Trim();
            var instructionName = inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal)
                ? $"INS_sve_{id}"
                : $"INS_{id}";
            if (instructionNameSet.Add(instructionName))
            {
                instructionNames.Add(instructionName);
            }
        });

        if (instructionNameSet.Add("INS_lea"))
        {
            instructionNames.Add("INS_lea");
        }

        instructionNames.Add("INS_none");

        var builder = new StringBuilder();
        foreach (var instructionName in instructionNames)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {instructionName},");
        }

        var instructionOrdinals = GenerateTargetInstructionOrdinals();
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

    INS_count = INS_none,
}
""");

        GenerateInstructionOrdinalMaps(instructionNames, instructionOrdinals);

        var xarchNameDataBuilder = new StringBuilder();
        var otherNameDataBuilder = new StringBuilder();
        var xarchNameOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var otherNameOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var xarchNameDataLength = 0;
        var otherNameDataLength = 0;

        var nameRangesBuilder = ProcessInstrs((rangeBuilder, inputFile, line, prefix, parts) => {
            if (parts.Length is 0)
            {
                return;
            }

            var name = parts[1].Trim();
            if (!name.StartsWith('"', StringComparison.Ordinal)
                || !name.EndsWith('"', StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected instruction name: '{line}'");
            }

            var id = parts[0].Trim();
            var nameDataBuilder = xarchNameDataBuilder;
            var nameOffsets = xarchNameOffsets;
            var nameDataLength = xarchNameDataLength;

            if (!inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                nameDataBuilder = otherNameDataBuilder;
                nameOffsets = otherNameOffsets;
                nameDataLength = otherNameDataLength;
            }

            if (inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal))
            {
                id = $"sve_{id}";
            }

            var characters = name.AsSpan(1, name.Length - 2);
            var nameValue = characters.ToString();

            if (!nameOffsets.TryGetValue(nameValue, out var characterIndex))
            {
                characterIndex = nameDataLength;
                nameOffsets.Add(nameValue, characterIndex);

                for (var index = 0; index < characters.Length; index++)
                {
                    AppendCharacterLiteral(nameDataBuilder, characters[index]);
                    _ = nameDataBuilder.Append(", ");
                }

                _ = nameDataBuilder.Append("'\\0',");
                _ = nameDataBuilder.AppendLine(CultureInfo.InvariantCulture, $" // {name}");
                nameDataLength += characters.Length + 1;

                if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
                {
                    xarchNameDataLength = nameDataLength;
                }
                else
                {
                    otherNameDataLength = nameDataLength;
                }
            }

            _ = rangeBuilder.AppendLine(CultureInfo.InvariantCulture,
                $"        {characterIndex}, {characters.Length}, // INS_{id}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\instr");
        File.WriteAllText(@"Outputs\jit\instr\CodeGen.InstructionNames.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private static ReadOnlySpan<char> s_instructionNameData => [
#if TARGET_XARCH
{{xarchNameDataBuilder}}    ];
#else
{{otherNameDataBuilder}}    ];
#endif

    private static ReadOnlySpan<int> s_instructionNameRanges => [
{{nameRangesBuilder}}    ];
}
""");

        var maskBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (!inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                return;
            }

            var maskSize = 0;
            foreach (var flag in parts[^1].Split('|', StringSplitOptions.TrimEntries))
            {
                if (flag.StartsWith("KMask_Base", StringComparison.Ordinal))
                {
                    maskSize = int.Parse(flag.AsSpan("KMask_Base".Length), CultureInfo.InvariantCulture);
                    if (maskSize is not 1 and not 2 and not 4 and not 8 and not 16)
                    {
                        throw new InvalidDataException($"Unexpected mask size: '{line}'");
                    }
                }
            }
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {maskSize}, // INS_{parts[0].Trim()}");
        });

        var tupleBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {parts[^2].Trim()}, // INS_{parts[0].Trim()}");
            }
        });

        var flagsBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                var flags = string.Join(" | ", parts[^1].Split('|', StringSplitOptions.TrimEntries));
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {flags}, // INS_{parts[0].Trim()}");
            }
        });

        var arm64FlagsBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsarm64.h", StringComparison.Ordinal)
                || inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal))
            {
                var flags = string.Join(" | ", parts[2].Split('|', StringSplitOptions.TrimEntries));
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {flags}, // {parts[0].Trim()}");
            }
        });

        var otherFlagsBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal)
                || inputFile.Equals(@"Inputs\instrsarm64.h", StringComparison.Ordinal)
                || inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal))
            {
                return;
            }

            var flags = inputFile.Equals(@"Inputs\instrsarm.h", StringComparison.Ordinal)
                ? $"({parts[3].Trim()}) | (INST_FP * ({parts[2].Trim()}))"
                : parts[2].Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {flags}, // INS_{parts[0].Trim()}");
        });

        var latencyBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        PERFSCORE_LATENCY_{parts[^4].Trim()}, // INS_{parts[0].Trim()}");
            }
        });

        var throughputBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) => {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        PERFSCORE_THROUGHPUT_{parts[^3].Trim()}, // INS_{parts[0].Trim()}");
            }
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\emitxarch");
        File.WriteAllText(@"Outputs\jit\emitxarch\Emitter.ExecutionInfo.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private static ReadOnlySpan<float> insLatencyInfos => [
{{latencyBuilder}}    ];

    private static ReadOnlySpan<float> insThroughputInfos => [
{{throughputBuilder}}    ];
#endif
}
""");

        File.WriteAllText(@"Outputs\jit\emitxarch\Emitter.InstructionInfo.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    private static ReadOnlySpan<insTupleType> s_tupleTypes => [
{{tupleBuilder}}
    ];
#endif
}
""");

        _ = Directory.CreateDirectory(@"Outputs\jit\codegen");
        File.WriteAllText(@"Outputs\jit\codegen\CodeGen.InstructionInfo.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    private static ReadOnlySpan<byte> s_kMaskBaseSizes => [
{{maskBuilder}}
    ];

#endif
}
""");

        File.WriteAllText(@"Outputs\jit\codegen\CodeGen.InstructionFlags.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    internal static ReadOnlySpan<insInfoFlags> instInfo => [
{{flagsBuilder}}
    ];
#elif TARGET_ARM64
    private const byte INST_FP = 1;
    internal const byte LD = 1;
    internal const byte ST = 2;
    private const byte CMP = 4;
    private const byte RSH = 8;
    private const byte WID = 16;
    private const byte LNG = 32;
    private const byte NRW = 64;
    private const byte WR2 = 128;

    internal static ReadOnlySpan<byte> instInfo => [
{{arm64FlagsBuilder}}
        0, // INS_lea is zero-initialized in native instInfo[INS_count].
    ];
#elif TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64 || TARGET_WASM
    private const byte INST_FP = 1;

#if TARGET_ARM
    private const byte LD = 2;
    private const byte ST = 4;
    private const byte CMP = 8;
#elif TARGET_LOONGARCH64 || TARGET_RISCV64
    private const byte LD = 1;
    private const byte ST = 2;
#endif

    internal static ReadOnlySpan<byte> instInfo => [
{{otherFlagsBuilder}}
#if TARGET_ARM || TARGET_LOONGARCH64 || TARGET_RISCV64
        0, // INS_lea is zero-initialized in native instInfo[INS_count].
#endif
    ];
#endif
}
""");
    }

    private static string[] ReadInstructionFormatLines(string formatInput)
    {
        var lines = File.ReadAllLines(formatInput);
        var firstFormat = Array.FindIndex(lines, line => line.TrimStart().StartsWith("IF_DEF(", StringComparison.Ordinal));
        var lastFormat = Array.FindLastIndex(lines, line => line.TrimStart().StartsWith("IF_DEF(", StringComparison.Ordinal));
        if (firstFormat < 0)
        {
            throw new InvalidDataException($"No instruction formats in '{formatInput}'.");
        }

        // The native header also contains DEFINE_ID_OPS/DEFINE_IS_OPS include modes.
        // Process the format-list region, retaining any conditional paths within it.
        return lines[firstFormat..(lastFormat + 1)];
    }

    private static void AppendInstructionFormat(StringBuilder builder, string inputFile, string line,
        string prefix, string[] parts)
    {
        if (parts.Length != 3)
        {
            throw new InvalidDataException($"Invalid line format: '{line}'");
        }

        _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        IF_{parts[0].Trim()},");
    }

    private static void GenerateInstructionFormats()
    {
        const string formatInput = @"Inputs\emitfmtsxarch.h";
        var formatLines = ReadInstructionFormatLines(formatInput);
        var formatBuilder = ProcessMacroBasedFile(formatInput, formatLines, ["IF_DEF("],
            AppendInstructionFormat);
        const string armFormatInput = @"Inputs\emitfmtsarm.h";
        var armFormatLines = ReadInstructionFormatLines(armFormatInput);
        var armFormatBuilder = ProcessMacroBasedFile(armFormatInput, armFormatLines, ["IF_DEF("],
            AppendInstructionFormat);
        var armOperandBuilder = ProcessMacroBasedFile(armFormatInput, armFormatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}"));
        var arm64FormatBuilder = new StringBuilder();
        var arm64OperandBuilder = new StringBuilder();
        string[] arm64Inputs = [@"Inputs\emitfmtsarm64.h", @"Inputs\emitfmtsarm64sve.h"];
        foreach (var input in arm64Inputs)
        {
            var lines = ReadInstructionFormatLines(input);
            _ = arm64FormatBuilder.Append(ProcessMacroBasedFile(input, lines,
                ["IF_DEF("], AppendInstructionFormat));
            _ = arm64OperandBuilder.Append(ProcessMacroBasedFile(input, lines, ["IF_DEF("],
                (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                    $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}")));
        }

        const string wasmFormatInput = @"Inputs\emitfmtswasm.h";
        var wasmFormatLines = ReadInstructionFormatLines(wasmFormatInput);
        var wasmFormatBuilder = ProcessMacroBasedFile(wasmFormatInput, wasmFormatLines, ["IF_DEF("],
            AppendInstructionFormat);
        var wasmOperandBuilder = ProcessMacroBasedFile(wasmFormatInput, wasmFormatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}"));
        const string loongArchFormatInput = @"Inputs\emitfmtsloongarch64.h";
        var loongArchFormatLines = ReadInstructionFormatLines(loongArchFormatInput);
        var loongArchFormatBuilder = ProcessMacroBasedFile(loongArchFormatInput, loongArchFormatLines, ["IF_DEF("],
            AppendInstructionFormat);
        var loongArchOperandBuilder = ProcessMacroBasedFile(loongArchFormatInput, loongArchFormatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}"));
        const string riscvFormatInput = @"Inputs\emitfmtsriscv64.h";
        var riscvFormatLines = ReadInstructionFormatLines(riscvFormatInput);
        var riscvFormatBuilder = ProcessMacroBasedFile(riscvFormatInput, riscvFormatLines, ["IF_DEF("],
            AppendInstructionFormat);
        var riscvOperandBuilder = ProcessMacroBasedFile(riscvFormatInput, riscvFormatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}"));

        var operandBuilder = ProcessMacroBasedFile(formatInput, formatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) => _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (byte)ID_OP_{parts[2].Trim()}, // IF_{parts[0].Trim()}"));
        var schedulingBuilder = ProcessMacroBasedFile(formatInput, formatLines, ["IF_DEF("],
            (builder, inputFile, line, prefix, parts) =>
            {
                var flags = string.Join(" | ", parts[1].Split('|', StringSplitOptions.TrimEntries));
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {flags}, // IF_{parts[0].Trim()}");
            });
        var updateModeBuilder = ProcessInstrs((builder, inputFile, line, prefix, parts) =>
        {
            if (inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture,
                    $"        (byte){parts[2].Trim()}, // INS_{parts[0].Trim()}");
            }
        });

        var arm64InstructionFormats = ProcessInstrs((builder, inputFile, line, prefix, parts) =>
        {
            if (inputFile.Equals(@"Inputs\instrsarm64.h", StringComparison.Ordinal)
                || inputFile.Equals(@"Inputs\instrsarm64sve.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {parts[3].Trim()}, // {parts[0].Trim()}");
            }
        });
        var armInstructionFormats = ProcessInstrs((builder, inputFile, line, prefix, parts) =>
        {
            if (inputFile.Equals(@"Inputs\instrsarm.h", StringComparison.Ordinal))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        {parts[4].Trim()}, // {parts[0].Trim()}");
            }
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\emit");
        File.WriteAllText(@"Outputs\jit\emit\Emitter.InstructionFormats.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
    public enum insFormat : uint
    {
#if TARGET_XARCH
{{formatBuilder}}#elif TARGET_ARM
{{armFormatBuilder}}#elif TARGET_ARM64
{{arm64FormatBuilder}}#elif TARGET_WASM
{{wasmFormatBuilder}}#elif TARGET_LOONGARCH64
{{loongArchFormatBuilder}}#elif TARGET_RISCV64
{{riscvFormatBuilder}}#endif
        IF_COUNT,
    }

#if TARGET_XARCH || TARGET_ARM || TARGET_ARM64 || TARGET_WASM || TARGET_LOONGARCH64 || TARGET_RISCV64
    internal static ReadOnlySpan<byte> emitFmtToOps => [
#if TARGET_ARM
{{armOperandBuilder}}#elif TARGET_ARM64
{{arm64OperandBuilder}}#elif TARGET_WASM
{{wasmOperandBuilder}}#elif TARGET_LOONGARCH64
{{loongArchOperandBuilder}}#elif TARGET_RISCV64
{{riscvOperandBuilder}}#else
{{operandBuilder}}#endif
    ];
#endif

#if TARGET_XARCH
    private static ReadOnlySpan<IS_INFO> emitFmtToSchedInfo => [
{{schedulingBuilder}}    ];
#endif
}
""");

        _ = Directory.CreateDirectory(@"Outputs\jit\emitxarch");
        File.WriteAllText(@"Outputs\jit\emitxarch\Emitter.InstructionUpdateModes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    internal static ReadOnlySpan<byte> emitInsModeFmtTab => [
{{updateModeBuilder}}
    ];
#endif
}
""");

        var xarchOperandKinds = ReadNativeEnumBody(formatInput, "ID_OPS");
        var armOperandKinds = ReadNativeEnumBody(armFormatInput, "ID_OPS");
        var arm64OperandKinds = ReadNativeEnumBody(arm64Inputs[0], "ID_OPS");
        var wasmOperandKinds = ReadNativeEnumBody(wasmFormatInput, "ID_OPS");
        var loongArchOperandKinds = ReadNativeEnumBody(loongArchFormatInput, "ID_OPS");
        var riscvOperandKinds = ReadNativeEnumBody(riscvFormatInput, "ID_OPS");
        File.WriteAllText(@"Outputs\jit\emit\ID_OPS.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ID_OPS;

namespace RyuJitSharp;

public enum ID_OPS
{
#if TARGET_ARM
{{armOperandKinds}}#elif TARGET_ARM64
{{arm64OperandKinds}}#elif TARGET_WASM
{{wasmOperandKinds}}#elif TARGET_LOONGARCH64
{{loongArchOperandKinds}}#elif TARGET_RISCV64
{{riscvOperandKinds}}#else
{{xarchOperandKinds}}#endif
}
""");
        GenerateNativeEnum(formatInput, "IS_INFO", @"Outputs\jit\emit\IS_INFO.generated.cs", isFlags: true);
        GenerateNativeEnum(@"Inputs\instr.h", "insUpdateModes",
            @"Outputs\jit\instr\insUpdateModes.generated.cs", isFlags: false);

        _ = Directory.CreateDirectory(@"Outputs\jit\emitarm64");
        File.WriteAllText(@"Outputs\jit\emitarm64\Emitter.InstructionFormats.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM64
    private static ReadOnlySpan<insFormat> s_instructionFormats => [
{{arm64InstructionFormats}}
    ];
#endif
}
""");

        _ = Directory.CreateDirectory(@"Outputs\jit\emitarm");
        File.WriteAllText(@"Outputs\jit\emitarm\Emitter.InstructionFormats.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    private static ReadOnlySpan<insFormat> s_instructionFormats => [
{{armInstructionFormats}}
    ];
#endif
}
""");
    }

    private static void GenerateInstructionOpcodes()
    {
        var macros = new Dictionary<string, (string[] Parameters, string Body)>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(@"Inputs\instrsxarch.h"))
        {
            var match = Regex.Match(line, @"^#define\s+(\w+)\(([^)]*)\)\s+(.+)$");
            if (match.Success)
            {
                macros.Add(match.Groups[1].Value,
                    (match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries), match.Groups[3].Value));
            }
        }

        var tables = new StringBuilder();
        (string Name, int MinimumArity, int Column, bool NativeWidth)[] layouts = [
            ("insCodes", 0, 3, false),
            ("insCodesACC", 4, 6, false),
            ("insCodesRR", 5, 7, false),
            ("insCodesRM", 3, 5, true),
            ("insCodesMI", 2, 4, true),
            ("insCodesMR", 1, 3, false),
        ];

        foreach (var (name, minimumArity, column, nativeWidth) in layouts)
        {
            var entries = ProcessInstrs((builder, inputFile, line, prefix, parts) =>
            {
                if (!inputFile.Equals(@"Inputs\instrsxarch.h", StringComparison.Ordinal))
                {
                    return;
                }

                var arity = prefix.Equals("INSTMUL(", StringComparison.Ordinal) ? 3 : prefix[4] - '0';
                var arguments = SplitOpcodeArguments(line);
                if ((arity < 0) || (arity > 5) || (arguments.Length != Math.Max(1, arity) + 7))
                {
                    throw new InvalidDataException($"Invalid opcode table entry: '{line}'");
                }

                // Native tables are prefixes of instruction order, not padded arrays:
                // e.g. insCodesRR includes INST5 only, while insCodes includes INST0 too.
                if (arity >= minimumArity)
                {
                    var opcode = ExpandOpcode(arguments[column], macros);
                    var expression = $"unchecked(({(nativeWidth ? "OpcodeNative" : "uint")})({opcode}))";
                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"        {expression}, // INS_{arguments[0]}");
                }
            });

            if (nativeWidth)
            {
                _ = tables.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<OpcodeNative> {name} => [");
            }
            else
            {
                _ = tables.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<uint> {name} => [");
            }

            _ = tables.Append(entries);
            _ = tables.AppendLine("    ];");
            _ = tables.AppendLine();
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\emitxarch");
        File.WriteAllText(@"Outputs\jit\emitxarch\Globals.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
#if TARGET_AMD64
using OpcodeNative = System.UInt64;
#else
using OpcodeNative = System.UInt32;
#endif

namespace RyuJitSharp;

public static partial class Globals
{
#if TARGET_XARCH
{{tables}}#endif
}
""");

        GenerateSveInstructionOpcodes();
        GenerateArm64InstructionOpcodes();
        GenerateArmInstructionOpcodes();
        GenerateRiscVInstructionOpcodes();
    }

    private static void GenerateRiscVInstructionOpcodes()
    {
        var opcodes = ProcessMacroBasedFile(@"Inputs\instrsriscv64.h", "INST(", (builder, inputFile, line, prefix, parts) =>
        {
            var arguments = SplitOpcodeArguments(line);
            if (arguments.Length != 4)
            {
                throw new InvalidDataException($"Invalid RISC-V opcode table entry: '{line}'");
            }

            var opcode = arguments[3].Trim();
            if (opcode == "BAD_CODE")
            {
                opcode = "Globals.BAD_CODE";
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        unchecked((uint)({opcode})), // INS_{arguments[0].Trim()}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\emitriscv64");
        File.WriteAllText(@"Outputs\jit\emitriscv64\Emitter.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_RISCV64
    private static ReadOnlySpan<uint> insCodes => [
{{opcodes}}    ];
#endif
}
""");
    }

    private static void GenerateLoongArchInstructionOpcodes()
    {
        var codes = ProcessMacroBasedFile(@"Inputs\instrsloongarch64.h", "INST(", (builder, inputFile, line, prefix, parts) =>
        {
            var arguments = SplitOpcodeArguments(line);
            if (arguments.Length != 6)
            {
                throw new InvalidDataException($"Invalid LoongArch64 instruction table entry: '{line}'");
            }

            var code = arguments[3].Trim();
            if (code == "BAD_CODE")
            {
                code = "Globals.BAD_CODE";
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        unchecked((uint)({code})), // INS_{arguments[0].Trim()}");
        });
        var masks = ProcessMacroBasedFile(@"Inputs\instrsloongarch64.h", "INST(", (builder, inputFile, line, prefix, parts) =>
        {
            var arguments = SplitOpcodeArguments(line);
            if (arguments.Length != 6)
            {
                throw new InvalidDataException($"Invalid LoongArch64 instruction table entry: '{line}'");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        unchecked((uint)({arguments[4].Trim()})), // INS_{arguments[0].Trim()}");
        });
        var formats = ProcessMacroBasedFile(@"Inputs\instrsloongarch64.h", "INST(", (builder, inputFile, line, prefix, parts) =>
        {
            var arguments = SplitOpcodeArguments(line);
            if (arguments.Length != 6)
            {
                throw new InvalidDataException($"Invalid LoongArch64 instruction table entry: '{line}'");
            }

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        insDisasmFmt.{arguments[5].Trim()}, // INS_{arguments[0].Trim()}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\emitloongarch64");
        File.WriteAllText(@"Outputs\jit\emitloongarch64\Emitter.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_LOONGARCH64
    private static ReadOnlySpan<uint> insCodes => [
{{codes}}    ];

    private static ReadOnlySpan<uint> insMasks => [
{{masks}}    ];

    private static ReadOnlySpan<insDisasmFmt> insDisasmFormats => [
{{formats}}    ];
#endif
}
""");
    }

        private static void GenerateWasmInstructionOpcodes()
    {
        var instructionRows = new List<(string Opcode, string Prefix, string Format, string Name)>();
        _ = ProcessInstrs((builder, inputFile, line, prefix, parts) =>
        {
            if (inputFile.Equals(@"Inputs\instrswasm.h", StringComparison.Ordinal))
            {
                var isPrefixed = prefix.Equals("INST2(", StringComparison.Ordinal);
                var expectedArity = isPrefixed ? 6 : 5;
                if (parts.Length != expectedArity)
                {
                    throw new InvalidDataException($"Invalid Wasm opcode table entry: '{line}'");
                }

                var name = $"INS_{parts[0].Trim()}";
                instructionRows.Add((
                    parts[isPrefixed ? 5 : 4].Trim(),
                    isPrefixed ? parts[4].Trim() : "0",
                    parts[3].Trim(),
                    name));
            }
        });

        var opcodes = new StringBuilder();
        var prefixes = new StringBuilder();
        var formats = new StringBuilder();
        foreach (var (opcode, prefix, format, name) in instructionRows)
        {
            _ = opcodes.AppendLine(CultureInfo.InvariantCulture,
                $"        unchecked((uint)({(opcode == "BAD_CODE" ? "Globals.BAD_CODE" : opcode)})), // {name}");
            _ = prefixes.AppendLine(CultureInfo.InvariantCulture,
                $"        unchecked((byte)({prefix})), // {name}");
            _ = formats.AppendLine(CultureInfo.InvariantCulture,
                $"        insFormat.{format}, // {name}");
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\emitwasm");
        File.WriteAllText(@"Outputs\jit\emitwasm\Emitter.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_WASM
    private static ReadOnlySpan<uint> s_wasmOpcodes => [
{{opcodes}}    ];

    private static ReadOnlySpan<byte> s_wasmOpcodePrefixes => [
{{prefixes}}    ];

    private static ReadOnlySpan<insFormat> s_wasmInstructionFormats => [
{{formats}}    ];

#endif
}

""");
    }

    private static void GenerateArm64InstructionOpcodes()
    {
        const string inputFile = @"Inputs\instrsarm64.h";
        ReadOnlySpan<string> prefixes = [
            "INST1(", "INST2(", "INST3(", "INST4(", "INST5(", "INST6(", "INST9(",
        ];
        var tables = new StringBuilder();

        for (var encoding = 1; encoding <= 9; encoding++)
        {
            var previousArity = 9;
            var entries = ProcessMacroBasedFile(inputFile, prefixes, (builder, file, line, prefix, parts) =>
            {
                var arity = int.Parse(prefix.AsSpan(4, prefix.Length - 5), NumberStyles.None,
                    CultureInfo.InvariantCulture);
                var arguments = SplitOpcodeArguments(line);
                if (arguments.Length != arity + 4)
                {
                    throw new InvalidDataException($"Invalid ARM64 opcode table entry: '{line}'");
                }

                // Filtering must leave an instruction-order prefix, since the native
                // selector indexes every encoding array directly by instruction.
                if (arity > previousArity)
                {
                    throw new InvalidDataException($"ARM64 opcode arity is not descending: '{line}'");
                }
                previousArity = arity;

                if (arity >= encoding)
                {
                    var opcode = arguments[3 + encoding];
                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"        unchecked((uint)({opcode})), // INS_{arguments[0]}");
                }
            });

            _ = tables.AppendLine(CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<uint> ordinaryInsCodes{encoding} => [");
            _ = tables.Append(entries);
            _ = tables.AppendLine("    ];");
            _ = tables.AppendLine();
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\emitarm64");
        File.WriteAllText(@"Outputs\jit\emitarm64\Emitter.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM64
{{tables}}#endif
}
""");
    }

    private static void GenerateArmInstructionOpcodes()
    {
        const string inputFile = @"Inputs\instrsarm.h";
        ReadOnlySpan<string> prefixes = [
            "INST1(", "INST2(", "INST3(", "INST4(", "INST5(", "INST6(", "INST8(", "INST9(",
        ];
        var tables = new StringBuilder();

        for (var encoding = 1; encoding <= 9; encoding++)
        {
            var previousArity = 9;
            var entries = ProcessMacroBasedFile(inputFile, prefixes, (builder, file, line, prefix, parts) =>
            {
                var arity = int.Parse(prefix.AsSpan(4, prefix.Length - 5), NumberStyles.None,
                    CultureInfo.InvariantCulture);
                var arguments = SplitOpcodeArguments(line);
                if (arguments.Length != arity + 5)
                {
                    throw new InvalidDataException($"Invalid ARM opcode table entry: '{line}'");
                }

                // Filtering must leave an instruction-order prefix, since the native
                // selector indexes every encoding array directly by instruction.
                if (arity > previousArity)
                {
                    throw new InvalidDataException($"ARM opcode arity is not descending: '{line}'");
                }
                previousArity = arity;

                if (arity >= encoding)
                {
                    var opcode = arguments[4 + encoding];
                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"        unchecked((uint)({opcode})), // INS_{arguments[0]}");
                }
            });

            _ = tables.AppendLine(CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<uint> ordinaryInsCodes{encoding} => [");
            _ = tables.Append(entries);
            _ = tables.AppendLine("    ];");
            _ = tables.AppendLine();
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\emitarm");
        File.WriteAllText(@"Outputs\jit\emitarm\Emitter.InstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
{{tables}}#endif
}
""");
    }

        private static void GenerateSveInstructionOpcodes()
    {
        const string inputFile = @"Inputs\instrsarm64sve.h";
        ReadOnlySpan<string> prefixes = [
            "INST1(", "INST2(", "INST3(", "INST4(", "INST5(", "INST6(",
            "INST7(", "INST8(", "INST9(", "INST11(", "INST13(",
        ];
        var tables = new StringBuilder();

        for (var encoding = 1; encoding <= 13; encoding++)
        {
            var previousArity = 13;
            var entries = ProcessMacroBasedFile(inputFile, prefixes, (builder, file, line, prefix, parts) =>
            {
                var arity = int.Parse(prefix.AsSpan(4, prefix.Length - 5), NumberStyles.None,
                    CultureInfo.InvariantCulture);
                var arguments = SplitOpcodeArguments(line);
                if (arguments.Length != arity + 4)
                {
                    throw new InvalidDataException($"Invalid SVE opcode table entry: '{line}'");
                }

                // Each native encoding array is an instruction-order prefix. Increasing
                // arity would break indexing by ins - INS_sve_invalid after filtering.
                if (arity > previousArity)
                {
                    throw new InvalidDataException($"SVE opcode arity is not descending: '{line}'");
                }
                previousArity = arity;

                if (arity >= encoding)
                {
                    var opcode = arguments[3 + encoding];
                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"        unchecked((uint)({opcode})), // INS_sve_{arguments[0]}");
                }
            });

            _ = tables.AppendLine(CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<uint> insCodes{encoding} => [");
            _ = tables.Append(entries);
            _ = tables.AppendLine("    ];");
            _ = tables.AppendLine();
        }

        _ = Directory.CreateDirectory(@"Outputs\jit\emitarm64");
        File.WriteAllText(@"Outputs\jit\emitarm64\Emitter.SveInstructionOpcodes.generated.cs", $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM64
{{tables}}#endif
}
""");
    }

    private static string[] SplitOpcodeArguments(string invocation)
    {
        var arguments = new List<string>();
        var start = invocation.IndexOf('(', StringComparison.Ordinal) + 1;
        var depth = 0;
        var quoted = false;

        for (var index = start; index < invocation.Length; index++)
        {
            var c = invocation[index];
            if ((c == '"') && ((index == 0) || (invocation[index - 1] != '\\')))
            {
                quoted = !quoted;
            }
            if (quoted)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if ((c == ')') && (depth-- == 0))
            {
                arguments.Add(invocation[start..index].Trim());
                return [.. arguments];
            }
            else if ((c == ',') && (depth == 0))
            {
                arguments.Add(invocation[start..index].Trim());
                start = index + 1;
            }
        }

        throw new InvalidDataException($"Unterminated opcode macro: '{invocation}'");
    }

    private static string ExpandOpcode(string expression,
        Dictionary<string, (string[] Parameters, string Body)> macros)
    {
        expression = expression.Trim();
        if (Regex.IsMatch(expression, @"^(0[xX][0-9a-fA-F]+|[0-9]+)$"))
        {
            return expression;
        }
        if ((expression == "BAD_CODE") || expression.StartsWith('(', StringComparison.Ordinal))
        {
            return expression;
        }

        var opening = expression.IndexOf('(', StringComparison.Ordinal);
        if ((opening < 0) || !macros.TryGetValue(expression[..opening], out var macro))
        {
            throw new InvalidDataException($"Unknown opcode expression: '{expression}'");
        }

        var arguments = SplitOpcodeArguments(expression);
        if (arguments.Length != macro.Parameters.Length)
        {
            throw new InvalidDataException($"Invalid opcode macro arguments: '{expression}'");
        }

        var body = macro.Body;
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = ExpandOpcode(arguments[index], macros);
            body = Regex.Replace(body, $@"\b{Regex.Escape(macro.Parameters[index])}\b", _ => $"({argument})");
        }

        return ExpandOpcode(body, macros);
    }

    private static StringBuilder ReadNativeEnumBody(string inputFile, string enumName)
    {
        var input = File.ReadAllText(inputFile);
        var match = Regex.Match(input, $@"enum\s+{Regex.Escape(enumName)}\s*\{{(?<body>.*?)\}};", RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidDataException($"Could not find enum '{enumName}' in '{inputFile}'.");
        }

        var builder = new StringBuilder();
        foreach (var line in match.Groups["body"].Value.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    {line.Trim()}");
            }
        }

        return builder;
    }

    private static void GenerateNativeEnum(string inputFile, string enumName, string outputFile, bool isFlags)
    {
        var builder = ReadNativeEnumBody(inputFile, enumName);
        File.WriteAllText(outputFile, $$"""
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.{{enumName}};
{{(isFlags ? "\nusing System;\n" : "")}}
namespace RyuJitSharp;

{{(isFlags ? "[Flags]\n" : "")}}public enum {{enumName}}
{
{{builder}}}
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

        string[] prefixes = [
            "CONFIG_INTEGER(", "CONFIG_STRING(", "CONFIG_METHODSET(",
            "OPT_CONFIG_INTEGER(", "OPT_CONFIG_STRING(", "OPT_CONFIG_METHODSET(",
            "RELEASE_CONFIG_INTEGER(", "RELEASE_CONFIG_STRING(", "RELEASE_CONFIG_METHODSET("
        ];

        var lastFlags = FLAG_NONE;
        var inputLines = File.ReadAllLines(@"Inputs\jitconfigvalues.h");

        var fieldBuilder = ProcessConfigValues((builder, inputFile, line, prefix, parts) => {
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

        var propertyBuilder = ProcessConfigValues((builder, inputFile, line, prefix, parts) => {
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

        var destroyBuilder = ProcessConfigValues((builder, inputFile, line, prefix, parts) => {
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

        var initializeBuilder = ProcessConfigValues((builder, inputFile, line, prefix, parts) => {
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

                if (name.Equals("jitAlignLoops", StringComparison.Ordinal))
                {
                    _ = builder.AppendLine("        if (Configuration.FEATURE_LOOP_ALIGN)");
                    _ = builder.AppendLine("        {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"            _{name} = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference({key}u8))), {value});");
                    _ = builder.AppendLine("        }");
                    _ = builder.AppendLine("        else");
                    _ = builder.AppendLine("        {");
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            _{name} = 0;");
                    _ = builder.AppendLine("        }");
                }
                else if (value.Equals("0xffffffff", StringComparison.OrdinalIgnoreCase))
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

        StringBuilder ProcessConfigValues(Action<StringBuilder, string, string, string, string[]> callback)
        {
            return ProcessMacroBasedFile(
                @"Inputs\jitconfigvalues.h", inputLines, prefixes, callback,
                ResetConditionalConfigScope, ResetConditionalConfigScope);
        }

        void ResetConditionalConfigScope(StringBuilder builder)
        {
            // Config guards must not span preprocessor regions emitted by the input parser.
            EmitIfdefWhereRequired(builder, FLAG_NONE, lastFlags);
            lastFlags = FLAG_NONE;
        }

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
            var type = parts[1].AsSpan().Trim();
            var value = type.Equals("double", StringComparison.Ordinal)
                ? $"formatFloat({name}, \"F6\")"
                : name.ToString();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        jitprintf($\"{{nameof({name})}}{{new string(' ', NameMaxWidth + 5 - {name.Length})}}: {{{value}}}\\n\");");
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
        var builder = ProcessHWIntrinsicListUnion((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 16 and not 18)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var isa = parts[0].AsSpan().Trim();
            var name = parts[1].AsSpan().Trim();

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    NI_{isa}_{name},");
        });

        var input = File.ReadAllText(@"Inputs\namedintrinsiclist.h").ReplaceLineEndings("\n");
        var declarations = Regex.Matches(input, @"enum NamedIntrinsic : unsigned short\s*\{(?<body>.*?)\};", RegexOptions.Singleline);

        if (declarations.Count != 1)
        {
            throw new InvalidDataException("Expected one NamedIntrinsic enum in namedintrinsiclist.h.");
        }

        var body = declarations[0].Groups["body"].Value;
        var hardwareDeclaration = new Regex(
            @"^#define HARDWARE_INTRINSIC\([^\n]+\) \\\n\s*NI_##isa##_##name,\n#include ""hwintrinsiclist\.h""",
            RegexOptions.Multiline);

        if (hardwareDeclaration.Count(body) != 1)
        {
            throw new InvalidDataException("Expected one hardware intrinsic expansion in namedintrinsiclist.h.");
        }

        body = hardwareDeclaration.Replace(body, _ => builder.ToString().TrimEnd());
        body = Regex.Replace(body, @"^#if defined\((FEATURE_HW_INTRINSICS|FEATURE_SIMD)\)", "#if $1", RegexOptions.Multiline);
        body = body.Trim('\n').ReplaceLineEndings();

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
{{body}}
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

            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        Phases.{enumName} => {stringName},");
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
    private static string GetName(Phases phase) => phase switch
    {
{{namesBuilder}}        _ => throw new FatalJitException("Invalid compiler phase."),
    };
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
        var targetQualifiedBuilder = ProcessTargetQualifiedRegisterMasks();

        _ = Directory.CreateDirectory(@"Outputs\jit\target");

        File.WriteAllText(@"Outputs\jit\target\regMask.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.regMask;

namespace RyuJitSharp;

public enum regMask : long
{
    SRBM_NONE = 0,
{{targetQualifiedBuilder}}
{{builder}}}
""");
    }

    private static StringBuilder ProcessTargetQualifiedRegisterMasks()
    {
        var builder = new StringBuilder();
        var prefixes = new[] { "REGDEF(", "REGALIAS(" };

        foreach (var (target, inputFile) in s_registerTargetHeaders)
        {
            var registerBases = ReadRegisterBaseValues(inputFile);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    SRBM_NONE_{target} = 0,");

            var targetBuilder = ProcessMacroBasedFileUnion(inputFile, prefixes, (output, sourceFile, line, prefix, parts) => {
                if (prefix is "REGALIAS(")
                {
                    if (parts.Length != 2)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var name = parts[0].AsSpan().Trim();
                    var realName = parts[1].AsSpan().Trim();
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    SRBM_{name}_{target} = SRBM_{realName}_{target},");
                }
                else
                {
                    if (parts.Length is not 4 and not 5)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var name = parts[0].AsSpan().Trim();
                    var mask = ParseRegisterMask(parts[2].AsSpan().Trim(), registerBases, target, inputFile);
                    var maskValue = mask is long.MinValue
                        ? "long.MinValue"
                        : mask.ToString(CultureInfo.InvariantCulture);
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    SRBM_{name}_{target} = {maskValue},");
                }
            });

            _ = builder.Append(targetBuilder);
        }

        return builder;
    }

    private static long ParseRegisterMask(
        ReadOnlySpan<char> expression, Dictionary<string, int> registerBases, string target, string inputFile)
    {
        var mask = expression.ToString();
        if (mask.StartsWith("0x", StringComparison.Ordinal))
        {
            if (!ulong.TryParse(mask.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidDataException($"Invalid register mask '{mask}' in '{inputFile}'.");
            }

            return unchecked((long)value);
        }

        if (long.TryParse(mask, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
        {
            return literal;
        }

        var openParenthesis = mask.IndexOf('(', StringComparison.Ordinal);
        var closeParenthesis = mask.LastIndexOf(')', StringComparison.Ordinal);
        if (openParenthesis < 0 || closeParenthesis <= openParenthesis ||
            !int.TryParse(mask.AsSpan(openParenthesis + 1, closeParenthesis - openParenthesis - 1).Trim(),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
        {
            throw new InvalidDataException($"Invalid register mask '{mask}' in '{inputFile}'.");
        }

        var baseName = mask[..openParenthesis] switch {
            "GPRMASK" or "RMASK" or "PMASK" => null,
            "XMMMASK" => "XMMBASE",
            "KMASK" when target is "X86" => "KBASE",
            "KMASK" => null,
            "VFPMASK" => "FPBASE",
            "VMASK" => "VBASE",
            "FMASK" => "FBASE",
            _ => throw new InvalidDataException($"Unsupported register mask '{mask}' in '{inputFile}'."),
        };

        var bit = offset;
        if (baseName is not null)
        {
            if (!registerBases.TryGetValue(baseName, out var registerBase))
            {
                throw new InvalidDataException($"Missing register mask base '{baseName}' in '{inputFile}'.");
            }

            bit = checked(bit + registerBase);
        }

        if ((uint)bit >= 64)
        {
            throw new InvalidDataException($"Register mask bit '{bit}' is outside the 64-bit mask in '{inputFile}'.");
        }

        return 1L << bit;
    }

    private static void GenerateRegNumber()
    {
        var targetQualifiedBuilder = ProcessTargetQualifiedRegisterNumbers();

        var builder = ProcessRegister((builder, inputFile, line, prefix, parts) => {
            if (parts.Length is not 2 and not 4 and not 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            if (name.SequenceEqual("FFR"))
            {
                return;
            }

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

public enum regNumber : uint
{
    // Target-qualified identities preserve each target's native register ordinals.
{{targetQualifiedBuilder}}
{{builder}}    REG_COUNT,

    REG_NA = REG_COUNT,

    // everything but REG_STK (only real regs)
    ACTUAL_REG_COUNT = REG_COUNT - 1,

    // registerarm64.h defines FFR as 1 + NBASE (80); keep the alias after the count sentinels.
    REG_FFR = 81,
}
""");
    }

    private static StringBuilder ProcessTargetQualifiedRegisterNumbers()
    {
        var builder = new StringBuilder();
        var prefixes = new[] { "REGDEF(", "REGALIAS(" };

        foreach (var (target, inputFile) in s_registerTargetHeaders)
        {
            var registerBases = ReadRegisterBaseValues(inputFile);
            var stackRegister = -1;

            var targetBuilder = ProcessMacroBasedFileUnion(inputFile, prefixes, (output, _, line, _, parts) => {
                if (parts.Length is not 2 and not 4 and not 5)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var name = parts[0].AsSpan().Trim().ToString();

                if (parts.Length is 2)
                {
                    var realName = parts[1].AsSpan().Trim().ToString();
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    REG_{name}_{target} = REG_{realName}_{target},");
                }
                else
                {
                    var registerNumber = ParseRegisterOrdinal(parts[1].AsSpan().Trim(), registerBases, inputFile);
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    REG_{name}_{target} = {registerNumber},");

                    if (name.Equals("STK", StringComparison.Ordinal))
                    {
                        stackRegister = registerNumber;
                    }
                }
            });

            if (stackRegister < 0)
            {
                throw new InvalidDataException($"Register header '{inputFile}' does not define REG_STK.");
            }

            _ = builder.Append(targetBuilder);

            var registerCount = checked(stackRegister + 1);
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    REG_COUNT_{target} = {registerCount},");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    REG_NA_{target} = REG_COUNT_{target},");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    ACTUAL_REG_COUNT_{target} = REG_COUNT_{target} - 1,");
        }

        return builder;
    }

    private static Dictionary<string, int> ReadRegisterBaseValues(string inputFile)
    {
        var baseValues = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(inputFile))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if ((parts.Length is 3) &&
                parts[0].Equals("#define", StringComparison.Ordinal) &&
                parts[1].EndsWith("BASE", StringComparison.Ordinal) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                baseValues.Add(parts[1], value);
            }
        }

        return baseValues;
    }

    private static int ParseRegisterOrdinal(
        ReadOnlySpan<char> expression, Dictionary<string, int> baseValues, string inputFile)
    {
        var ordinal = expression.ToString();
        var terms = ordinal.Split('+', StringSplitOptions.TrimEntries);

        if ((terms.Length is 1) &&
            int.TryParse(terms[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if ((terms.Length is 2) &&
            int.TryParse(terms[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) &&
            baseValues.TryGetValue(terms[1], out var baseValue))
        {
            return checked(offset + baseValue);
        }

        throw new InvalidDataException($"Invalid register ordinal '{ordinal}' in '{inputFile}'.");
    }

    private static void GenerateRegNumberExtensions()
    {
        var namesBuilder = new StringBuilder();

        foreach (var (target, inputFile) in s_registerTargetHeaders)
        {
            if (target is "WASM")
            {
                continue;
            }

            var targetBuilder = ProcessMacroBasedFileUnion(inputFile, "REGDEF(", (output, _, line, _, parts) => {
                if (parts.Length is not 4 and not 5)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var name = parts[0].AsSpan().Trim();
                var sname = parts[3].AsSpan().Trim();

                _ = output.AppendLine(CultureInfo.InvariantCulture, $"        REG_{name}_{target} => {sname},");
            });

            _ = namesBuilder.AppendLine(CultureInfo.InvariantCulture, $"    private static string GetName{target}(regNumber regNum) => regNum switch");
            _ = namesBuilder.AppendLine("    {");
            _ = namesBuilder.Append(targetBuilder);
            _ = namesBuilder.AppendLine("        _ => throw new FatalJitException(\"Invalid register number.\"),");
            _ = namesBuilder.AppendLine("    };");
            _ = namesBuilder.AppendLine();
        }

        var masksBuilder = ProcessTargetQualifiedRegisterMaskTables();

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
{{namesBuilder}}

{{masksBuilder}}
}
""");
    }

    private static StringBuilder ProcessTargetQualifiedRegisterMaskTables()
    {
        var builder = new StringBuilder();

        foreach (var (target, inputFile) in s_registerTargetHeaders)
        {
            var registerBases = ReadRegisterBaseValues(inputFile);
            var registerMasks = new SortedDictionary<int, string>();
            var stackRegister = -1;

            _ = ProcessMacroBasedFileUnion(inputFile, "REGDEF(", (_, _, line, _, parts) => {
                if (parts.Length is not 4 and not 5)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var name = parts[0].AsSpan().Trim().ToString();
                var registerNumber = ParseRegisterOrdinal(parts[1].AsSpan().Trim(), registerBases, inputFile);
                if (!registerMasks.TryAdd(registerNumber, name))
                {
                    throw new InvalidDataException(
                        $"Duplicate register ordinal '{registerNumber}' in '{inputFile}'.");
                }

                if (name.Equals("STK", StringComparison.Ordinal))
                {
                    stackRegister = registerNumber;
                }
            });

            if ((stackRegister < 0) || (registerMasks.Keys.Max() != stackRegister))
            {
                throw new InvalidDataException($"Register header '{inputFile}' does not end with REG_STK.");
            }

            _ = builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"    private static ReadOnlySpan<regMask> s_masks_{target} => [");

            for (var registerNumber = 0; registerNumber <= stackRegister; registerNumber++)
            {
                if (registerMasks.TryGetValue(registerNumber, out var name))
                {
                    _ = builder.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"        SRBM_{name}_{target}, // REG_{name}_{target}");
                }
                else
                {
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"        SRBM_NONE_{target}, // unused register ordinal {registerNumber}");
                }
            }

            _ = builder.AppendLine("    ];");
            _ = builder.AppendLine();
        }

        _ = builder.AppendLine("    private static ReadOnlySpan<regMask> s_masks");
        _ = builder.AppendLine("    {");
        _ = builder.AppendLine("        get");
        _ = builder.AppendLine("        {");

        foreach (var (target, _) in s_registerTargetHeaders)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            if (Configuration.TARGET_{target})");
            _ = builder.AppendLine("            {");
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                return s_masks_{target};");
            _ = builder.AppendLine("            }");
        }

        _ = builder.AppendLine("            throw new FatalJitException(\"Invalid target architecture.\");");
        _ = builder.AppendLine("        }");
        _ = builder.AppendLine("    }");

        return builder;
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
        var builder = ProcessTargetQualifiedRegisterMaskFactories();

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

    private static StringBuilder ProcessTargetQualifiedRegisterMaskFactories()
    {
        var builder = new StringBuilder();
        var targetRegisterNames = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var registerNameOrder = new List<string>();
        var prefixes = new[] { "REGDEF(", "REGALIAS(" };

        foreach (var (target, inputFile) in s_registerTargetHeaders)
        {
            var targetRegisterNameSet = new HashSet<string>(StringComparer.Ordinal);
            var targetBuilder = ProcessMacroBasedFileUnion(inputFile, prefixes, (output, sourceFile, line, prefix, parts) => {
                if (prefix is "REGALIAS(")
                {
                    if (parts.Length != 2)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }
                }
                else if (parts.Length is not 4 and not 5)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var name = parts[0].AsSpan().Trim().ToString();
                if (!targetRegisterNameSet.Add(name))
                {
                    throw new InvalidDataException($"Duplicate register mask '{name}' in '{inputFile}'.");
                }

                if (!targetRegisterNames.TryGetValue(name, out var targets))
                {
                    targets = [];
                    targetRegisterNames.Add(name, targets);
                    registerNameOrder.Add(name);
                }

                targets.Add(target);

                if (prefix is "REGALIAS(")
                {
                    var realName = parts[1].AsSpan().Trim();
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    public static regMaskTP RBM_{name}_{target} => RBM_{realName}_{target};");
                }
                else
                {
                    _ = output.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    public static regMaskTP RBM_{name}_{target} => regMaskTP.CreateFromRegNum(REG_{name}_{target}, SRBM_{name}_{target});");
                }
            });

            _ = builder.Append(targetBuilder);
        }

        foreach (var name in registerNameOrder)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    public static regMaskTP RBM_{name}");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        get");
            _ = builder.AppendLine("        {");

            foreach (var target in targetRegisterNames[name])
            {
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"            if (Configuration.TARGET_{target})");
                _ = builder.AppendLine("            {");
                _ = builder.AppendLine(CultureInfo.InvariantCulture, $"                return RBM_{name}_{target};");
                _ = builder.AppendLine("            }");
            }

            _ = builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"            throw new FatalJitException(\"Register mask '{name}' is not available for the configured target.\");");
            _ = builder.AppendLine("        }");
            _ = builder.AppendLine("    }");
        }

        return builder;
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
        var builder1 = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
            if (parts.Length != 5)
            {
                throw new InvalidDataException($"Invalid line format: '{line}'");
            }

            var name = parts[0].AsSpan().Trim();
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_{name},");
        });

        var builder2 = ProcessMacroBasedFile(@"Inputs\valuenumfuncs.h", "ValueNumFuncDef(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclist.h\"", StringComparison.Ordinal))
            {
                var hwiBuilder = ProcessHWIntrinsicListUnion((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length is not 16 and not 18)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();

                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    VNF_HWI_{isa}_{name},");
                }, ValidateDuplicateHardwareIntrinsicVNAttributes);

                _ = builder.Append(hwiBuilder);
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
        var builder1 = ProcessGenTreeOpDefinitions((builder, inputFile, line, prefix, parts) => {
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

            var arity = name.Equals("SELECT", StringComparison.Ordinal) ? 3
                : (flags.Contains("GTK_UNOP", StringComparison.Ordinal) ? 1 : 0)
                    | (flags.Contains("GTK_BINOP", StringComparison.Ordinal) ? 2 : 0);
            var attributes = EncodeValueNumAttributes(arity, commute.Equals("1", StringComparison.Ordinal), knownNonNull: false);
            attributes |= illegalAsVNFunc.Equals("1", StringComparison.Ordinal) ? (byte)1 : (byte)0;

            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (ValueNumStore.VNFOpAttrib)0x{attributes:X2}, // VNF_{name}");
        });

        var builder2 = ProcessMacroBasedFile(@"Inputs\valuenumfuncs.h", "ValueNumFuncDef(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclist.h\"", StringComparison.Ordinal))
            {
                var hwiBuilder = ProcessHWIntrinsicListUnion((builder, inputFile, line, prefix, parts) => {
                    if (parts.Length is not 16 and not 18)
                    {
                        throw new InvalidDataException($"Invalid line format: '{line}'");
                    }

                    var isa = parts[0].AsSpan().Trim();
                    var name = parts[1].AsSpan().Trim();
                    var numArgs = parts[3].AsSpan().Trim();
                    var flags = parts[(parts.Length is 18) ? 17 : 15].AsSpan().Trim();

                    var argumentCount = int.Parse(numArgs, CultureInfo.InvariantCulture);
                    var arity = argumentCount == -1 ? -1 : argumentCount + 1;
                    var commute = flags.Contains("HW_Flag_Commutative", StringComparison.Ordinal);
                    var attributes = EncodeValueNumAttributes(arity, commute, knownNonNull: false);

                    _ = builder.AppendLine(CultureInfo.InvariantCulture,
                        $"        (ValueNumStore.VNFOpAttrib)0x{attributes:X2}, // VNF_HWI_{isa}_{name}");
                }, ValidateDuplicateHardwareIntrinsicVNAttributes);

                _ = builder.Append(hwiBuilder);
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

            var attributes = EncodeValueNumAttributes(int.Parse(arity, CultureInfo.InvariantCulture),
                bool.Parse(commute), bool.Parse(knownNonNull));
            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        (ValueNumStore.VNFOpAttrib)0x{attributes:X2}, // VNF_{name}");
        });

        _ = Directory.CreateDirectory(@"Outputs\jit\valuenum");

        var validationBuilder = ProcessMacroBasedFile(@"Inputs\valuenumfuncs.h", "ValueNumFuncDef(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclist.h\"", StringComparison.Ordinal))
            {
                _ = builder.AppendLine("""
        if (Configuration.FEATURE_HW_INTRINSICS)
        {
            for (var tableIndex = 0; tableIndex < HWIntrinsicInfo.GetNativeTableEntryCount(); tableIndex++)
            {
                var id = HWIntrinsicInfo.GetIntrinsicIdForNativeTableIndex(tableIndex);
                var func = VNF_HWI_INTRINSIC_START + (id - NI_HW_INTRINSIC_START - 1);
                var numArgs = HWIntrinsicInfo.lookupNumArgs(id);
                ValueNumStore.SetValidationAttributes(attributes, func, numArgs == -1 ? -1 : numArgs + 1,
                    HWIntrinsicInfo.IsCommutative(id), knownNonNull: false);
            }
        }
""");
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
            _ = builder.AppendLine(CultureInfo.InvariantCulture,
                $"        ValueNumStore.SetValidationAttributes(attributes, VNF_{name}, {arity}, {commute}, {knownNonNull});");
        });

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

#if DEBUG
    internal static void InitializeValidationAttributes(Span<ValueNumStore.VNFOpAttrib> attributes)
    {
{{validationBuilder}}    }
#endif
}
""");
    }

    private static byte EncodeValueNumAttributes(int arity, bool commute, bool knownNonNull)
    {
        // Match ValueNumStore's VNFOA encoding: commute bit 1, arity bits 2-4, non-null bit 5.
        var attributes = (Math.Max(arity, 0) << 2) & 0x1C;
        attributes |= commute ? 0x02 : 0;
        attributes |= knownNonNull ? 0x20 : 0;
        return (byte)attributes;
    }

    private static void ProcessInstructionSetDesc()
    {
        var lines = File.ReadAllLines(@"Inputs\InstructionSetDesc.txt");

        var insSetBuilder = new StringBuilder();
        var insSetFlagsBuilder = new StringBuilder();
        var ensureValidBuilder = new StringBuilder();
        var insSetToStringBuilder = new StringBuilder();
        var nativeInstructionSetEnumBuilder = new StringBuilder();
        var fromR2RInsSetBuilders = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        var r2rInsSetBuilder = new StringBuilder();

        var previousTargetArchInsSetFlags = "";
        var previousTargetArchEnsureValid = "";
        var previousTargetArchR2RInsSet = "";

        var addedInsSet = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var managedInstructionSetOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var nativeInstructionSetOrdinals = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var definedArchitectures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var instructionSetsByArchitecture = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var vectorInstructionSets = new List<(string Architecture, string Name)>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(';', StringComparison.Ordinal))
            {
                continue;
            }

            var parts = trimmedLine.Split(',');
            var kind = parts[0].Trim();

            if (kind.Equals("definearch", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 6 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                if (!definedArchitectures.Add(parts[1].Trim()))
                {
                    throw new InvalidDataException($"Architecture defined more than once: '{line}'");
                }

                continue;
            }

            if (kind.Equals("copyinstructionsets", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 3 || !definedArchitectures.Contains(parts[1].Trim()) || !definedArchitectures.Contains(parts[2].Trim()))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                continue;
            }

            if (kind.Equals("instructionsetgroup", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 4 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[3]))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var architectures = parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (architectures.Length is 0)
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                foreach (var architecture in architectures)
                {
                    if (!definedArchitectures.Contains(architecture))
                    {
                        throw new InvalidDataException($"Undefined architecture in instruction-set group: '{line}'");
                    }
                }

                continue;
            }

            if (kind.Equals("vectorinstructionset", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 3 || !definedArchitectures.Contains(parts[1].Trim()) || string.IsNullOrWhiteSpace(parts[2]))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                vectorInstructionSets.Add((NormalizeTargetArch(parts[1].Trim()), parts[2].Trim()));
                continue;
            }

            if (kind.Equals("instructionset", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 7 || !definedArchitectures.Contains(parts[1].Trim()) || string.IsNullOrWhiteSpace(parts[5]))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var targetArch = NormalizeTargetArch(parts[1].Trim());
                var name = parts[5].Trim();

                if (!instructionSetsByArchitecture.TryGetValue(targetArch, out var instructionSets))
                {
                    instructionSets = [with(StringComparer.OrdinalIgnoreCase)];
                    instructionSetsByArchitecture[targetArch] = instructionSets;
                }

                _ = instructionSets.Add(name);

                if (!addedInsSet.TryGetValue(targetArch, out var addedInsSetForTargetArch))
                {
                    addedInsSetForTargetArch = [with(StringComparer.OrdinalIgnoreCase)];
                    addedInsSet[targetArch] = addedInsSetForTargetArch;
                }

                if (addedInsSetForTargetArch.Add(name))
                {
                    AddManagedInstructionSet(name);
                }

                _ = AddNativeInstructionSet(targetArch, name);

                // Prefer the canonical ReadyToRun name when the descriptor also provides an xarch alias.
                var r2rName = parts[3].Trim();

                if (string.IsNullOrEmpty(r2rName))
                {
                    r2rName = parts[2].Trim();
                }

                var r2rValue = parts[4].Trim();

                if (!string.IsNullOrEmpty(r2rName))
                {
                    if (!fromR2RInsSetBuilders.TryGetValue(targetArch, out var targetFromR2RInsSetBuilder))
                    {
                        targetFromR2RInsSetBuilder = new StringBuilder();
                        fromR2RInsSetBuilders.Add(targetArch, targetFromR2RInsSetBuilder);
                    }

                    _ = targetFromR2RInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"            READYTORUN_INSTRUCTION_{r2rName} => InstructionSet_{name},");

                    _ = AppendIfDefWhenRequired(r2rInsSetBuilder, ref previousTargetArchR2RInsSet, targetArch, allowElif: true);
                    _ = r2rInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"            READYTORUN_INSTRUCTION_{r2rName} = {r2rValue},");
                }
                continue;
            }

            if (kind.Equals("instructionset64bit", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 3 || !definedArchitectures.Contains(parts[1].Trim()) || string.IsNullOrWhiteSpace(parts[2]))
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
                var managedName = $"{name}_{targetArchSuffix}";
                AddManagedInstructionSet(managedName);
                _ = AddNativeInstructionSet(targetArchSuffix is "X64" ? "XARCH" : "ARM64", managedName);

                if (!AppendConfigurationWhenRequired(insSetFlagsBuilder, ref previousTargetArchInsSetFlags, targetArch, "        "))
                {
                    _ = insSetFlagsBuilder.AppendLine();
                }

                AppendIndentedLines(insSetFlagsBuilder, $$"""
        if (HasInstructionSet(InstructionSet_{{name}}))
        {
            AddInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}});
        }
""", "    ");

                if (!AppendConfigurationWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, targetArch, "            "))
                {
                    _ = ensureValidBuilder.AppendLine();
                }
                AppendIndentedLines(ensureValidBuilder, $$"""
            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}) && !resultFlags.HasInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}}))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}});
            }

            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}}) && !resultFlags.HasInstructionSet(InstructionSet_{{name}}))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}}_{{targetArchSuffix}});
            }
""", "    ");

                continue;
            }

            if (kind.Equals("implication", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 4 || !definedArchitectures.Contains(parts[1].Trim()) ||
                    string.IsNullOrWhiteSpace(parts[2]) || string.IsNullOrWhiteSpace(parts[3]))
                {
                    throw new InvalidDataException($"Invalid line format: '{line}'");
                }

                var targetArch = NormalizeTargetArch(parts[1].Trim());
                var name = parts[2].Trim();
                var dependency = parts[3].Trim();

                if (!AppendConfigurationWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, targetArch, "            "))
                {
                    _ = ensureValidBuilder.AppendLine();
                }

                AppendIndentedLines(ensureValidBuilder, $$"""
            if (resultFlags.HasInstructionSet(InstructionSet_{{name}}) && !resultFlags.HasInstructionSet(InstructionSet_{{dependency}}))
            {
                resultFlags.RemoveInstructionSet(InstructionSet_{{name}});
            }
""", "    ");

                continue;
            }

            throw new InvalidDataException($"Unsupported instruction-set descriptor '{kind}': '{line}'");
        }

        foreach (var (architecture, name) in vectorInstructionSets)
        {
            if (!instructionSetsByArchitecture.TryGetValue(architecture, out var instructionSets) || !instructionSets.Contains(name))
            {
                throw new InvalidDataException($"Vector instruction set '{name}' is not defined for '{architecture}'");
            }
        }

        _ = AppendConfigurationWhenRequired(insSetFlagsBuilder, ref previousTargetArchInsSetFlags, "", "        ");
        _ = AppendConfigurationWhenRequired(ensureValidBuilder, ref previousTargetArchEnsureValid, "", "            ");

        var fromR2RInsSetBuilder = new StringBuilder();

        string[] targetArchitectures = ["XARCH", "ARM64", "RISCV64", "WASM32"];

        foreach (var targetArch in targetArchitectures)
        {
            if (!fromR2RInsSetBuilders.TryGetValue(targetArch, out var targetBuilder))
            {
                continue;
            }

            _ = fromR2RInsSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"        if ({GetConfigurationTarget(targetArch)})");
            _ = fromR2RInsSetBuilder.AppendLine("        {");
            _ = fromR2RInsSetBuilder.AppendLine("            return r2rSet switch {");
            _ = fromR2RInsSetBuilder.Append(targetBuilder);
            _ = fromR2RInsSetBuilder.AppendLine("                _ => InstructionSet_ILLEGAL,");
            _ = fromR2RInsSetBuilder.AppendLine("            };");
            _ = fromR2RInsSetBuilder.AppendLine("        }");
            _ = fromR2RInsSetBuilder.AppendLine();
        }

        _ = fromR2RInsSetBuilder.AppendLine("        return InstructionSet_ILLEGAL;");

        var nativeInstructionSetMapBuilder = new StringBuilder();
        AppendNativeInstructionSetMap(nativeInstructionSetMapBuilder, "xarch32", managedInstructionSetOrdinals, nativeInstructionSetOrdinals, "XARCH", excludeX64: true);
        AppendNativeInstructionSetMap(nativeInstructionSetMapBuilder, "xarch64", managedInstructionSetOrdinals, nativeInstructionSetOrdinals, "XARCH", excludeX64: false);
        AppendNativeInstructionSetMap(nativeInstructionSetMapBuilder, "arm64", managedInstructionSetOrdinals, nativeInstructionSetOrdinals, "ARM64", excludeX64: false);
        AppendNativeInstructionSetMap(nativeInstructionSetMapBuilder, "riscv64", managedInstructionSetOrdinals, nativeInstructionSetOrdinals, "RISCV64", excludeX64: false);
        AppendNativeInstructionSetMap(nativeInstructionSetMapBuilder, "wasm", managedInstructionSetOrdinals, nativeInstructionSetOrdinals, "WASM32", excludeX64: false);

        _ = Directory.CreateDirectory(@"Outputs\inc\corinfoinstructionset");

        File.WriteAllText(@"Outputs\inc\corinfoinstructionset\CORINFO_InstructionSet.generated.cs", $$"""
// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.ManagedInstructionSet;

namespace RyuJitSharp;

public enum ManagedInstructionSet
{
    InstructionSet_ILLEGAL = 0,

{{insSetBuilder}}

    InstructionSet_Vector = 126,
    InstructionSet_NONE = 127,
}

// Target-prefixed aliases retain the native ordinals sent across the EE ABI.
public enum CORINFO_InstructionSet : int
{
    Illegal = 0,
{{nativeInstructionSetEnumBuilder}}
    Vector = 126,
    None = 127,
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

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    // byte.MaxValue marks a managed identity that has no ordinal for that target.
{{nativeInstructionSetMapBuilder}}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> GetNativeInstructionSetOrdinals()
    {
        if (Configuration.TARGET_XARCH)
        {
            return Configuration.TARGET_64BIT ? s_xarch64NativeInstructionSetOrdinals : s_xarch32NativeInstructionSetOrdinals;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_arm64NativeInstructionSetOrdinals;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return s_riscv64NativeInstructionSetOrdinals;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_wasmNativeInstructionSetOrdinals;
        }

        return ReadOnlySpan<byte>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> GetManagedInstructionSetOrdinals()
    {
        if (Configuration.TARGET_XARCH)
        {
            return Configuration.TARGET_64BIT ? s_xarch64ManagedInstructionSetOrdinals : s_xarch32ManagedInstructionSetOrdinals;
        }

        if (Configuration.TARGET_ARM64)
        {
            return s_arm64ManagedInstructionSetOrdinals;
        }

        if (Configuration.TARGET_RISCV64)
        {
            return s_riscv64ManagedInstructionSetOrdinals;
        }

        if (Configuration.TARGET_WASM)
        {
            return s_wasmManagedInstructionSetOrdinals;
        }

        return ReadOnlySpan<byte>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetNativeInstructionSetOrdinal(ManagedInstructionSet instructionSet, out int nativeOrdinal)
    {
        if (instructionSet is InstructionSet_ILLEGAL or InstructionSet_Vector or InstructionSet_NONE)
        {
            nativeOrdinal = (int)instructionSet;
            return true;
        }

        var managedOrdinal = (int)instructionSet;
        var nativeOrdinals = GetNativeInstructionSetOrdinals();

        if (managedOrdinal >= 0 && managedOrdinal < nativeOrdinals.Length)
        {
            var value = GetRvaSpanEntry(nativeOrdinals, managedOrdinal);

            if (value != byte.MaxValue)
            {
                nativeOrdinal = value;
                return true;
            }
        }

        nativeOrdinal = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNativeInstructionSetOrdinal(ManagedInstructionSet instructionSet)
    {
        if (TryGetNativeInstructionSetOrdinal(instructionSet, out var nativeOrdinal))
        {
            return nativeOrdinal;
        }

        throw new PlatformNotSupportedException($"Instruction set '{InstructionSetToString(instructionSet)}' is unavailable for this target.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetManagedInstructionSet(int nativeOrdinal, out ManagedInstructionSet instructionSet)
    {
        if (nativeOrdinal is 0 or 126 or 127)
        {
            instructionSet = (ManagedInstructionSet)nativeOrdinal;
            return true;
        }

        var managedOrdinals = GetManagedInstructionSetOrdinals();

        if (nativeOrdinal >= 0 && nativeOrdinal < managedOrdinals.Length)
        {
            var value = GetRvaSpanEntry(managedOrdinals, nativeOrdinal);

            if (value != byte.MaxValue)
            {
                instructionSet = (ManagedInstructionSet)value;
                return true;
            }
        }

        instructionSet = InstructionSet_ILLEGAL;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CORINFO_InstructionSet GetNativeInstructionSet(ManagedInstructionSet instructionSet)
        => (CORINFO_InstructionSet)GetNativeInstructionSetOrdinal(instructionSet);

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
    public static string InstructionSetToString(ManagedInstructionSet instructionSet)
    {
        return instructionSet switch {
{{insSetToStringBuilder}}

            _ => "UnknownInstructionSet",
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManagedInstructionSet InstructionSetFromR2RInstructionSet(ReadyToRunInstructionSet r2rSet)
    {
{{fromR2RInsSetBuilder}}
    }
}
""");

        void AddManagedInstructionSet(string name)
        {
            if (managedInstructionSetOrdinals.ContainsKey(name))
            {
                return;
            }

            if (managedInstructionSetOrdinals.Count >= 125)
            {
                throw new InvalidDataException("The managed instruction-set union exceeds the available CORINFO_InstructionSet values.");
            }

            var ordinal = managedInstructionSetOrdinals.Count + 1;
            managedInstructionSetOrdinals.Add(name, ordinal);
            _ = insSetBuilder.AppendLine(CultureInfo.InvariantCulture, $"    InstructionSet_{name} = {ordinal},");
            _ = insSetToStringBuilder.AppendLine(CultureInfo.InvariantCulture, $"            InstructionSet_{name} => \"{name}\",");
        }

        int AddNativeInstructionSet(string targetArch, string name)
        {
            if (!nativeInstructionSetOrdinals.TryGetValue(targetArch, out var nativeOrdinals))
            {
                nativeOrdinals = CreateNativeInstructionSetOrdinals();
                nativeInstructionSetOrdinals.Add(targetArch, nativeOrdinals);
            }

            if (nativeOrdinals.TryGetValue(name, out var existingOrdinal))
            {
                return existingOrdinal;
            }

            if (nativeOrdinals.Count >= 125)
            {
                throw new InvalidDataException($"Native instruction-set ordinals exceed the available CORINFO_InstructionSet values for '{targetArch}'.");
            }

            var ordinal = nativeOrdinals.Count + 1;
            nativeOrdinals.Add(name, ordinal);
            _ = nativeInstructionSetEnumBuilder.AppendLine(CultureInfo.InvariantCulture,
                $"    InstructionSet_{targetArch}_{name} = {ordinal},");
            return ordinal;
        }

        static Dictionary<string, int> CreateNativeInstructionSetOrdinals()
        {
            return new Dictionary<string, int>(capacity: 0, comparer: StringComparer.OrdinalIgnoreCase);
        }

        void AppendNativeInstructionSetMap(
            StringBuilder builder,
            string targetName,
            Dictionary<string, int> managedOrdinals,
            Dictionary<string, Dictionary<string, int>> nativeOrdinals,
            string targetArch,
            bool excludeX64)
        {
            var managedToNative = Enumerable.Repeat(byte.MaxValue, managedOrdinals.Count + 1).ToArray();
            managedToNative[0] = 0;

            var nativeToManaged = Enumerable.Repeat(byte.MaxValue, 128).ToArray();
            nativeToManaged[0] = 0;
            nativeToManaged[126] = 126;
            nativeToManaged[127] = 127;

            if (nativeOrdinals.TryGetValue(targetArch, out var targetOrdinals))
            {
                foreach (var (name, nativeOrdinal) in targetOrdinals)
                {
                    if (excludeX64 && name.EndsWith("_X64", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var managedOrdinal = managedOrdinals[name];
                    managedToNative[managedOrdinal] = (byte)nativeOrdinal;
                    nativeToManaged[nativeOrdinal] = (byte)managedOrdinal;
                }
            }

            AppendByteMap(builder, $"s_{targetName}NativeInstructionSetOrdinals", managedToNative);
            AppendByteMap(builder, $"s_{targetName}ManagedInstructionSetOrdinals", nativeToManaged);
        }

        void AppendByteMap(StringBuilder builder, string name, byte[] values)
        {
            _ = builder.AppendLine(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<byte> {name} => [");

            for (var i = 0; i < values.Length; i += 16)
            {
                var count = Math.Min(16, values.Length - i);
                _ = builder.Append("        ");

                for (var j = 0; j < count; j++)
                {
                    if (j > 0)
                    {
                        _ = builder.Append(", ");
                    }

                    var value = values[i + j];
                    _ = builder.Append(value == byte.MaxValue ? "byte.MaxValue" : value.ToString(CultureInfo.InvariantCulture));
                }

                _ = builder.AppendLine(",");
            }

            _ = builder.AppendLine("    ];");
            _ = builder.AppendLine();
        }

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

        static bool AppendConfigurationWhenRequired(StringBuilder builder, ref string previousTargetArch, string targetArch, string indent)
        {
            var appended = false;

            if (!targetArch.Equals(previousTargetArch, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(previousTargetArch))
                {
                    _ = builder.Append(indent);
                    _ = builder.AppendLine("}");
                    _ = builder.AppendLine();
                }

                if (!string.IsNullOrEmpty(targetArch))
                {
                    _ = builder.Append(indent);
                    _ = builder.AppendLine(CultureInfo.InvariantCulture, $"if ({GetConfigurationTarget(targetArch)})");
                    _ = builder.Append(indent);
                    _ = builder.AppendLine("{");
                }

                previousTargetArch = targetArch;
                appended = true;
            }

            return appended;
        }

        static string GetConfigurationTarget(string targetArch)
        {
            return targetArch switch
            {
                "AMD64" => "Configuration.TARGET_XARCH && Configuration.TARGET_64BIT",
                "XARCH" => "Configuration.TARGET_XARCH",
                "ARM64" => "Configuration.TARGET_ARM64",
                "RISCV64" => "Configuration.TARGET_RISCV64",
                "WASM32" => "Configuration.TARGET_WASM",
                _ => throw new InvalidDataException($"Unsupported instruction-set target '{targetArch}'."),
            };
        }

        static void AppendIndentedLines(StringBuilder builder, string lines, string indent)
        {
            var normalizedLines = lines.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n');

            foreach (var line in normalizedLines.Split('\n'))
            {
                if (line.Length > 0)
                {
                    _ = builder.Append(indent);
                }

                _ = builder.AppendLine(line.TrimEnd('\r'));
            }
        }

        static string NormalizeTargetArch(string targetArch)
        {
            var normalizedTargetArch = targetArch.ToUpperInvariant();

            if (normalizedTargetArch is "X86" or "X64")
            {
                return "XARCH";
            }

            return normalizedTargetArch;
        }
    }

    private static StringBuilder ProcessHWIntrinsicList(Action<StringBuilder, string, string, string, string[]> callback)
        => ProcessMacroBasedFile(@"Inputs\hwintrinsiclist.h", "HARDWARE_INTRINSIC(", (builder, inputFile, line, prefix, parts) => {
            if (line.Equals("#include \"hwintrinsiclistxarch.h\"", StringComparison.Ordinal))
            {
                var xarchBuilder = ProcessHWIntrinsicListXarch(callback);
                _ = builder.Append(xarchBuilder);
            }
            else if (line.Equals("#include \"hwintrinsiclistarm64.h\"", StringComparison.Ordinal))
            {
                var arm64Builder = ProcessHWIntrinsicListArm64(callback);
                _ = builder.Append(arm64Builder);
            }
            else if (line.Equals("#include \"hwintrinsiclistwasm.h\"", StringComparison.Ordinal))
            {
                var wasmBuilder = ProcessMacroBasedFile(@"Inputs\hwintrinsiclistwasm.h", "HARDWARE_INTRINSIC(", callback);
                _ = builder.Append(wasmBuilder);
            }
            else
            {
                callback(builder, inputFile, line, prefix, parts);
            }
        });

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

    private static StringBuilder ProcessHWIntrinsicListUnion(
        Action<StringBuilder, string, string, string, string[]> callback,
        Action<string[], string[]>? onDuplicate = null)
    {
        var seen = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var builder = new StringBuilder();

        foreach (var target in new[] { "XARCH", "ARM64", "WASM" })
        {
            _ = builder.Append(ProcessTargetHardwareIntrinsicList(target,
                (targetBuilder, inputFile, line, prefix, parts) =>
            {
                var identity = GetHardwareIntrinsicIdentity(parts);

                if (seen.TryGetValue(identity, out var previous))
                {
                    onDuplicate?.Invoke(previous, parts);
                    return;
                }

                seen.Add(identity, parts);
                callback(targetBuilder, inputFile, line, prefix, parts);
            }));
        }

        return builder;
    }

    private static void ValidateDuplicateHardwareIntrinsicVNAttributes(string[] previous, string[] duplicate)
    {
        var previousNumArgs = previous[3].Trim();
        var duplicateNumArgs = duplicate[3].Trim();
        var previousFlags = previous[(previous.Length is 18) ? 17 : 15].Trim();
        var duplicateFlags = duplicate[(duplicate.Length is 18) ? 17 : 15].Trim();
        var previousCommutative = previousFlags.Contains("HW_Flag_Commutative", StringComparison.Ordinal);
        var duplicateCommutative = duplicateFlags.Contains("HW_Flag_Commutative", StringComparison.Ordinal);

        if (!previousNumArgs.Equals(duplicateNumArgs, StringComparison.Ordinal) ||
            previousCommutative != duplicateCommutative)
        {
            var identity = GetHardwareIntrinsicIdentity(previous);
            throw new InvalidDataException($"Hardware intrinsic VN attributes differ between target rows for '{identity}'.");
        }
    }

    private static StringBuilder ProcessMacroBasedFileUnion(
        string inputFile, string prefix, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var lines = File.ReadAllLines(inputFile);
        return ProcessMacroBasedFile(inputFile, lines, [prefix], callback, includeAllConditionalBranches: true);
    }

    private static StringBuilder ProcessMacroBasedFileUnion(
        string inputFile, ReadOnlySpan<string> prefixes, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var lines = File.ReadAllLines(inputFile);
        return ProcessMacroBasedFile(inputFile, lines, prefixes, callback, includeAllConditionalBranches: true);
    }

    private static StringBuilder ProcessMacroBasedFile(string inputFile, string prefix, Action<StringBuilder, string, string, string, string[]> callback)
        => ProcessMacroBasedFile(inputFile, [prefix], callback);

    private static StringBuilder ProcessMacroBasedFile(string inputFile, ReadOnlySpan<string> prefixes, Action<StringBuilder, string, string, string, string[]> callback)
    {
        var lines = File.ReadAllLines(inputFile);
        return ProcessMacroBasedFile(inputFile, lines, prefixes, callback);
    }

    private static StringBuilder ProcessMacroBasedFile(
        string inputFile, ReadOnlySpan<string> lines, ReadOnlySpan<string> prefixes,
        Action<StringBuilder, string, string, string, string[]> callback,
        Action<StringBuilder>? onConditionalRegionStart = null,
        Action<StringBuilder>? onConditionalRegionEnd = null,
        bool includeAllConditionalBranches = false)
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

                if (includeAllConditionalBranches)
                {
                    if (directive.Equals("#include ", StringComparison.Ordinal))
                    {
                        callback(builder, inputFile, line, "", []);
                    }
                    continue;
                }

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
                        var regionBuilder = new StringBuilder();
                        onConditionalRegionStart?.Invoke(regionBuilder);
                        _ = regionBuilder.Append(ProcessMacroBasedFile(
                            inputFile, lines[(regionStart + 1)..regionEnd], prefixes, callback,
                            onConditionalRegionStart, onConditionalRegionEnd));
                        onConditionalRegionEnd?.Invoke(regionBuilder);

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

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 && DEBUG
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    private const uint UFI_INITIALIZED_PATTERN = 0x0FACADE0;

    public void Dump(bool isHotCode, int indent = 0)
    {
        var fragmentCount = 0;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragmentCount++;
        }

        var prefix = Indent(indent);
        jitprintf($"{prefix}UnwindInfo{(isHotCode ? " " : " COLD ")}@{Identity(this)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: {Identity(m_compiler)}\n");
        jitprintf($"{prefix}  {fragmentCount} fragment{(fragmentCount != 1 ? "s" : "")}\n");
        jitprintf($"{prefix}  uwiFragmentLast: {Identity(uwiFragmentLast)}\n");
        jitprintf($"{prefix}  uwiEndLoc: {LocationState(uwiEndLoc)}\n");
        jitprintf($"{prefix}  uwiInitialized: 0x{(uwiInitialized ? UFI_INITIALIZED_PATTERN : 0u):x8}\n");

        var fragmentNumber = 0;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            DumpFragment(fragment, fragmentNumber++, indent + 2);
        }
    }

    private static void DumpFragment(UnwindFragmentInfo fragment, int fragmentNumber, int indent)
    {
        var prefix = Indent(indent);
        var epilogCount = 0;
        for (var epilog = fragment.ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
        {
            epilogCount++;
        }

        jitprintf($"{prefix}UnwindFragmentInfo #{fragmentNumber}, @{Identity(fragment)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: {Identity(fragment.m_compiler)}\n");
        jitprintf($"{prefix}  ufiNext: {Identity(fragment.ufiNext)}\n");
        jitprintf($"{prefix}  ufiEmitLoc: {LocationState(fragment.ufiEmitLoc)}\n");
        jitprintf($"{prefix}  ufiHasPhantomProlog: {dspBool(fragment.ufiHasPhantomProlog)}\n");
        jitprintf($"{prefix}  {epilogCount} epilog{(epilogCount != 1 ? "s" : "")}\n");
        jitprintf($"{prefix}  ufiEpilogList: {Identity(fragment.ufiEpilogList)}\n");
        jitprintf($"{prefix}  ufiEpilogLast: {Identity(fragment.ufiEpilogLast)}\n");
        jitprintf($"{prefix}  ufiCurCodes: {Identity(fragment.ufiCurCodes)}\n");
        jitprintf($"{prefix}  ufiSize: {fragment.ufiSize}\n");
        jitprintf($"{prefix}  ufiSetEBit: {dspBool(fragment.ufiSetEBit)}\n");
        jitprintf($"{prefix}  ufiNeedExtendedCodeWordsEpilogCount: " +
            $"{dspBool(fragment.ufiNeedExtendedCodeWordsEpilogCount)}\n");
        jitprintf($"{prefix}  ufiCodeWords: {fragment.ufiCodeWords}\n");
        jitprintf($"{prefix}  ufiEpilogScopes: {fragment.ufiEpilogScopes}\n");
        jitprintf($"{prefix}  ufiStartOffset: 0x{fragment.ufiStartOffset:x}\n");
        jitprintf($"{prefix}  ufiInProlog: {dspBool(fragment.ufiInProlog)}\n");
        // Managed fragment construction replaces the native sentinel field; retain its dump value.
        jitprintf($"{prefix}  ufiInitialized: 0x{UFI_INITIALIZED_PATTERN:x8}\n");

        DumpPrologCodes(fragment.ufiPrologCodes, indent + 2);
        for (var epilog = fragment.ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
        {
            DumpEpilogInfo(epilog, indent + 2);
        }
    }

    private static void DumpPrologCodes(UnwindPrologCodes codes, int indent)
    {
        var prefix = Indent(indent);
        jitprintf($"{prefix}UnwindPrologCodes @{Identity(codes)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: {Identity(codes.m_compiler)}\n");
        jitprintf($"{prefix}  &upcMemLocal[0]: managed buffer is upcMem\n");
        jitprintf($"{prefix}  upcMem: {Identity(codes.upcMem)}\n");
        jitprintf($"{prefix}  upcMemSize: {codes.upcMemSize}\n");
        jitprintf($"{prefix}  upcCodeSlot: {codes.upcCodeSlot}\n");
        jitprintf($"{prefix}  upcHeaderSlot: {codes.upcHeaderSlot}\n");
        jitprintf($"{prefix}  upcEpilogSlot: {codes.upcEpilogSlot}\n");
        jitprintf($"{prefix}  upcUnwindBlockSlot: {codes.upcUnwindBlockSlot}\n");

        if (codes.upcMemSize > 0)
        {
            jitprintf($"{prefix}  codes:");
            for (var index = 0; index < codes.upcMemSize; index++)
            {
                var marker = index == codes.upcCodeSlot ? " <-C"
                    : index == codes.upcHeaderSlot ? " <-H"
                    : index == codes.upcEpilogSlot ? " <-E"
                    : index == codes.upcUnwindBlockSlot ? " <-U"
                    : "";
                jitprintf($" {codes.upcMem[index]:x2}{marker}");
            }

            jitprintf("\n");
        }
    }

    private static void DumpEpilogCodes(UnwindEpilogCodes codes, int indent)
    {
        var prefix = Indent(indent);
        jitprintf($"{prefix}UnwindEpilogCodes @{Identity(codes)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: not stored\n");
        jitprintf($"{prefix}  &uecMemLocal[0]: managed buffer is uecMem\n");
        jitprintf($"{prefix}  uecMem: {Identity(codes.uecMem)}\n");
        jitprintf($"{prefix}  uecMemSize: {codes.uecMemSize}\n");
        jitprintf($"{prefix}  uecCodeSlot: {codes.uecCodeSlot}\n");
        jitprintf($"{prefix}  uecFinalized: {dspBool(codes.uecFinalized)}\n");

        if (codes.uecMemSize > 0)
        {
            jitprintf($"{prefix}  codes:");
            for (var index = 0; index < codes.uecMemSize; index++)
            {
                var marker = index == codes.uecCodeSlot ? " <-C" : "";
                jitprintf($" {codes.uecMem[index]:x2}{marker}");
            }

            jitprintf("\n");
        }
    }

    private static void DumpEpilogInfo(UnwindEpilogInfo epilog, int indent)
    {
        var prefix = Indent(indent);
        jitprintf($"{prefix}UnwindEpilogInfo @{Identity(epilog)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: {Identity(epilog.m_compiler)}\n");
        jitprintf($"{prefix}  epiNext: {Identity(epilog.epiNext)}\n");
        jitprintf($"{prefix}  epiEmitLocation: {LocationState(epilog.epiEmitLocation)}\n");
        jitprintf($"{prefix}  epiStartOffset: 0x{epilog.epiStartOffset:x}\n");
        jitprintf($"{prefix}  epiMatches: {dspBool(epilog.epiMatches)}\n");
        jitprintf($"{prefix}  epiStartIndex: {epilog.epiStartIndex}\n");
        DumpEpilogCodes(epilog.epiCodes, indent + 2);
    }

    private static string Indent(int indent)
    {
        assert(indent >= 0);
        return new string(' ', indent);
    }

    private static string Identity(object? value)
    {
        return value is null ? "null" : $"managed@{unchecked((uint)RuntimeHelpers.GetHashCode(value)):x8}";
    }

    private static string LocationState(emitLocation? location)
    {
        return location.HasValue ? "captured" : "null";
    }
}
#endif

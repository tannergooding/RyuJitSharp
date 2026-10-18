// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    private const uint UWI_LOONGARCH64_INITIALIZED_PATTERN = 0x0FACADE1;
    private const uint UFI_LOONGARCH64_INITIALIZED_PATTERN = 0x0FACADE0;

    [Conditional("DEBUG")]
    private void DumpLoongArch64(bool isHotCode, int indent)
    {
        var fragmentCount = 0;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragmentCount++;
        }

        var prefix = LoongArch64Indent(indent);
        jitprintf($"{prefix}UnwindInfo{(isHotCode ? " " : " COLD ")}@{LoongArch64Identity(this)}, size:managed:\n");
        jitprintf($"{prefix}  m_compiler: {LoongArch64Identity(m_compiler)}\n");
        jitprintf($"{prefix}  {fragmentCount} fragment{(fragmentCount != 1 ? "s" : "")}\n");
        jitprintf($"{prefix}  uwiFragmentLast: {LoongArch64Identity(uwiFragmentLast)}\n");
        jitprintf($"{prefix}  uwiEndLoc: {LoongArch64LocationState(uwiEndLoc)}\n");
        jitprintf($"{prefix}  uwiInitialized: 0x{(uwiInitialized ? UWI_LOONGARCH64_INITIALIZED_PATTERN : 0u):x8}\n");

        var fragmentNumber = 0;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.DumpLoongArch64(fragmentNumber++, indent + 2);
        }
    }

    private sealed partial class UnwindFragmentInfo
    {
        [Conditional("DEBUG")]
        public void DumpLoongArch64(int fragmentNumber, int indent)
        {
            var prefix = UnwindInfo.LoongArch64Indent(indent);
            var epilogCount = 0;
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilogCount++;
            }

            jitprintf($"{prefix}UnwindFragmentInfo #{fragmentNumber}, @{UnwindInfo.LoongArch64Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.LoongArch64Identity(m_compiler)}\n");
            jitprintf($"{prefix}  ufiNext: {UnwindInfo.LoongArch64Identity(ufiNext)}\n");
            jitprintf($"{prefix}  ufiEmitLoc: {UnwindInfo.LoongArch64LocationState(ufiEmitLoc)}\n");
            jitprintf($"{prefix}  ufiHasPhantomProlog: {dspBool(ufiHasPhantomProlog)}\n");
            jitprintf($"{prefix}  {epilogCount} epilog{(epilogCount != 1 ? "s" : "")}\n");
            jitprintf($"{prefix}  ufiEpilogList: {UnwindInfo.LoongArch64Identity(ufiEpilogList)}\n");
            jitprintf($"{prefix}  ufiEpilogLast: {UnwindInfo.LoongArch64Identity(ufiEpilogLast)}\n");
            jitprintf($"{prefix}  ufiCurCodes: {UnwindInfo.LoongArch64Identity(ufiCurCodes)}\n");
            jitprintf($"{prefix}  ufiSize: {ufiSize}\n");
            jitprintf($"{prefix}  ufiSetEBit: {dspBool(ufiSetEBit)}\n");
            jitprintf($"{prefix}  ufiNeedExtendedCodeWordsEpilogCount: " +
                $"{dspBool(ufiNeedExtendedCodeWordsEpilogCount)}\n");
            jitprintf($"{prefix}  ufiCodeWords: {ufiCodeWords}\n");
            jitprintf($"{prefix}  ufiEpilogScopes: {ufiEpilogScopes}\n");
            jitprintf($"{prefix}  ufiStartOffset: 0x{ufiStartOffset:x}\n");
            jitprintf($"{prefix}  ufiInProlog: {dspBool(ufiInProlog)}\n");
            jitprintf($"{prefix}  ufiInitialized: 0x{UnwindInfo.UFI_LOONGARCH64_INITIALIZED_PATTERN:x8}\n");

            ufiPrologCodes.DumpLoongArch64(indent + 2);
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilog.DumpLoongArch64(indent + 2);
            }
        }
    }

    private sealed partial class UnwindPrologCodes
    {
        [Conditional("DEBUG")]
        public void DumpLoongArch64(int indent)
        {
            var prefix = UnwindInfo.LoongArch64Indent(indent);
            jitprintf($"{prefix}UnwindPrologCodes @{UnwindInfo.LoongArch64Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.LoongArch64Identity(m_compiler)}\n");
            jitprintf($"{prefix}  &upcMemLocal[0]: managed buffer is upcMem\n");
            jitprintf($"{prefix}  upcMem: {UnwindInfo.LoongArch64Identity(upcMem)}\n");
            jitprintf($"{prefix}  upcMemSize: {upcMemSize}\n");
            jitprintf($"{prefix}  upcCodeSlot: {upcCodeSlot}\n");
            jitprintf($"{prefix}  upcHeaderSlot: {upcHeaderSlot}\n");
            jitprintf($"{prefix}  upcEpilogSlot: {upcEpilogSlot}\n");
            jitprintf($"{prefix}  upcUnwindBlockSlot: {upcUnwindBlockSlot}\n");

            if (upcMemSize > 0)
            {
                jitprintf($"{prefix}  codes:");
                for (var index = 0; index < upcMemSize; index++)
                {
                    var marker = index == upcCodeSlot ? " <-C"
                        : index == upcHeaderSlot ? " <-H"
                        : index == upcEpilogSlot ? " <-E"
                        : index == upcUnwindBlockSlot ? " <-U"
                        : "";
                    jitprintf($" {upcMem[index]:x2}{marker}");
                }

                jitprintf("\n");
            }
        }
    }

    private sealed partial class UnwindEpilogCodes
    {
        [Conditional("DEBUG")]
        public void DumpLoongArch64(int indent)
        {
            var prefix = UnwindInfo.LoongArch64Indent(indent);
            jitprintf($"{prefix}UnwindEpilogCodes @{UnwindInfo.LoongArch64Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: not stored\n");
            jitprintf($"{prefix}  &uecMemLocal[0]: managed buffer is uecMem\n");
            jitprintf($"{prefix}  uecMem: {UnwindInfo.LoongArch64Identity(uecMem)}\n");
            jitprintf($"{prefix}  uecMemSize: {uecMemSize}\n");
            jitprintf($"{prefix}  uecCodeSlot: {uecCodeSlot}\n");
            jitprintf($"{prefix}  uecFinalized: {dspBool(uecFinalized)}\n");

            if (uecMemSize > 0)
            {
                jitprintf($"{prefix}  codes:");
                for (var index = 0; index < uecMemSize; index++)
                {
                    var marker = index == uecCodeSlot ? " <-C" : "";
                    jitprintf($" {uecMem[index]:x2}{marker}");
                }

                jitprintf("\n");
            }
        }
    }

    private sealed partial class UnwindEpilogInfo
    {
        [Conditional("DEBUG")]
        public void DumpLoongArch64(int indent)
        {
            var prefix = UnwindInfo.LoongArch64Indent(indent);
            jitprintf($"{prefix}UnwindEpilogInfo @{UnwindInfo.LoongArch64Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.LoongArch64Identity(m_compiler)}\n");
            jitprintf($"{prefix}  epiNext: {UnwindInfo.LoongArch64Identity(epiNext)}\n");
            jitprintf($"{prefix}  epiEmitLocation: {UnwindInfo.LoongArch64LocationState(epiEmitLocation)}\n");
            jitprintf($"{prefix}  epiStartOffset: 0x{epiStartOffset:x}\n");
            jitprintf($"{prefix}  epiMatches: {dspBool(epiMatches)}\n");
            jitprintf($"{prefix}  epiStartIndex: {epiStartIndex}\n");
            epiCodes.DumpLoongArch64(indent + 2);
        }
    }

    private static string LoongArch64Indent(int indent)
    {
        assert(indent >= 0);
        return new string(' ', indent);
    }

    private static string LoongArch64Identity(object? value)
    {
        return value is null ? "null" : $"managed@{unchecked((uint)RuntimeHelpers.GetHashCode(value)):x8}";
    }

    private static string LoongArch64LocationState(emitLocation? location)
    {
        return location.HasValue ? "captured" : "null";
    }
}

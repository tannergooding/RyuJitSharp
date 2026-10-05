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
            fragment.Dump(fragmentNumber++, indent + 2);
        }
    }

    private sealed partial class UnwindFragmentInfo
    {
        public void Dump(int fragmentNumber, int indent)
        {
            var prefix = UnwindInfo.Indent(indent);
            var epilogCount = 0;
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilogCount++;
            }

            jitprintf($"{prefix}UnwindFragmentInfo #{fragmentNumber}, @{UnwindInfo.Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.Identity(m_compiler)}\n");
            jitprintf($"{prefix}  ufiNext: {UnwindInfo.Identity(ufiNext)}\n");
            jitprintf($"{prefix}  ufiEmitLoc: {UnwindInfo.LocationState(ufiEmitLoc)}\n");
            jitprintf($"{prefix}  ufiHasPhantomProlog: {dspBool(ufiHasPhantomProlog)}\n");
            jitprintf($"{prefix}  {epilogCount} epilog{(epilogCount != 1 ? "s" : "")}\n");
            jitprintf($"{prefix}  ufiEpilogList: {UnwindInfo.Identity(ufiEpilogList)}\n");
            jitprintf($"{prefix}  ufiEpilogLast: {UnwindInfo.Identity(ufiEpilogLast)}\n");
            jitprintf($"{prefix}  ufiCurCodes: {UnwindInfo.Identity(ufiCurCodes)}\n");
            jitprintf($"{prefix}  ufiSize: {ufiSize}\n");
            jitprintf($"{prefix}  ufiSetEBit: {dspBool(ufiSetEBit)}\n");
            jitprintf($"{prefix}  ufiNeedExtendedCodeWordsEpilogCount: " +
                $"{dspBool(ufiNeedExtendedCodeWordsEpilogCount)}\n");
            jitprintf($"{prefix}  ufiCodeWords: {ufiCodeWords}\n");
            jitprintf($"{prefix}  ufiEpilogScopes: {ufiEpilogScopes}\n");
            jitprintf($"{prefix}  ufiStartOffset: 0x{ufiStartOffset:x}\n");
            jitprintf($"{prefix}  ufiInProlog: {dspBool(ufiInProlog)}\n");
            jitprintf($"{prefix}  ufiInitialized: 0x{UnwindInfo.UFI_INITIALIZED_PATTERN:x8}\n");

            ufiPrologCodes.Dump(indent + 2);
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilog.Dump(indent + 2);
            }
        }
    }

    private sealed partial class UnwindPrologCodes
    {
        public void Dump(int indent)
        {
            var prefix = UnwindInfo.Indent(indent);
            jitprintf($"{prefix}UnwindPrologCodes @{UnwindInfo.Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.Identity(m_compiler)}\n");
            jitprintf($"{prefix}  &upcMemLocal[0]: managed buffer is upcMem\n");
            jitprintf($"{prefix}  upcMem: {UnwindInfo.Identity(upcMem)}\n");
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
        public void Dump(int indent)
        {
            var prefix = UnwindInfo.Indent(indent);
            jitprintf($"{prefix}UnwindEpilogCodes @{UnwindInfo.Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: not stored\n");
            jitprintf($"{prefix}  &uecMemLocal[0]: managed buffer is uecMem\n");
            jitprintf($"{prefix}  uecMem: {UnwindInfo.Identity(uecMem)}\n");
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
        public void Dump(int indent)
        {
            var prefix = UnwindInfo.Indent(indent);
            jitprintf($"{prefix}UnwindEpilogInfo @{UnwindInfo.Identity(this)}, size:managed:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.Identity(m_compiler)}\n");
            jitprintf($"{prefix}  epiNext: {UnwindInfo.Identity(epiNext)}\n");
            jitprintf($"{prefix}  epiEmitLocation: {UnwindInfo.LocationState(epiEmitLocation)}\n");
            jitprintf($"{prefix}  epiStartOffset: 0x{epiStartOffset:x}\n");
            jitprintf($"{prefix}  epiMatches: {dspBool(epiMatches)}\n");
            jitprintf($"{prefix}  epiStartIndex: {epiStartIndex}\n");
            epiCodes.Dump(indent + 2);
        }
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

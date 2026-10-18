// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
    private const uint UFI_ARMARCH_INITIALIZED_PATTERN = 0x0FACADE0;
    private const uint UWI_INITIALIZED_PATTERN = 0x0FACADE1;

    // These sizes match the DEBUG layouts in pinned unwind.h, including its base
    // subobjects, 24/4-byte local buffers, and DEBUG fields. Pointer size follows
    // the host process rather than the target ISA.
    private static int NATIVE_UNWIND_INFO_SIZE => ArmArchNativeSize(x86: 196, x64: 296);
    private static int NATIVE_UNWIND_FRAGMENT_INFO_SIZE => ArmArchNativeSize(x86: 172, x64: 248);
    private static int NATIVE_UNWIND_PROLOG_CODES_SIZE => ArmArchNativeSize(x86: 56, x64: 72);
    private static int NATIVE_UNWIND_EPILOG_INFO_SIZE => ArmArchNativeSize(x86: 56, x64: 88);
    private static int NATIVE_UNWIND_EPILOG_CODES_SIZE => ArmArchNativeSize(x86: 32, x64: 48);

    private void DumpArmArch(bool isHotCode, int indent)
    {
        var fragmentCount = 0;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragmentCount++;
        }

        var prefix = ArmArchIndent(indent);
        jitprintf($"{prefix}UnwindInfo{(isHotCode ? " " : " COLD ")}@{ArmArchIdentity(this)}, " +
            $"size:{NATIVE_UNWIND_INFO_SIZE}:\n");
        jitprintf($"{prefix}  m_compiler: {ArmArchIdentity(m_compiler)}\n");
        jitprintf($"{prefix}  {fragmentCount} fragment{(fragmentCount != 1 ? "s" : "")}\n");
        jitprintf($"{prefix}  uwiFragmentLast: {ArmArchIdentity(uwiFragmentLast)}\n");
        jitprintf($"{prefix}  uwiEndLoc: {ArmArchLocationIdentity(uwiEndLoc)}\n");
        jitprintf($"{prefix}  uwiInitialized: 0x{(uwiInitialized ? UWI_INITIALIZED_PATTERN : 0u):x8}\n");

        var fragmentNumber = 1;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.DumpArmArch(fragmentNumber++, indent + 2);
        }
    }

    private sealed partial class UnwindFragmentInfo
    {
        public void DumpArmArch(int fragmentNumber, int indent)
        {
            var prefix = UnwindInfo.ArmArchIndent(indent);
            var epilogCount = 0;
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilogCount++;
            }

            jitprintf($"{prefix}UnwindFragmentInfo #{fragmentNumber}, @{UnwindInfo.ArmArchIdentity(this)}, " +
                $"size:{NATIVE_UNWIND_FRAGMENT_INFO_SIZE}:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.ArmArchIdentity(m_compiler)}\n");
            jitprintf($"{prefix}  ufiNext: {UnwindInfo.ArmArchIdentity(ufiNext)}\n");
            jitprintf($"{prefix}  ufiEmitLoc: {UnwindInfo.ArmArchLocationIdentity(ufiEmitLoc)} ");
            if (ufiEmitLoc.HasValue)
            {
                ufiEmitLoc.Value.Print(m_compiler.compMethodID);
            }

            jitprintf("\n");
            jitprintf($"{prefix}  ufiHasPhantomProlog: {dspBool(ufiHasPhantomProlog)}\n");
            jitprintf($"{prefix}  {epilogCount} epilog{(epilogCount != 1 ? "s" : "")}\n");
            jitprintf($"{prefix}  ufiEpilogList: {UnwindInfo.ArmArchIdentity(ufiEpilogList)}\n");
            jitprintf($"{prefix}  ufiEpilogLast: {UnwindInfo.ArmArchIdentity(ufiEpilogLast)}\n");
            jitprintf($"{prefix}  ufiCurCodes: {UnwindInfo.ArmArchIdentity(ufiCurCodes)}\n");
            jitprintf($"{prefix}  ufiSize: {ufiSize}\n");
            jitprintf($"{prefix}  ufiSetEBit: {dspBool(ufiSetEBit)}\n");
            jitprintf($"{prefix}  ufiNeedExtendedCodeWordsEpilogCount: " +
                $"{dspBool(ufiNeedExtendedCodeWordsEpilogCount)}\n");
            jitprintf($"{prefix}  ufiCodeWords: {ufiCodeWords}\n");
            jitprintf($"{prefix}  ufiEpilogScopes: {ufiEpilogScopes}\n");
            jitprintf($"{prefix}  ufiStartOffset: 0x{ufiStartOffset:x}\n");
            jitprintf($"{prefix}  ufiInProlog: {dspBool(ufiInProlog)}\n");
            jitprintf($"{prefix}  ufiInitialized: 0x{UnwindInfo.UFI_ARMARCH_INITIALIZED_PATTERN:x8}\n");

            ufiPrologCodes.DumpArmArch(indent + 2);
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilog.DumpArmArch(indent + 2);
            }
        }
    }

    private sealed partial class UnwindPrologCodes
    {
        public void DumpArmArch(int indent)
        {
            var prefix = UnwindInfo.ArmArchIndent(indent);
            jitprintf($"{prefix}UnwindPrologCodes @{UnwindInfo.ArmArchIdentity(this)}, " +
                $"size:{NATIVE_UNWIND_PROLOG_CODES_SIZE}:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.ArmArchIdentity(m_compiler)}\n");
            jitprintf($"{prefix}  &upcMemLocal[0]: {UnwindInfo.ArmArchIdentity(upcMem)}\n");
            jitprintf($"{prefix}  upcMem: {UnwindInfo.ArmArchIdentity(upcMem)}\n");
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
        public void DumpArmArch(Compiler compiler, int indent)
        {
            var prefix = UnwindInfo.ArmArchIndent(indent);
            jitprintf($"{prefix}UnwindEpilogCodes @{UnwindInfo.ArmArchIdentity(this)}, " +
                $"size:{NATIVE_UNWIND_EPILOG_CODES_SIZE}:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.ArmArchIdentity(compiler)}\n");
            jitprintf($"{prefix}  &uecMemLocal[0]: {UnwindInfo.ArmArchIdentity(uecMem)}\n");
            jitprintf($"{prefix}  uecMem: {UnwindInfo.ArmArchIdentity(uecMem)}\n");
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
        public void DumpArmArch(int indent)
        {
            var prefix = UnwindInfo.ArmArchIndent(indent);
            jitprintf($"{prefix}UnwindEpilogInfo @{UnwindInfo.ArmArchIdentity(this)}, " +
                $"size:{NATIVE_UNWIND_EPILOG_INFO_SIZE}:\n");
            jitprintf($"{prefix}  m_compiler: {UnwindInfo.ArmArchIdentity(m_compiler)}\n");
            jitprintf($"{prefix}  epiNext: {UnwindInfo.ArmArchIdentity(epiNext)}\n");
            jitprintf($"{prefix}  epiEmitLocation: {UnwindInfo.ArmArchLocationIdentity(epiEmitLocation)}\n");
            jitprintf($"{prefix}  epiStartOffset: 0x{epiStartOffset:x}\n");
            jitprintf($"{prefix}  epiMatches: {dspBool(epiMatches)}\n");
            jitprintf($"{prefix}  epiStartIndex: {epiStartIndex}\n");
            epiCodes.DumpArmArch(m_compiler, indent + 2);
        }
    }

    private static string ArmArchIndent(int indent)
    {
        assert(indent >= 0);
        return new string(' ', indent);
    }

    // Managed references and locations cannot expose native addresses; these hashes preserve
    // only the pointer fields' process-specific diagnostic shape.
    private static string ArmArchIdentity(object? value)
    {
        return value is null
            ? ArmArchFormatAddress(0)
            : ArmArchFormatAddress(unchecked((nuint)(uint)RuntimeHelpers.GetHashCode(value)));
    }

    private static string ArmArchLocationIdentity(emitLocation? location)
    {
        return location.HasValue
            ? ArmArchFormatAddress(unchecked((nuint)(uint)location.Value.GetHashCode()))
            : ArmArchFormatAddress(0);
    }

    private static string ArmArchFormatAddress(nuint address)
    {
        return IntPtr.Size == 8 ? $"0x{address:x16}" : $"0x{address:x8}";
    }

    private static int ArmArchNativeSize(int x86, int x64)
    {
        return IntPtr.Size == 8 ? x64 : x86;
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEmitterUnitTests()
    {
        if (!JitConfig.JitEmitUnitTests.contains(_compiler.info.compMethodHnd, _compiler.info.compClassHnd,
            &_compiler.info.compMethodInfo->args))
        {
            return;
        }

        var unitTestSection = JitConfig.JitEmitUnitTestsSections;
        if (unitTestSection is null)
        {
            return;
        }

#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Emitter instruction-test payloads require AMD64.");
#else
        JITDUMP("*************** In genEmitterUnitTests()\n");

        // The payload tests encoding, not execution of the synthetic instructions.
        var skipLabel = genCreateTempLabel();
        inst_JMP(EJ_jmp, skipLabel);
        instGen(INS_nop);

        var sections = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unitTestSection);
        var all = sections.IndexOf("all"u8) >= 0;
        if (all || (sections.IndexOf("sse2"u8) >= 0))
        {
            genAmd64EmitterUnitTestsSse2();
        }
        if (all || (sections.IndexOf("apx"u8) >= 0))
        {
            genAmd64EmitterUnitTestsApx();
        }
        if (all || (sections.IndexOf("avx10v2"u8) >= 0))
        {
            genAmd64EmitterUnitTestsAvx10v2();
        }
        if (all || (sections.IndexOf("ccmp"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCCMP();
        }
        if (all || (sections.IndexOf("cfcmov"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCFCMOV();
        }
        if (all || (sections.IndexOf("ctest"u8) >= 0))
        {
            genAmd64EmitterUnitTestsCTEST();
        }

        genDefineTempLabel(skipLabel);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);

        JITDUMP("*************** End of genEmitterUnitTests()\n");
#endif
    }
}
#endif

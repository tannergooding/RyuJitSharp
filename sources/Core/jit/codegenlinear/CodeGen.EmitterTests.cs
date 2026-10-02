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

        JITDUMP("*************** In genEmitterUnitTests()\n");

        var skipLabel = genCreateTempLabel();
#if TARGET_WASM
        genEmitWasmEmitterTestSkipBlock();
#else
        inst_JMP(EJ_jmp, skipLabel);
#endif
        instGen(INS_nop);

        var sections = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(unitTestSection);
        var all = sections.IndexOf("all"u8) >= 0;
#if TARGET_AMD64
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
#elif TARGET_ARM64
        if (all || (sections.IndexOf("general"u8) >= 0))
        {
            genArm64EmitterUnitTestsGeneral();
        }
        if (all || (sections.IndexOf("advsimd"u8) >= 0))
        {
            genArm64EmitterUnitTestsAdvSimd();
        }
        if (all || (sections.IndexOf("fp16"u8) >= 0))
        {
            genArm64EmitterUnitTestsFp16();
        }
        if (all || (sections.IndexOf("sve"u8) >= 0))
        {
            genArm64EmitterUnitTestsSve();
        }
        if (all || (sections.IndexOf("pac"u8) >= 0))
        {
            genArm64EmitterUnitTestsPac();
        }
#elif TARGET_WASM
        if (all || (sections.IndexOf("simd"u8) >= 0))
        {
            genWasmEmitterUnitTestsSimd();
        }
        instGen(INS_end);
#endif

        genDefineTempLabel(skipLabel);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);
        instGen(INS_nop);

        JITDUMP("*************** End of genEmitterUnitTests()\n");
    }

#if TARGET_ARM64
    private void genArm64EmitterUnitTestsGeneral()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 general emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsAdvSimd()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 AdvSimd emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsFp16()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 FP16 emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsSve()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE emitter tests are not ported.");
    }

    private void genArm64EmitterUnitTestsPac()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM64 PAC emitter tests are not ported.");
    }
#endif

#if TARGET_WASM
    private void genEmitWasmEmitterTestSkipBlock()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm emitter-test skip block emission is not ported.");
    }

    private void genWasmEmitterUnitTestsSimd()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm SIMD emitter tests are not ported.");
    }
#endif
}
#endif

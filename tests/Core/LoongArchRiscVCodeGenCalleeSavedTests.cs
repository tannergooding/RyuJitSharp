// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class LoongArchRiscVCodeGenCalleeSavedTests
{
    [Test]
    public static void EmptyCalleeSavedMasksSkipEmitterAndUnwindDependencies()
    {
        Assume.That(Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64,
            "requires LoongArch64 or RISC-V64 target semantics");

        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));

        SaveCalleeSavedRegisters(codeGen, default, 0);
        var stackPointer = Configuration.TARGET_LOONGARCH64 ? REG_LOONGARCH64_SP : REG_RISCV64_SP;
        RestoreCalleeSavedRegisters(codeGen, default, stackPointer, 0, reportUnwindData: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genSaveCalleeSavedRegistersHelp")]
    private static extern void SaveCalleeSavedRegisters(CodeGen codeGen, regMaskTP mask, int offset);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegistersHelp")]
    private static extern void RestoreCalleeSavedRegisters(CodeGen codeGen, regMaskTP mask, regNumber baseReg,
        int offset, bool reportUnwindData);
}

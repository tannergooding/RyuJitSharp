// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if HAS_FIXED_REGISTER_SET
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void instGen_Set_Reg_To_Zero(emitAttr size, regNumber reg,
        insFlags flags = insFlags.INS_FLAGS_DONT_CARE)
    {
        assert(genIsValidIntOrFakeReg(reg));

#if TARGET_XARCH
        Emitter.emitIns_R_R(INS_xor, size, reg, reg);
#elif TARGET_ARM
        Emitter.emitIns_R_I(INS_mov, size, reg, 0, flags);
#elif TARGET_ARM64
        Emitter.emitIns_Mov(INS_mov, size, reg, REG_ZR, canSkip: true);
#elif TARGET_LOONGARCH64
        Emitter.emitIns_R_R_I(INS_ori, size, reg, REG_R0, 0);
#elif TARGET_RISCV64
        Emitter.emitIns_R_R_I(INS_addi, size, reg, REG_R0, 0);
#else
#error Unknown target
#endif

        _regSet.verifyRegUsed(reg);
    }
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genEmitCallNullCheckArm64(regNumber thisReg)
    {
        Emitter.emitIns_R_R_IArm64(INS_ldr, EA_4BYTE, REG_ARM64_ZR, thisReg, 0);
    }

    private void genEmitNullCheckArm64(GenTreeIndir tree)
    {
        Emitter.emitInsLoadStoreOpArm64(ins_Load(tree.Type), tree.Type.EmitActualSize, REG_ARM64_ZR, tree);
    }

    private regNumber genGetZeroRegArm64()
    {
        return REG_ARM64_ZR;
    }
}

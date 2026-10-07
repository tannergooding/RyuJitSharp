// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNullCheck(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_NULLCHECK);

        genConsumeRegs(tree.Op1);

        GetEmitter().emitInsLoadStoreOp(ins_Load(tree.Type), tree.Type.EmitActualSize, REG_R0, tree);
    }
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForNullCheck(GenTreeIndir tree)
    {
#if TARGET_ARM
        assert(false, "!\"GT_NULLCHECK isn't supported for Arm32; use GT_IND.\"");
#else
        assert(tree.Oper is GT_NULLCHECK);
        genConsumeRegs(tree.Op1);

        Emitter.emitInsLoadStoreOp(ins_Load(tree.Type), tree.Type.EmitActualSize, REG_ZR, tree);
#endif
    }
}
#endif

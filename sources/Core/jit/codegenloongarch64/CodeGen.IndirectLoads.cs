// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIndirLoongArch64(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_IND);

        if (Configuration.FEATURE_SIMD && tree.Type is TYP_SIMD12)
        {
            genLoadIndTypeSimd12(tree);
            return;
        }

        var type = tree.Type;
        var ins = ins_Load(type);
        var targetReg = tree.RegNum;

        genConsumeAddress(tree.Addr);

        if (tree.IsVolatile)
        {
            instGen_MemoryBarrierLoongArch64(BARRIER_FULL);
        }

        Emitter.emitInsLoadStoreOpLoongArch64(ins, type.EmitActualSize, targetReg, tree);
        genProduceReg(tree);
    }

    private void genLoadIndTypeSimd12(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 SIMD12 indirect loads are not ported.");
    }
}

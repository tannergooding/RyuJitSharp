// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsStoreLcl(instruction ins, emitAttr attr, GenTreeLclVarCommon tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local store recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(tree.Oper is GT_STORE_LCL_VAR);
        assert(tree.RegNum == REG_NA);
        var data = tree.Op1;

#if DEBUG
        assert(_compiler is not null);
        assert((uint)tree.LclNum < _compiler.lvaCount);
        emitVarRefOffs = unchecked((int)tree.AsLclVar().LclIlOffs);
#endif
        if (data.IsContainedIntOrIImmed)
        {
            emitIns_S_I(ins, attr, tree.LclNum, 0, unchecked((int)data.AsIntConCommon().IconValue));
        }
        else
        {
            assert(!data.IsContained);
            emitIns_S_R(ins, attr, data.RegNum, tree.LclNum, 0);
        }
#endif
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genFloatReturn(GenTree tree)
    {
        assert(tree.Oper is GT_RETURN or GT_RETFILT);
        assert(varTypeIsFloating(tree.Type));

        var source = tree.AsUnOp().Op1;
        if (genIsRegCandidateLocal(source) && _compiler.lvaGetDesc(source.AsLclVarCommon().LclNum).lvOnFrame)
        {
            var localNum = source.AsLclVarCommon().LclNum;
            if (_compiler.lvaGetDesc(localNum).RegNum != REG_STK)
            {
                source.Flags |= GTF_SPILL;
                inst_TT_RV(ins_Store(source.Type, _compiler.isSIMDTypeLocalAligned(localNum)),
                    source.Type.EmitSize, source, source.RegNum);
            }

            Emitter.emitIns_S(INS_fld, source.Type.EmitSize, localNum, 0);
        }
        else
        {
            // The x86 return ABI transfers the value from an XMM register to the x87 stack.
            source.Flags |= GTF_SPILL;
            _regSet.rsSpillTree(source.RegNum, source);
            source.Flags |= GTF_SPILLED;
            source.Flags &= ~GTF_SPILL;

            var temp = _regSet.rsUnspillInPlace(source, source.RegNum);
            inst_FS_ST(INS_fld, source.Type.EmitActualSize, temp, 0);
            source.Flags &= ~GTF_SPILLED;
            _regSet.tmpRlsTemp(temp);
        }
    }
}
#endif

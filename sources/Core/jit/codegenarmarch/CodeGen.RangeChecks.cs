// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genRangeCheck(GenTree oper)
    {
        noway_assert(oper.Oper is GT_BOUNDS_CHECK);
        var bounds = oper.AsBoundsChk();
        var index = bounds.Index;
        var length = bounds.ArrayLength;

        genConsumeRegs(index);
        genConsumeRegs(length);

        GenTree src1;
        GenTree src2;
        emitJumpKind jumpKind;

        if (index.IsContainedIntOrIImmed)
        {
            src1 = length;
            src2 = index;
            jumpKind = EJ_ls;

#if TARGET_ARM64
            if (index.IsIntegralConst(0))
            {
                assert(!length.IsContained);
                genJumpToThrowHlpBlk(bounds.ThrowKind, (target, isInline) =>
                {
                    var condition = isInline ? GenCondition.NE : GenCondition.EQ;
                    genCompareImmAndJump(condition, length.RegNum, 0, length.Type.EmitActualSize, target);
                });
                return;
            }
#endif
        }
        else
        {
            src1 = index;
            src2 = length;
            jumpKind = EJ_hs;
        }

        var boundsType = genActualType(src2.Type);
#if DEBUG
        assert(boundsType is TYP_INT or TYP_LONG);
        assert(boundsType.EmitSize >= genActualType(src1.Type).EmitSize);
#endif

        var size = boundsType.EmitSize;
        if (src2.IsContainedIntOrIImmed)
        {
            Emitter.emitIns_R_I(INS_cmp, size, src1.RegNum,
                unchecked((nint)src2.AsIntConCommon().IconValue));
        }
        else
        {
#if TARGET_ARM
            Emitter.emitIns_R_R(INS_cmp, size, src1.RegNum, src2.RegNum, INS_FLAGS_DONT_CARE);
#else
            Emitter.emitIns_R_R(INS_cmp, size, src1.RegNum, src2.RegNum);
#endif
        }

        genJumpToThrowHlpBlk(jumpKind, bounds.ThrowKind);
    }
}
#endif

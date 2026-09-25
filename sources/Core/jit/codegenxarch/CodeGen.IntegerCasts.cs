// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CodeGen.GenIntCastDesc.CheckKind;
using static RyuJitSharp.CodeGen.GenIntCastDesc.ExtendKind;

#if TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genIntCastOverflowCheck(GenTreeCast cast, in GenIntCastDesc desc, regNumber reg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer cast overflow checks require AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        RequireSharedThrowHelperBlocks();
        switch (desc.Check)
        {
            case CHECK_POSITIVE:
            {
                Emitter.emitIns_R_R(INS_test, (emitAttr)desc.CheckSrcSize, reg, reg);
                genJumpToSharedThrowHlpBlk(EJ_jl, SCK_OVERFLOW);
                break;
            }

            case CHECK_UINT_RANGE:
            {
                // 0xFFFFFFFF is not an encodable compare immediate; check the upper half instead.
                var tempReg = _internalRegisters.GetSingle(cast);
                assert(tempReg != reg);
                _ = Emitter.emitIns_Mov(INS_mov, EA_8BYTE, tempReg, reg, canSkip: false);
                Emitter.emitIns_R_I(INS_shr_N, EA_8BYTE, tempReg, 32);
                genJumpToSharedThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
                break;
            }

            case CHECK_POSITIVE_INT_RANGE:
            {
                Emitter.emitIns_R_I(INS_cmp, EA_8BYTE, reg, int.MaxValue);
                genJumpToSharedThrowHlpBlk(EJ_ja, SCK_OVERFLOW);
                break;
            }

            case CHECK_INT_RANGE:
            {
                var tempReg = _internalRegisters.GetSingle(cast);
                _ = Emitter.emitIns_Mov(INS_movsxd, EA_8BYTE, tempReg, reg, canSkip: true);
                Emitter.emitIns_R_R(INS_cmp, EA_8BYTE, reg, tempReg);
                genJumpToSharedThrowHlpBlk(EJ_jne, SCK_OVERFLOW);
                break;
            }

            default:
            {
                assert(desc.Check == CHECK_SMALL_INT_RANGE);
                var max = desc.CheckSmallIntMax;
                var min = desc.CheckSmallIntMin;
                Emitter.emitIns_R_I(INS_cmp, (emitAttr)desc.CheckSrcSize, reg, max);
                genJumpToSharedThrowHlpBlk(min == 0 ? EJ_ja : EJ_jg, SCK_OVERFLOW);
                if (min != 0)
                {
                    Emitter.emitIns_R_I(INS_cmp, (emitAttr)desc.CheckSrcSize, reg, min);
                    genJumpToSharedThrowHlpBlk(EJ_jl, SCK_OVERFLOW);
                }
                break;
            }
        }
#endif
    }

    public void genIntToIntCast(GenTreeCast cast)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Integer cast generation requires AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
        if (cast.HasOverflowCheck && !_compiler.fgUseThrowHelperBlocks())
        {
            var boundary = new GenIntCastDesc(cast);
            if (boundary.Check != CHECK_NONE)
            {
                RequireSharedThrowHelperBlocks();
            }
        }

        genConsumeRegs(cast.CastOp);
        var src = cast.CastOp;
        var srcReg = src.IsUsedFromReg ? src.RegNum : REG_NA;
        var dstReg = cast.RegNum;
        assert(genIsValidIntReg(dstReg));
        var desc = new GenIntCastDesc(cast);
        if (desc.Check != CHECK_NONE)
        {
            assert(genIsValidIntReg(srcReg));
            genIntCastOverflowCheck(cast, in desc, srcReg);
        }

        instruction ins;
        uint size;
        var canSkip = false;
        switch (desc.Extend)
        {
            case ZERO_EXTEND_SMALL_INT:
            case LOAD_ZERO_EXTEND_SMALL_INT:
            {
                ins = INS_movzx;
                size = desc.ExtendSrcSize;
                break;
            }

            case SIGN_EXTEND_SMALL_INT:
            case LOAD_SIGN_EXTEND_SMALL_INT:
            {
                ins = INS_movsx;
                size = desc.ExtendSrcSize;
                break;
            }

            case ZERO_EXTEND_INT:
            case LOAD_ZERO_EXTEND_INT:
            {
                ins = INS_mov;
                size = 4;
                break;
            }

            case SIGN_EXTEND_INT:
            case LOAD_SIGN_EXTEND_INT:
            {
                ins = INS_movsxd;
                size = 4;
                break;
            }

            case COPY:
            {
                ins = INS_mov;
                size = desc.ExtendSrcSize;
                canSkip = true;
                break;
            }

            case LOAD_SOURCE:
            {
                ins = ins_Load(src.Type);
                size = src.Type.Size;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        if (srcReg != REG_NA)
        {
            _ = Emitter.emitIns_Mov(ins, (emitAttr)size, dstReg, srcReg, canSkip);
        }
        else
        {
            assert(src.IsUsedFromMemory);
            inst_RV_TT(ins, (emitAttr)size, dstReg, src);
        }

        genProduceReg(cast);
#endif
    }
}
#endif

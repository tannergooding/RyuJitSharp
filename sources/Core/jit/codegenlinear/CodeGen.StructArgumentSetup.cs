// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // sizeReg may be REG_NA when copying a struct with references. The caller sets
    // _stkArgVarNum to the local for the outgoing (or incoming tail-call) argument area.
    private void genConsumePutStructArgStk(GenTreePutArgStk putArgNode,
        regNumber dstReg, regNumber srcReg, regNumber sizeReg)
    {
        var src = putArgNode.Data;
        assert(src.IsContained);
        assert(varTypeIsStruct(src.Type));
        assert((src.Oper is GT_BLK) || src.Oper.IsLocalRead ||
            ((src.Oper is GT_IND) && varTypeIsSimd(src.Type)));
        assert(dstReg != REG_NA);
        assert(srcReg != REG_NA);
        var srcAddrReg = REG_NA;

        // Consume the source address before overwriting any of its register homes.
        if (src.Oper.IsIndir)
        {
            srcAddrReg = genConsumeReg(src.AsIndir().Addr);
        }

#if TARGET_X86
        assert(dstReg != REG_SPBASE);
        inst_Mov(TYP_I_IMPL, dstReg, REG_SPBASE, canSkip: false);
#else
        if (putArgNode.RegNum != dstReg)
        {
            // The destination is always a stack address, including incoming homes for tail calls.
            assert(_stkArgVarNum != BAD_VAR_NUM);
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, dstReg, _stkArgVarNum, putArgNode.ArgOffset);
        }
#endif

        if (srcAddrReg != REG_NA)
        {
            // Source is not known to be on the stack. Use EA_BYREF.
            Emitter.emitIns_Mov(INS_mov, EA_BYREF, srcReg, srcAddrReg, canSkip: true);
        }
        else
        {
            // Source is known to be on the stack. Use EA_PTRSIZE.
            var local = src.AsLclVarCommon();
            Emitter.emitIns_R_S(INS_lea, EA_PTRSIZE, srcReg, local.LclNum, local.LclOffs);
        }

        if (sizeReg != REG_NA)
        {
            var size = unchecked((uint)putArgNode.StackByteSize);
            inst_RV_IV(INS_mov, sizeReg, unchecked((nint)(nuint)size), EA_PTRSIZE);
        }
    }

#if !TARGET_X86
    // The x86 overload pushes its fields and does not take an outgoing-area local.
    private void genPutArgStkFieldList(GenTreePutArgStk putArgStk, int outArgVarNum)
    {
        assert(putArgStk.Op1.Oper is GT_FIELD_LIST);
        var argOffset = putArgStk.ArgOffset;

        foreach (var use in putArgStk.Op1.AsFieldList().Uses)
        {
            var nextArgNode = use.Node;
            _ = genConsumeReg(nextArgNode);
            var reg = nextArgNode.RegNum;
            var type = use.Type;
            var thisFieldOffset = unchecked(argOffset + use.Offset);

#if FEATURE_SIMD
            if (type == TYP_SIMD12)
            {
                Emitter.emitStoreSimd12ToLclOffset(unchecked((uint)outArgVarNum),
                    unchecked((uint)thisFieldOffset), reg, nextArgNode);
            }
            else
#endif
            {
                Emitter.emitIns_S_R(ins_Store(type), type.EmitSize, reg, outArgVarNum, thisFieldOffset);
            }

            // We can't write beyond the arg area unless this is a tail call, in which
            // case the first stack argument is the base of the incoming arg area.
#if DEBUG
            var areaSize = _compiler.lvaLclStackHomeSize(outArgVarNum);
#if FEATURE_FASTTAILCALL
            var call = putArgStk.Call;
            assert(call is not null);
            if (call.IsFastTailCall)
            {
                areaSize = _compiler.lvaParameterStackSize;
            }
#endif
            assert(unchecked((uint)(thisFieldOffset + type.Size)) <= unchecked((uint)areaSize));
#endif
        }
    }
#endif
}

#if !TARGET_XARCH && FEATURE_SIMD
public partial class Emitter
{
    public void emitStoreSimd12ToLclOffset(uint varNum, uint offset, regNumber dataReg, GenTree? tmpRegProvider)
    {
#if TARGET_ARM64
        assert(varNum != unchecked((uint)BAD_VAR_NUM));
        assert(isVectorRegister(dataReg));

        emitIns_S_R(INS_str, EA_8BYTE, dataReg, unchecked((int)varNum), unchecked((int)offset));

        if ((tmpRegProvider is null) || (codeGen.InternalRegisters.Count(tmpRegProvider) == 0))
        {
            emitIns_R_R_R_I(INS_ext, EA_16BYTE, dataReg, dataReg, dataReg, 8, INS_OPTS_16B);
            emitIns_S_R(INS_str, EA_4BYTE, dataReg, unchecked((int)varNum), unchecked((int)(offset + 8)));
            emitIns_R_R_R_I(INS_ext, EA_16BYTE, dataReg, dataReg, dataReg, 8, INS_OPTS_16B);
        }
        else
        {
            var tmpReg = codeGen.InternalRegisters.Extract(tmpRegProvider);
            emitIns_R_R_I(INS_mov, EA_4BYTE, tmpReg, dataReg, 2);
            emitIns_S_R(INS_str, EA_4BYTE, tmpReg, unchecked((int)varNum), unchecked((int)(offset + 8)));
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Target SIMD12 local-stack store recording is not yet ported.");
#endif
    }
}
#endif
#endif

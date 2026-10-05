// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genPutArgStkLoongArch64(GenTreePutArgStk treeNode)
    {
        assert(treeNode.Oper is GT_PUTARG_STK);
        var source = treeNode.Op1;
        var targetType = source.Type.ActualType;

        var varNumOut = BAD_VAR_NUM;
        var argOffsetMax = uint.MaxValue;
        var argOffsetOut = unchecked((uint)treeNode.ArgOffset);

        if (treeNode.PutInIncomingArgArea)
        {
            varNumOut = getFirstArgWithStackSlot();
            argOffsetMax = unchecked((uint)_compiler.lvaParameterStackSize);

#if FEATURE_FASTTAILCALL
            var call = treeNode.Call;
            assert(call is not null);
            assert(call.IsFastTailCall);
            assert(varNumOut != BAD_VAR_NUM);
#endif
        }
        else
        {
            varNumOut = _compiler.lvaOutgoingArgSpaceVar;
            argOffsetMax = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        }

        var isStruct = (targetType == TYP_STRUCT) || (source.Oper is GT_FIELD_LIST);
        if (!isStruct)
        {
            if (varTypeIsSimd(targetType))
            {
                throw new FatalJitException(CORJIT_SKIPPED, "unimplemented on LOONGARCH64 yet");
            }

            var storeIns = ins_Store(targetType);
            var storeAttr = targetType.EmitSize;

            if (source.IsContained)
            {
                assert(source.Oper is GT_CNS_INT);
                assert(source.AsIntConCommon().IconValue == 0);

                Emitter.emitIns_S_R(storeIns, storeAttr, REG_R0, varNumOut, unchecked((int)argOffsetOut));
            }
            else
            {
                _ = genConsumeReg(source);
                if (storeIns is INS_st_w)
                {
                    Emitter.emitIns_R_R_R(
                        INS_add_w,
                        EA_4BYTE,
                        source.RegNum,
                        source.RegNum,
                        REG_R0);
                    storeIns = INS_st_d;
                    storeAttr = EA_8BYTE;
                }

                Emitter.emitIns_S_R(
                    storeIns,
                    storeAttr,
                    source.RegNum,
                    varNumOut,
                    unchecked((int)argOffsetOut));
            }

            argOffsetOut = unchecked(argOffsetOut + (uint)EA_SIZE_IN_BYTES(storeAttr));
            assert(argOffsetOut <= argOffsetMax);
        }
        else
        {
            assert(source.IsContained);

            if (source.Oper is GT_FIELD_LIST)
            {
                genPutArgStkFieldList(treeNode, varNumOut);
                return;
            }

            noway_assert((source.Oper is GT_LCL_VAR) || (source.Oper is GT_BLK));

            targetType = source.Type;
            noway_assert(varTypeIsStruct(targetType));

            var loReg = InternalRegisters.Extract(treeNode);
            var addrReg = REG_NA;

            GenTreeLclVarCommon? varNode = null;
            GenTree? addrNode = null;

            if (source.Oper is GT_LCL_VAR)
            {
                varNode = source.AsLclVarCommon();
            }
            else
            {
                var address = source.AsOp().Op1;
                if (address.IsContained && address.IsLclVarAddr)
                {
                    varNode = address.AsLclVarCommon();
                }
                else
                {
                    addrNode = address;
                    genConsumeAddress(address);
                    addrReg = address.RegNum;
                }
            }

            assert((varNode is not null) ^ (addrNode is not null));

            ClassLayout layout;
            uint srcSize;

            if (source.Oper is GT_LCL_VAR)
            {
                assert(varNode is not null);
                ref var varDsc = ref _compiler.lvaGetDesc(varNode.LclNum);

                assert(varDsc.Type == TYP_STRUCT);
                assert(varDsc.lvOnFrame && !varDsc.lvRegister);

                srcSize = unchecked((uint)_compiler.lvaLclStackHomeSize(varNode.LclNum));
                layout = source.GetLayout(_compiler);
            }
            else
            {
                assert(source.Oper is GT_BLK);
                layout = source.AsBlk().Layout;
                srcSize = unchecked((uint)layout.Size);
            }

            var dstSize = unchecked((uint)treeNode.StackByteSize);
            if (dstSize != srcSize && (varNode is not null))
            {
                var varStackSize = unchecked((uint)_compiler.lvaLclStackHomeSize(varNode.LclNum));
                if (varStackSize >= srcSize)
                {
                    srcSize = varStackSize;
                }
            }

            var structSize = (dstSize == srcSize) ? dstSize : (dstSize < srcSize) ? dstSize : srcSize;
            var remainingSize = unchecked((int)structSize);
            uint structOffset = 0;

            while (remainingSize > 0)
            {
                var nextIndex = structOffset / TARGET_POINTER_SIZE;

                var type = TYP_UBYTE;
                if (remainingSize >= TARGET_POINTER_SIZE)
                {
                    type = layout.GetGCPtrType(unchecked((int)nextIndex));
                }
                else
                {
                    assert(!layout.IsGCPtr(unchecked((int)nextIndex)));

                    if (remainingSize >= 4)
                    {
                        type = TYP_INT;
                    }
                    else if (remainingSize >= 2)
                    {
                        type = TYP_USHORT;
                    }
                    else
                    {
                        assert(remainingSize == 1);
                    }
                }

                var attr = type.EmitSize;
                var moveSize = genTypeSize(type);
                assert(unchecked((uint)EA_SIZE_IN_BYTES(attr)) == unchecked((uint)moveSize));

                remainingSize = unchecked((int)(unchecked((uint)remainingSize) - moveSize));

                var loadIns = ins_Load(type);
                if (varNode is not null)
                {
                    Emitter.emitIns_R_S(
                        loadIns,
                        attr,
                        loReg,
                        varNode.LclNum,
                        unchecked((int)structOffset));
                }
                else
                {
                    assert(loReg != addrReg);
                    Emitter.emitIns_R_R_I(
                        loadIns,
                        attr,
                        loReg,
                        addrReg,
                        unchecked((int)structOffset));
                }

                var storeIns = ins_Store(type);
                Emitter.emitIns_S_R(
                    storeIns,
                    attr,
                    loReg,
                    varNumOut,
                    unchecked((int)argOffsetOut));
                argOffsetOut = unchecked(argOffsetOut + unchecked((uint)moveSize));
                assert(argOffsetOut <= argOffsetMax);

                structOffset = unchecked(structOffset + unchecked((uint)moveSize));
            }
        }
    }
}
#endif

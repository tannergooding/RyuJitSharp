// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private unsafe void LowerRetStruct(GenTreeUnOp ret)
    {
#if WINDOWS_AMD64_ABI
        var compiler = CompilerInstance;
        if (compiler.compMethodReturnsMultiRegRetType)
        {
            return;
        }

        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
        assert(varTypeIsStruct(ret.Type));
        var value = ret.Op1;
        var nativeReturnType = compiler.info.compRetNativeType;
        ret.ChangeType(nativeReturnType.ActualType);

        switch (value.Oper)
        {
            case GT_CNS_INT:
            {
                if (varTypeUsesFloatReg(nativeReturnType))
                {
                    assert((value.Type.Size == nativeReturnType.Size) || value.IsIntegralConst(0));
                    var bits = value.AsIntCon().IconValue;
                    var constant = nativeReturnType is TYP_FLOAT
                        ? BitConverter.Int32BitsToSingle(unchecked((int)bits))
                        : BitConverter.Int64BitsToDouble(bits);
                    var replacement = new GenTreeDblCon(nativeReturnType is TYP_FLOAT ? TYP_FLOAT : TYP_DOUBLE,
                        constant, value, NodeThreading.LIR);
                    replacement._vnPair.SetBoth(ValueNumStore.NoVN);
                    BlockRange().ReplaceNode(value, replacement);
                }
                else
                {
                    assert(varTypeUsesIntReg(nativeReturnType));
                }
                break;
            }

            case GT_BLK:
            case GT_IND:
            {
                // An enclosing return register must not widen a memory read past
                // the source object. Spill a struct, or zero-extend a scalar load.
                if (nativeReturnType.Size > value.AsIndir().Size)
                {
                    var use = new LIR.Use(BlockRange(), ref ret.Op1Ref, ret);
                    if (value.Type is TYP_STRUCT)
                    {
                        var localNumber = compiler.lvaGrabTemp(shortLifetime: true, "mis-sized struct return");
                        compiler.lvaSetStruct(localNumber, compiler.info.compMethodInfo->args.retTypeClass, false);
                        _ = ReplaceWithLclVar(use, localNumber);
                        LowerRetSingleRegStructLclVar(ret);
                    }
                    else
                    {
                        var cast = compiler.gtNewCastNode(nativeReturnType, value, true, value.Type);
                        assert(cast.IsZeroExtending);
                        BlockRange().InsertBefore(ret, cast);
                        ContainCheckCast(cast);
                        use.ReplaceWith(cast);
                    }
                    break;
                }

                var flags = value.Flags & (GTF_COMMON_MASK | GTF_IND_NONFAULTING);
                if (value.Oper is GT_BLK)
                {
                    var replacement = new GenTreeIndir(GT_IND, nativeReturnType, value.AsIndir().Addr, null,
                        value, NodeThreading.LIR) {
                        Flags = flags,
                    };
                    BlockRange().ReplaceNode(value, replacement);
                    value = replacement;
                }
                else
                {
                    value.SetOper(GT_IND);
                    value.Flags = flags;
                    value.ChangeType(nativeReturnType);
                }
                _ = LowerIndir(value.AsIndir());
                break;
            }

            case GT_LCL_VAR:
            {
                LowerRetSingleRegStructLclVar(ret);
                break;
            }

            case GT_LCL_FLD:
            {
                value.ChangeType(nativeReturnType);
                break;
            }

            default:
            {
                assert(varTypeIsEnregisterable(value.Type));
                if (!varTypeUsesSameRegType(ret.Type, value.Type))
                {
                    var bitcast = compiler.gtNewBitCastNode(ret.Type, value);
                    ret.Op1 = bitcast;
                    BlockRange().InsertBefore(ret, bitcast);
                    ContainCheckBitCast(bitcast);
                }
                break;
            }
        }
#else
        throw new NotImplementedException("Struct return lowering outside Windows AMD64 is not ported.");
#endif
    }

    private void LowerRetSingleRegStructLclVar(GenTreeUnOp ret)
    {
        var compiler = CompilerInstance;
        assert(!compiler.compMethodReturnsMultiRegRetType);
        assert(ret.Oper is GT_RETURN or GT_SWIFT_ERROR_RET);
        var local = GetRetValueRef(ret).AsLclVar();
        assert(local.Oper is GT_LCL_VAR);
        var localNumber = local.LclNum;
        ref var descriptor = ref compiler.lvaGetDesc(localNumber);

        if (descriptor.lvPromoted)
        {
            // A whole-struct return prevents independent field enregistration.
            compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.BlockOpRet);
        }

        if (descriptor.lvDoNotEnregister)
        {
            assert(compiler.info.compRetNativeType is not TYP_STRUCT);
            assert((compiler.info.compRetNativeType.Size == ret.Type.Size) ||
                (varTypeIsIntegral(ret.Type) && varTypeIsIntegral(compiler.info.compRetNativeType) &&
                    (compiler.info.compRetNativeType.Size <= ret.Type.Size)));
            var type = ret.Type;
            if (varTypeIsSmall(compiler.info.compRetType))
            {
                assert(compiler.info.compRetNativeType.Size == compiler.info.compRetType.Size);
                type = compiler.info.compRetType;
            }

            var replacement = new GenTreeLclFld(GT_LCL_FLD, type, localNumber,
                checked((ushort)compiler.compRetTypeDesc.SingleReturnFieldOffset), null, null, local, NodeThreading.LIR);
            replacement.CopySsaIdentityFrom(local);
            BlockRange().ReplaceNode(local, replacement);
        }
        else
        {
            var registerType = descriptor.GetRegisterType(local);
            assert(registerType is not TYP_UNDEF);
            local.ChangeType(registerType.ActualType);
            if (!varTypeUsesSameRegType(ret.Type, registerType))
            {
                var bitcast = compiler.gtNewBitCastNode(ret.Type, ret.Op1);
                GetRetValueRef(ret) = bitcast;
                BlockRange().InsertBefore(ret, bitcast);
                ContainCheckBitCast(bitcast);
            }
        }
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
#if FEATURE_HW_INTRINSICS
    private static bool impIsTableDrivenHWIntrinsic(NamedIntrinsic intrinsic, HWIntrinsicCategory category)
    {
        return (category != HW_Category_Special) &&
            ((HWIntrinsicInfo.lookupFlags(intrinsic) & (HW_Flag_SpecialImport | HW_Flag_InvalidNodeId)) == 0);
    }

    private static bool isSupportedBaseType(NamedIntrinsic intrinsic, var_types baseType)
    {
        if (baseType is TYP_UNDEF)
        {
            return false;
        }

        if (varTypeIsArithmetic(baseType))
        {
            return true;
        }

        assert(HWIntrinsicInfo.lookupIsa(intrinsic) is InstructionSet_Vector);
        return false;
    }

    internal unsafe struct HWIntrinsicSignatureReader
    {
        public CORINFO_CLASS_HANDLE op1ClsHnd;
        public CORINFO_CLASS_HANDLE op2ClsHnd;
        public CORINFO_CLASS_HANDLE op3ClsHnd;
        public CORINFO_CLASS_HANDLE op4ClsHnd;
        public CorInfoType op1JitType;
        public CorInfoType op2JitType;
        public CorInfoType op3JitType;
        public CorInfoType op4JitType;

        public void Read(ICorJitInfo* compHnd, CORINFO_SIG_INFO* sig)
        {
            var args = sig->args;

            fixed (HWIntrinsicSignatureReader* reader = &this)
            {
                if (sig->numArgs > 0)
                {
                    op1JitType = strip(compHnd->getArgType(sig, args, &reader->op1ClsHnd));

                    if (sig->numArgs > 1)
                    {
                        args = compHnd->getArgNext(args);
                        op2JitType = strip(compHnd->getArgType(sig, args, &reader->op2ClsHnd));
                    }

                    if (sig->numArgs > 2)
                    {
                        args = compHnd->getArgNext(args);
                        op3JitType = strip(compHnd->getArgType(sig, args, &reader->op3ClsHnd));
                    }

                    if (sig->numArgs > 3)
                    {
                        args = compHnd->getArgNext(args);
                        op4JitType = strip(compHnd->getArgType(sig, args, &reader->op4ClsHnd));
                    }
                }
            }
        }

        public readonly var_types GetOp1Type() => op1JitType.VarType;
        public readonly var_types GetOp2Type() => op2JitType.VarType;
        public readonly var_types GetOp3Type() => op3JitType.VarType;
        public readonly var_types GetOp4Type() => op4JitType.VarType;

        public readonly var_types GetOp1TypeAsPrecise() => op1JitType.PreciseVarType;
        public readonly var_types GetOp2TypeAsPrecise() => op2JitType.PreciseVarType;
        public readonly var_types GetOp3TypeAsPrecise() => op3JitType.PreciseVarType;
        public readonly var_types GetOp4TypeAsPrecise() => op4JitType.PreciseVarType;
    }

    private void getHWIntrinsicImmOps(
        NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig, ref GenTree? immOp1, ref GenTree? immOp2)
    {
#if TARGET_XARCH
        if ((sig.numArgs > 0) && HWIntrinsicInfo.isImmOp(intrinsic, impStackTop().val))
        {
            // Xarch immediate operands occupy the last position on the importer stack.
            immOp1 = impStackTop().val;
        }
#else
        NYI("Hardware-intrinsic immediate discovery outside xarch");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("Hardware-intrinsic immediate discovery outside xarch.");
#endif
    }

    private GenTree addRangeCheckIfNeeded(
        NamedIntrinsic intrinsic, GenTree immOp, int immLowerBound, int immUpperBound)
    {
        if (!immOp.Oper.IsCnsIntOrI && HWIntrinsicInfo.isImmOp(intrinsic, immOp)
#if TARGET_XARCH
            && !HWIntrinsicInfo.isAVX2GatherIntrinsic(intrinsic) &&
            !HWIntrinsicInfo.HasFullRangeImm(intrinsic)
#endif
            )
        {
            assert(varTypeIsIntegral(immOp.Type));
            return addRangeCheckForHWIntrinsic(immOp, immLowerBound, immUpperBound);
        }

        return immOp;
    }
#endif
}

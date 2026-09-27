// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTreeOpCC LowerModPow2(GenTreeOp mod)
    {
        assert(mod.Oper is GT_MOD);
        var divisor = mod.Op2;
        JITDUMP("Lower: optimize X MOD POW2");
        assert(divisor.IsIntegralConstPow2);
        var type = mod.Type;
        assert(type is TYP_INT or TYP_LONG);
        var divisorValue = (nint)divisor.AsIntConCommon().IntegralValue;
        var mask = unchecked(divisorValue - 1);
        BlockRange().Remove(divisor);

        // Both candidates use the dividend; evaluate it once before testing its sign.
        var dividendUse = new LIR.Use(BlockRange(), ref mod.Op1Ref, mod);
        var dividend = ReplaceWithLclVar(dividendUse);
        var secondDividend = CompilerInstance.gtClone(dividend);
        assert(secondDividend is not null);
        BlockRange().InsertAfter(dividend, secondDividend);
        var constant = CompilerInstance.gtNewIconNode(type, mask);
        BlockRange().InsertAfter(secondDividend, constant);
        var trueValue = CompilerInstance.gtNewBinaryNode(GT_AND, type, dividend, constant);
        BlockRange().InsertAfter(constant, trueValue);
        _ = LowerBinaryArithmetic(trueValue);

        GenTreeOpCC result;
        if (divisorValue == 2)
        {
            // A negative dividend selects -(a & 1); otherwise it selects a & 1.
            var zero = CompilerInstance.gtNewIconNode(type, 0);
            BlockRange().InsertAfter(trueValue, zero);
            var compare = CompilerInstance.gtNewBinaryNode(GT_CMP, TYP_VOID, secondDividend, zero);
            compare.Flags |= GTF_SET_FLAGS;
            BlockRange().InsertAfter(zero, compare);
            ContainCheckCompare(compare);

            result = new GenTreeOpCC(GT_SELECT_NEGCC, type, new GenCondition(GenCondition.SLT),
                trueValue, null, mod, NodeThreading.LIR) {
                Flags = mod.Flags & GTF_COMMON_MASK,
            };
        }
        else
        {
            // NEG sets N for positive a. Select a & mask then, or -(-a & mask) otherwise.
            var negate = CompilerInstance.gtNewUnaryNode(GT_NEG, type, secondDividend);
            negate.Flags |= GTF_SET_FLAGS;
            BlockRange().InsertAfter(trueValue, negate);
            var secondConstant = CompilerInstance.gtNewIconNode(type, mask);
            BlockRange().InsertAfter(negate, secondConstant);
            var falseValue = CompilerInstance.gtNewBinaryNode(GT_AND, type, negate, secondConstant);
            BlockRange().InsertAfter(secondConstant, falseValue);
            _ = LowerBinaryArithmetic(falseValue);

            result = new GenTreeOpCC(GT_SELECT_NEGCC, type, new GenCondition(GenCondition.S),
                trueValue, falseValue, mod, NodeThreading.LIR) {
                Flags = mod.Flags,
            };
        }

        result._vnPair.SetBoth(ValueNumStore.NoVN);
        BlockRange().ReplaceNode(mod, result);
        ContainCheckNode(result);

        return result;
    }
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool IsProfitableToSetZeroFlag(GenTree op)
    {
#if TARGET_XARCH
        if (op.Oper is GT_LSH or GT_RSH or GT_RSZ or GT_ROR or GT_ROL)
        {
            // BMI2's SHLX, SARX, SHRX and RORX do not set the zero flag.
            if (!op.AsOp().Op2.Oper.IsConst)
            {
                return false;
            }
        }
#endif

        return true;
    }

    // The single-bit reduction lambda in OptimizeConstCompare. Its caller changes
    // TEST_EQ/NE to BITTEST_EQ/NE and clears containment on the resulting bit index.
    private bool TryReduceSingleBitTestOps(GenTreeOp test)
    {
#if TARGET_XARCH
        assert(test.Oper is GT_AND or GT_TEST_EQ or GT_TEST_NE);
        var testedOp = test.Op1;
        var bitOp = test.Op2;
        if (bitOp.Oper is not GT_LSH)
        {
            (bitOp, testedOp) = (testedOp, bitOp);
        }

        if ((bitOp.Oper is GT_LSH) && varTypeIsIntOrI(bitOp.Type) && bitOp.AsOp().Op1.IsIntegralConst(1))
        {
            BlockRange().Remove(bitOp.AsOp().Op1);
            BlockRange().Remove(bitOp);
            test.Op1 = testedOp;
            test.Op2 = bitOp.AsOp().Op2;

            return true;
        }

        // (x >> y) & 1 tests the same bit for either signed or unsigned shifts.
        // BT masks the index modulo the operand width, just like the shift. Keep
        // constant-index shifts: the existing constant-mask TEST is already optimal.
        var shiftOp = test.Op1;
        var oneOp = test.Op2;
        if (!oneOp.IsIntegralConst(1))
        {
            (shiftOp, oneOp) = (oneOp, shiftOp);
        }

        if (oneOp.IsIntegralConst(1) && (shiftOp.Oper is GT_RSH or GT_RSZ) &&
            varTypeIsIntOrI(shiftOp.Type) && !shiftOp.AsOp().Op2.Oper.IsIntegralConst)
        {
            BlockRange().Remove(oneOp);
            BlockRange().Remove(shiftOp);
            test.Op1 = shiftOp.AsOp().Op1;
            test.Op2 = shiftOp.AsOp().Op2;

            // Compare containment is skipped after this reduction. A memory value
            // contained by the removed shift must become a register for reg,reg BT.
            test.Op1.IsContained = false;

            return true;
        }

        return false;
#else
        throw new System.NotImplementedException("Non-xarch single-bit comparison reduction is not ported.");
#endif
    }
}

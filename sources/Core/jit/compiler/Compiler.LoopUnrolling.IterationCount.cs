// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private static bool optIterSmallOverflow(int iterAtExit, var_types incrType)
    {
        int maximum;
        switch (incrType)
        {
            case TYP_BYTE:
            {
                maximum = sbyte.MaxValue;
                break;
            }

            case TYP_UBYTE:
            {
                maximum = byte.MaxValue;
                break;
            }

            case TYP_SHORT:
            {
                maximum = short.MaxValue;
                break;
            }

            case TYP_USHORT:
            {
                maximum = ushort.MaxValue;
                break;
            }

            case TYP_INT:
            case TYP_UINT:
            {
                maximum = int.MaxValue;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage165
                );
                return false;
            }
        }

        return iterAtExit > maximum;
    }

    private static bool optIterSmallUnderflow(int iterAtExit, var_types decrType)
    {
        int minimum;
        switch (decrType)
        {
            case TYP_BYTE:
            {
                minimum = sbyte.MinValue;
                break;
            }

            case TYP_SHORT:
            {
                minimum = short.MinValue;
                break;
            }

            case TYP_UBYTE:
            case TYP_USHORT:
            {
                minimum = 0;
                break;
            }

            case TYP_INT:
            case TYP_UINT:
            {
                minimum = int.MinValue;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage165
                );
                return false;
            }
        }

        return iterAtExit < minimum;
    }

    private bool optComputeLoopRep(int constInit, int constLimit, int iterInc,
        genTreeOps iterOper, var_types iterOperType, genTreeOps testOper, bool unsTest, out uint iterCount)
    {
        noway_assert(iterOperType.ActualType is TYP_INT);
        iterCount = 0;

        long constLimitX = unsTest ? unchecked((uint)constLimit) : constLimit;
        long constInitX;

        switch (iterOperType)
        {
            case TYP_BYTE:
            {
                constInitX = unchecked((sbyte)constInit);
                iterInc = unchecked((sbyte)iterInc);
                break;
            }

            case TYP_UBYTE:
            {
                constInitX = unchecked((byte)constInit);
                iterInc = unchecked((byte)iterInc);
                break;
            }

            case TYP_SHORT:
            {
                constInitX = unchecked((short)constInit);
                iterInc = unchecked((short)iterInc);
                break;
            }

            case TYP_USHORT:
            {
                constInitX = unchecked((ushort)constInit);
                iterInc = unchecked((ushort)iterInc);
                break;
            }

            case TYP_INT:
            {
                constInitX = unsTest ? unchecked((uint)constInit) : constInit;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage165
                );
                iterCount = default!;
                return default!;
            }
        }

        var iterIncX = iterOper is GT_SUB ? -(long)iterInc : iterInc;
        if (iterIncX == 0)
        {
            return false;
        }

        var iterSign = iterIncX > 0 ? 1 : -1;
        if ((iterIncX > 0 && constLimitX < constInitX) ||
            (iterIncX < 0 && constLimitX > constInitX))
        {
            return false;
        }

        if (iterOper is not (GT_ADD or GT_SUB))
        {
            if (iterOper is GT_MUL or GT_DIV or GT_UDIV or GT_RSH or GT_LSH)
            {
                return false;
            }

            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage166
            );
            iterCount = default!;
            return default!;
        }

        uint loopCount = 0;
        switch (testOper)
        {
            case GT_EQ:
            {
                return false;
            }

            case GT_NE:
            {
                if ((constLimitX - constInitX) % iterIncX != 0)
                {
                    return false;
                }

                if (constInitX != constLimitX)
                {
                    loopCount = unchecked(loopCount +
                        (uint)((constLimitX - constInitX - iterSign) / iterIncX) + 1);
                }
                break;
            }

            case GT_LT:
            {
                if (constInitX < constLimitX)
                {
                    loopCount = unchecked(loopCount +
                        (uint)((constLimitX - constInitX - iterSign) / iterIncX) + 1);
                }
                break;
            }

            case GT_LE:
            {
                if (constInitX <= constLimitX)
                {
                    loopCount = unchecked(loopCount +
                        (uint)((constLimitX - constInitX) / iterIncX) + 1);
                }
                break;
            }

            case GT_GT:
            {
                if (constInitX > constLimitX)
                {
                    loopCount = unchecked(loopCount +
                        (uint)((constLimitX - constInitX - iterSign) / iterIncX) + 1);
                }
                break;
            }

            case GT_GE:
            {
                if (constInitX >= constLimitX)
                {
                    loopCount = unchecked(loopCount +
                        (uint)((constLimitX - constInitX) / iterIncX) + 1);
                }
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage167
                );
                iterCount = default!;
                return default!;
            }
        }

        // Native truncates the computed exit iterator to int32 before checking
        // the small-type range and comparing it with the original limit.
        long iterAtExitX = unchecked((int)(constInitX + iterIncX * unchecked((int)loopCount)));
        if (unsTest)
        {
            iterAtExitX = unchecked((uint)iterAtExitX);
        }

        var exitValue = unchecked((int)iterAtExitX);
        var overflow = testOper is GT_GT or GT_GE
            ? optIterSmallUnderflow(exitValue, iterOperType)
            : optIterSmallOverflow(exitValue, iterOperType);
        if (overflow)
        {
            return false;
        }

        bool stillInLoop;
        switch (testOper)
        {
            case GT_NE:
            case GT_LT:
            {
                stillInLoop = iterAtExitX < constLimitX;
                break;
            }

            case GT_LE:
            {
                stillInLoop = iterAtExitX <= constLimitX;
                break;
            }

            case GT_GT:
            {
                stillInLoop = iterAtExitX > constLimitX;
                break;
            }

            case GT_GE:
            {
                stillInLoop = iterAtExitX >= constLimitX;
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage167
                );
                return false;
            }
        }
        if (stillInLoop)
        {
            return false;
        }

        iterCount = loopCount;
        return true;
    }
}

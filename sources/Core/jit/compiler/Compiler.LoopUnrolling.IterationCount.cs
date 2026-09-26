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
        var maximum = incrType switch
        {
            TYP_BYTE => sbyte.MaxValue,
            TYP_UBYTE => byte.MaxValue,
            TYP_SHORT => short.MaxValue,
            TYP_USHORT => ushort.MaxValue,
            TYP_INT or TYP_UINT => int.MaxValue,
            _ => throw new FatalJitException("Unexpected loop iterator type."),
        };

        return iterAtExit > maximum;
    }

    private static bool optIterSmallUnderflow(int iterAtExit, var_types decrType)
    {
        var minimum = decrType switch
        {
            TYP_BYTE => sbyte.MinValue,
            TYP_SHORT => short.MinValue,
            TYP_UBYTE or TYP_USHORT => 0,
            TYP_INT or TYP_UINT => int.MinValue,
            _ => throw new FatalJitException("Unexpected loop iterator type."),
        };

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
                throw new FatalJitException("Unexpected loop iterator type.");
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

            throw new FatalJitException("Unexpected loop iterator operator.");
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
                throw new FatalJitException("Unexpected loop test operator.");
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

        var stillInLoop = testOper switch
        {
            GT_NE or GT_LT => iterAtExitX < constLimitX,
            GT_LE => iterAtExitX <= constLimitX,
            GT_GT => iterAtExitX > constLimitX,
            GT_GE => iterAtExitX >= constLimitX,
            _ => throw new FatalJitException("Unexpected loop test operator."),
        };
        if (stillInLoop)
        {
            return false;
        }

        iterCount = loopCount;
        return true;
    }
}

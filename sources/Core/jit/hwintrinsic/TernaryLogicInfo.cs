// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum TernaryLogicUseFlags : byte
{
    None = 0,
    A = 1,
    B = 2,
    AB = A | B,
    C = 4,
    AC = A | C,
    BC = B | C,
    ABC = A | B | C,
}

public enum TernaryLogicOperKind : byte
{
    None,
    Select,
    True,
    False,
    Not,
    And,
    Nand,
    Or,
    Nor,
    Xor,
    Xnor,
    Cond,
    Major,
    Minor,
}

public readonly partial struct TernaryLogicInfo
{
    // Three steps, each with four operation bits and three operand-use bits,
    // fit in one constant word without a managed object table.
    private TernaryLogicInfo(uint packed)
    {
        Oper1 = (TernaryLogicOperKind)(packed & 0x0F);
        Oper1Use = (TernaryLogicUseFlags)((packed >> 4) & 0x07);
        Oper2 = (TernaryLogicOperKind)((packed >> 7) & 0x0F);
        Oper2Use = (TernaryLogicUseFlags)((packed >> 11) & 0x07);
        Oper3 = (TernaryLogicOperKind)((packed >> 14) & 0x0F);
        Oper3Use = (TernaryLogicUseFlags)((packed >> 18) & 0x07);
    }

    public TernaryLogicOperKind Oper1 { get; }
    public TernaryLogicUseFlags Oper1Use { get; }
    public TernaryLogicOperKind Oper2 { get; }
    public TernaryLogicUseFlags Oper2Use { get; }
    public TernaryLogicOperKind Oper3 { get; }
    public TernaryLogicUseFlags Oper3Use { get; }

    public static TernaryLogicInfo Lookup(byte control) => new(PackedEntries[control]);

    public TernaryLogicUseFlags GetAllUseFlags() => Oper1Use | Oper2Use | Oper3Use;

    public static byte GetTernaryControlByte(genTreeOps oper, byte op1, byte op2)
    {
        switch (oper)
        {
            case GT_AND:
            {
                return unchecked((byte)(op1 & op2));
            }

            case GT_AND_NOT:
            {
                return unchecked((byte)(~op1 & op2));
            }

            case GT_OR:
            {
                return unchecked((byte)(op1 | op2));
            }

            case GT_XOR:
            {
                return unchecked((byte)(op1 ^ op2));
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedTernaryLogicOperationArgument, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? oper : default));
                return default;
            }
        }
    }

    public static byte GetTernaryControlByte(TernaryLogicOperKind oper, byte op1, byte op2)
    {
        switch (oper)
        {
            case TernaryLogicOperKind.Select:
            {
                return op2;
            }

            case TernaryLogicOperKind.Not:
            {
                return unchecked((byte)~op2);
            }

            case TernaryLogicOperKind.And:
            {
                return unchecked((byte)(op1 & op2));
            }

            case TernaryLogicOperKind.Nand:
            {
                return unchecked((byte)~(op1 & op2));
            }

            case TernaryLogicOperKind.Or:
            {
                return unchecked((byte)(op1 | op2));
            }

            case TernaryLogicOperKind.Nor:
            {
                return unchecked((byte)~(op1 | op2));
            }

            case TernaryLogicOperKind.Xor:
            {
                return unchecked((byte)(op1 ^ op2));
            }

            case TernaryLogicOperKind.Xnor:
            {
                return unchecked((byte)~(op1 ^ op2));
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.UnexpectedTernaryLogicOperationArgument, (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? oper : default));
                return default;
            }
        }
    }

    public static byte GetTernaryControlByte(TernaryLogicInfo info, byte op1, byte op2, byte op3)
    {
        byte first;
        switch (info.Oper1Use)
        {
            case TernaryLogicUseFlags.None:
            {
                switch (info.Oper1)
                {
                    case TernaryLogicOperKind.False:
                    {
                        first = 0x00;
                        break;
                    }

                    case TernaryLogicOperKind.True:
                    {
                        first = 0xFF;
                        break;
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InvalidConstantTernaryLogicOperation);
                        first = default;
                        break;
                    }
                }
                break;
            }

            case TernaryLogicUseFlags.A:
            case TernaryLogicUseFlags.B:
            case TernaryLogicUseFlags.C:
            case TernaryLogicUseFlags.AB:
            case TernaryLogicUseFlags.AC:
            case TernaryLogicUseFlags.BC:
            {
                var unary = info.Oper1Use is TernaryLogicUseFlags.A or TernaryLogicUseFlags.B or TernaryLogicUseFlags.C;
                var firstInput = unary ? (byte)0 : info.Oper1Use is TernaryLogicUseFlags.BC ? op2 : op1;
                var secondInput = info.Oper1Use switch {
                    TernaryLogicUseFlags.A => op1,
                    TernaryLogicUseFlags.B => op2,
                    TernaryLogicUseFlags.C => op3,
                    TernaryLogicUseFlags.AB => op2,
                    _ => op3,
                };
                first = GetTernaryControlByte(info.Oper1, firstInput, secondInput);
                break;
            }

            case TernaryLogicUseFlags.ABC:
            {
                switch (info.Oper1)
                {
                    case TernaryLogicOperKind.Nor:
                    {
                        first = unchecked((byte)~(op1 | op2 | op3));
                        break;
                    }

                    case TernaryLogicOperKind.Minor:
                    {
                        first = 0x17;
                        break;
                    }

                    case TernaryLogicOperKind.Xnor:
                    {
                        first = unchecked((byte)~(op1 ^ op2 ^ op3));
                        break;
                    }

                    case TernaryLogicOperKind.Nand:
                    {
                        first = unchecked((byte)~(op1 & op2 & op3));
                        break;
                    }

                    case TernaryLogicOperKind.And:
                    {
                        first = unchecked((byte)(op1 & op2 & op3));
                        break;
                    }

                    case TernaryLogicOperKind.Xor:
                    {
                        first = unchecked((byte)(op1 ^ op2 ^ op3));
                        break;
                    }

                    case TernaryLogicOperKind.Major:
                    {
                        first = 0xE8;
                        break;
                    }

                    case TernaryLogicOperKind.Or:
                    {
                        first = unchecked((byte)(op1 | op2 | op3));
                        break;
                    }

                    default:
                    {
                        JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InvalidThreeInputTernaryOperation);
                        first = default;
                        break;
                    }
                }
                break;
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InvalidFirstTernaryLogicUsage);
                return default;
            }
        }

        byte second;
        switch (info.Oper2Use)
        {
            case TernaryLogicUseFlags.None:
            {
                second = first;
                break;
            }

            case TernaryLogicUseFlags.A:
            case TernaryLogicUseFlags.B:
            case TernaryLogicUseFlags.C:
            {
                var input = info.Oper2Use switch {
                    TernaryLogicUseFlags.A => op1,
                    TernaryLogicUseFlags.B => op2,
                    _ => op3,
                };
                second = GetTernaryControlByte(info.Oper2, first, input);
                break;
            }

            case TernaryLogicUseFlags.AB:
            case TernaryLogicUseFlags.AC:
            case TernaryLogicUseFlags.BC:
            {
                var firstInput = info.Oper2Use is TernaryLogicUseFlags.BC ? op2 : op1;
                var secondInput = info.Oper2Use is TernaryLogicUseFlags.AB ? op2 : op3;
                second = GetTernaryControlByte(info.Oper2, firstInput, secondInput);
                break;
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InvalidSecondTernaryLogicUsage);
                return default;
            }
        }

        switch (info.Oper3Use)
        {
            case TernaryLogicUseFlags.None:
            {
                return second;
            }

            case TernaryLogicUseFlags.A:
            {
                return unchecked((byte)((first & op1) | (second & ~op1)));
            }

            case TernaryLogicUseFlags.B:
            {
                return unchecked((byte)((first & op2) | (second & ~op2)));
            }

            case TernaryLogicUseFlags.C:
            {
                return unchecked((byte)((first & op3) | (second & ~op3)));
            }

            default:
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.InvalidConditionalTernaryLogicUsage);
                return default;
            }
        }
    }
}

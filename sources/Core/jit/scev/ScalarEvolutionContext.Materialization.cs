// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class ScalarEvolutionContext
{
    private bool Materialize(Scev scev, bool createIR, out GenTree? result, out ValueNumPair resultVNP)
    {
        result = null;
        resultVNP = new ValueNumPair();
        var vnStore = _compiler.vnStore;
        if (vnStore is null)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ScalarEvolutionRequiresValueNumbering);
        }

        switch (scev)
        {
            case ScevConstant constant:
            {
                if (constant.Type is TYP_REF or TYP_BYREF)
                {
                    if (constant.Value != 0)
                    {
                        return false;
                    }

                    resultVNP.SetBoth(ValueNumStore.VNForNull());
                }
                else
                {
                    Span<byte> bytes = stackalloc byte[sizeof(long)];
                    _ = BitConverter.TryWriteBytes(bytes, constant.Value);
                    resultVNP.SetBoth(vnStore.VNForGenericCon(constant.Type, bytes));
                }

                if (createIR)
                {
                    result = constant.Type is TYP_LONG
                        ? _compiler.gtNewLconNode(constant.Value)
                        : _compiler.gtNewIconNode(constant.Type, unchecked((nint)constant.Value));
                }

                break;
            }

            case ScevLocal local:
            {
                ref readonly var ssaDsc = ref _compiler.lvaGetDesc(local.LclNum).GetPerSsaData(local.SsaNum);
                resultVNP = vnStore.VNPNormalPair(ssaDsc._vnPair);
                if (createIR)
                {
                    result = _compiler.gtNewLclvNode(local.Type, local.LclNum);
                }

                break;
            }

            case ScevUnop { Oper: ScevOper.ZeroExtend or ScevOper.SignExtend } extension:
            {
                if (!Materialize(extension.Op1, createIR, out var operand, out var operandVN))
                {
                    return false;
                }

                var isUnsigned = extension.Oper is ScevOper.ZeroExtend;
                resultVNP = vnStore.VNPairForCast(operandVN, TYP_LONG, extension.Type, isUnsigned);
                if (createIR)
                {
                    assert(operand is not null);
                    result = _compiler.gtNewCastNode(extension.Type, operand, isUnsigned, TYP_LONG);
                }

                break;
            }

            case ScevBinop binop:
            {
                if (!Materialize(binop.Op1, createIR, out var op1, out var op1VN) ||
                    !Materialize(binop.Op2, createIR, out var op2, out var op2VN))
                {
                    return false;
                }

                genTreeOps oper;
                switch (binop.Oper)
                {
                    case ScevOper.Add:
                    {
                        oper = GT_ADD;
                        break;
                    }

                    case ScevOper.Mul:
                    {
                        oper = GT_MUL;
                        break;
                    }

                    case ScevOper.Lsh:
                    {
                        oper = GT_LSH;
                        break;
                    }

                    default:
                    {
                        if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnexpectedBinaryScalarEvolutionOperationArgument,
                                binop.Oper);
                        }
                        else
                        {
                            JitExceptionHelpers.ThrowInvalidOperationException(
                                FatalJitErrorCode.UnexpectedBinaryScalarEvolutionOperationArgument);
                        }

                        oper = default;
                        break;
                    }
                }
                resultVNP = vnStore.VNPairForFunc(binop.Type, (VNFunc)oper, op1VN, op2VN);
                if (createIR)
                {
                    assert(op1 is not null && op2 is not null);
                    if (oper is GT_MUL && op1.IsIntegralConst(-1))
                    {
                        result = _compiler.gtNewUnaryNode(GT_NEG, op2.Type, op2);
                    }
                    else if (oper is GT_MUL && op2.IsIntegralConst(-1))
                    {
                        result = _compiler.gtNewUnaryNode(GT_NEG, op1.Type, op1);
                    }
                    else
                    {
                        if (!Configuration.TARGET_64BIT && (oper is GT_MUL) && binop.Type is TYP_LONG)
                        {
                            return false;
                        }
                        result = _compiler.gtNewBinaryNode(oper, binop.Type, op1, op2);
                    }
                }

                break;
            }

            case ScevAddRec:
            {
                return false;
            }

            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedScalarEvolutionOperationArgument,
                        scev.Oper);
                }
                else
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(
                        FatalJitErrorCode.UnexpectedScalarEvolutionOperationArgument);
                }

                return false;
            }
        }

        if (createIR)
        {
            assert(result is not null);
            result._vnPair = resultVNP;
        }

        return true;
    }

    public GenTree? Materialize(Scev scev)
    {
        var previousTreeId = 0;
        savePreviousTreeId(ref previousTreeId);
        if (Materialize(scev, true, out var result, out _))
        {
            return result;
        }

        restorePreviousTreeId(previousTreeId);
        return null;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void savePreviousTreeId(ref int previousTreeId)
    {
        previousTreeId = _compiler.compGenTreeID;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void restorePreviousTreeId(int previousTreeId)
    {
        _compiler.compGenTreeID = previousTreeId;
    }

    public ValueNumPair MaterializeVN(Scev scev)
        => Materialize(scev, false, out _, out var pair) ? pair : new ValueNumPair();
}

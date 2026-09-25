// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private readonly struct LowerFieldListRegisterInfo
    {
        public readonly int Offset;
        public readonly var_types RegType;

        public LowerFieldListRegisterInfo(int offset, var_types regType)
        {
            Offset = offset;
            RegType = regType;
        }
    }

    private int StoreFieldListToNewLocal(ClassLayout layout, GenTreeFieldList fieldList)
    {
#if DEBUG
        JITDUMP($"Spilling field list [{fieldList.TreeId:D6}] to stack\n");
#endif
        var compiler = CompilerInstance;
        var localNumber = compiler.lvaGrabTemp(shortLifetime: true, "Spilled local for field list");
        compiler.lvaSetStruct(localNumber, layout, unsafeValueClsCheck: false);
        compiler.lvaSetVarDoNotEnregister(localNumber, DoNotEnregisterReason.LocalField);

        foreach (var use in fieldList.Uses)
        {
            var store = compiler.gtNewStoreLclFldNode(use.Type, localNumber, use.Offset, use.Node);
            BlockRange().InsertAfter(use.Node, store);
            _ = LowerNode(store);
        }
        return localNumber;
    }

    private unsafe void LowerArgFieldList(CallArg arg, GenTreeFieldList fieldList)
    {
        assert(!arg.AbiInfo.HasAnyStackSegment);

        LowerFieldListRegisterInfo GetRegisterInfo(int index)
        {
            ref readonly var segment = ref arg.AbiInfo.Segments[index];
            return new LowerFieldListRegisterInfo(segment.Offset, segment.GetRegisterType());
        }

        if (!IsFieldListCompatibleWithRegisters(fieldList, arg.AbiInfo.NumSegments, GetRegisterInfo))
        {
            var layout = CompilerInstance.typGetObjLayout(arg.SignatureClassHandle);
            var localNumber = StoreFieldListToNewLocal(layout, fieldList);
            fieldList.Uses.Clear();
            foreach (ref readonly var segment in arg.AbiInfo.Segments)
            {
                var offset = (ushort)segment.Offset;
                var field = CompilerInstance.gtNewLclFldNode(segment.GetRegisterType(layout), localNumber, offset);
                fieldList.AddFieldLIR(CompilerInstance, field, offset, field.Type);
                BlockRange().InsertBefore(fieldList, field);
            }
        }
        else
        {
            LowerFieldListToFieldListOfRegisters(fieldList, arg.AbiInfo.NumSegments, GetRegisterInfo);
        }

        var use = fieldList.Uses.Head;
        foreach (ref readonly var segment in arg.AbiInfo.Segments)
        {
            assert(use is not null, "Ran out of fields while inserting PUTARG_REG");
            InsertPutArgReg(ref use.NodeRef, in segment);
            use = use.Next;
        }
        assert(use is null, "Missed fields while inserting PUTARG_REG");

        arg.NodeRef = fieldList.SoleFieldOrThis;
        if (arg.Node != fieldList)
        {
            BlockRange().Remove(fieldList);
        }
    }

    private static bool IsFieldListCompatibleWithRegisters(
        GenTreeFieldList fieldList, int numRegs, Func<int, LowerFieldListRegisterInfo> getRegInfo)
    {
#if DEBUG
        JITDUMP($"Checking if field list [{fieldList.TreeId:D6}] is compatible with registers: ");
#endif
        var use = fieldList.Uses.Head;
        for (var index = 0; index < numRegs; index++)
        {
            var regInfo = getRegInfo(index);
            var regStart = regInfo.Offset;
            var regType = regInfo.RegType;
            var regEnd = regStart + regType.Size;
            if ((index == numRegs - 1) && !varTypeUsesFloatReg(regType))
            {
                // The final integer register may carry undefined trailing bits.
                regEnd = regStart + REGSIZE_BYTES;
            }

            if ((use is null) || (use.Offset >= regEnd))
            {
                JITDUMP($"it is not; register {index} has no corresponding field\n");
                return false;
            }

            do
            {
                var fieldStart = use.Offset;
                if (fieldStart < regStart)
                {
#if DEBUG
                    JITDUMP($"it is not; field [{use.Node.TreeId:D6}] starts before register {index}\n");
#endif
                    return false;
                }
                if (fieldStart >= regEnd)
                {
                    break;
                }

                if (fieldStart + use.Type.Size > regEnd)
                {
#if DEBUG
                    JITDUMP($"it is not; field [{use.Node.TreeId:D6}] ends after register {index}\n");
#endif
                    return false;
                }

                if (varTypeUsesFloatReg(use.Node.Type) && varTypeUsesFloatReg(regType) && (fieldStart != regStart))
                {
#if DEBUG
                    JITDUMP($"it is not; field [{use.Node.TreeId:D6}] requires an insertion into register {index}\n");
#endif
                    return false;
                }

                // Integer-to-float conversion must fit in a single bitcast.
                if (varTypeUsesIntReg(use.Node.Type) && varTypeUsesFloatReg(regType) && (regType.Size > TARGET_POINTER_SIZE))
                {
#if DEBUG
                    JITDUMP($"it is not; field [{use.Node.TreeId:D6}] requires an insertion into float register {index} of size {regType.Size}\n");
#endif
                    return false;
                }
                use = use.Next;
            }
            while (use is not null);
        }

        if (use is not null)
        {
#if DEBUG
            JITDUMP($"it is not; field [{use.Node.TreeId:D6}] corresponds to no register\n");
#endif
            return false;
        }

        JITDUMP("it is\n");
        return true;
    }

    private void LowerFieldListToFieldListOfRegisters(
        GenTreeFieldList fieldList, int numRegs, Func<int, LowerFieldListRegisterInfo> getRegInfo)
    {
        var compiler = CompilerInstance;
        var use = fieldList.Uses.Head;
        assert(fieldList.Uses.IsSorted);

        for (var index = 0; index < numRegs; index++)
        {
            var regInfo = getRegInfo(index);
            var regStart = regInfo.Offset;
            var regType = regInfo.RegType;
            var regEnd = regStart + regType.Size;
            if ((index == numRegs - 1) && !varTypeUsesFloatReg(regType))
            {
                regEnd = regStart + REGSIZE_BYTES;
            }

            var regEntry = use;
            assert(regEntry is not null);
            assert(use is not null);
            var fieldListPrevious = fieldList.Prev;
            assert(fieldListPrevious is not null);

            do
            {
                var fieldStart = use.Offset;
                assert(fieldStart >= regStart);
                if (fieldStart >= regEnd)
                {
                    break;
                }

                var fieldType = use.Type;
                var value = use.Node;
                var insertOffset = fieldStart - regStart;
                var nextUse = use.Next;

                // Normalized small values must not overwrite the next field's bits.
                if ((nextUse is not null) && (nextUse.Offset < regEnd) &&
                    (fieldStart + fieldType.ActualType.Size > nextUse.Offset))
                {
                    assert(varTypeIsSmall(fieldType));
                    if (compiler.fgCastNeeded(value, varTypeToUnsigned(fieldType)))
                    {
                        value = compiler.gtNewCastNode(TYP_INT, value, true, varTypeToUnsigned(fieldType));
                        BlockRange().InsertBefore(fieldList, value);
                    }
                }

                if (varTypeUsesFloatReg(value.Type) && varTypeUsesIntReg(regInfo.RegType))
                {
                    assert(value.Type.Size is 4 or 8);
                    value = compiler.gtNewBitCastNode(value.Type.Size == 4 ? TYP_INT : TYP_LONG, value);
                    BlockRange().InsertBefore(fieldList, value);
                }

                if (insertOffset + fieldType.Size > value.Type.ActualType.Size)
                {
                    value = compiler.gtNewCastNode(TYP_LONG, value, true, TYP_LONG);
                    BlockRange().InsertBefore(fieldList, value);
                }

                if (fieldStart != regStart)
                {
                    var shiftAmount = compiler.gtNewIconNode(TYP_INT, insertOffset * BITS_PER_BYTE);
                    value = new GenTreeOp(GT_LSH, value.Type.ActualType, value, shiftAmount);
                    BlockRange().InsertBefore(fieldList, shiftAmount, value);
                }

                if (regEntry != use)
                {
                    var previousValue = regEntry.Node;
                    if (value.Type.ActualType != previousValue.Type.ActualType)
                    {
                        previousValue = compiler.gtNewCastNode(TYP_LONG, previousValue, true, TYP_LONG);
                        BlockRange().InsertBefore(fieldList, previousValue);
                        regEntry.Node = previousValue;
                    }

                    value = new GenTreeOp(GT_OR, value.Type.ActualType, previousValue, value);
                    BlockRange().InsertBefore(fieldList, value);
                    regEntry.Next = use.Next;
                }

                regEntry.Node = value;
                regEntry.Type = value.Type.ActualType;
                use = regEntry.Next;
            }
            while (use is not null);

            if (varTypeUsesIntReg(regEntry.Node.Type) != varTypeUsesIntReg(regType))
            {
                var bitCast = compiler.gtNewBitCastNode(regType, regEntry.Node);
                BlockRange().InsertBefore(fieldList, bitCast);
                regEntry.Node = bitCast;
            }

            if ((index == numRegs - 1) && varTypeUsesIntReg(regType))
            {
                var node = regEntry.Node;
                // All struct ABIs leave bits past the return size undefined.
                while ((node.Oper is GT_CAST) && !node.HasOverflowCheck &&
                    (node.AsCast().CastOp.Type.ActualType is TYP_INT) &&
                    (node.AsCast().CastType.ActualType is TYP_INT) &&
                    (regType.Size <= node.AsCast().CastType.Size))
                {
                    var operand = node.AsCast().CastOp;
                    regEntry.Node = operand;
                    operand.IsContained = false;
                    node.BashToNOP();
                    node = operand;
                }
            }

            if (fieldListPrevious.Next != fieldList)
            {
                LowerRange(fieldListPrevious.Next, fieldList.Prev);
            }
        }
        assert(use is null);
    }
}

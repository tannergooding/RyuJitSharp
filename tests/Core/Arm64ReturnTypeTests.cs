// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.CorInfoHFAElemType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ReturnTypeTests
{
    [TestCase(9, TYPE_GC_NONE, TYPE_GC_NONE, TYP_I_IMPL, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_REF, TYPE_GC_NONE, TYP_REF, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_REF, TYP_I_IMPL, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_BYREF, TYP_I_IMPL, TYP_BYREF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_BYREF, TYP_REF, TYP_BYREF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_REF, TYP_BYREF, TYP_REF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_REF, TYP_REF, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_BYREF, TYP_BYREF, TYP_BYREF)]
    public static void TwoRegisterReturnsMapBothTypedGcLayoutSlots(
        int size, CorInfoGCType first, CorInfoGCType second, var_types firstType, var_types secondType)
    {
        WithCompiler(size, first, second, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.Multiple(() => {
                Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
                Assert.That(descriptor.IsMultiRegRetType, Is.True);
                Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(firstType));
                Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(secondType));
                Assert.That(descriptor.GetAbiReturnReg(0, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R0));
                Assert.That(descriptor.GetAbiReturnReg(1, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R1));
                Assert.That(metadata.GcLayoutCalls, Is.EqualTo(1));
                Assert.That(metadata.GcLayoutWasInitialized, Is.True);
                Assert.That(metadata.SizeCalls, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public static void ResetClearsBothReturnSlotsBeforeReinitialization()
    {
        WithCompiler(16, TYPE_GC_REF, TYPE_GC_BYREF, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_REF));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_BYREF));

            descriptor.Reset();
            metadata.First = TYPE_GC_NONE;
            metadata.Second = TYPE_GC_NONE;
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_I_IMPL));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_I_IMPL));
            Assert.That(metadata.GcLayoutCalls, Is.EqualTo(2));
        });
    }

    [TestCase(TYP_SIMD8, 8)]
    [TestCase(TYP_SIMD12, 12)]
    [TestCase(TYP_SIMD16, 16)]
    public static void SimdHfaReturnsNormalizeToTheMultiRegisterStructAbi(var_types type, int size)
    {
        WithLowering(size, TYP_STRUCT, (compiler, lowering, block) => {
            var value = new GenTreeCall(type);
            var ret = new GenTreeUnOp(GT_RETURN, type, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(compiler.compRetTypeDesc.ReturnRegCount, Is.EqualTo(size / 4));
            Assert.That(ret.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(ret.Op1, Is.SameAs(value));
            Assert.That(value.Type, Is.EqualTo(type));
        });
    }

    [TestCase(TYP_INT, TYP_FLOAT)]
    [TestCase(TYP_DOUBLE, TYP_LONG)]
    public static void PrimitiveReturnRegisterChangesInsertBitcasts(var_types sourceType, var_types nativeType)
    {
        WithLowering(nativeType.Size, nativeType, (compiler, lowering, block) => {
            compiler.lvaTable[0].Type = sourceType;
            var value = compiler.gtNewLclvNode(sourceType, 0);
            var ret = new GenTreeUnOp(GT_RETURN, nativeType, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(ret.Op1.Type, Is.EqualTo(nativeType));
            Assert.That(ret.Op1.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(value.IsRegOptional, Is.True);
        });
    }

    [TestCase(TYP_UBYTE, TYP_INT)]
    [TestCase(TYP_LONG, TYP_LONG)]
    public static void StackStructReturnsPreserveTheNativeFieldType(var_types nativeType, var_types fieldType)
    {
        WithLowering(nativeType.Size, nativeType, (compiler, lowering, block) => {
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            value.SsaNum = 17;
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(ret.Type, Is.EqualTo(nativeType.ActualType));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(fieldType));
            Assert.That(ret.Op1.AsLclFld().SsaNum, Is.EqualTo(17));
            Assert.That(value.Next, Is.Null);
        });
    }

    [Test]
    public static void IncompatibleReturnFieldsSpillWithoutRetypingNonzeroFloatingStores()
    {
        WithLowering(8, TYP_DOUBLE, (compiler, lowering, block) => {
            var value = compiler.gtNewDconNode(TYP_FLOAT, 3.0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, 4, TYP_FLOAT);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);
            block.InsertAtEnd(value);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(ret);
            var localCount = compiler.lvaCount;
            LowerRet(lowering, ret);

            Assert.That(compiler.lvaCount, Is.EqualTo(localCount + 1));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_DOUBLE));
            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(value.Next?.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(value.Next?.AsLclFld().LclOffs, Is.EqualTo(4));
            Assert.That(fields.Next, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReturnContainmentUsesTheValueRatherThanTheSwiftError(bool swift)
    {
        WithLowering(16, TYP_STRUCT, (compiler, lowering, block) => {
            compiler.lvaTable[0].lvIsMultiRegRet = true;
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var error = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            GenTreeUnOp ret;
            if (swift)
            {
                block.InsertAtEnd(error);
                ret = new GenTreeOp(GT_SWIFT_ERROR_RET, TYP_STRUCT, error, value);
            }
            else
            {
                ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, value);
            }
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            ContainCheckRet(lowering, ret);

            Assert.That(value.IsContained, Is.True);
            Assert.That(error.IsContained, Is.False);
        });
    }

    [TestCase(GT_LCL_VAR)]
    [TestCase(GT_LCL_FLD)]
    [TestCase(GT_BLK)]
    [TestCase(GT_FIELD_LIST)]
    public static void StackStructArgumentsStayContainedWithoutPrimitiveRetyping(genTreeOps oper)
    {
        WithLowering(8, TYP_LONG, (compiler, lowering, block) => {
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            GenTree value;
            if (oper is GT_BLK)
            {
                var address = compiler.gtNewLclvNode(TYP_BYREF, 1);
                block.InsertAtEnd(address);
                value = compiler.gtNewBlkIndir(address, layout);
            }
            else if (oper is GT_FIELD_LIST)
            {
                value = new GenTreeFieldList();
            }
            else
            {
                value = oper is GT_LCL_VAR
                    ? compiler.gtNewLclvNode(TYP_STRUCT, 0)
                    : compiler.gtNewLclFldNode(TYP_STRUCT, 0, 0, layout);
            }
            var argument = new GenTreePutArgStk(TYP_STRUCT, value, null, 0, 8, false);
            block.InsertAtEnd(value);
            block.InsertAtEnd(argument);
            LowerPutArgStk(lowering, argument);

            Assert.That(value.IsContained, Is.True);
            Assert.That(value.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.EqualTo(oper is GT_LCL_VAR));
        });
    }

    [TestCase(0x1234L)]
    [TestCase(0x7FFF00000000L)]
    public static void DirectCallsKeepRelocatableTargetsWithoutRangeExpansion(long address)
    {
        WithLowering(8, TYP_LONG, (compiler, lowering, block) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
                _directCallAddress = (void*)0x9999,
                _entryPoint = new CORINFO_CONST_LOOKUP {
                    accessType = InfoAccessType.IAT_VALUE,
                    addr = (void*)(nint)address,
                },
            };
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)address));
            Assert.That(call._controlExpr, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(call));
        });
    }

    [Test]
    public static void IndirectCallsKeepTheirMemoryTargetsUncontained()
    {
        WithLowering(8, TYP_LONG, (compiler, lowering, block) => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var target = compiler.gtNewIndir(TYP_I_IMPL, address);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _controlExpr = target,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That(call._controlExpr, Is.SameAs(target));
            Assert.That(target.IsContained, Is.False);
            Assert.That(target.IsRegOptional, Is.False);
            Assert.That(target.Next, Is.SameAs(call));
        });
    }

    [TestCase(TYP_INT, REG_R0, false)]
    [TestCase(TYP_LONG, REG_R7, false)]
    [TestCase(TYP_FLOAT, REG_V0, false)]
    [TestCase(TYP_DOUBLE, REG_V7, false)]
    [TestCase(TYP_LONG, REG_NA, true)]
    [TestCase(TYP_DOUBLE, REG_NA, true)]
    public static void CallArgumentsUseTheirClassifiedRegisterOrStackSlot(var_types type, regNumber register, bool stack)
    {
        WithLowering(8, TYP_LONG, (compiler, lowering, block) => {
            GenTree value = varTypeUsesFloatReg(type)
                ? compiler.gtNewDconNode(type, 3)
                : compiler.gtNewIconNode(type, 3);
            var call = new GenTreeCall(TYP_VOID);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            argument.AbiInfo.Segments[0] = stack
                ? AbiPassingSegment.OnStack(24, 0, type.Size)
                : AbiPassingSegment.InRegister(register, 0, type.Size);
            block.InsertAtEnd(value);
            block.InsertAtEnd(call);
            LowerArgsForCall(lowering, call);

            Assert.That(argument.Node.Oper, Is.EqualTo(stack ? GT_PUTARG_STK : GT_PUTARG_REG));
            Assert.That(argument.Node.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(argument.Node));
            Assert.That(argument.Node.Next, Is.SameAs(call));
            if (stack)
            {
                Assert.That(argument.Node.AsPutArgStk().ArgOffset, Is.EqualTo(24));
                Assert.That(argument.Node.AsPutArgStk().StackByteSize, Is.EqualTo(8));
            }
            else
            {
                Assert.That(argument.Node.RegNum, Is.EqualTo(register));
            }
        });
    }

    [TestCase(4, CORINFO_HFA_ELEM_NONE, TYP_INT)]
    [TestCase(8, CORINFO_HFA_ELEM_NONE, TYP_LONG)]
    [TestCase(16, CORINFO_HFA_ELEM_NONE, TYP_STRUCT)]
    [TestCase(4, CORINFO_HFA_ELEM_FLOAT, TYP_FLOAT)]
    [TestCase(8, CORINFO_HFA_ELEM_DOUBLE, TYP_DOUBLE)]
    [TestCase(8, CORINFO_HFA_ELEM_FLOAT, TYP_STRUCT)]
    public static void StructCallsNormalizeSingleRegisterResultsAndPreserveMultiRegisterHfas(
        int size, CorInfoHFAElemType hfaType, var_types expectedType)
    {
        WithLowering(size, TYP_LONG, (compiler, lowering, block) => {
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            GetMetadata(layout.ClassHandle).HfaType = hfaType;
            var call = new GenTreeCall(TYP_STRUCT) {
                _callType = CT_USER_FUNC,
                _returnType = TYP_STRUCT,
                RetClsHnd = layout.ClassHandle,
            };
            call._returnTypeDesc.InitializeStructReturnType(compiler, layout.ClassHandle, CorInfoCallConvExtension.Managed);
            call.IsUnusedValue = true;
            block.InsertAtEnd(call);
            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(expectedType));
            Assert.That(call._returnType, Is.EqualTo(TYP_STRUCT));
            Assert.That((nint)call.RetClsHnd, Is.EqualTo((nint)layout.ClassHandle));
            Assert.That(block.FirstNode, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(call));
        });
    }

    [TestCase(GT_LCL_VAR, false)]
    [TestCase(GT_LCL_VAR, true)]
    [TestCase(GT_LCL_FLD, false)]
    [TestCase(GT_LCL_FLD, true)]
    [TestCase(GT_FIELD_LIST, false)]
    [TestCase(GT_FIELD_LIST, true)]
    public static void SplitArgumentsPreserveValuesAbiOffsetsAndEarlyLateOrdering(genTreeOps oper, bool late)
    {
        WithLowering(oper is GT_LCL_FLD ? 24 : 16, TYP_LONG, (compiler, lowering, block) => {
            var originalLayout = compiler.lvaTable[0].Layout;
            assert(originalLayout is not null);
            var layout = originalLayout.SliceLayout(compiler, 0, 16);
            var localOffset = oper is GT_LCL_FLD ? 8 : 0;
            GenTree value;
            GenTree? low = null;
            GenTree? high = null;
            if (oper is GT_FIELD_LIST)
            {
                low = compiler.gtNewIconNode(TYP_LONG, 11);
                high = compiler.gtNewIconNode(TYP_LONG, 22);
                var fields = new GenTreeFieldList();
                fields.AddFieldLIR(compiler, low, 0, TYP_LONG);
                fields.AddFieldLIR(compiler, high, 8, TYP_LONG);
                block.InsertAtEnd(low);
                block.InsertAtEnd(high);
                value = fields;
            }
            else
            {
                value = oper is GT_LCL_VAR
                    ? compiler.gtNewLclvNode(TYP_STRUCT, 0)
                    : compiler.gtNewLclFldNode(TYP_STRUCT, 0, (ushort)localOffset, layout);
            }
            var call = new GenTreeCall(TYP_VOID);
            var argument = call.Args.PushBack(NewCallArg.CreateForStruct(value, TYP_STRUCT, layout));
            argument.AbiInfo = new AbiPassingInformation(2);
            argument.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(REG_R7, 0, 8);
            argument.AbiInfo.Segments[1] = AbiPassingSegment.OnStack(16, 8, 8);
            var tailValue = compiler.gtNewIconNode(TYP_LONG, 33);
            var tail = call.Args.PushBack(NewCallArg.CreateForPrimitive(tailValue));
            tail.AbiInfo.Segments[0] = AbiPassingSegment.OnStack(24, 0, 8);
            if (late)
            {
                argument.LateNode = value;
                argument.EarlyNode = null;
                tail.LateNode = tailValue;
                tail.EarlyNode = null;
                call.Args.PushLateBack(argument);
                call.Args.PushLateBack(tail);
            }
            block.InsertAtEnd(value);
            block.InsertAtEnd(tailValue);
            block.InsertAtEnd(call);
            if (!compFeatureArgSplit())
            {
                // Only Windows ARM64 produces this ABI; exercise its transformation directly on other targets.
                SplitArgumentBetweenRegistersAndStack(lowering, call, argument);
            }
            LowerArgsForCall(lowering, call);

            var registers = argument.Next;
            assert(registers is not null);
            Assert.That(registers.Next, Is.SameAs(tail));
            Assert.That(argument.AbiInfo.HasExactlyOneStackSegment, Is.True);
            Assert.That(argument.AbiInfo.Segments[0].Offset, Is.Zero);
            Assert.That(argument.AbiInfo.Segments[0].StackOffset, Is.EqualTo(16));
            Assert.That(registers.AbiInfo.HasExactlyOneRegisterSegment, Is.True);
            Assert.That(registers.AbiInfo.Segments[0].Offset, Is.Zero);
            Assert.That(registers.AbiInfo.Segments[0].Size, Is.EqualTo(8));
            Assert.That(registers.SignatureLayout?.Size, Is.EqualTo(8));
            Assert.That(argument.Node.Oper, Is.EqualTo(GT_PUTARG_STK));
            Assert.That(registers.Node.Oper, Is.EqualTo(GT_PUTARG_REG));
            Assert.That(registers.Node.RegNum, Is.EqualTo(REG_R7));
            Assert.That(value.Next, Is.Null);
            var stackValue = argument.Node.AsPutArgStk().Data;
            var registerValue = registers.Node.AsUnOp().Op1;
            if (oper is GT_FIELD_LIST)
            {
                Assert.That(stackValue.AsFieldList().Uses.Head?.Node, Is.SameAs(high));
                Assert.That(stackValue.AsFieldList().Uses.Head?.Offset, Is.Zero);
                Assert.That(registerValue, Is.SameAs(low));
            }
            else
            {
                Assert.That(stackValue.AsLclFld().LclOffs, Is.EqualTo(localOffset + 8));
                Assert.That(registerValue.AsLclFld().LclOffs, Is.EqualTo(localOffset));
                Assert.That(registerValue.Type, Is.EqualTo(TYP_LONG));
                Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
            }
            if (late)
            {
                Assert.That(call.Args.LateHead, Is.SameAs(argument));
                Assert.That(argument.LateNext, Is.SameAs(registers));
                Assert.That(registers.LateNext, Is.SameAs(tail));
                Assert.That(registers.EarlyNode, Is.Null);
                Assert.That(registers.LateNode, Is.SameAs(registers.Node));
            }
            else
            {
                Assert.That(registers.EarlyNode, Is.SameAs(registers.Node));
                Assert.That(registers.LateNode, Is.Null);
            }
        });
    }

    [TestCase(0)]
    [TestCase(4)]
    public static void SplitFieldListsWithoutACleanBoundarySpillBeforeCreatingTheirTwoLoads(int fieldOffset)
    {
        WithLowering(16, TYP_LONG, (compiler, lowering, block) => {
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            var value = compiler.gtNewIconNode(TYP_LONG, 42);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, (ushort)fieldOffset, TYP_LONG);
            var call = new GenTreeCall(TYP_VOID);
            var argument = call.Args.PushBack(NewCallArg.CreateForStruct(fields, TYP_STRUCT, layout));
            argument.AbiInfo = new AbiPassingInformation(2);
            argument.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(REG_R7, 0, 8);
            argument.AbiInfo.Segments[1] = AbiPassingSegment.OnStack(0, 8, 8);
            block.InsertAtEnd(value);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(call);
            var localCount = compiler.lvaCount;
            SplitArgumentBetweenRegistersAndStack(lowering, call, argument);

            var registers = argument.Next;
            assert(registers is not null);
            Assert.That(compiler.lvaCount, Is.EqualTo(localCount + 1));
            Assert.That(value.Next?.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(value.Next?.AsLclFld().LclOffs, Is.EqualTo(fieldOffset));
            Assert.That(value.Next?.AsLclFld().LclNum, Is.EqualTo(localCount));
            Assert.That(argument.Node.AsLclFld().LclNum, Is.EqualTo(localCount));
            Assert.That(argument.Node.AsLclFld().LclOffs, Is.EqualTo(8));
            Assert.That(registers.Node.AsLclFld().LclNum, Is.EqualTo(localCount));
            Assert.That(registers.Node.AsLclFld().LclOffs, Is.Zero);
            Assert.That(value.Next?.Next, Is.SameAs(argument.Node));
            Assert.That(argument.Node.Next, Is.SameAs(registers.Node));
            Assert.That(registers.Node.Next, Is.SameAs(call));
            Assert.That(fields.Next, Is.Null);
            Assert.That(compiler.lvaTable[localCount].lvDoNotEnregister, Is.True);

            LowerArgsForCall(lowering, call);

            Assert.That(argument.Node.Oper, Is.EqualTo(GT_PUTARG_STK));
            Assert.That(registers.Node.Oper, Is.EqualTo(GT_PUTARG_REG));
            Assert.That(registers.Node.RegNum, Is.EqualTo(REG_R7));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCall")]
    private static extern GenTree? LowerCall(Lowering lowering, GenTree call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerArgsForCall")]
    private static extern void LowerArgsForCall(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCallStruct")]
    private static extern void LowerCallStruct(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SplitArgumentBetweenRegistersAndStack")]
    private static extern void SplitArgumentBetweenRegistersAndStack(Lowering lowering, GenTreeCall call, CallArg argument);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable? LayoutTable(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerRet")]
    private static extern void LowerRet(Lowering lowering, GenTreeUnOp ret);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckRet")]
    private static extern void ContainCheckRet(Lowering lowering, GenTreeUnOp ret);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerPutArgStk")]
    private static extern void LowerPutArgStk(Lowering lowering, GenTreePutArgStk argument);

    private static void WithLowering(int size, var_types nativeType, Action<Compiler, Lowering, BasicBlock> action)
    {
        WithCompiler(size, TYPE_GC_NONE, TYPE_GC_NONE, (compiler, handle, metadata) => {
            compiler.opts.SetMinOpts(true);
            compiler.opts.compFlags = CLFLG_REGVAR;
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.info.compRetType = TYP_STRUCT;
            compiler.info.compRetNativeType = nativeType;
            CORINFO_METHOD_INFO methodInfo = default;
            methodInfo.args.retTypeClass = handle;
            compiler.info.compMethodInfo = &methodInfo;
            var layout = new ClassLayout(handle, true, (uint)size, TYP_STRUCT, "Return", "Return");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            LayoutTable(compiler) = layouts;
            compiler.lvaTable = new LclVarDsc[4];
            compiler.lvaCount = 2;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[1].Type = TYP_BYREF;
            compiler.compRetTypeDesc = new ReturnTypeDesc();
            if (nativeType is TYP_STRUCT)
            {
                metadata.HfaType = CORINFO_HFA_ELEM_FLOAT;
                compiler.compRetTypeDesc.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
            }
            else
            {
                compiler.compRetTypeDesc.InitializeReturnType(compiler, nativeType, null, CorInfoCallConvExtension.Managed);
            }
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null) { Kind = BBKinds.BBJ_RETURN };
            block.MakeLir(null, null);
            compiler.compCurBB = block;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        });
    }

    private sealed class Metadata
    {
        public int Size;
        public CorInfoGCType First;
        public CorInfoGCType Second;
        public int SizeCalls;
        public int GcLayoutCalls;
        public bool GcLayoutWasInitialized = true;
        public CorInfoHFAElemType HfaType;
    }

    private delegate void ReturnAction(Compiler compiler, CORINFO_CLASS_STRUCT_* handle, Metadata metadata);

    private static void WithCompiler(int size, CorInfoGCType first, CorInfoGCType second, ReturnAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGcLayout;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getHFAType = &GetHfaType;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        vtable.Base.Base.getTypeLayout = &GetTypeLayout;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.opts.jitFlags = &flags;
        var metadata = new Metadata {
            Size = size,
            First = first,
            Second = second,
        };
        var metadataHandle = GCHandle.Alloc(metadata);

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            action(compiler, (CORINFO_CLASS_STRUCT_*)GCHandle.ToIntPtr(metadataHandle), metadata);
        }
        finally
        {
            metadataHandle.Free();
            JitTls.Compiler = previous;
        }
    }

    private static Metadata GetMetadata(CORINFO_CLASS_STRUCT_* handle)
    {
        return (Metadata)(GCHandle.FromIntPtr((nint)handle).Target
            ?? throw new InvalidOperationException("Missing struct return metadata"));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static GetTypeLayoutResult GetTypeLayout(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CORINFO_TYPE_LAYOUT_NODE* nodes, nint* count)
        => GetTypeLayoutResult.Failure;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GetMetadata(handle);
        metadata.SizeCalls++;
        return metadata.Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGcLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var metadata = GetMetadata(handle);
        metadata.GcLayoutCalls++;
        metadata.GcLayoutWasInitialized &= (layout[0] == TYPE_GC_NONE) && (layout[1] == TYPE_GC_NONE);
        layout[0] = metadata.First;
        layout[1] = metadata.Second;

        return (metadata.First is TYPE_GC_NONE ? 0 : 1) + (metadata.Second is TYPE_GC_NONE ? 0 : 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
        => GetMetadata(handle).HfaType;
}
#endif

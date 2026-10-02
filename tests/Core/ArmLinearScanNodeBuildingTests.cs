// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Collections.Generic;
using System.Runtime.InteropServices;
#endif
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ArmLinearScanNodeBuildingTests
{
    [TestCase(TYP_INT, 0)]
    [TestCase(TYP_FLOAT, 1)]
    [TestCase(TYP_DOUBLE, 2)]
    public static void ConstantsMaterializeFloatingWordsBeforeTheResult(var_types type, int internalCount)
    {
        WithAllocator((compiler, allocator) => {
            GenTree constant = type is TYP_INT
                ? compiler.gtNewIconNode(type, 23)
                : compiler.gtNewDconNode(type, 2.5);

            Assert.That(Build(allocator, constant), Is.Zero);
            var references = allocator.refPositions;
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(internalCount));
            Assert.That(references.FindAll(reference => reference.getInterval().isInternal),
                Has.Count.EqualTo(2 * internalCount));
            Assert.That(references[^1].treeNode, Is.SameAs(constant));
            Assert.That(references[^1].getInterval().isConstant, Is.True);
            Assert.That(references[^1].getInterval().isInternal, Is.False);

            Assert.That(Build(allocator, compiler.gtNewIconNode(TYP_INT, 7)), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
            Assert.That(PendingDelayFree(allocator), Is.False);
        });
    }

    [TestCase(GT_ADD)]
    [TestCase(GT_SUB)]
    [TestCase(GT_ADD_LO)]
    [TestCase(GT_ADD_HI)]
    [TestCase(GT_SUB_LO)]
    [TestCase(GT_SUB_HI)]
    [TestCase(GT_AND)]
    [TestCase(GT_AND_NOT)]
    [TestCase(GT_OR)]
    [TestCase(GT_XOR)]
    [TestCase(GT_LSH)]
    [TestCase(GT_RSH)]
    [TestCase(GT_RSZ)]
    [TestCase(GT_ROR)]
    public static void BinaryArithmeticPreservesContainedImmediateUseCounts(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 23);
            var right = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, left);
            right.IsContained = true;
            var node = compiler.gtNewBinaryNode(operation, TYP_INT, left, right);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(left));
            Assert.That(allocator.refPositions[start].refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
            Assert.That(allocator.refPositions.Exists(reference => reference.treeNode == right), Is.False);
        });
    }

    [TestCase(GT_ADD)]
    [TestCase(GT_SUB)]
    public static void FloatingArithmeticConsumesBothExplicitOperands(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            var right = compiler.gtNewDconNode(TYP_DOUBLE, 1.25);
            _ = Build(allocator, left);
            _ = Build(allocator, right);
            var node = compiler.gtNewBinaryNode(operation, TYP_DOUBLE, left, right);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(left));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(right));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
        });
    }

    [TestCase(GT_MUL, false, 1)]
    [TestCase(GT_MUL, true, 1)]
    [TestCase(GT_DIV, false, 1)]
    [TestCase(GT_MULHI, false, 1)]
    [TestCase(GT_UDIV, false, 1)]
    [TestCase(GT_MUL_LONG, false, 2)]
    public static void MultiplyAndDividePreserveInternalAndMultipleDefinitions(
        genTreeOps operation, bool overflow, int definitionCount)
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 23);
            var right = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, left);
            _ = Build(allocator, right);
            GenTreeOp node = definitionCount == 2
                ? new GenTreeMultiRegOp(operation, TYP_LONG, left, right)
                : compiler.gtNewBinaryNode(operation, TYP_INT, left, right);
            if (overflow)
            {
                node.Flags |= GTF_OVERFLOW;
            }

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(definitionCount));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(overflow ? 1 : 0));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeUse &&
                reference.getInterval().isInternal && reference.delayRegFree),
                Has.Count.EqualTo(overflow ? 1 : 0));
            Assert.That(PendingDelayFree(allocator), Is.EqualTo(overflow));
        });
    }

    [TestCase(GT_LSH_HI, false, false)]
    [TestCase(GT_LSH_HI, false, true)]
    [TestCase(GT_LSH_HI, true, false)]
    [TestCase(GT_LSH_HI, true, true)]
    [TestCase(GT_RSH_LO, false, false)]
    [TestCase(GT_RSH_LO, false, true)]
    [TestCase(GT_RSH_LO, true, false)]
    [TestCase(GT_RSH_LO, true, true)]
    public static void CarryShiftsDelayOnlyTheInterferingHalf(
        genTreeOps operation, bool contained, bool immediate)
    {
        WithAllocator((compiler, allocator) => {
            var low = compiler.gtNewIconNode(TYP_INT, 17);
            var high = compiler.gtNewIconNode(TYP_INT, 23);
            var count = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, low);
            var lowDef = allocator.refPositions[^1];
            _ = Build(allocator, high);
            var highDef = allocator.refPositions[^1];
            if (!immediate)
            {
                _ = Build(allocator, count);
            }
            count.IsContained = immediate;
            var pair = new GenTreeOp(GT_LONG, TYP_LONG, low, high) { IsContained = true };
            var node = new GenTreeOp(operation, TYP_INT, pair, count) { IsContained = contained };
            var start = allocator.refPositions.Count;

            int sourceCount;
            if (contained)
            {
                ReferenceBuildLocation(allocator) += 2;
                ClearBuildState(allocator);
                sourceCount = BuildShiftLongCarry(allocator, node);
            }
            else
            {
                sourceCount = Build(allocator, node);
            }

            Assert.That(sourceCount, Is.EqualTo(immediate ? 2 : 3));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(low));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(high));
            Assert.That(lowDef.nextRefPosition?.delayRegFree,
                Is.EqualTo(!contained && operation is GT_LSH_HI));
            Assert.That(highDef.nextRefPosition?.delayRegFree,
                Is.EqualTo(!contained && operation is GT_RSH_LO));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef),
                Has.Count.EqualTo(contained ? 0 : 1));
            Assert.That(PendingDelayFree(allocator), Is.EqualTo(!contained));
        });
    }

    [Test]
    public static void UnusedLongConsumesBothHalvesWithoutAResult()
    {
        WithAllocator((compiler, allocator) => {
            var low = compiler.gtNewIconNode(TYP_INT, 17);
            var high = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, low);
            _ = Build(allocator, high);
            var pair = new GenTreeOp(GT_LONG, TYP_LONG, low, high) { IsUnusedValue = true };
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, pair), Is.EqualTo(2));
            Assert.That(pair.Type, Is.EqualTo(TYP_VOID));
            Assert.That(pair.IsUnusedValue, Is.False);
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(low));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(high));
        });
    }

    [TestCase(GT_NOP)]
    [TestCase(GT_JMP)]
    [TestCase(GT_NO_OP)]
    [TestCase(GT_START_NONGC)]
    [TestCase(GT_PROF_HOOK)]
    [TestCase(GT_MEMORYBARRIER)]
    public static void NoResultLeavesDoNotConstructReferences(genTreeOps operation)
    {
        WithAllocator((_, allocator) => {
            Assert.That(Build(allocator, new GenTree(operation, TYP_VOID)), Is.Zero);
            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

    [TestCase(GT_JMPTABLE, REG_NA)]
    [TestCase(GT_CATCH_ARG, REG_EXCEPTION_OBJECT)]
    [TestCase(GT_ASYNC_CONTINUATION, REG_ASYNC_CONTINUATION_RET)]
    [TestCase(GT_SETCC, REG_NA)]
    public static void ValueLeavesRetainFixedDestinationCandidates(genTreeOps operation, regNumber register)
    {
        WithAllocator((_, allocator) => {
            var type = operation switch {
                GT_CATCH_ARG or GT_ASYNC_CONTINUATION => TYP_REF,
                GT_JMPTABLE => TYP_I_IMPL,
                _ => TYP_INT,
            };
            var node = new GenTree(operation, type);
            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(allocator.refPositions, Has.Count.EqualTo(1));
            Assert.That(allocator.refPositions[0].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[0].registerAssignment,
                Is.EqualTo(register is REG_NA ? AvailableIntRegs(allocator) : genSingleTypeRegMask(register)));
        });
    }

    [TestCase(NI_System_Math_Abs, TYP_DOUBLE)]
    [TestCase(NI_System_Math_Sqrt, TYP_FLOAT)]
    [TestCase(NI_PRIMITIVE_SaturateToInt8, TYP_INT)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16, TYP_INT)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8, TYP_INT)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16, TYP_INT)]
    public static void ScalarIntrinsicsUseTheOperandBankAndDefineTheResult(NamedIntrinsic intrinsic, var_types type)
    {
        WithAllocator((compiler, allocator) => {
            GenTree operand = type is TYP_INT
                ? compiler.gtNewIconNode(type, 23)
                : compiler.gtNewDconNode(type, 2.5);
            _ = Build(allocator, operand);
            var node = new GenTreeIntrinsic(type, operand, intrinsic, null);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(operand));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
        });
    }

    [TestCase(GT_NEG, 0)]
    [TestCase(GT_NOT, 0)]
    [TestCase(GT_CKFINITE, 1)]
    public static void UnaryOperationsReserveOnlyTheFiniteCheckTemporary(genTreeOps operation, int internalCount)
    {
        WithAllocator((compiler, allocator) => {
            var type = operation is GT_CKFINITE ? TYP_DOUBLE : TYP_INT;
            GenTree operand = type is TYP_INT
                ? compiler.gtNewIconNode(type, 23)
                : compiler.gtNewDconNode(type, 2.5);
            _ = Build(allocator, operand);
            var node = new GenTreeUnOp(operation, type, operand);

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(internalCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [TestCase(GT_EQ)]
    [TestCase(GT_NE)]
    [TestCase(GT_LT)]
    [TestCase(GT_LE)]
    [TestCase(GT_GE)]
    [TestCase(GT_GT)]
    [TestCase(GT_CMP)]
    public static void ComparisonsConsumeLeftThenRight(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var left = compiler.gtNewIconNode(TYP_INT, 23);
            var right = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, left);
            _ = Build(allocator, right);
            var node = compiler.gtNewBinaryNode(operation, operation is GT_CMP ? TYP_VOID : TYP_INT, left, right);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(left));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(right));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(operation is GT_CMP ? 2 : 3));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalLoadsRetainCandidateAndOptionalContainment(bool candidate, bool optional)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_INT, lvLRACandidate = candidate }];
            compiler.lvaCount = 1;
            var node = compiler.gtNewLclvNode(TYP_INT, 0);
            node.IsRegOptional = optional;

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(node.IsContained, Is.EqualTo(!candidate && optional));
            Assert.That(allocator.refPositions.Count, Is.EqualTo(candidate || optional ? 0 : 1));
        });
    }

    [TestCase(TYP_INT, 1, 0)]
    [TestCase(TYP_FLOAT, 4, 0)]
    [TestCase(TYP_FLOAT, 1, 2)]
    [TestCase(TYP_DOUBLE, 4, 0)]
    [TestCase(TYP_DOUBLE, 1, 3)]
    public static void MisalignedFloatingLocalFieldsReserveAddressAndWordRegisters(
        var_types type, ushort offset, int internalCount)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_STRUCT }];
            compiler.lvaCount = 1;
            var node = new GenTreeLclFld(GT_LCL_FLD, type, 0, offset);

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(internalCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.refType is RefType.RefTypeUse && reference.getInterval().isInternal),
                Has.Count.EqualTo(internalCount));
        });
    }

    [TestCase(false, 0, 0)]
    [TestCase(false, 0x12345678, 1)]
    [TestCase(true, 0, 0)]
    [TestCase(true, 8, 1)]
    public static void AddressModesReserveATemporaryForUnencodableOrThreePartAddresses(
        bool indexed, int offset, int internalCount)
    {
        WithAllocator((compiler, allocator) => {
            var addressBase = compiler.gtNewIconNode(TYP_I_IMPL, 32);
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, addressBase);
            if (indexed)
            {
                _ = Build(allocator, index);
            }
            var node = new GenTreeAddrMode(TYP_I_IMPL, addressBase, indexed ? index : null, indexed ? (byte)1 : (byte)0, offset);

            Assert.That(Build(allocator, node), Is.EqualTo(indexed ? 2 : 1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(internalCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
        });
    }

    [Test]
    public static void IndexAddressesAlwaysReserveAnIntegerTemporary()
    {
        WithAllocator((compiler, allocator) => {
            var array = compiler.gtNewIconNode(TYP_REF, 0);
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, array);
            _ = Build(allocator, index);
            var node = new GenTreeIndexAddr(array, index, TYP_INT, null, 4, 8, 16, true);

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
        });
    }

    [TestCase(GT_KEEPALIVE)]
    [TestCase(GT_JTRUE)]
    public static void VoidUnaryConsumersDoNotDefineAResult(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, new GenTreeUnOp(operation, TYP_VOID, value)), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(value));
        });
    }

    [Test]
    public static void BoundsChecksConsumeIndexBeforeLength()
    {
        WithAllocator((compiler, allocator) => {
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            var length = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, index);
            _ = Build(allocator, length);
            var node = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(index));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(length));
        });
    }

    [Test]
    public static void SwitchTablesConsumeIndexAndTableWithoutAResult()
    {
        WithAllocator((compiler, allocator) => {
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            var table = new GenTree(GT_JMPTABLE, TYP_I_IMPL);
            _ = Build(allocator, index);
            _ = Build(allocator, table);
            var node = new GenTreeOp(GT_SWITCH_TABLE, TYP_VOID, index, table);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(index));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(table));
        });
    }

    [TestCase(false, REG_COUNT)]
    [TestCase(false, REG_F0)]
    [TestCase(true, REG_COUNT)]
    [TestCase(true, REG_F0)]
    public static void BitcastsRetainOptionalInputAndExplicitDestinationMask(bool contained, regNumber register)
    {
        WithAllocator((compiler, allocator) => {
            var operand = compiler.gtNewIconNode(TYP_INT, 23);
            if (!contained)
            {
                _ = Build(allocator, operand);
            }
            operand.IsContained = contained;
            var node = new GenTreeUnOp(GT_BITCAST, TYP_FLOAT, operand) { RegNum = register };
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(contained ? 0 : 1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(contained ? 1 : 2));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[^1].registerAssignment,
                Is.EqualTo(register is REG_COUNT ? AvailableFloatRegs(allocator) : genSingleTypeRegMask(REG_F0)));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalStoresConsumeTheirDataWithoutDefiningANoncandidate(bool field)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_INT }];
            compiler.lvaCount = 1;
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            GenTree node = field
                ? new GenTreeLclFld(TYP_INT, 0, 0, value, null)
                : compiler.gtNewStoreLclVarNode(0, value);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(value));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeUse));
        });
    }

    [TestCase(GT_RETURN, TYP_INT)]
    [TestCase(GT_RETURN, TYP_VOID)]
    [TestCase(GT_RETFILT, TYP_INT)]
    [TestCase(GT_RETFILT, TYP_VOID)]
    public static void ReturnsRetainFixedIntegerUsesAndVoidSourceCounts(genTreeOps operation, var_types type)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            if (type is not TYP_VOID)
            {
                _ = Build(allocator, value);
            }
            var node = new GenTreeUnOp(operation, type, type is TYP_VOID ? null : value);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(type is TYP_VOID ? 0 : 1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(type is TYP_VOID ? 0 : 1));
            if (type is not TYP_VOID)
            {
                Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(value));
                Assert.That(allocator.refPositions[^1].registerAssignment,
                    Is.EqualTo(genSingleTypeRegMask(REG_R0)));
            }
        });
    }

    [Test]
    public static void LongReturnsConstrainLowAndHighHalvesToR0AndR1()
    {
        WithAllocator((compiler, allocator) => {
            var low = compiler.gtNewIconNode(TYP_INT, 17);
            var high = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, low);
            _ = Build(allocator, high);
            var pair = new GenTreeOp(GT_LONG, TYP_LONG, low, high) { IsContained = true };
            var node = new GenTreeUnOp(GT_RETURN, TYP_LONG, pair);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(low));
            Assert.That(allocator.refPositions[start].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R0)));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(high));
            Assert.That(allocator.refPositions[start + 1].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R1)));
        });
    }

    [Test]
    public static void CopiesConsumeTheSourceBeforeDefiningTheDestination()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var node = new GenTreeCopyOrReload(GT_COPY, TYP_INT, value);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(value));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [Test]
    public static void RegisterArgumentsConstrainBothTheUseAndDefinition()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var node = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, value) { RegNum = REG_R2 };
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R2)));
            Assert.That(allocator.refPositions[^1].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R2)));
            Assert.That(PlacedArgumentRegisters(allocator).IsSet(REG_R2), Is.True);
        });
    }

    [Test]
    public static void HeapReferenceStoresReuseTheArmWriteBarrierBuilder()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_REF }];
            compiler.lvaCount = 1;
            var value = compiler.gtNewLclvNode(TYP_REF, 0);
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            _ = Build(allocator, address);
            _ = Build(allocator, value);
            var node = new GenTreeStoreInd(TYP_REF, address, value) { Flags = GTF_IND_TGT_HEAP };
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(address));
            Assert.That(allocator.refPositions[start].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R0)));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(value));
            Assert.That(allocator.refPositions[start + 1].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R1)));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef), Is.Empty);
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.refType is RefType.RefTypeKill), Is.Not.Empty);
        });
    }

    [Test]
    public static void ReturnTrapConsumesItsConditionBeforeHelperKills()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var node = new GenTreeUnOp(GT_RETURNTRAP, TYP_VOID, value);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(value));
            Assert.That(allocator.refPositions[start].refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef), Is.Empty);
        });
    }

    [Test]
    public static void PreemptiveGcTransitionBuildsNoFixedRegisterKills()
    {
        WithAllocator((_, allocator) => {
            Assert.That(Build(allocator, new GenTree(GT_START_PREEMPTGC, TYP_VOID)), Is.Zero);
            Assert.That(allocator.refPositions, Is.Empty);
        });
    }

    [TestCase(GT_CAST)]
    [TestCase(GT_CALL)]
    [TestCase(GT_STORE_BLK)]
    [TestCase(GT_LCLHEAP)]
    [TestCase(GT_IND)]
    [TestCase(GT_STOREIND)]
    [TestCase(GT_PUTARG_STK)]
    public static void ArmSupportBuildersConstructTheirActualReferences(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            if (operation is GT_LCLHEAP)
            {
                value.IsContained = true;
            }
            else if (operation is not GT_CALL)
            {
                _ = Build(allocator, value);
            }
            if (operation is GT_STORE_BLK or GT_IND or GT_STOREIND)
            {
                _ = Build(allocator, address);
            }
            compiler.eeInfo.osPageSize = 4096;
            compiler.eeInfoInitialized = true;
            var initialization = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, value) { IsContained = true };
            GenTree node = operation switch {
                GT_CAST => new GenTreeCast(TYP_DOUBLE, value, false, TYP_DOUBLE),
                GT_CALL => new GenTreeCall(TYP_VOID),
                GT_STORE_BLK => new GenTreeBlk(TYP_STRUCT, address, initialization, new ClassLayout(4)) {
                    _kind = GenTreeBlk.BlkOpKindUnroll,
                },
                GT_LCLHEAP => new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, value),
                GT_IND => new GenTreeIndir(GT_IND, TYP_INT, address),
                GT_STOREIND => new GenTreeStoreInd(TYP_INT, address, value),
                GT_PUTARG_STK => new GenTreePutArgStk(TYP_VOID, value, null, 0, 4, false),
                _ => throw new AssertionException("Unexpected builder fixture."),
            };

            var expectedSourceCount = operation switch {
                GT_CALL or GT_LCLHEAP => 0,
                GT_STORE_BLK or GT_STOREIND => 2,
                _ => 1,
            };
            Assert.That(Build(allocator, node), Is.EqualTo(expectedSourceCount));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(operation is GT_CALL ? 1 : 0));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal),
                Has.Count.EqualTo(operation is GT_CAST or GT_LCLHEAP or GT_IND ? 1 : 0));
        });
    }

    [TestCase(TYP_FLOAT, TYP_INT, 1)]
    [TestCase(TYP_DOUBLE, TYP_INT, 1)]
    [TestCase(TYP_INT, TYP_DOUBLE, 0)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, 0)]
    public static void CastsDelayOnlyTheirFloatingToIntegerTemporary(
        var_types sourceType, var_types destinationType, int temporaryCount)
    {
        WithAllocator((compiler, allocator) => {
            GenTree source = varTypeIsFloating(sourceType)
                ? compiler.gtNewDconNode(sourceType, 2.5)
                : compiler.gtNewIconNode(sourceType, 23);
            _ = Build(allocator, source);
            var node = new GenTreeCast(destinationType, source, false, destinationType);

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(temporaryCount));
            Assert.That(PendingDelayFree(allocator), Is.EqualTo(temporaryCount != 0));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeUse &&
                reference.getInterval().isInternal && reference.delayRegFree),
                Has.Count.EqualTo(temporaryCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeDef));
        });
    }

    [Test]
    public static void ContainedLongCastsConsumeBothIndependentHalves()
    {
        WithAllocator((compiler, allocator) => {
            var low = compiler.gtNewIconNode(TYP_INT, 17);
            var high = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, low);
            _ = Build(allocator, high);
            var pair = new GenTreeOp(GT_LONG, TYP_LONG, low, high) { IsContained = true };
            var node = new GenTreeCast(TYP_INT, pair, false, TYP_INT);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(low));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(high));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
        });
    }

    [TestCase(TYP_INT, false, false, 0)]
    [TestCase(TYP_FLOAT, false, false, 0)]
    [TestCase(TYP_DOUBLE, false, false, 0)]
    [TestCase(TYP_INT, true, false, 0)]
    [TestCase(TYP_FLOAT, true, false, 1)]
    [TestCase(TYP_DOUBLE, true, false, 2)]
    [TestCase(TYP_INT, false, true, 0)]
    [TestCase(TYP_FLOAT, false, true, 0)]
    [TestCase(TYP_DOUBLE, false, true, 0)]
    [TestCase(TYP_INT, true, true, 0)]
    [TestCase(TYP_FLOAT, true, true, 1)]
    [TestCase(TYP_DOUBLE, true, true, 2)]
    public static void UnalignedIndirectionsMaterializeEachFloatingWord(
        var_types type, bool unaligned, bool store, int temporaryCount)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            _ = Build(allocator, address);
            GenTree value = varTypeIsFloating(type)
                ? compiler.gtNewDconNode(type, 2.5)
                : compiler.gtNewIconNode(type, 23);
            if (store)
            {
                _ = Build(allocator, value);
            }
            GenTreeIndir node = store
                ? new GenTreeStoreInd(type, address, value)
                : new GenTreeIndir(GT_IND, type, address);
            if (unaligned)
            {
                node.Flags |= GTF_IND_UNALIGNED;
            }

            Assert.That(Build(allocator, node), Is.EqualTo(store ? 2 : 1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(temporaryCount));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal), Has.Count.EqualTo(store ? 0 : 1));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == address && reference.refType is RefType.RefTypeUse),
                Has.Count.EqualTo(1));
            if (store)
            {
                Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(value));
                Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeUse));
            }
        });
    }

    [TestCase(false, -256, 1)]
    [TestCase(false, -255, 0)]
    [TestCase(false, 4095, 0)]
    [TestCase(false, 4096, 1)]
    [TestCase(true, 0, 0)]
    [TestCase(true, 8, 1)]
    public static void ContainedIndirectionAddressesRespectArmLoadStoreOffsetEncodings(
        bool indexed, int offset, int temporaryCount)
    {
        WithAllocator((compiler, allocator) => {
            var addressBase = compiler.gtNewIconNode(TYP_BYREF, 32);
            var index = compiler.gtNewIconNode(TYP_INT, 3);
            _ = Build(allocator, addressBase);
            if (indexed)
            {
                _ = Build(allocator, index);
            }
            var address = new GenTreeAddrMode(
                TYP_BYREF, addressBase, indexed ? index : null, indexed ? (byte)1 : (byte)0, offset) {
                IsContained = true,
            };
            var node = new GenTreeIndir(GT_IND, TYP_INT, address);

            Assert.That(Build(allocator, node), Is.EqualTo(indexed ? 2 : 1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(temporaryCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
        });
    }

    [TestCase(0, false, false, 0)]
    [TestCase(16, true, false, 0)]
    [TestCase(17, false, false, 0)]
    [TestCase(17, true, false, 1)]
    [TestCase(4095, false, false, 1)]
    [TestCase(4096, false, false, 1)]
    [TestCase(-1, true, false, 0)]
    [TestCase(0, false, true, 1)]
    [TestCase(4096, true, true, 1)]
    public static void ConstantLocalHeapTempsFollowArmPushProbeAndOutgoingAreaRules(
        int size, bool initialize, bool outgoingArea, int temporaryCount)
    {
        WithAllocator((compiler, allocator) => {
            compiler.info.compInitMem = initialize;
            compiler.eeInfo.osPageSize = 4096;
            compiler.eeInfoInitialized = true;
            var sizeNode = compiler.gtNewIconNode(TYP_I_IMPL, size);
            sizeNode.IsContained = true;
            var node = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, sizeNode);

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(temporaryCount));
            Assert.That(PendingDelayFree(allocator), Is.EqualTo(temporaryCount != 0));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeUse &&
                reference.getInterval().isInternal && reference.delayRegFree),
                Has.Count.EqualTo(temporaryCount));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));

            _ = Build(allocator, compiler.gtNewIconNode(TYP_INT, 7));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
            Assert.That(PendingDelayFree(allocator), Is.False);
        }, outgoingArgSpace: outgoingArea ? 64 : 0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VariableLocalHeapConsumesTheSizeAndDelaysItsTemporary(bool initialize)
    {
        WithAllocator((compiler, allocator) => {
            compiler.info.compInitMem = initialize;
            compiler.lvaTable = [new() { Type = TYP_I_IMPL }];
            compiler.lvaCount = 1;
            var size = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            _ = Build(allocator, size);
            var node = new GenTreeUnOp(GT_LCLHEAP, TYP_I_IMPL, size);

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(PendingDelayFree(allocator), Is.True);
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(node));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructStackArgumentsReserveOneCopyRegisterAndOnlyUseExplicitAddresses(bool indirect)
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new() { Type = TYP_STRUCT }];
            compiler.lvaCount = 1;
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            if (indirect)
            {
                _ = Build(allocator, address);
            }
            GenTree source = indirect
                ? new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(8)) { IsContained = true }
                : new GenTreeLclVar(TYP_STRUCT, 0) { IsContained = true };
            var node = new GenTreePutArgStk(TYP_VOID, source, null, 0, 8, false);

            Assert.That(Build(allocator, node), Is.EqualTo(indirect ? 1 : 0));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal), Is.Empty);
        });
    }

    [Test]
    public static void FieldListStackArgumentsConsumeFieldsInOrderWithoutAnExtraCopyRegister()
    {
        WithAllocator((compiler, allocator) => {
            var first = compiler.gtNewIconNode(TYP_INT, 17);
            var second = compiler.gtNewDconNode(TYP_DOUBLE, 2.5);
            _ = Build(allocator, first);
            _ = Build(allocator, second);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddField(compiler, first, 0, TYP_INT);
            fields.AddField(compiler, second, 4, TYP_DOUBLE);
            var node = new GenTreePutArgStk(TYP_VOID, fields, null, 0, 12, false);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(allocator.refPositions[start].treeNode, Is.SameAs(first));
            Assert.That(allocator.refPositions[start + 1].treeNode, Is.SameAs(second));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
        });
    }

    [TestCase(GenTreeBlk.BlkOpKindUnroll, false, 0)]
    [TestCase(GenTreeBlk.BlkOpKindUnroll, true, 0)]
    [TestCase(GenTreeBlk.BlkOpKindLoop, false, 1)]
    [TestCase(GenTreeBlk.BlkOpKindLoop, true, 1)]
    public static void BlockInitializationReservesOnlyTheLoopOffsetRegister(
        GenTreeBlk.BlkOpKind kind, bool containedFill, int temporaryCount)
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            var fill = compiler.gtNewIconNode(TYP_INT, containedFill ? 0 : 0x5A);
            _ = Build(allocator, address);
            if (!containedFill)
            {
                _ = Build(allocator, fill);
            }
            fill.IsContained = containedFill;
            var initialization = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill) { IsContained = true };
            var node = new GenTreeBlk(TYP_STRUCT, address, initialization, new ClassLayout(24)) { _kind = kind };

            Assert.That(Build(allocator, node), Is.EqualTo(containedFill ? 1 : 2));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(temporaryCount));
            Assert.That(allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal), Is.Empty);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void UnrolledBlockCopiesConsumeContainedAddressBasesAndOneCopyRegister(
        bool containedDestination, bool containedSource)
    {
        WithAllocator((compiler, allocator) => {
            var destinationBase = compiler.gtNewIconNode(TYP_BYREF, 32);
            var sourceBase = compiler.gtNewIconNode(TYP_BYREF, 64);
            _ = Build(allocator, destinationBase);
            _ = Build(allocator, sourceBase);
            GenTree destination = containedDestination
                ? new GenTreeAddrMode(TYP_BYREF, destinationBase, null, 0, 8) { IsContained = true }
                : destinationBase;
            GenTree sourceAddress = containedSource
                ? new GenTreeAddrMode(TYP_BYREF, sourceBase, null, 0, 8) { IsContained = true }
                : sourceBase;
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var node = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout(24)) {
                _kind = GenTreeBlk.BlkOpKindUnroll,
            };

            Assert.That(Build(allocator, node), Is.EqualTo(2));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            var uses = allocator.refPositions.FindAll(reference =>
                reference.refType is RefType.RefTypeUse && !reference.getInterval().isInternal);
            Assert.That(uses, Has.Count.EqualTo(2));
            Assert.That(uses[0].treeNode, Is.SameAs(destinationBase));
            Assert.That(uses[1].treeNode, Is.SameAs(sourceBase));
        });
    }

    [TestCase(TYP_VOID, REG_NA)]
    [TestCase(TYP_INT, REG_R0)]
    [TestCase(TYP_FLOAT, REG_F0)]
    [TestCase(TYP_DOUBLE, REG_F0)]
    public static void DirectCallsBuildOneTargetTemporaryAndTheirAbiResult(var_types type, regNumber returnRegister)
    {
        WithAllocator((_, allocator) => {
            var node = new GenTreeCall(type);

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            var definitions = allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal);
            Assert.That(definitions, Has.Count.EqualTo(type is TYP_VOID ? 0 : 1));
            if (type is not TYP_VOID)
            {
                Assert.That(definitions[0].registerAssignment, Is.EqualTo(genSingleTypeRegMask(returnRegister)));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void IndirectCallsConstrainFastTailTargetsAndNullChecks(bool fastTail, bool nullCheck)
    {
        WithAllocator((compiler, allocator) => {
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 32);
            _ = Build(allocator, target);
            var targetDefinition = allocator.refPositions[^1];
            var node = new GenTreeCall(TYP_VOID) {
                _callType = gtCallTypes.CT_INDIRECT, ControlExpr = target,
            };
            if (fastTail)
            {
                node._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            if (nullCheck)
            {
                node.Flags |= GTF_CALL_NULLCHECK;
            }

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(nullCheck ? 1 : 0));
            Assert.That(targetDefinition.nextRefPosition?.registerAssignment,
                Is.EqualTo(fastTail ? AvailableIntRegs(allocator) & SRBM_INT_CALLEE_TRASH & ~SRBM_LR
                    : AvailableIntRegs(allocator)));
            if (fastTail && nullCheck)
            {
                Assert.That(allocator.refPositions.Find(reference =>
                    reference.getInterval().isInternal && reference.refType is RefType.RefTypeDef)
                    ?.registerAssignment, Is.EqualTo(SRBM_LR));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RelativeStubCallsReserveTheirTargetRegisterWithNativeVolatilePreferences(bool fastTail)
    {
        WithAllocator((_, allocator) => {
            var node = new GenTreeCall(TYP_VOID) { Flags = GTF_CALL_VIRT_STUB };
            node._callMoreFlags |= GTF_CALL_M_VIRTSTUB_REL_INDIRECT;
            if (fastTail)
            {
                node._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(InternalDefinitionCount(allocator), Is.EqualTo(1));
            Assert.That(allocator.refPositions[0].getInterval().isInternal, Is.True);
            Assert.That(allocator.refPositions[0].registerAssignment,
                Is.EqualTo(fastTail ? AvailableIntRegs(allocator) & SRBM_INT_CALLEE_TRASH
                    : AvailableIntRegs(allocator)));
        });
    }

    [Test]
    public static void FastTailCookieChecksExcludeBothNativeScratchRegisters()
    {
        WithAllocator((compiler, allocator) => {
            compiler.NeedsGSSecurityCookie = true;
            var target = compiler.gtNewIconNode(TYP_I_IMPL, 32);
            _ = Build(allocator, target);
            var targetDefinition = allocator.refPositions[^1];
            var node = new GenTreeCall(TYP_VOID) {
                _callType = gtCallTypes.CT_INDIRECT, ControlExpr = target,
            };
            node._callMoreFlags |= GTF_CALL_M_TAILCALL;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(targetDefinition.nextRefPosition?.registerAssignment,
                Is.EqualTo(AvailableIntRegs(allocator) & SRBM_INT_CALLEE_TRASH & ~(SRBM_R12 | SRBM_LR)));
            Assert.That(InternalDefinitionCount(allocator), Is.Zero);
        });
    }

    [Test]
    public static void LongCallsDefineBothInitializedAbiReturnRegisters()
    {
        WithAllocator((_, allocator) => {
            var node = new GenTreeCall(TYP_LONG);
            node._returnTypeDesc.InitializeLongReturnType();

            Assert.That(Build(allocator, node), Is.Zero);
            var definitions = allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal);
            Assert.That(definitions, Has.Count.EqualTo(2));
            Assert.That(definitions[0].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R0)));
            Assert.That(definitions[1].registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R1)));
        });
    }

    [Test]
    public static void InitPInvokeFrameUsesThePinnedR5ReturnConvention()
    {
        WithAllocator((compiler, allocator) => {
            var node = compiler.gtNewHelperCallNode(TYP_I_IMPL, CorInfoHelpFunc.CORINFO_HELP_INIT_PINVOKE_FRAME);

            Assert.That(Build(allocator, node), Is.Zero);
            var definition = allocator.refPositions.FindLast(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal);
            Assert.That(definition?.registerAssignment, Is.EqualTo(genSingleTypeRegMask(REG_R5)));
        });
    }

    [Test]
    public static void AsyncCallsKeepTheContinuationRegisterBusyUntilTheFollowingNode()
    {
        WithAllocator((compiler, allocator) => {
            compiler.compIsAsync = true;
            var node = new GenTreeCall(TYP_VOID);
            node._callMoreFlags |= GTF_CALL_M_ASYNC;
            node.Next = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF);

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(allocator.refPositions.Exists(reference =>
                reference.refType is RefType.RefTypeKill && reference.delayRegFree &&
                reference.registerAssignment == genSingleTypeRegMask(REG_ASYNC_CONTINUATION_RET)), Is.True);
            Assert.That(PendingDelayFree(allocator), Is.True);
        });
    }

    [Test]
    public static void CallsConsumePlacedRegisterArgumentsThenResetTheirPlacementState()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            _ = Build(allocator, value);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, value) { RegNum = REG_R2 };
            _ = Build(allocator, argument);
            var argumentDefinition = allocator.refPositions[^1];
            Assert.That(PlacedArgumentRegisters(allocator).IsSet(REG_R2), Is.True);
            var node = new GenTreeCall(TYP_VOID);
            node.Args.PushLateBack(new CallArg(NewCallArg.CreateForPrimitive(value)) { LateNode = argument });

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(argumentDefinition.nextRefPosition?.refType, Is.EqualTo(RefType.RefTypeUse));
            Assert.That(argumentDefinition.nextRefPosition?.registerAssignment,
                Is.EqualTo(genSingleTypeRegMask(REG_R2)));
            Assert.That(PlacedArgumentRegisters(allocator), Is.EqualTo(RBM_NONE));
        });
    }

    [Test]
    public static void LongCopiesRetainBothDefinitionsOfAnInitializedMultiRegisterSource()
    {
        WithAllocator((compiler, allocator) => {
            var source = new GenTreeCall(TYP_LONG);
            source._returnTypeDesc.InitializeLongReturnType();
            _ = Build(allocator, source);
            var node = new GenTreeCopyOrReload(GT_COPY, TYP_LONG, source);

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            var definitions = allocator.refPositions.FindAll(reference =>
                reference.treeNode == node && reference.refType is RefType.RefTypeDef &&
                !reference.getInterval().isInternal);
            Assert.That(definitions, Has.Count.EqualTo(2));
            Assert.That(node.GetRegisterDstCount(compiler), Is.EqualTo(2));
        });
    }

    [Test]
    public static void ArmUnrolledMemmoveRetainsTheNativeUnreachedBoundary()
    {
        WithAllocator((compiler, allocator) => {
            var destination = compiler.gtNewIconNode(TYP_BYREF, 32);
            var sourceAddress = compiler.gtNewIconNode(TYP_BYREF, 64);
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var node = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout(24)) {
                _kind = GenTreeBlk.BlkOpKindUnrollMemmove,
            };

            var exception = Assert.Throws<FatalJitException>(() => Build(allocator, node));
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(allocator.refPositions, Is.Empty);
        }, captureAssertions: true);
    }

    [TestCase(GT_PATCHPOINT)]
    [TestCase(GT_PATCHPOINT_FORCED)]
    public static void ArmPatchpointsKeepTheirNativeNyiBoundary(genTreeOps operation)
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            GenTree node = operation is GT_PATCHPOINT
                ? new GenTreeOp(operation, TYP_VOID, value, value)
                : new GenTreeUnOp(operation, TYP_VOID, value);

            var exception = Assert.Throws<FatalJitException>(() => Build(allocator, node));
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(allocator.refPositions, Is.Empty);
        }, captureAssertions: true);
    }

    [Test]
    public static void UnhandledOperationsRetainTheDebugNyiAndReleaseUnreachedDistinction()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            var node = new GenTreeOp(GT_MOD, TYP_INT, value, value);

            var exception = Assert.Throws<FatalJitException>(() => Build(allocator, node));
#if DEBUG
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#else
            Assert.That(exception?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
#endif
            Assert.That(allocator.refPositions, Is.Empty);
        }, captureAssertions: true);
    }

#if DEBUG
    [Test]
    public static void UnexpectedInitValuePreservesItsAssertionAndContinuingEePath()
    {
        WithAllocator((compiler, allocator) => {
            var value = compiler.gtNewIconNode(TYP_INT, 23);
            var node = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, value);

            Assert.That(Build(allocator, node), Is.Zero);
            Assert.That(s_assertions, Is.EqualTo(new[] { "!\"INIT_VAL should always be contained\"" }));
            Assert.That(allocator.refPositions, Is.Empty);
        }, captureAssertions: true);
    }

    [Test]
    public static void UnexpectedNullCheckAssertsBeforeBuildingOnlyItsAddressUse()
    {
        WithAllocator((compiler, allocator) => {
            var address = compiler.gtNewIconNode(TYP_BYREF, 32);
            _ = Build(allocator, address);
            var node = new GenTreeIndir(GT_NULLCHECK, TYP_VOID, address);
            var start = allocator.refPositions.Count;

            Assert.That(Build(allocator, node), Is.EqualTo(1));
            Assert.That(s_assertions, Is.EqualTo(new[] { "!\"Should never see GT_NULLCHECK on Arm/32\"" }));
            Assert.That(allocator.refPositions.Count - start, Is.EqualTo(1));
            Assert.That(allocator.refPositions[^1].treeNode, Is.SameAs(address));
            Assert.That(allocator.refPositions[^1].refType, Is.EqualTo(RefType.RefTypeUse));
        }, captureAssertions: true);
    }

    private static readonly List<string> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);
#endif

    private static int Build(LinearScan allocator, GenTree node)
    {
        ReferenceBuildLocation(allocator) += 2;
        return BuildNode(allocator, node);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildNode")]
    private static extern int BuildNode(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildShiftLongCarry")]
    private static extern int BuildShiftLongCarry(LinearScan allocator, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "clearBuildState")]
    private static extern void ClearBuildState(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "buildPhysRegRecords")]
    private static extern void BuildPhysRegRecords(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_referenceBuildLocation")]
    private static extern ref uint ReferenceBuildLocation(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_internalDefinitionCount")]
    private static extern ref int InternalDefinitionCount(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingDelayFree")]
    private static extern ref bool PendingDelayFree(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_placedArgumentRegisters")]
    private static extern ref regMaskTP PlacedArgumentRegisters(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableIntRegs")]
    private static extern ref regMask AvailableIntRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_availableFloatRegs")]
    private static extern ref regMask AvailableFloatRegs(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmIntCalleeTrash")]
    private static extern ref regMask CompilerIntCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmFltCalleeTrash")]
    private static extern ref regMask CompilerFloatCalleeTrash(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmMskCalleeTrash")]
    private static extern ref regMask CompilerMaskCalleeTrash(Compiler compiler);

    private static void WithAllocator(
        Action<Compiler, LinearScan> action, bool captureAssertions = false, int outgoingArgSpace = 0)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(captureAssertions ? &ee : null);
        var previousConfig = JitConfig;
        if (captureAssertions)
        {
            JitConfig = new JitConfigValues();
            AltJitSkipOnAssert(ref JitConfig) = 1;
            s_assertions.Clear();
        }
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaOutgoingArgSpaceSize.Value = outgoingArgSpace;
#if DEBUG
        compiler.info.compFullName = nameof(ArmLinearScanNodeBuildingTests);
        if (captureAssertions)
        {
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
        }
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        CompilerIntCalleeTrash(compiler) = SRBM_INT_CALLEE_TRASH_INIT;
        CompilerFloatCalleeTrash(compiler) = SRBM_FLT_CALLEE_TRASH_INIT;
        CompilerMaskCalleeTrash(compiler) = SRBM_MSK_CALLEE_TRASH_INIT;
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.CopyRegisterInfo();
            codeGen.IsFramePointerRequired = false;
            codeGen.IsFramePointerUsed = false;
            codeGen.IsFrameRequired = false;
            codeGen.RegSet.rsClearRegsModified();
            var allocator = new LinearScan(compiler);
            BuildPhysRegRecords(allocator);
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
#if DEBUG
            if (captureAssertions)
            {
                JitConfig = previousConfig;
            }
#endif
        }
    }
}
#endif

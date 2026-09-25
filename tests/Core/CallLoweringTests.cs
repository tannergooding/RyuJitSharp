// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallLoweringTests
{
    [TestCase(InfoAccessType.IAT_VALUE)]
    [TestCase(InfoAccessType.IAT_PVALUE)]
    [TestCase(InfoAccessType.IAT_RELPVALUE)]
    public static void DirectCallTargetsPreserveLookupShape(InfoAccessType accessType)
    {
        WithCompiler(compiler => {
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x5678,
                _entryPoint = new CORINFO_CONST_LOOKUP { accessType = accessType, addr = (void*)0x1234 },
            };
            var result = InvokeLowerDirectCall(lowering, call);
            if (accessType is InfoAccessType.IAT_VALUE)
            {
                Assert.That(result, Is.Null);
                Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x1234));
                return;
            }

            var target = result ?? throw new InvalidOperationException();
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0));
            if (accessType is InfoAccessType.IAT_PVALUE)
            {
                Assert.That(target.Oper, Is.EqualTo(genTreeOps.GT_IND));
                Assert.That(target.Flags & GenTreeFlags.GTF_IND_NONFAULTING, Is.Not.EqualTo(GenTreeFlags.GTF_EMPTY));
                AssertAddress(target.AsIndir().Addr);
#if DEBUG
                Assert.That(target.AsIndir().Addr.AsIntCon().TargetHandle, Is.EqualTo((nint)0x5678));
#endif
            }
            else
            {
                Assert.That(target.Oper, Is.EqualTo(genTreeOps.GT_ADD));
                var add = target.AsOp();
                Assert.That(add.Op1.Oper, Is.EqualTo(genTreeOps.GT_IND));
                AssertAddress(add.Op1.AsIndir().Addr);
                AssertAddress(add.Op2);
                Assert.That(add.Op1.AsIndir().Addr, Is.Not.SameAs(add.Op2));
            }
        });
    }

    [Test]
    public static void FastTailCallReusesItsReadyToRunIndirectionCell()
    {
        WithCompiler(compiler => {
            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMoreFlags = GenTreeCallFlags.GTF_CALL_M_TAILCALL,
                _entryPoint = new CORINFO_CONST_LOOKUP {
                    accessType = InfoAccessType.IAT_PVALUE,
                    addr = (void*)0x1234,
                },
            };

            Assert.That(call.IndirectionCellArgKind, Is.EqualTo(WellKnownArg.R2RIndirectionCell));
            Assert.That(InvokeLowerDirectCall(new Lowering(compiler, new LinearScan(compiler)), call), Is.Null);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ArgumentPlacementPreservesOrderAcrossInterferingCalls(bool interference)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var blockField = typeof(Lowering).GetField("_block", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            blockField.SetValue(lowering, block);

            var outer = new GenTreeCall(var_types.TYP_VOID);
            var nested = new GenTreeCall(var_types.TYP_VOID);
            var firstValue = new GenTreeIntCon(var_types.TYP_INT, 1);
            var secondValue = new GenTreeIntCon(var_types.TYP_INT, 2);
            var firstArg = new GenTreeUnOp(genTreeOps.GT_PUTARG_REG, var_types.TYP_INT, firstValue);
            var secondArg = new GenTreePutArgStk(var_types.TYP_VOID, secondValue, outer, 32, 8, false);
            outer.Args.PushBack(NewCallArg.CreateForPrimitive(firstValue)).EarlyNode = firstArg;
            var lateArg = outer.Args.PushBack(NewCallArg.CreateForPrimitive(secondValue));
            lateArg.LateNode = secondArg;
            lateArg.EarlyNode = null;

            GenTree[] nodes = interference
                ? [firstValue, firstArg, secondValue, secondArg, nested, outer]
                : [firstValue, secondValue, nested, firstArg, secondArg, outer];
            foreach (var node in nodes)
            {
                block.InsertAtEnd(node);
            }

            var legalize = typeof(Lowering).GetMethod("LegalizeArgPlacement", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = legalize.Invoke(lowering, [outer]);

            GenTree[] expected = [firstValue, secondValue, nested, firstArg, secondArg, outer];
            var current = block.FirstNode;
            GenTree? previous = null;
            foreach (var node in expected)
            {
                Assert.That(current, Is.SameAs(node));
                Assert.That(node.Prev, Is.SameAs(previous));
                Assert.That(node._lirFlags & LIR.Flags.Mark, Is.EqualTo(LIR.Flags.None));
                previous = node;
                current = node.Next;
            }
            Assert.That(current, Is.Null);
            Assert.That(block.LastNode, Is.SameAs(outer));
        });
    }

    [TestCase(0, 0, false)]
    [TestCase(32, 0, false)]
    [TestCase(56, 32, false)]
    [TestCase(64, 32, true)]
    public static void OutgoingAreaRequiresFramePointerAtFourExtraSlots(int firstSize, int secondSize, bool required)
    {
        WithCompiler(compiler => {
            var codeGen = new CodeGen(compiler) { IsFramePointerRequired = false };
            compiler.codeGen = codeGen;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var call = new GenTreeCall(var_types.TYP_VOID);
            var requireSpace = typeof(Lowering).GetMethod("RequireOutgoingArgSpace", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = requireSpace.Invoke(lowering, [call, firstSize]);
            _ = requireSpace.Invoke(lowering, [call, secondSize]);

            var setFramePointer = typeof(Lowering).GetMethod("SetFramePointerFromArgSpaceSize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            _ = setFramePointer.Invoke(lowering, null);
            Assert.That(codeGen.IsFramePointerRequired, Is.EqualTo(required));
        });
    }

    [TestCase(int.MinValue, false)]
    [TestCase(0x7FC12345, false)]
    [TestCase(int.MinValue, true)]
    public static void RegisterArgumentsReinterpretOnlyValueBits(int bits, bool minOpts)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var call = new GenTreeCall(var_types.TYP_VOID);
            var value = new GenTreeDblCon(var_types.TYP_FLOAT, BitConverter.Int32BitsToSingle(bits));
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            block.InsertAtEnd(value);
            block.InsertAtEnd(call);
            var segment = AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 8);

            InsertPutArgReg(lowering, ref arg.NodeRef, in segment);

            var putArg = arg.Node;
            Assert.That(putArg.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(putArg.Type, Is.EqualTo(var_types.TYP_INT));
            Assert.That(putArg.RegNum, Is.EqualTo(regNumber.REG_RCX));
            Assert.That(putArg.Next, Is.SameAs(call));
            Assert.That(call.Prev, Is.SameAs(putArg));
            var operand = putArg.AsUnOp().Op1;
            if (minOpts)
            {
                Assert.That(operand.Oper, Is.EqualTo(genTreeOps.GT_BITCAST));
                Assert.That(operand.AsUnOp().Op1, Is.SameAs(value));
                Assert.That(block.FirstNode, Is.SameAs(value));
                Assert.That(value.Next, Is.SameAs(operand));
            }
            else
            {
                Assert.That(operand.Oper, Is.EqualTo(genTreeOps.GT_CNS_INT));
                Assert.That(operand.AsIntCon().IconValue, Is.EqualTo((nint)bits));
                Assert.That(block.FirstNode, Is.SameAs(operand));
                Assert.That(value.Next, Is.Null);
                Assert.That(value.Prev, Is.Null);
            }
            Assert.That(operand.Next, Is.SameAs(putArg));
            Assert.That(putArg.Prev, Is.SameAs(operand));
        }, minOpts);
    }

    [Test]
    public static void IntegerFieldsPackIntoOneRegisterWithoutSignExtension()
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(true);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var first = compiler.gtNewIconNode(var_types.TYP_INT, -1);
            var second = compiler.gtNewIconNode(var_types.TYP_INT, int.MinValue);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, var_types.TYP_INT);
            fields.AddFieldLIR(compiler, second, 4, var_types.TYP_INT);
            var call = new GenTreeCall(var_types.TYP_VOID);
            var arg = call.Args.PushBack(NewCallArg.CreateForStruct(fields, var_types.TYP_STRUCT, new ClassLayout(8)));
            arg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 8);
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(call);

            LowerArgsForCall(lowering, call);

            var putArg = arg.Node;
            Assert.That(putArg.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(putArg.RegNum, Is.EqualTo(regNumber.REG_RCX));
            var combined = putArg.AsUnOp().Op1.AsOp();
            Assert.That(combined.Oper, Is.EqualTo(genTreeOps.GT_OR));
            Assert.That(combined.Type, Is.EqualTo(var_types.TYP_LONG));
            var low = combined.Op1.AsCast();
            Assert.That(low.IsUnsigned, Is.True);
            Assert.That(low.CastOp, Is.SameAs(first));
            var shift = combined.Op2.AsOp();
            Assert.That(shift.Oper, Is.EqualTo(genTreeOps.GT_LSH));
            Assert.That(shift.Op2.AsIntCon().IconValue, Is.EqualTo((nint)32));
            Assert.That(shift.Op2.IsContained, Is.True);
            var high = shift.Op1.AsCast();
            Assert.That(high.IsUnsigned, Is.True);
            Assert.That(high.CastOp, Is.SameAs(second));
            Assert.That(putArg.Next, Is.SameAs(call));
            Assert.That(fields.Next, Is.Null);
            Assert.That(fields.Prev, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(16, 1, true)]
    [TestCase(16, 2, false)]
    [TestCase(24, 1, false)]
    public static void DependentVectorFieldsWidenOnlyWithinSingleFieldParent(int parentSize, byte fieldCount, bool widen)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[2];
            compiler.lvaCount = 2;
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = var_types.TYP_STRUCT;
            parent.Layout = new ClassLayout(parentSize);
            parent.lvPromoted = true;
            parent.lvDoNotEnregister = true;
            parent.lvFieldCnt = fieldCount;
            ref var field = ref compiler.lvaTable[1];
            field.Type = var_types.TYP_SIMD12;
            field.Layout = new ClassLayout(12);
            field.lvIsStructField = true;
            field.lvParentLcl = 0;
            var local = compiler.gtNewLclvNode(var_types.TYP_SIMD12, 1);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            block.InsertAtEnd(local);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;

            Assert.That(LowerNode(lowering, local), Is.Null);
            Assert.That(local.Type, Is.EqualTo(widen ? var_types.TYP_SIMD16 : var_types.TYP_SIMD12));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalBitcastsUseAllocatorContainmentRules(bool enregisterLocals, bool doNotEnregister)
    {
        WithCompiler(compiler => {
            compiler.opts.compFlags = enregisterLocals ? Globals.CLFLG_REGVAR : 0;
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            compiler.lvaTable[0].Type = var_types.TYP_FLOAT;
            compiler.lvaTable[0].lvDoNotEnregister = doNotEnregister;
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var value = compiler.gtNewLclvNode(var_types.TYP_FLOAT, 0);
            var bitcast = compiler.gtNewBitCastNode(var_types.TYP_INT, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(bitcast);

            ContainCheckBitCast(lowering, bitcast);

            var contained = !enregisterLocals || doNotEnregister;
            Assert.That(value.IsContained, Is.EqualTo(contained));
            Assert.That(value.IsRegOptional, Is.EqualTo(!contained));
        });
    }

    [Test]
    public static void SingleFieldArgumentBecomesItsRegisterPlacement()
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(true);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var value = compiler.gtNewIconNode(var_types.TYP_INT, 42);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, 0, var_types.TYP_INT);
            var call = new GenTreeCall(var_types.TYP_VOID);
            var arg = call.Args.PushBack(NewCallArg.CreateForStruct(fields, var_types.TYP_STRUCT, compiler.typGetBlkLayout(4)));
            arg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 4);
            block.InsertAtEnd(value);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(call);

            LowerArgFieldList(lowering, arg, fields);

            Assert.That(arg.Node.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(arg.Node.RegNum, Is.EqualTo(regNumber.REG_RCX));
            Assert.That(arg.Node.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(arg.Node));
            Assert.That(arg.Node.Next, Is.SameAs(call));
            Assert.That(fields.Next, Is.Null);
            Assert.That(fields.Prev, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CallArgumentsLowerEarlyAndLateOwnersBeforePlacement(bool lateStackArg, bool tailCall)
    {
        WithCompiler(compiler => {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            var call = new GenTreeCall(var_types.TYP_VOID);
            if (tailCall)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
            }
            var nested = new GenTreeCall(var_types.TYP_VOID);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_INT, 1);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_LONG, 2);
            var firstArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(firstValue));
            firstArg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 4);
            var secondArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(secondValue));
            secondArg.AbiInfo.Segments[0] = AbiPassingSegment.OnStack(32, 0, 8);
            if (lateStackArg)
            {
                secondArg.EarlyNode = null;
                secondArg.LateNode = secondValue;
                LateHead(ref call.Args) = secondArg;
            }
            block.InsertAtEnd(firstValue);
            block.InsertAtEnd(secondValue);
            block.InsertAtEnd(nested);
            block.InsertAtEnd(call);

            LowerArgsForCall(lowering, call);

            Assert.That(firstArg.Node.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(firstArg.Node.RegNum, Is.EqualTo(regNumber.REG_RCX));
            Assert.That(firstArg.Node.AsUnOp().Op1, Is.SameAs(firstValue));
            var stackArg = secondArg.Node.AsPutArgStk();
            Assert.That(stackArg.ArgOffset, Is.EqualTo(32));
            Assert.That(stackArg.StackByteSize, Is.EqualTo(8));
            Assert.That(stackArg.PutInIncomingArgArea, Is.EqualTo(tailCall));
            Assert.That(stackArg.Data, Is.SameAs(secondValue));
            Assert.That(secondValue.IsContained, Is.True);
            Assert.That(lateStackArg ? secondArg.LateNode : secondArg.EarlyNode, Is.SameAs(stackArg));
            GenTree[] expected = [firstValue, secondValue, nested, firstArg.Node, stackArg, call];
            var current = block.FirstNode;
            foreach (var node in expected)
            {
                Assert.That(current, Is.SameAs(node));
                current = node.Next;
            }
            Assert.That(current, Is.Null);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lateHead")]
    private static extern ref CallArg LateHead(ref CallArgs args);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerArgsForCall")]
    private static extern void LowerArgsForCall(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InsertPutArgReg")]
    private static extern void InsertPutArgReg(Lowering lowering, ref GenTree argNode, in AbiPassingSegment registerSegment);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBitCast")]
    private static extern void ContainCheckBitCast(Lowering lowering, GenTreeUnOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerArgFieldList")]
    private static extern void LowerArgFieldList(Lowering lowering, CallArg arg, GenTreeFieldList fieldList);

    private static GenTree? InvokeLowerDirectCall(Lowering lowering, GenTreeCall call)
    {
        var method = typeof(Lowering).GetMethod("LowerDirectCall", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        return (GenTree?)method.Invoke(lowering, [call]);
    }

    private static void AssertAddress(GenTree node)
    {
        Assert.That(node.Oper, Is.EqualTo(genTreeOps.GT_CNS_INT));
        Assert.That(node.AsIntCon().IconValue, Is.EqualTo((nint)0x1234));
        Assert.That(node.Flags & GenTreeFlags.GTF_ICON_HDL_MASK, Is.EqualTo(Globals.GTF_ICON_FTN_ADDR));
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

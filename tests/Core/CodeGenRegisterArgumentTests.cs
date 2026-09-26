// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
#endif
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenRegisterArgumentTests
{
    [TestCase(TYP_INT, REG_RAX, REG_RCX, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, REG_R9, REG_RDX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_REF, REG_RAX, REG_RCX, INS_mov, EA_8BYTE)]
    [TestCase(TYP_BYREF, REG_RDX, REG_R8, INS_mov, EA_8BYTE)]
    [TestCase(TYP_FLOAT, REG_XMM4, REG_XMM0, INS_movaps, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, REG_XMM5, REG_XMM1, INS_movaps, EA_8BYTE)]
    public static void PutArgConsumesMovesAndProducesTheArgument(
        var_types type, regNumber sourceReg, regNumber targetReg, instruction ins, emitAttr size)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var source = Physical(type, sourceReg);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, type, source) { RegNum = targetReg };
            codeGen.GCInfo.gcMarkRegPtrVal(sourceReg, type);

            codeGen.genPutArgReg(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertMove(descriptors[0], ins, size, targetReg, sourceReg);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(type == TYP_REF ? Mask(targetReg) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? Mask(targetReg) : default));
#if DEBUG
            Assert.That(source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(argument._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

    [TestCase(TYP_INT, REG_RCX)]
    [TestCase(TYP_REF, REG_RCX)]
    [TestCase(TYP_BYREF, REG_RDX)]
    [TestCase(TYP_FLOAT, REG_XMM0)]
    [TestCase(TYP_DOUBLE, REG_XMM1)]
    public static void AliasPutArgsStillConsumeAndProduceWithoutAnInstruction(var_types type, regNumber reg)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var source = Physical(type, reg);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, type, source) { RegNum = reg };
            codeGen.GCInfo.gcMarkRegPtrVal(reg, type);

            codeGen.genPutArgReg(argument);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(type == TYP_REF ? Mask(reg) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(type == TYP_BYREF ? Mask(reg) : default));
#if DEBUG
            Assert.That(source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(argument._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PutArgConsumesTheCopyBeforeUsingItsResult(bool alias)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var source = Physical(TYP_REF, REG_RAX);
            var copyReg = alias ? REG_RCX : REG_RDX;
            var copy = new GenTreeCopyOrReload(GT_COPY, TYP_REF, source) { RegNum = copyReg };
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_REF, copy) { RegNum = REG_RCX };
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);

            codeGen.genPutArgReg(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(alias ? 1 : 2));
            AssertMove(descriptors[0], INS_mov, EA_8BYTE, copyReg, REG_RAX);
            if (!alias)
            {
                AssertMove(descriptors[1], INS_mov, EA_8BYTE, REG_RCX, REG_RDX);
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RCX)));
#if DEBUG
            Assert.That(source._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(copy._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(argument._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED));
#endif
        });
    }

    [Test]
    public static void PlacementUsesLateOrderAndLeavesStackAndEarlyArgumentsAlone()
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            var second = PutArg(TYP_LONG, REG_R8);
            var first = PutArg(TYP_LONG, REG_RDX);
            var secondArg = AddArgument(compiler, call, second,
                AbiPassingSegment.InRegister(REG_RDX, 0, 8), late: false);
            var firstArg = AddArgument(compiler, call, first,
                AbiPassingSegment.InRegister(REG_RCX, 0, 8), late: false);
            secondArg.EarlyNode = null;
            secondArg.LateNode = second;
            firstArg.EarlyNode = null;
            firstArg.LateNode = first;
            call.Args.PushLateBack(firstArg);
            call.Args.PushLateBack(secondArg);

            var stackSource = Physical(TYP_LONG, REG_R11);
            var stack = new GenTreePutArgStk(TYP_LONG, stackSource, call, 32, 8, false);
            _ = AddArgument(compiler, call, stack, AbiPassingSegment.OnStack(32, 0, 8));
            var early = PutArg(TYP_LONG, REG_R9);
            _ = AddArgument(compiler, call, early, AbiPassingSegment.InRegister(REG_R9, 0, 8), late: false);
#if DEBUG
            first.UseNum = 0;
            second.UseNum = 1;
#endif

            codeGen.genCallPlaceRegArgs(call);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            // RDX must reach RCX before the next placement overwrites RDX.
            AssertMove(descriptors[0], INS_mov, EA_8BYTE, REG_RCX, REG_RDX);
            AssertMove(descriptors[1], INS_mov, EA_8BYTE, REG_RDX, REG_R8);
#if DEBUG
            Assert.That(first._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(second._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(stack._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo((GenTreeDebugFlags)0));
            Assert.That(stackSource._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo((GenTreeDebugFlags)0));
            Assert.That(early._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo((GenTreeDebugFlags)0));
#endif
        });
    }

    [TestCase(TYP_REF, false, false)]
    [TestCase(TYP_REF, false, true)]
    [TestCase(TYP_REF, true, false)]
    [TestCase(TYP_REF, true, true)]
    [TestCase(TYP_BYREF, false, false)]
    [TestCase(TYP_BYREF, false, true)]
    [TestCase(TYP_BYREF, true, false)]
    [TestCase(TYP_BYREF, true, true)]
    public static void OnlyFastTailCallsKeepArgumentsAliveInTheirAbiRegisters(
        var_types type, bool tailCall, bool alias)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            if (tailCall)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
            }
            var sourceReg = alias ? REG_RCX : REG_RAX;
            var argument = PutArg(type, sourceReg);
            _ = AddArgument(compiler, call, argument, AbiPassingSegment.InRegister(REG_RCX, 0, 8));
            codeGen.GCInfo.gcMarkRegPtrVal(sourceReg, type);

            codeGen.genCallPlaceRegArgs(call);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(alias ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur,
                Is.EqualTo(tailCall && type == TYP_REF ? Mask(REG_RCX) : default));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur,
                Is.EqualTo(tailCall && type == TYP_BYREF ? Mask(REG_RCX) : default));
#if DEBUG
            Assert.That(argument._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
#endif
        });
    }

    [TestCase(TYP_FLOAT, false)]
    [TestCase(TYP_FLOAT, true)]
    [TestCase(TYP_DOUBLE, false)]
    [TestCase(TYP_DOUBLE, true)]
    public static void VarargsDuplicateAllFloatAbiRegistersAfterPlacement(var_types type, bool varargs)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var call = new GenTreeCall(TYP_VOID);
            call.Args.IsVarArgs = varargs;
            regNumber[] floatRegs = [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3];
            regNumber[] sourceRegs = [REG_XMM4, REG_XMM5, REG_XMM6, REG_XMM3];
            regNumber[] intRegs = [REG_RCX, REG_RDX, REG_R8, REG_R9];
            var size = type == TYP_FLOAT ? EA_4BYTE : EA_8BYTE;

            for (var i = 0; i < floatRegs.Length; i++)
            {
                var argument = PutArg(type, sourceRegs[i]);
                _ = AddArgument(compiler, call, argument,
                    AbiPassingSegment.InRegister(floatRegs[i], 0, (int)size), late: i != 3);
            }
            var stack = new GenTreePutArgStk(type, Physical(type, REG_XMM7), call, 32, 8, false);
            _ = AddArgument(compiler, call, stack, AbiPassingSegment.OnStack(32, 0, (int)size));

            codeGen.genCallPlaceRegArgs(call);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(varargs ? 7 : 3));
            for (var i = 0; i < 3; i++)
            {
                AssertMove(descriptors[i], INS_movaps, size, floatRegs[i], sourceRegs[i]);
            }
            if (varargs)
            {
                for (var i = 0; i < floatRegs.Length; i++)
                {
                    AssertMove(descriptors[3 + i], INS_movd64, EA_8BYTE, intRegs[i], floatRegs[i]);
                }
            }
        });
    }

#if DEBUG
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DspCodeRecordsRegisterArgumentsAndGcState(bool placement, bool alias)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var sourceReg = alias ? REG_RCX : REG_RAX;
            var source = Physical(TYP_REF, sourceReg);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_REF, source)
            {
                RegNum = placement ? sourceReg : REG_RCX,
            };
            var call = new GenTreeCall(TYP_VOID);
            _ = AddArgument(compiler, call, argument, AbiPassingSegment.InRegister(REG_RCX, 0, 8));
            codeGen.GCInfo.gcMarkRegPtrVal(sourceReg, TYP_REF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_R10, TYP_BYREF);
            compiler.opts.dspCode = true;

            void Generate()
            {
                if (placement)
                {
                    codeGen.genCallPlaceRegArgs(call);
                }
                else
                {
                    codeGen.genPutArgReg(argument);
                }
            }

            var diagnostic = InstructionRecordingTestSupport.Capture(Generate);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(alias ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(placement ? default : Mask(REG_RCX)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(REG_R10)));
            Assert.That(diagnostic.Contains("mov", StringComparison.Ordinal), Is.EqualTo(!alias));
        });
    }
#endif

    private static GenTreePhysReg Physical(var_types type, regNumber reg)
        => new(reg, type) { RegNum = reg };

    private static GenTreeUnOp PutArg(var_types type, regNumber reg)
        => new(GT_PUTARG_REG, type, Physical(type, reg)) { RegNum = reg };

    private static CallArg AddArgument(Compiler compiler, GenTreeCall call, GenTree node,
        AbiPassingSegment segment, bool late = true)
    {
        var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(node));
        argument.AbiInfo = AbiPassingInformation.FromSegment(compiler, passedByRef: false, segment);
        if (late)
        {
            argument.EarlyNode = null;
            argument.LateNode = node;
            call.Args.PushLateBack(argument);
        }

        return argument;
    }

    private static regMaskTP Mask(regNumber reg)
        => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void AssertMove(Emitter.instrDesc descriptor, instruction ins, emitAttr size,
        regNumber destination, regNumber source)
    {
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
        Assert.That(descriptor.idReg1(), Is.EqualTo(destination));
        Assert.That(descriptor.idReg2(), Is.EqualTo(source));
    }
}

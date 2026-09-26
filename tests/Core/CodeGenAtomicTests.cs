// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BarrierKind;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenAtomicTests
{
    [TestCase(INS_prefetcht0)]
    [TestCase(INS_prefetcht1)]
    [TestCase(INS_prefetcht2)]
    [TestCase(INS_prefetchnta)]
    public static void UnaryMemoryEntrySupportsNativePrefetchForms(instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            codeGen.Emitter.emitIns_AR(ins, EA_1BYTE, REG_RCX, 16);
            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amDisp, Is.EqualTo(16));
        });
    }

    [TestCase(INS_shl_N)]
    [TestCase(INS_ror_N)]
    [TestCase(INS_sar_N)]
    public static void ImmediateMemoryShiftsMaskTheCountAndRetainLargeDisplacements(instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            codeGen.Emitter.emitIns_I_AR(ins, EA_8BYTE, 130, REG_RCX, 8192);
            var id = Descriptors(codeGen).Single();
            Assert.That(id.idInsFmt(), Is.EqualTo(Emitter.insFormat.IF_ARW_SHF));
            Assert.That(id.idIsLargeDsp(), Is.True);
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)2));
        });
    }

    [TestCase(1, true, INS_inc)]
    [TestCase(-1, true, INS_dec)]
    [TestCase(127, true, INS_add)]
    [TestCase(128, true, INS_add)]
    [TestCase(0, false, INS_add)]
    public static void UnusedAdditionsRetainLockAndNativeImmediateForms(int value, bool immediate, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            var data = compiler.gtNewIconNode(TYP_INT, value);
            data.IsContained = immediate;
            data.RegNum = immediate ? REG_NA : REG_RDX;
            var tree = new GenTreeOp(GT_LOCKADD, TYP_VOID, Register(compiler, TYP_BYREF, REG_RCX), data);

            codeGen.genCodeForLockAdd(tree);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo<instruction[]>([INS_lock, expected]));
            Assert.That(ids[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
            Assert.That(ids[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(ids[1].idIsNoApxEvexPromotion(), Is.True);
            if (immediate && (expected == INS_add))
            {
                Assert.That(InstructionConstant(codeGen.Emitter, ids[1]), Is.EqualTo((nint)value));
            }
        });
    }

    [TestCase(GT_XADD, TYP_INT, false)]
    [TestCase(GT_XADD, TYP_LONG, true)]
    [TestCase(GT_XCHG, TYP_INT, false)]
    [TestCase(GT_XCHG, TYP_LONG, true)]
    [TestCase(GT_XCHG, TYP_BYTE, false)]
    [TestCase(GT_XCHG, TYP_UBYTE, false)]
    [TestCase(GT_XCHG, TYP_SHORT, false)]
    [TestCase(GT_XCHG, TYP_USHORT, false)]
    [TestCase(GT_XCHG, TYP_REF, false)]
    public static void ExchangeOperationsPreserveImplicitLocksAndExtendSmallResults(genTreeOps oper, var_types type, bool same)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            var target = same ? REG_RDX : REG_R8;
            var tree = new GenTreeOp(oper, type, Register(compiler, TYP_BYREF, REG_RCX),
                Register(compiler, type.ActualType, REG_RDX)) { RegNum = target };

            Assert.That(tree.IndirOrArrMetaDataAddr, Is.SameAs(tree.Op1));
            codeGen.genLockedInstructions(tree);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Count(id => id.idIns() == INS_lock), Is.EqualTo(oper == GT_XADD ? 1 : 0));
            var memory = ids.Single(id => id.idIns() == (oper == GT_XADD ? INS_xadd : INS_xchg));
            Assert.That(memory.idReg1(), Is.EqualTo(target));
            Assert.That(memory.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
            if (varTypeIsSmall(type))
            {
                Assert.That(ids[^1].idIns(), Is.EqualTo(varTypeIsSigned(type) ? INS_movsx : INS_movzx));
                Assert.That(ids[^1].idReg1(), Is.EqualTo(target));
                Assert.That(ids[^1].idReg2(), Is.EqualTo(target));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_R8 : RBM_NONE));
        });
    }

    [TestCase(GT_XORR, true)]
    [TestCase(GT_XORR, false)]
    [TestCase(GT_XAND, true)]
    [TestCase(GT_XAND, false)]
    public static void BitwiseAtomicsUseCompareExchangeOnlyForUsedResults(genTreeOps oper, bool unused)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            var tree = new GenTreeOp(oper, TYP_INT, Register(compiler, TYP_BYREF, REG_RCX),
                Register(compiler, TYP_INT, REG_RDX)) { RegNum = unused ? REG_NA : REG_R8 };
            if (unused)
            {
                tree.IsUnusedValue = true;
            }
            else
            {
                codeGen.InternalRegisters.Add(tree, RBM_R9);
            }
            var first = codeGen.Emitter.emitCurIG;
            codeGen.genLockedInstructions(tree);
            var ids = CodeGenLocalHeapTests.AllDescriptors(first, codeGen);

            if (unused)
            {
                Assert.That(ids.Select(id => id.idIns()), Is.EqualTo<instruction[]>([
                    INS_lock, oper == GT_XORR ? INS_or : INS_and
                ]));
                Assert.That(ids[1].idIsNoApxEvexPromotion(), Is.True);
            }
            else
            {
                Assert.That(ids.Select(id => id.idIns()), Is.EqualTo<instruction[]>([
                    INS_mov, INS_mov, oper == GT_XORR ? INS_or : INS_and,
                    INS_lock, INS_cmpxchg, INS_jne, INS_mov
                ]));
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(ids[4].idReg1(), Is.EqualTo(REG_R9));
                Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_R8));
                Assert.That(ids[^1].idReg2(), Is.EqualTo(REG_RAX));
                Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            }
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_LONG)]
    [TestCase(TYP_BYTE)]
    [TestCase(TYP_UBYTE)]
    [TestCase(TYP_SHORT)]
    [TestCase(TYP_USHORT)]
    [TestCase(TYP_REF)]
    public static void CompareExchangeLoadsComparandAndReturnsTheAccumulator(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            var tree = new GenTreeCmpXchg(type, Register(compiler, TYP_BYREF, REG_RCX),
                Register(compiler, type.ActualType, REG_RDX), Register(compiler, type.ActualType, REG_R9))
            {
                RegNum = REG_R8
            };
            codeGen.genCodeForCmpXchg(tree);
            var ids = Descriptors(codeGen);

            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo<instruction[]>([
                INS_mov, INS_lock, INS_cmpxchg,
                varTypeIsSmall(type) ? (varTypeIsSigned(type) ? INS_movsx : INS_movzx) : INS_mov
            ]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(ids[0].idReg2(), Is.EqualTo(REG_R9));
            Assert.That(ids[2].idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(ids[2].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
            Assert.That(ids[3].idReg1(), Is.EqualTo(REG_R8));
            Assert.That(ids[3].idReg2(), Is.EqualTo(REG_RAX));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_R8 : RBM_NONE));
        });
    }

    [TestCase(BARRIER_FULL, 2)]
    [TestCase(BARRIER_LOAD_ONLY, 0)]
    [TestCase(BARRIER_STORE_ONLY, 0)]
    public static void OnlyFullBarriersEmitLockedStackOperations(BarrierKind barrier, int count)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            codeGen.instGen_MemoryBarrier(barrier);
            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(count));
            if (count != 0)
            {
                Assert.That(ids[0].idIns(), Is.EqualTo(INS_lock));
                Assert.That(ids[1].idIns(), Is.EqualTo(INS_or));
                Assert.That(ids[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_SPBASE));
                Assert.That(ids[1].idOpSize(), Is.EqualTo(EA_4BYTE));
                Assert.That(ids[1].idIsNoApxEvexPromotion(), Is.True);
                Assert.That(InstructionConstant(codeGen.Emitter, ids[1]), Is.EqualTo((nint)0));
            }
        });
    }

#if DEBUG
    [TestCase(GT_LOCKADD)]
    [TestCase(GT_XADD)]
    [TestCase(GT_CMPXCHG)]
    public static void DspCodeRecordsAtomicInstructionsAndConsumesOperands(genTreeOps oper)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) => {
            var address = Register(compiler, TYP_BYREF, REG_RCX);
            var value = Register(compiler, TYP_INT, REG_RDX);
            GenTree tree = oper == GT_CMPXCHG
                ? new GenTreeCmpXchg(TYP_INT, address, value, Register(compiler, TYP_INT, REG_R9))
                : new GenTreeOp(oper, TYP_INT, address, value);
            tree.RegNum = REG_R8;
            compiler.opts.dspCode = true;
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                if (oper == GT_CMPXCHG)
                {
                    codeGen.genCodeForCmpXchg(tree.AsCmpXchg());
                }
                else if (oper == GT_LOCKADD)
                {
                    codeGen.genCodeForLockAdd(tree.AsOp());
                }
                else
                {
                    codeGen.genLockedInstructions(tree.AsOp());
                }
            });
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
            Assert.That(address._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
            Assert.That(value._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
        });
    }
#endif
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_AMD64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
#if DEBUG
using static RyuJitSharp.GenTreeDebugFlags;
#endif
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockOperandSetupTests
{
    [Test]
    public static void MissingSizeRegisterDoesNotRequireAnInternalRegisterOrEmitAnInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var block = Block(Source(2));

            codeGen.genSetBlockSize(block, REG_NA);

            Assert.That(codeGen.InternalRegisters.GetAll(block).IsEmpty, Is.True);
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask().IsEmpty, Is.True);
        });
    }

    [TestCase(1u)]
    [TestCase(16u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void SizeUsesAnOwnedRegisterAndZeroExtendsTheUnsignedBlockSize(uint size)
    {
        WithCodeGen((_, codeGen) =>
        {
            var block = Block(Source(2), size);
            var owned = Mask(REG_R8) | Mask(REG_R9);
            codeGen.InternalRegisters.Add(block, owned);

            codeGen.genSetBlockSize(block, REG_R8);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var descriptor = descriptors[0];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R8));
            // The x64 emitter encodes a native-width unsigned uint as a zero-extending mov r32, imm32.
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(GetConstant(codeGen.Emitter, descriptor), Is.EqualTo((nint)(nuint)size));
            Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
            Assert.That(codeGen.InternalRegisters.GetAll(block), Is.EqualTo(owned));
            Assert.That(codeGen.RegSet.rsGetModifiedRegsMask(), Is.EqualTo(Mask(REG_R8)));
        });
    }

#if DEBUG
    private static readonly List<string?> s_assertions = [];

    [Test]
    public static void SizeOwnershipAssertionUsesTheNativeConditionAndTheSpecificBlock()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var block = Block(Source(2));
            codeGen.InternalRegisters.Add(block, Mask(REG_R9));
            codeGen.InternalRegisters.Add(Block(Source(2)), Mask(REG_R8));
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.doAssert = &RecordAssertion;
            ICorJitInfo ee = new() { lpVtbl = &vtable };
            using var tls = new JitTls(&ee);
            JitTls.Compiler = compiler;
            s_assertions.Clear();

            codeGen.genSetBlockSize(block, REG_R8);

            string[] expectedAssertions = [
                "(internalRegisters.GetAll(blkNode) & genRegMask(sizeReg)) != 0",
            ];
            Assert.That(s_assertions, Is.EqualTo(expectedAssertions));
            Assert.That(codeGen.InternalRegisters.GetAll(block), Is.EqualTo(Mask(REG_R9)));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));

        return 0;
    }
#endif

    [TestCase(GT_LCL_VAR, 0)]
    [TestCase(GT_LCL_FLD, 12)]
    public static void ContainedLocalCopiesDoNotConsumeARegister(genTreeOps oper, int offset)
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = Local(oper, offset);
            var block = Block(source);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);

            codeGen.genConsumeBlockSrc(block);

            Assert.That(source.RegNum, Is.EqualTo(REG_NA));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(REG_RCX)));
            Assert.That(Descriptors(codeGen), Is.Empty);
#if DEBUG
            Assert.That((source._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) == 0, Is.True);
#endif
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void ConsumptionSelectsTheIndirectAddressOrInitializationValue(int sourceKind)
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = Source(sourceKind);
            var block = Block(source);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
#if DEBUG
            var operand = sourceKind == 2 ? source : source.AsUnOp().Op1;
            var useNum = 0;
            codeGen.genNumberOperandUse(source, ref useNum);
            Assert.That(useNum, Is.EqualTo(1));
#endif

            codeGen.genConsumeBlockSrc(block);

            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RBX)));
            Assert.That(Descriptors(codeGen), Is.Empty);
#if DEBUG
            Assert.That((operand._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.True);
            if (sourceKind != 2)
            {
                Assert.That((source._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) == 0, Is.True);
            }
#endif
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public static void SourceSetupRetainsMoveWidthGcClassificationAndSameRegisterElision(
        int sourceKind, bool sameRegister)
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = Source(sourceKind);
            var block = Block(source);
            var target = sameRegister ? REG_RCX : REG_R8;

            codeGen.genSetBlockSrc(block, target);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(sameRegister ? 0 : 1));
            if (!sameRegister)
            {
                var descriptor = descriptors[0];
                Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R8));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_RCX));
                Assert.That(descriptor.idOpSize(), Is.EqualTo(sourceKind == 0 ? EA_PTRSIZE : EA_4BYTE));
                Assert.That(descriptor.idGCref(), Is.EqualTo(
                    sourceKind == 0 ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
            }
        });
    }

    [TestCase(GT_LCL_VAR, 0)]
    [TestCase(GT_LCL_FLD, 0)]
    [TestCase(GT_LCL_FLD, 12)]
    [TestCase(GT_LCL_FLD, 255)]
    [TestCase(GT_LCL_FLD, 32768)]
    public static void LocalSourceSetupMaterializesTheStructAddressAndFieldOffset(genTreeOps oper, int offset)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(65552);
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvTracked = false;
            compiler.lvaTable[0].lvLRACandidate = false;
            compiler.lvaTable[0].StackOffset = -65552;
            var block = Block(Local(oper, offset));

            codeGen.genSetBlockSrc(block, REG_R8);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var descriptor = descriptors[0];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R8));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
            Assert.That(descriptor.idCodeSize(), Is.GreaterThan(0));
        });
    }

    [Test]
    public static void BlockSetupConsumesTheSourceHomeBeforeCopyingIntoThatRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].RegNum = REG_RCX;
            codeGen.RegSet.SetMaskVars(Mask(REG_RCX));
            VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            address.RegNum = REG_RDX;
            address.Flags = GTF_VAR_DEATH;
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, address) { IsContained = true };
            var block = Block(source);
            codeGen.InternalRegisters.Add(block, Mask(REG_R9));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RCX, TYP_BYREF);
#if DEBUG
            var useNum = 0;
            codeGen.genNumberOperandUse(block.Addr, ref useNum);
            codeGen.genNumberOperandUse(source, ref useNum);
            Assert.That(block.Addr.UseNum, Is.Zero);
            Assert.That(address.UseNum, Is.EqualTo(1));
#endif

            codeGen.genConsumeBlockOp(block, REG_RCX, REG_R8, REG_R9);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(4));
            AssertMove(descriptors[0], REG_RDX, REG_RCX);
            AssertMove(descriptors[1], REG_RCX, REG_RAX);
            AssertMove(descriptors[2], REG_R8, REG_RDX);
            Assert.That(descriptors[3].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[3].idReg1(), Is.EqualTo(REG_R9));
            Assert.That(GetConstant(codeGen.Emitter, descriptors[3]), Is.EqualTo((nint)16));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.False);
            Assert.That(codeGen.RegSet.GetMaskVars().IsEmpty, Is.True);
#if DEBUG
            Assert.That((block.Addr._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.True);
            Assert.That((address._debugFlags & GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.True);
#endif
        });
    }

    [Test]
    public static void LocalBlockSetupConsumesOnlyTheDestinationAndOmitsTheOptionalSize()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(32);
            compiler.lvaTable[0].RegNum = REG_STK;
            compiler.lvaTable[0].lvTracked = false;
            compiler.lvaTable[0].lvLRACandidate = false;
            compiler.lvaTable[0].StackOffset = -32;
            var block = Block(Local(GT_LCL_FLD, 12));
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);

            codeGen.genConsumeBlockOp(block, REG_RAX, REG_R8, REG_NA);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_lea));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_R8));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
        });
    }

    private static void AssertMove(Emitter.instrDesc descriptor, regNumber destination, regNumber source)
    {
        Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
        Assert.That(descriptor.idReg1(), Is.EqualTo(destination));
        Assert.That(descriptor.idReg2(), Is.EqualTo(source));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
        Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
    }

    private static GenTree Source(int kind)
    {
        return kind switch
        {
            0 => new GenTreeIndir(GT_IND, TYP_STRUCT, Physical(REG_RCX)) { IsContained = true },
            1 => new GenTreeUnOp(GT_INIT_VAL, TYP_INT, new GenTreeIntCon(TYP_INT, 255) { RegNum = REG_RCX })
                { IsContained = true },
            2 => new GenTreeIntCon(TYP_INT, 0) { RegNum = REG_RCX },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static GenTree Local(genTreeOps oper, int offset)
    {
        return oper switch
        {
            GT_LCL_VAR => new GenTreeLclVar(TYP_STRUCT, 0) { IsContained = true },
            GT_LCL_FLD => new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 0, checked((ushort)offset))
                { Layout = new ClassLayout(16), IsContained = true },
            _ => throw new ArgumentOutOfRangeException(nameof(oper)),
        };
    }

    private static GenTreeBlk Block(GenTree source, uint size = 16)
        => new(TYP_STRUCT, Physical(REG_RAX), source, new ClassLayout(size));

    private static GenTreePhysReg Physical(regNumber reg) => new(reg, TYP_BYREF) { RegNum = reg };

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
        => CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.lvaDoneFrameLayout = Compiler.INITIAL_FRAME_LAYOUT;
            codeGen.RegSet.rsClearRegsModified();
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            codeGen.RegSet.ClearMaskVars();
            compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint GetConstant(Emitter emitter, Emitter.instrDesc descriptor);
}
#endif

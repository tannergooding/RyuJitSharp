// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64 || TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class SharedStructArgumentSetupTests
{
#if TARGET_AMD64
    private const regNumber DestinationRegister = REG_RDI;
    private const regNumber SourceRegister = REG_RSI;
    private const regNumber AddressRegister = REG_R10;
    private const regNumber FieldRegister = REG_RAX;
    private const regNumber ReferenceRegister = REG_RDX;
#else
    private const regNumber DestinationRegister = REG_R0;
    private const regNumber SourceRegister = REG_R1;
    private const regNumber AddressRegister = REG_R2;
    private const regNumber FieldRegister = REG_R3;
    private const regNumber ReferenceRegister = REG_R4;
#endif

    [TestCase(GT_BLK, false)]
    [TestCase(GT_BLK, true)]
#if FEATURE_SIMD
    [TestCase(GT_IND, false)]
    [TestCase(GT_IND, true)]
#endif
    public static void IndirectSetupConsumesOnlyTheAddressAndElidesOnlyTheSameRegisterMove(
        genTreeOps oper, bool sourceReady)
    {
        WithCompiler((_, codeGen) =>
        {
            var addressReg = sourceReady ? SourceRegister : AddressRegister;
            var address = Physical(TYP_BYREF, addressReg);
            GenTree source = oper == GT_BLK
                ? new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(16)) { IsContained = true }
                : new GenTreeIndir(GT_IND, TYP_SIMD16, address) { IsContained = true };
            var argument = Argument(source, 0, 16);
            argument.RegNum = DestinationRegister;
            codeGen.GCInfo.gcMarkRegPtrVal(addressReg, TYP_BYREF);
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);

            ConsumeStructArgument(codeGen, argument, DestinationRegister, SourceRegister, REG_NA);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(sourceReady ? 0 : 1));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
            AssertConsumed(address, true);
            AssertConsumed(source, false);
            if (!sourceReady)
            {
                var move = descriptors[0];
                Assert.That(move.idIns(), Is.EqualTo(INS_mov));
                Assert.That(move.idOpSize(), Is.EqualTo(EA_PTRSIZE));
                Assert.That(move.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
                Assert.That(move.idReg1(), Is.EqualTo(SourceRegister));
                Assert.That(move.idReg2(), Is.EqualTo(AddressRegister));
            }
        });
    }

    [Test]
    public static void SourceAddressCopyPrecedesOverwritingItsOriginalRegisterWithTheDestination()
    {
        WithCompiler((_, codeGen) =>
        {
            var original = Physical(TYP_BYREF, DestinationRegister);
            var address = new GenTreeCopyOrReload(GT_COPY, TYP_BYREF, original) { RegNum = AddressRegister };
            var source = new GenTreeBlk(TYP_STRUCT, address, new ClassLayout(16)) { IsContained = true };
            var argument = Argument(source, 16, 16);
            StackArgVariable(codeGen) = 0;
            codeGen.GCInfo.gcMarkRegPtrVal(DestinationRegister, TYP_BYREF);

            ConsumeStructArgument(codeGen, argument, DestinationRegister, SourceRegister, REG_NA);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(AddressRegister));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(DestinationRegister));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptors[0].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            AssertStack(descriptors[1],
#if TARGET_AMD64
                INS_lea,
#else
                INS_add,
#endif
                EA_PTRSIZE, DestinationRegister, 0, 16);
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptors[2].idReg1(), Is.EqualTo(SourceRegister));
            Assert.That(descriptors[2].idReg2(), Is.EqualTo(AddressRegister));
            Assert.That(descriptors[2].idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptors[2].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_BYREF));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur.IsEmpty, Is.True);
            AssertConsumed(original, true);
            AssertConsumed(address, true);
            AssertConsumed(source, false);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalSetupUsesPointerSizedAddressesAndDoesNotConsumeTheContainedLocal(bool destinationReady)
    {
        WithCompiler((_, codeGen) =>
        {
            var source = new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 1, 8)
            {
                Layout = new ClassLayout(16),
                IsContained = true,
            };
            var argument = Argument(source, 16, 16);
            if (destinationReady)
            {
                argument.RegNum = DestinationRegister;
            }
            StackArgVariable(codeGen) = 0;
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);

            ConsumeStructArgument(codeGen, argument, DestinationRegister, SourceRegister, REG_NA);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(destinationReady ? 1 : 2));
            if (!destinationReady)
            {
                AssertStack(descriptors[0],
#if TARGET_AMD64
                    INS_lea,
#else
                    INS_add,
#endif
                    EA_PTRSIZE, DestinationRegister, 0, 16);
            }
            AssertStack(descriptors[^1],
#if TARGET_AMD64
                INS_lea,
#else
                INS_sub,
#endif
                EA_PTRSIZE, SourceRegister, 1, 8);
            foreach (var descriptor in descriptors)
            {
                Assert.That(descriptor.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_NONE));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(StackArgVariable(codeGen), Is.Zero);
            AssertConsumed(source, false);
        });
    }

    [Test]
    public static void EmptyFieldListDoesNotReadTheStackHomeOrConsumeRegisters()
    {
        WithCompiler((_, codeGen) =>
        {
            var fields = new GenTreeFieldList { IsContained = true };
            var argument = Argument(fields, 0, 0);
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);

            StoreFieldList(codeGen, argument, BAD_VAR_NUM);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
            Assert.That(StackArgVariable(codeGen), Is.EqualTo(BAD_VAR_NUM));
            AssertConsumed(fields, false);
        });
    }

    [TestCase(false)]
#if FEATURE_FASTTAILCALL
    [TestCase(true)]
#endif
    public static void FieldListConsumesAndStoresEachFieldInOrderWithNativeWidthsAndTailCallBounds(bool incoming)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = new ClassLayout(16),
                RegNum = REG_STK,
                lvOnFrame = true,
                StackOffset = 0,
            };
            var small = Physical(TYP_INT, FieldRegister);
            var reference = Physical(TYP_REF, ReferenceRegister);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddFieldLIR(compiler, small, 0, TYP_BYTE);
            fields.AddFieldLIR(compiler, reference, 8, TYP_REF);
            var offset = incoming ? 32 : 0;
            var argument = Argument(fields, offset, 16, incoming);
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);
#if DEBUG
            small.UseNum = 0;
            reference.UseNum = 1;
#endif

            StoreFieldList(codeGen, argument, 0);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            AssertStack(descriptors[0],
#if TARGET_AMD64
                INS_mov,
#else
                INS_strb,
#endif
                EA_1BYTE, FieldRegister, 0, offset);
            AssertStack(descriptors[1],
#if TARGET_AMD64
                INS_mov,
#else
                INS_str,
#endif
                EA_PTRSIZE, ReferenceRegister, 0, offset + 8);
            Assert.That(descriptors[1].idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur.IsEmpty, Is.True);
            AssertConsumed(small, true);
            AssertConsumed(reference, true);
            AssertConsumed(fields, false);
        });
    }

#if TARGET_AMD64
    [TestCase(0u)]
    [TestCase(96u)]
    [TestCase(0x80000000u)]
    [TestCase(uint.MaxValue)]
    public static void OptionalSizePreservesTheNativeUnsignedStackByteSize(uint size)
    {
        WithCompiler((_, codeGen) =>
        {
            var source = new GenTreeBlk(TYP_STRUCT, Physical(TYP_BYREF, SourceRegister), new ClassLayout(16))
            {
                IsContained = true,
            };
            var argument = Argument(source, 0, unchecked((int)size));
            argument.RegNum = DestinationRegister;

            ConsumeStructArgument(codeGen, argument, DestinationRegister, SourceRegister, REG_RCX);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var descriptor = descriptors[0];
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(GetConstant(codeGen.Emitter, descriptor), Is.EqualTo((nint)(nuint)size));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint GetConstant(Emitter emitter, Emitter.instrDesc descriptor);
#endif

#if TARGET_ARM64 && FEATURE_SIMD
    [Test]
    public static void Simd12ReachesItsTypedEmitterBoundaryAfterConsumingOnlyTheCurrentField()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var small = Physical(TYP_INT, FieldRegister);
            var vector = Physical(TYP_SIMD16, REG_V0);
            var reference = Physical(TYP_REF, ReferenceRegister);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddFieldLIR(compiler, small, 0, TYP_INT);
            fields.AddFieldLIR(compiler, vector, 8, TYP_SIMD12);
            fields.AddFieldLIR(compiler, reference, 24, TYP_REF);
            var argument = Argument(fields, 0, 32);
            codeGen.GCInfo.gcMarkRegPtrVal(ReferenceRegister, TYP_REF);
#if DEBUG
            small.UseNum = 0;
            vector.UseNum = 1;
            reference.UseNum = 2;
#endif

            var error = Assert.Throws<FatalJitException>(() => StoreFieldList(codeGen, argument, 0));

            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(error, Has.Message.EqualTo("Target SIMD12 local-stack store recording is not yet ported."));
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            AssertStack(descriptors[0], INS_str, EA_4BYTE, FieldRegister, 0, 0);
            AssertConsumed(small, true);
            AssertConsumed(vector, true);
            AssertConsumed(reference, false);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(ReferenceRegister)));
        });
    }
#endif

    private static GenTreePutArgStk Argument(GenTree source, int offset, int size, bool incoming = false)
    {
        var call = new GenTreeCall(TYP_VOID);
        if (incoming)
        {
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
        }

        return new GenTreePutArgStk(TYP_VOID, source, call, offset, size, incoming);
    }

    private static GenTreePhysReg Physical(var_types type, regNumber reg) => new(reg, type) { RegNum = reg };

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
        => CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    private static void AssertStack(Emitter.instrDesc descriptor, instruction ins, emitAttr size,
        regNumber reg, int local, int offset)
    {
        Assert.That(descriptor.idIns(), Is.EqualTo(ins));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(size));
        Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
        Assert.That(descriptor.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(local));
        Assert.That(descriptor.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
    }

    private static void AssertConsumed(GenTree node, bool consumed)
    {
#if DEBUG
        Assert.That((node._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED) != 0, Is.EqualTo(consumed));
#else
        Assert.That(node.IsContained || node.IsUsedFromReg, Is.True);
#endif
    }

    private static void WithCompiler(Action<Compiler, CodeGen> action)
    {
#if TARGET_AMD64
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ConfigureCompiler(compiler, codeGen);
            action(compiler, codeGen);
        });
#else
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            ConfigureCompiler(compiler, codeGen);
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
#endif
    }

    private static void ConfigureCompiler(Compiler compiler, CodeGen codeGen)
    {
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.lvaTable = [
            new() { Type = TYP_STRUCT, Layout = new ClassLayout(64), RegNum = REG_STK,
                lvOnFrame = true, StackOffset = 0 },
            new() { Type = TYP_STRUCT, Layout = new ClassLayout(64), RegNum = REG_STK,
                lvOnFrame = true, StackOffset = -64 },
        ];
        compiler.lvaCount = 2;
        compiler.lvaOutgoingArgSpaceVar = 0;
        compiler.lvaParameterStackSize = 64;
        compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        codeGen.RegSet.ClearMaskVars();
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
        LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genConsumePutStructArgStk")]
    private static extern void ConsumeStructArgument(CodeGen codeGen, GenTreePutArgStk argument,
        regNumber destination, regNumber source, regNumber size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genPutArgStkFieldList")]
    private static extern void StoreFieldList(CodeGen codeGen, GenTreePutArgStk argument, int outArgVarNum);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgVarNum")]
    private static extern ref int StackArgVariable(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);
}
#endif

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenIndirectLoadTests
{
    [TestCase(TYP_BYTE, false, false, INS_movsx)]
    [TestCase(TYP_UBYTE, false, false, INS_movzx)]
    [TestCase(TYP_SHORT, true, false, INS_mov)]
    [TestCase(TYP_INT, false, true, INS_mov)]
    [TestCase(TYP_REF, false, false, INS_mov)]
    public static void IndirectReadsRetainExtensionAndGcResults(
        var_types type, bool noExtension, bool local, instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            GenTree address = local
                ? new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4) { IsContained = true }
                : Register(compiler, TYP_BYREF, REG_RAX);
            var tree = new GenTreeIndir(GT_IND, type, address)
            {
                RegNum = REG_RCX,
                Flags = noExtension ? GTF_DONT_EXTEND : GTF_EMPTY,
            };

            codeGen.genCodeForIndir(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(ins));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(type == TYP_REF ? RBM_RCX : RBM_NONE));
            if (local)
            {
                Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(4u));
            }
        });
    }

    [Test]
    public static void TlsReadsUseTheGsSegmentWithoutConsumingTheHandle()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x58);
            address.Flags |= GTF_ICON_TLS_HDL;
            address.IsContained = true;
            var tree = new GenTreeIndir(GT_IND, TYP_I_IMPL, address) { RegNum = REG_RAX };

            codeGen.genCodeForIndir(tree);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idAddr().iiaFieldHnd == FLD_GLOBAL_GS, Is.True);
#if DEBUG
            Assert.That(address._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#endif
        });
    }

    [Test]
    public static void Simd12ReadsPreserveAllNativeAddressForms(
        [Values(0, 1, 2, 3)] int addressKind, [Values(false, true)] bool vex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            GenTree address = addressKind switch
            {
                0 => Register(compiler, TYP_BYREF, REG_RAX),
                1 => new GenTreeAddrMode(TYP_BYREF, Register(compiler, TYP_BYREF, REG_RAX), null, 0, 4)
                    { IsContained = true },
                2 => compiler.gtNewIconNode(TYP_I_IMPL, 0x100),
                _ => new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4),
            };
            address.IsContained = addressKind != 0;
            var tree = new GenTreeIndir(GT_IND, TYP_SIMD12, address) { RegNum = REG_XMM0 };

            codeGen.genCodeForIndir(tree);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_insertps));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)0x28));
            if (addressKind == 0)
            {
                Assert.That(tree.Addr.AsAddrMode().BaseAddress, Is.SameAs(address));
                Assert.That(tree.Addr.AsAddrMode().Offset, Is.EqualTo(8));
            }
            else
            {
                Assert.That(tree.Addr, Is.SameAs(address));
                if (addressKind == 1)
                {
                    Assert.That(tree.Addr.AsAddrMode().Offset, Is.EqualTo(12));
                }
                else if (addressKind == 2)
                {
                    Assert.That(tree.Addr.AsIntConCommon().IconValue, Is.EqualTo((nint)0x108));
                }
                else
                {
                    Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
                }
            }
        });
    }

    [Test]
    public static void IndexedAddressesRetainWideningScalingBoundsAndGcRoots(
        [Values(false, true)] bool nativeIndex, [Values(false, true)] bool bounds, [Values(1, 3, 8)] int scale)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var elementType = scale switch
            {
                1 => TYP_UBYTE,
                3 => TYP_STRUCT,
                _ => TYP_LONG,
            };
            var tree = new GenTreeIndexAddr(Register(compiler, TYP_REF, REG_RAX),
                Register(compiler, nativeIndex ? TYP_I_IMPL : TYP_INT, REG_RCX),
                elementType, scale == 3 ? (CORINFO_CLASS_STRUCT_*)1 : NO_CLASS_HANDLE, scale, 8, 16, bounds)
            {
                RegNum = REG_RDX,
            };
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            if (bounds)
            {
                _ = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_RNGCHK_FAIL);
            }
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_REF);

            codeGen.genCodeForIndexAddr(tree);

            var descriptors = Descriptors(codeGen);
            var count = 1 + (nativeIndex ? 0 : 1) + (scale == 3 ? 1 : 0) +
                (bounds ? nativeIndex ? 3 : 2 : 0);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_lea));
            var address = descriptors[^1].idAddr().iiaAddrMode;
            Assert.That(address.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(address.amIndxReg, Is.EqualTo(!nativeIndex || (scale == 3) ? REG_R11 : REG_RCX));
            Assert.That(address.amScale, Is.EqualTo(scale == 8 ? 3 : 0));
            Assert.That(address.amDisp, Is.EqualTo(16));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_RDX));
            if (bounds)
            {
                Assert.That(descriptors[nativeIndex ? 2 : 1].idIns(), Is.EqualTo(INS_jae));
            }
#if DEBUG
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_NONE));
#else
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_R11));
#endif
        });
    }

    [TestCase(REG_R8)]
    [TestCase(REG_XMM8)]
    [TestCase(REG_K1)]
    public static void SingleInternalRegisterSelectionPreservesBothBanks(regNumber reg)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = compiler.gtNewIconNode(TYP_INT, 0);
            var selected = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
            codeGen.InternalRegisters.Add(tree, selected | RBM_RDX);

            Assert.That(codeGen.InternalRegisters.GetSingle(tree, selected), Is.EqualTo(reg));
#if DEBUG
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_RDX));
#else
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(selected | RBM_RDX));
#endif
        });
    }

    [Test]
    public static void InlineBoundsThrowsRejectBeforeConsumingTheInternalRegister()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var tree = new GenTreeIndexAddr(Register(compiler, TYP_REF, REG_RAX),
                Register(compiler, TYP_INT, REG_RCX), TYP_INT, NO_CLASS_HANDLE, 4, 8, 16, boundsCheck: true)
            {
                RegNum = REG_RDX,
            };
            codeGen.InternalRegisters.Add(tree, RBM_R11);
            compiler.opts.compDbgCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIndexAddr(tree));
            Assert.That(codeGen.InternalRegisters.GetAll(tree), Is.EqualTo(RBM_R11));
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.compDbgCode = false;
            _ = CodeGenBinaryTests.PrepareThrowTarget(compiler, SCK_RNGCHK_FAIL);

            codeGen.genCodeForIndexAddr(tree);
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(4));
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRejectsBeforeSimdAddressMutation()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = new GenTreeAddrMode(TYP_BYREF, Register(compiler, TYP_BYREF, REG_RAX), null, 0, 4)
            {
                IsContained = true,
            };
            var tree = new GenTreeIndir(GT_IND, TYP_SIMD12, address) { RegNum = REG_XMM0 };
            compiler.opts.dspCode = true;
            _ = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIndir(tree));
            Assert.That(address.Offset, Is.EqualTo(4));
            Assert.That(Descriptors(codeGen), Is.Empty);
            compiler.opts.dspCode = false;

            codeGen.genCodeForIndir(tree);
            Assert.That(address.Offset, Is.EqualTo(12));
        });
    }
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* address) => CorInfoReloc.NONE;
}

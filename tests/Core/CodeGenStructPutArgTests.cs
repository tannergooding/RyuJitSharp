// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenStructPutArgTests
{
#if TARGET_AMD64 && WINDOWS_AMD64_ABI
    [TestCase(8, new[] { 8 })]
    [TestCase(15, new[] { 8, 4, 2, 1 })]
    [TestCase(24, new[] { 16, 8 })]
    public static void UnrolledCopyPreservesChunkOrderAndRegisterBanks(int size, int[] widths)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.lvaTable = [
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(64), lvOnFrame = true,
                    StackOffset = 0, RegNum = REG_STK },
                new() { Type = TYP_STRUCT, Layout = new ClassLayout(64), lvOnFrame = true,
                    StackOffset = -64, RegNum = REG_STK },
            ];
            compiler.lvaCount = 2;
            compiler.lvaOutgoingArgSpaceVar = 0;
            AllFloat(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            EnableAvx2(compiler);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            var source = new GenTreeLclFld(GT_LCL_FLD, TYP_STRUCT, 1, 0)
            {
                Layout = new ClassLayout(size),
                IsContained = true,
            };
            var argument = new GenTreePutArgStk(TYP_VOID, source, new GenTreeCall(TYP_VOID), 16,
                (size + 7) & ~7, false)
            {
                _kind = GenTreePutArgStk.Kind.Unroll,
                ArgLoadSize = size,
            };
            var temps = default(regMaskTP);
            if (size >= 16)
            {
                temps |= regMaskTP.CreateFromRegNum(REG_XMM1, REG_XMM1.SingleTypeMask);
            }
            if ((size % 16) != 0)
            {
                temps |= regMaskTP.CreateFromRegNum(REG_RAX, REG_RAX.SingleTypeMask);
            }
            codeGen.InternalRegisters.Add(argument, temps);
            StackArgVariable(codeGen) = 0;
            StackArgOffset(codeGen) = 16;

            codeGen.genPutStructArgStk(argument);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(widths.Length * 2));
            var offset = 0;
            for (var i = 0; i < widths.Length; i++)
            {
                var width = widths[i];
                var reg = width == 16 ? REG_XMM1 : REG_RAX;
                var ins = width == 16 ? INS_movdqu32 : INS_mov;
                Assert.That(descriptors[2 * i].idIns(), Is.EqualTo(ins));
                Assert.That(descriptors[2 * i].idOpSize(), Is.EqualTo((emitAttr)width));
                Assert.That(descriptors[2 * i].idReg1(), Is.EqualTo(reg));
                Assert.That(descriptors[(2 * i) + 1].idIns(), Is.EqualTo(ins));
                Assert.That(descriptors[(2 * i) + 1].idOpSize(), Is.EqualTo((emitAttr)width));
                Assert.That(descriptors[(2 * i) + 1].idReg1(), Is.EqualTo(reg));
                Assert.That(descriptors[(2 * i) + 1].idAddr().iiaLclVar.lvaOffset(),
                    Is.EqualTo((uint)(16 + offset)));
                offset += width;
            }
            Assert.That(offset, Is.EqualTo(size));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgVarNum")]
    private static extern ref int StackArgVariable(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloat(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_stkArgOffset")]
    private static extern ref int StackArgOffset(CodeGen codeGen);
#endif
}

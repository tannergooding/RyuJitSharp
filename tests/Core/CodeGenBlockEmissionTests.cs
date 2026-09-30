// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using NUnit.Framework;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenBlockEmissionTests
{
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, true)]
    public static void WholeBlockEmissionFamilyPreservesScalarLoadBeforeStore(int method, bool copy)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var destination = new GenTreePhysReg(REG_RDX, TYP_BYREF) { RegNum = REG_RDX };
            GenTree source;
            if (copy)
            {
                var address = new GenTreePhysReg(REG_RCX, TYP_BYREF) { RegNum = REG_RCX };
                source = new GenTreeIndir(GT_IND, TYP_STRUCT, address) { IsContained = true };
            }
            else
            {
                source = new GenTreeIntCon(TYP_INT, 0) { RegNum = REG_RAX };
            }

            var node = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout(8))
            {
                _kind = method == 3 ? BlkOpKindLoop : method == 1 ? BlkOpKindUnrollMemmove : BlkOpKindUnroll,
            };
            if (copy)
            {
                codeGen.InternalRegisters.Add(node,
                    regMaskTP.CreateFromRegNum(REG_R8, REG_R8.SingleTypeMask));
            }

            switch (method)
            {
                case 0:
                {
                    codeGen.genCodeForStoreBlk(node);
                    break;
                }

                case 1:
                {
                    codeGen.genCodeForMemmove(node);
                    break;
                }

                case 2:
                {
                    codeGen.genCodeForInitBlkUnroll(node);
                    break;
                }

                case 3:
                {
                    codeGen.genCodeForInitBlkLoop(node);
                    break;
                }

                case 4:
                {
                    codeGen.genCodeForCpBlkUnroll(node);
                    break;
                }
            }

            var instructions = Descriptors(codeGen);
            Assert.That(instructions, Has.Count.EqualTo(copy ? 2 : 1));
            Assert.That(instructions[0].idIns(), Is.EqualTo(INS_mov));
            if (copy)
            {
                Assert.That(instructions[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RCX));
                Assert.That(instructions[1].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDX));
            }
            else
            {
                Assert.That(instructions[0].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RDX));
            }
        });
    }
}

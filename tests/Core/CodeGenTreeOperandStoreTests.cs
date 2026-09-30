// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_AMD64
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenTreeOperandStoreTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void LocalAndSpillStoresKeepTheirSeparateStackHomes(bool writeThrough)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            try
            {
                temp.tdTempOffs = -32;

                var local = writeThrough
                    ? new GenTreeLclVar(TYP_INT, 0, compiler.gtNewIconNode(TYP_INT, 7))
                    : new GenTreeLclVar(TYP_INT, 0);
                if (writeThrough)
                {
                    local.Flags |= GTF_SPILLED | GTF_SPILL;
                }

                codeGen.inst_TT_RV(INS_mov, EA_4BYTE, local, REG_RAX);
                codeGen.inst_ST_RV(INS_mov, temp, 0, REG_RAX, TYP_INT);

                var descriptors = Descriptors(codeGen);
                Assert.That(descriptors, Has.Count.EqualTo(2));
                foreach (var descriptor in descriptors)
                {
                    Assert.That(descriptor.idIns(), Is.EqualTo(INS_mov));
                    Assert.That(descriptor.idInsFmt(), Is.EqualTo(IF_SWR_RRD));
                    Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_4BYTE));
                    Assert.That(descriptor.idReg1(), Is.EqualTo(REG_RAX));
                }
                Assert.That(descriptors[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                Assert.That(descriptors[1].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
            }
            finally
            {
                codeGen.RegSet.tmpRlsTemp(temp);
            }
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_INT)]
    public static void SpillStoreUsesTheSourceActualWidth(var_types type)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(TYP_INT, 1);
            var temp = codeGen.RegSet.tmpGetTemp(TYP_INT);
            try
            {
                temp.tdTempOffs = -32;

                codeGen.inst_ST_RV(INS_mov, temp, 0, REG_RAX, type);

                var descriptors = Descriptors(codeGen);
                Assert.That(descriptors, Has.Count.EqualTo(1));
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_4BYTE));
                Assert.That(descriptors[0].idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(temp.tdTempNum));
                Assert.That(descriptors[0].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            }
            finally
            {
                codeGen.RegSet.tmpRlsTemp(temp);
            }
        });
    }
}
#endif

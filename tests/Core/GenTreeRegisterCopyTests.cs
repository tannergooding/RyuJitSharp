// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GenTreeRegisterCopyTests
{
    [TestCase(GT_NEG, true)]
    [TestCase(GT_NOT, false)]
    public static void OrdinaryReplacementCopiesRegisterAcrossOperators(genTreeOps oper, bool sourceHasReg)
    {
        WithCompiler(() => {
            var source = new GenTreeIntCon(TYP_INT, 42);
            var destination = new GenTreeUnOp(oper, TYP_INT, new GenTreeIntCon(TYP_INT, 7)) {
                RegNum = REG_R8,
            };
            if (sourceHasReg)
            {
                source.RegNum = REG_RCX;
            }

            destination.CopyReg(source);

            Assert.That(destination.RegNum, Is.EqualTo(sourceHasReg ? REG_RCX : REG_NA));
#if DEBUG
            Assert.That(destination.RegTag, Is.EqualTo(source.RegTag));
#endif
        });
    }

    [Test]
    public static void WindowsAmd64CallCopiesItsOnlyReturnRegister()
    {
        WithCompiler(() => {
            Assert.That(MAX_RET_REG_COUNT, Is.EqualTo(1));
            var source = new GenTreeCall(TYP_INT) { RegNum = REG_RAX };
            var destination = new GenTreeCall(TYP_INT) { RegNum = REG_RDX };
            source.Flags |= GTF_SPILL;
            destination.Flags |= GTF_SPILLED;

            destination.CopyReg(source);

            Assert.That(destination.GetRegNumByIdx(0), Is.EqualTo(REG_RAX));
            Assert.That(destination.Flags & (GTF_SPILL | GTF_SPILLED), Is.EqualTo(GTF_SPILLED));
#if DEBUG
            Assert.That(destination.RegTag, Is.EqualTo(source.RegTag));
#endif
        });
    }

    [TestCase(GT_COPY)]
    [TestCase(GT_RELOAD)]
    public static void WindowsAmd64CopyOrReloadRetainsItsIndexedRegister(genTreeOps oper)
    {
        WithCompiler(() => {
            Assert.That(MAX_MULTIREG_COUNT, Is.EqualTo(2));
            var source = new GenTreeCopyOrReload(oper, TYP_INT, new GenTreeIntCon(TYP_INT, 1)) {
                RegNum = REG_RAX,
            };
            var operand = new GenTreeIntCon(TYP_INT, 2);
            var destination = new GenTreeCopyOrReload(oper, TYP_INT, operand) {
                RegNum = REG_RBX,
            };
            source.SetRegNumByIdx(REG_RDX, 1);
            destination.SetRegNumByIdx(REG_R9, 1);

            destination.CopyReg(source);

            Assert.That(destination.GetRegNumByIdx(0), Is.EqualTo(REG_RAX));
            Assert.That(destination.GetRegNumByIdx(1), Is.EqualTo(REG_R9));
            Assert.That(destination.Op1, Is.SameAs(operand));
#if DEBUG
            Assert.That(destination.RegTag, Is.EqualTo(source.RegTag));
#endif
        });
    }

    private static void WithCompiler(Action action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        try
        {
            action();
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

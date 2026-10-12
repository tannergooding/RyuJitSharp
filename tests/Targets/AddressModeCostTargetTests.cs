// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AddressModeCostTargetTests
{
    [Test]
    public static void LikelyRegisterVariablesUseTheConfiguredX86TypeRules()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_FLOAT;
            compiler.lvaRefCountState = RCS_NORMAL;
            compiler.lvaTable[0].setLvRefCntWtd(3 * BB_UNITY_WEIGHT);

            var local = compiler.gtNewLclvNode(TYP_FLOAT, 0);

            Assert.That(IsLikelyRegVar(compiler, local), Is.EqualTo(!Configuration.TARGET_X86));
        });
    }

    [Test]
    public static void MarkedAddressModeUsesConfiguredTargetCostRules()
    {
        WithCompiler(compiler => {
            var baseType = Configuration.TARGET_WASM ? TYP_REF : TYP_LONG;
            var addressType = Configuration.TARGET_WASM ? TYP_BYREF : TYP_LONG;
            var displacementType = Configuration.TARGET_WASM ? TYP_I_IMPL : TYP_LONG;
            compiler.lvaTable[0].Type = baseType;

            var baseAddress = compiler.gtNewLclvNode(baseType, 0);
            baseAddress.SetCosts(3, 6);

            var displacement = compiler.gtNewIconNode(displacementType, 4);
            var address = new GenTreeOp(GT_ADD, addressType, baseAddress, displacement);
            address.SetCosts(4, 4);

            var costEx = 0;
            var costSz = 0;

            Assert.That(compiler.gtMarkAddrMode(address, ref costEx, ref costSz, TYP_LONG), Is.True);
            Assert.That(costEx, Is.EqualTo(3));
            Assert.That(costSz, Is.EqualTo(Configuration.TARGET_XARCH ? 7 : Configuration.TARGET_ARM ? 5 : 6));
        });
    }

    [Test]
    public static void VolatileAddressModeUsesConfiguredArm64Rules()
    {
        WithCompiler(compiler => {
            var baseType = Configuration.TARGET_WASM ? TYP_REF : TYP_LONG;
            var addressType = Configuration.TARGET_WASM ? TYP_BYREF : TYP_LONG;
            var displacementType = Configuration.TARGET_WASM ? TYP_I_IMPL : TYP_LONG;
            compiler.lvaTable[0].Type = baseType;

            var baseAddress = compiler.gtNewLclvNode(baseType, 0);
            baseAddress.SetCosts(3, 6);

            var displacement = compiler.gtNewIconNode(displacementType, 4);
            var address = new GenTreeOp(GT_ADD, addressType, baseAddress, displacement);
            address.SetCosts(4, 4);

            var includesAddrCost = compiler.gtGetAddrNodeCost(address, TYP_LONG, isVolatile: true, out var costEx, out var costSz);

            Assert.That(includesAddrCost, Is.EqualTo(!Configuration.TARGET_ARM64));
            Assert.That(costEx, Is.EqualTo(Configuration.TARGET_ARM64 ? 0 : 3));
            Assert.That(costSz, Is.EqualTo(Configuration.TARGET_ARM64 ? 0 : Configuration.TARGET_XARCH ? 7 : Configuration.TARGET_ARM ? 5 : 6));
        });
    }

    [Test]
    public static void ConstantAddressCostIsXarchSpecific()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1000);
            address.SetCosts(4, 7);

            var includesAddrCost = compiler.gtGetAddrNodeCost(address, TYP_LONG, isVolatile: false, out var costEx, out var costSz);

            Assert.That(includesAddrCost, Is.EqualTo(Configuration.TARGET_XARCH));
            Assert.That(costEx, Is.EqualTo(Configuration.TARGET_XARCH ? 3 : 0));
            Assert.That(costSz, Is.EqualTo(Configuration.TARGET_XARCH ? 7 : 0));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_LONG;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "gtIsLikelyRegVar")]
    private static extern bool IsLikelyRegVar(Compiler compiler, GenTree tree);
}

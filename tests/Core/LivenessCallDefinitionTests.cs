// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LivenessCallDefinitionTests
{
    private readonly struct Policy : ILivenessPolicy
    {
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void RetbufDefinitionFollowsOnlyValueProducingArgumentUses(int placement)
    {
        WithCompiler(compiler => {
            var address = Address(compiler);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            GenTree argument = placement switch {
                1 => new GenTreeUnOp(GT_PUTARG_REG, TYP_BYREF, address),
                2 => new GenTreePutArgStk(TYP_BYREF, address, call, 0, TARGET_POINTER_SIZE, false),
                _ => address,
            };
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.RetBuffer));
            arg.EarlyNode = null;
            arg.LateNode = argument;
            call.Args.PushLateBack(arg);
            var block = Block(address);
            if (!ReferenceEquals(argument, address))
            {
                block.InsertAtEnd(argument);
            }
            block.InsertAtEnd(call);

            Assert.That(block.TryGetUse(address, out var addressUse), Is.True);
            Assert.That(addressUse.User(), Is.SameAs(placement == 0 ? call : argument));
            if (placement == 1)
            {
                Assert.That(argument.IsValue, Is.True);
                Assert.That(block.TryGetUse(argument, out var argumentUse), Is.True);
                Assert.That(argumentUse.User(), Is.SameAs(call));
            }
            Assert.That(new Liveness<Policy>(compiler).IsTrackedCallDefinition(block, address), Is.EqualTo(placement != 2));
            Assert.That(compiler.gtCallGetDefinedRetBufLclAddr(call), Is.SameAs(address));
        });
    }

    [Test]
    public static void AsyncResumedDefinitionsUseTheCanonicalPhysicalDefinitionVisitor()
    {
        WithCompiler(compiler => {
            var address = Address(compiler);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.SetIsAsync(default);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            var block = Block(address, call);

            Assert.That(new Liveness<Policy>(compiler).IsTrackedCallDefinition(block, address), Is.True);
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void OnlyTrackedDefinitionsAreDeferredToTheirCalls(bool tracked, bool definition)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].lvTracked = tracked;
            var address = Address(compiler);
            if (!definition)
            {
                address.Flags &= ~GTF_VAR_DEF;
            }
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address).WithWellKnownArg(WellKnownArg.RetBuffer));
            var block = Block(address, call);

            Assert.That(new Liveness<Policy>(compiler).IsTrackedCallDefinition(block, address), Is.False);
        });
    }

    [Test]
    public static void MatchingLocalNumbersDoNotReplacePhysicalDefinitionIdentity()
    {
        WithCompiler(compiler => {
            var ordinaryAddress = Address(compiler);
            var definedAddress = Address(compiler);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG_LCLOPT;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(ordinaryAddress));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(definedAddress).WithWellKnownArg(WellKnownArg.RetBuffer));
            var block = Block(ordinaryAddress, definedAddress, call);
            var liveness = new Liveness<Policy>(compiler);

            Assert.That(liveness.IsTrackedCallDefinition(block, ordinaryAddress), Is.False);
            Assert.That(liveness.IsTrackedCallDefinition(block, definedAddress), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnusedAddressesAndNonArgumentUsersAreNotCallDefinitions(bool hasUser)
    {
        WithCompiler(compiler => {
            var address = Address(compiler);
            var block = Block(address);
            if (hasUser)
            {
                block.InsertAtEnd(new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, address));
            }

            Assert.That(new Liveness<Policy>(compiler).IsTrackedCallDefinition(block, address), Is.False);
        });
    }

    [Test]
    public static void FieldListArgumentIsNotItselfAPhysicalCallDefinition()
    {
        WithCompiler(compiler => {
            var address = Address(compiler);
            var argument = new GenTreeUnOp(GT_PUTARG_REG, TYP_BYREF, address);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, argument, 0, TYP_BYREF);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            _ = call.Args.PushBack(NewCallArg.CreateForStruct(fields, TYP_STRUCT, new ClassLayout(TARGET_POINTER_SIZE)));
            var block = Block(address, argument, fields, call);

            Assert.That(new Liveness<Policy>(compiler).IsTrackedCallDefinition(block, address), Is.False);
        });
    }

    private static GenTreeLclFld Address(Compiler compiler)
    {
        var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
        address.Flags |= GTF_VAR_DEF;

        return address;
    }

    private static BasicBlock Block(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null) { Kind = BBKinds.BBJ_RETURN };
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL, lvTracked = true }];
#if DEBUG
        compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}

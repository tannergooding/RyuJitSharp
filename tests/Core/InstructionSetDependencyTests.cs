// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.ManagedInstructionSet;

namespace RyuJitSharp.UnitTests;

internal static unsafe class InstructionSetDependencyTests
{
    [TestCase(typeof(ICorDynamicInfo))]
    [TestCase(typeof(ICorJitInfo))]
    [TestCase(typeof(ICorDynamicInfo.Interface))]
    public static void InterfacePreservesNegativeDependencyArgument(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type interfaceType)
    {
        var method = interfaceType.GetMethod(nameof(ICorDynamicInfo.notifyInstructionSetUsage))
            ?? throw new AssertionException("Missing instruction set usage method.");
        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(3));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(ManagedInstructionSet)));
        Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(bool)));
        Assert.That(parameters[2].ParameterType, Is.EqualTo(typeof(bool)));
    }

    [Test]
    public static void VtablePreservesNativeSignature()
    {
        var slot = typeof(ICorDynamicInfo.Vtbl<ICorDynamicInfo>)
            .GetField(nameof(ICorDynamicInfo.Vtbl<>.notifyInstructionSetUsage))
            ?? throw new AssertionException("Missing instruction set usage slot.");
        Type[] expectedParameters = [typeof(ICorDynamicInfo*), typeof(CORINFO_InstructionSet), typeof(bool), typeof(bool)];
        Assert.That(slot.FieldType.GetFunctionPointerParameterTypes(), Is.EqualTo(expectedParameters));
        Assert.That(slot.FieldType.GetFunctionPointerReturnType(), Is.EqualTo(typeof(byte)));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void WrappersForwardBothBooleanArguments(bool supported, bool preserve)
    {
        var instructionSet = GetTargetInstructionSet();
        var vtable = CreateVtable();
        TestJitInfo info = new() { Interface = new() { lpVtbl = &vtable } };

        Assert.That(info.Interface.notifyInstructionSetUsage(instructionSet, supported, preserve),
            Is.EqualTo(supported));
        Assert.That(((ICorDynamicInfo*)&info)->notifyInstructionSetUsage(instructionSet, supported, preserve),
            Is.EqualTo(supported));
        Assert.That(info.Count, Is.EqualTo(2));
        Assert.That(info.InstructionSet, Is.EqualTo((CORINFO_InstructionSet)Globals.GetNativeInstructionSetOrdinal(instructionSet)));
        Assert.That(info.Supported, Is.EqualTo(supported ? 1 : 0));
        Assert.That(info.Preserve, Is.EqualTo(preserve ? 1 : 0));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NegativeDependencyBypassesReportedIsaCache(bool supported)
    {
        var instructionSet = GetTargetInstructionSet();
        var vtable = CreateVtable();
        TestJitInfo info = new() { Interface = new() { lpVtbl = &vtable } };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &info.Interface;
        if (supported)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(instructionSet);
        }

#if DEBUG
        using var tls = new JitTls(&info.Interface);
        JitTls.Compiler = compiler;
        JitTls.LogEnv.Compiler = compiler;
#endif
        if (Configuration.TARGET_WASM)
        {
            Assert.That(ExactlyDependsOn(compiler, instructionSet, false), Is.False);
            Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet, true), Is.False);
            Assert.That(info.Count, Is.Zero);
            return;
        }

        Assert.That(ExactlyDependsOn(compiler, instructionSet, false), Is.EqualTo(supported));
        Assert.That(ExactlyDependsOn(compiler, instructionSet, false), Is.EqualTo(supported));
        Assert.That(info.Count, Is.EqualTo(1));
        Assert.That(info.Preserve, Is.Zero);

        Assert.That(ExactlyDependsOn(compiler, instructionSet, true), Is.EqualTo(supported));
        Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet, true), Is.EqualTo(supported));
        Assert.That(info.Count, Is.EqualTo(3));
        Assert.That(info.Preserve, Is.EqualTo(1));
        Assert.That(info.Supported, Is.EqualTo(supported ? 1 : 0));
    }

    [Test]
    public static void OpportunisticAbsentIsaOnlyNotifiesWhenPreservingDependency()
    {
        var instructionSet = GetTargetInstructionSet();
        var vtable = CreateVtable();
        TestJitInfo info = new() { Interface = new() { lpVtbl = &vtable } };
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &info.Interface;
#if DEBUG
        using var tls = new JitTls(&info.Interface);
        JitTls.Compiler = compiler;
        JitTls.LogEnv.Compiler = compiler;
#endif
        if (Configuration.TARGET_WASM)
        {
            Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet), Is.False);
            Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet, true), Is.False);
            Assert.That(info.Count, Is.Zero);
            return;
        }

        Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet), Is.False);
        Assert.That(info.Count, Is.Zero);
        Assert.That(compiler.compOpportunisticallyDependsOn(instructionSet, true), Is.False);
        Assert.That(info.Count, Is.EqualTo(1));
        Assert.That(info.Preserve, Is.EqualTo(1));
    }

    private static ManagedInstructionSet GetTargetInstructionSet()
    {
        foreach (var instructionSet in Enum.GetValues<ManagedInstructionSet>())
        {
            if (instructionSet is InstructionSet_ILLEGAL or InstructionSet_Vector or InstructionSet_NONE)
            {
                continue;
            }

            if (instructionSet.ToString().EndsWith("Base", StringComparison.Ordinal))
            {
                continue;
            }

            if (Globals.TryGetNativeInstructionSetOrdinal(instructionSet, out _))
            {
                return instructionSet;
            }
        }

        throw new AssertionException("The target does not define an instruction set.");
    }

    private static ICorJitInfo.Vtbl<ICorJitInfo> CreateVtable()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&Notify;
        return vtable;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Notify(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported, byte preserve)
    {
        var info = (TestJitInfo*)self;
        info->Count++;
        info->InstructionSet = isa;
        info->Supported = supported;
        info->Preserve = preserve;
        return supported;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "compExactlyDependsOn")]
    private static extern bool ExactlyDependsOn(Compiler compiler, ManagedInstructionSet isa, bool preserve);

    private struct TestJitInfo
    {
        public ICorJitInfo Interface;
        public CORINFO_InstructionSet InstructionSet;
        public int Count;
        public byte Supported;
        public byte Preserve;
    }
}

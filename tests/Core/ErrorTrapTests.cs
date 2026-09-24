// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ErrorTrapTests
{
    private static bool s_reject;

    [TestCase(CorJitResult.CORJIT_BADCODE)]
    [TestCase(CorJitResult.CORJIT_INTERNALERROR)]
    [TestCase(CorJitResult.CORJIT_IMPLLIMITATION)]
    [TestCase(CorJitResult.CORJIT_SKIPPED)]
    public static void FatalFailurePreservesJitResult(CorJitResult result)
    {
        var failure = Assert.Throws<FatalJitException>(() => Globals.fatal(result));
        Assert.That(failure?.Result, Is.EqualTo(result));
        Assert.That(failure?.HResult, Is.EqualTo(Globals.FATAL_JIT_EXCEPTION));
    }

    [Test]
    public static void UnspecifiedFatalResultRemainsInternalError()
    {
        var inner = new InvalidOperationException();
        var failure = new FatalJitException("failure", inner);
        Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_INTERNALERROR));
        Assert.That(failure.Message, Is.EqualTo("failure"));
        Assert.That(failure.InnerException, Is.SameAs(inner));
    }

    [TestCase(false, 0)]
    [TestCase(false, 2)]
    [TestCase(false, 3)]
    [TestCase(true, 0)]
    [TestCase(true, 1)]
    [TestCase(true, 2)]
    [TestCase(true, 3)]
    public static void ManagedCallbacksRespectTrapAndTerminalSemantics(bool spmiOnly, int kind)
    {
        WithCompiler(compiler => {
            Exception? failure = kind switch {
                1 => new FatalJitException(),
                2 => new InvalidOperationException("callback failure"),
                3 => new InvalidOperationException("terminal HRESULT") { HResult = unchecked((int)0x80131530) },
                _ => null,
            };
            var invoked = false;
            bool Invoke()
            {
                return spmiOnly
                    ? compiler.eeRunFunctorWithSpmiErrorTrap(Callback)
                    : compiler.eeRunFunctorWithErrorTrap(Callback);
            }
            void Callback()
            {
                invoked = true;

                if (failure is not null)
                {
                    ThrowFromManagedCallback(failure);
                }
            }

            if (failure is not null && (spmiOnly || kind == 3))
            {
                var propagated = Assert.Catch<Exception>(() => _ = Invoke());
                Assert.That(propagated, Is.SameAs(failure));
                Assert.That(propagated?.StackTrace, Does.Contain(nameof(ThrowFromManagedCallback)));
            }
            else
            {
                Assert.That(Invoke(), Is.EqualTo(kind == 0));
            }

            Assert.That(invoked, Is.True);
        });
    }

    [Test]
    public static void FatalJitFailureDoesNotCrossUnmanagedBoundary()
        => ManagedCallbacksRespectTrapAndTerminalSemantics(false, 1);

    [TestCase(false)]
    [TestCase(true)]
    public static void RetainsEEFailureWithoutInvokingTheCallback(bool spmiOnly)
    {
        WithCompiler(compiler => {
            s_reject = true;
            var invoked = false;
            var succeeded = spmiOnly
                ? compiler.eeRunFunctorWithSpmiErrorTrap(() => invoked = true)
                : compiler.eeRunFunctorWithErrorTrap(() => invoked = true);
            Assert.That(succeeded, Is.False);
            Assert.That(invoked, Is.False);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromManagedCallback(Exception failure) => throw failure;

    private static void WithCompiler(Action<Compiler> action)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.runWithErrorTrap = vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&InvokeCallback;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        s_reject = false;
        action(compiler);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte InvokeCallback(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        if (s_reject)
        {
            return 0;
        }

        callback(state);
        return 1;
    }
}

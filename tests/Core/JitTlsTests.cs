// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if DEBUG
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class JitTlsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void NestedScopesRestoreEachThreadsCompilerAndLog(bool throwFromInnerScope)
    {
        const int workerCount = 4;
        using var start = new Barrier(workerCount);
        var workers = new Task[workerCount];

        for (var worker = 0; worker < workerCount; worker++)
        {
            workers[worker] = Task.Factory.StartNew(() => {
                var previousCompiler = JitTls.Compiler;
                var previousLogIsNull = Unsafe.IsNullRef(ref JitTls.LogEnv);
                using (var outer = new JitTls(null))
                {
                    var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                    JitTls.Compiler = compiler;
                    JitTls.LogEnv.Compiler = compiler;
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("TLS workers did not reach the start barrier.");
                    }

                    if (throwFromInnerScope)
                    {
                        _ = Assert.Throws<InvalidOperationException>(RunInnerScope);
                    }
                    else
                    {
                        RunInnerScope();
                    }

                    Assert.That(JitTls.Compiler, Is.SameAs(compiler));
                    Assert.That(JitTls.LogEnv.Compiler, Is.SameAs(compiler));
                }

                Assert.That(JitTls.Compiler, Is.SameAs(previousCompiler));
                Assert.That(Unsafe.IsNullRef(ref JitTls.LogEnv), Is.EqualTo(previousLogIsNull));

                void RunInnerScope()
                {
                    using var inner = new JitTls(null);
                    Assert.That(JitTls.Compiler, Is.Null);
                    Assert.That(JitTls.LogEnv.Compiler, Is.Null);
                    var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                    JitTls.Compiler = compiler;
                    JitTls.LogEnv.Compiler = compiler;
                    Assert.That(JitTls.Compiler, Is.SameAs(compiler));
                    Assert.That(JitTls.LogEnv.Compiler, Is.SameAs(compiler));
                    if (throwFromInnerScope)
                    {
                        throw new InvalidOperationException("Unwind the nested JIT scope.");
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        Task.WaitAll(workers);
    }

    [Test]
    public static void AbandonedScopeDoesNotInstallCompilerOnFinalizerThread()
    {
        _ = CurrentScope(null);
        JitTls? previousScope = null;
        Compiler? previousCompiler = null;
        RunOnFinalizerThread(() => {
            previousScope = CurrentScope(null);
            previousCompiler = JitTls.Compiler;
        });

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var (outer, inner) = AbandonNestedScope(compiler);
        Compiler? observedCompiler = null;
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RunOnFinalizerThread(() => observedCompiler = JitTls.Compiler);
        }
        finally
        {
            RunOnFinalizerThread(() => {
                outer.Dispose();
                CurrentScope(null) = previousScope;
            });
            GC.KeepAlive(outer);
        }

        Assert.That(inner.IsAlive, Is.False, "The abandoned inner scope must have been collected.");
        Assert.That(observedCompiler, Is.SameAs(previousCompiler), "Finalization must not restore another thread's compiler.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (JitTls Outer, WeakReference Inner) AbandonNestedScope(Compiler compiler)
    {
        JitTls? outer = null;
        WeakReference? inner = null;
        var thread = new Thread(() => {
            outer = new JitTls(null);
            JitTls.Compiler = compiler;
            inner = new WeakReference(new JitTls(null), trackResurrection: true);
        });
        thread.Start();
        thread.Join();

        return (outer ?? throw new InvalidOperationException("Missing outer scope."),
            inner ?? throw new InvalidOperationException("Missing inner scope."));
    }

    private static void RunOnFinalizerThread(Action action)
    {
        var completed = false;
        QueueFinalizer(() => {
            action();
            completed = true;
        });
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.That(completed, Is.True, "The finalizer-thread observation must have executed.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void QueueFinalizer(Action action)
    {
        _ = new FinalizerAction(action);
    }

    private sealed class FinalizerAction(Action action)
    {
        ~FinalizerAction()
        {
            action();
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "t_jitTls")]
    private static extern ref JitTls? CurrentScope(JitTls? type);
}
#endif

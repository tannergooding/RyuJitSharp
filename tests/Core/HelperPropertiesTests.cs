// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class HelperPropertiesTests
{
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETCURRENTMANAGEDTHREADID, false, false, false, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_BULK_WRITEBARRIER_SMALL, false, true, true, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_JIT_RESUME_AFTER_CATCH, false, false, false, true)]
    public static void UpdatedHelpersMatchNativeProperties(CorInfoHelpFunc helper, bool isPure, bool isNoGC, bool mutatesHeap, bool noThrow)
    {
        Assert.Multiple(() => {
            Assert.That(helper.IsPure, Is.EqualTo(isPure));
            Assert.That(helper.IsNoGC, Is.EqualTo(isNoGC));
            Assert.That(helper.MutatesHeap, Is.EqualTo(mutatesHeap));
            Assert.That(helper.NoThrow, Is.EqualTo(noThrow));
        });
    }
}

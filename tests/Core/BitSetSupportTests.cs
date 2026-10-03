// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RyuJitSharp;

namespace RyuJitSharp.UnitTests;

internal static class BitSetSupportTests
{
    [Test]
    public static void OperationCounterWritesSortedCountsAtTheNativeInterval()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var counter = new BitSetSupport.BitSetOpCounter(path);

            for (var i = 0; i < 1_000_000; i++)
            {
                counter.RecordOp(BitSetSupport.Operation.BSOP_AddElemD);
            }

            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var expectedTopLine = string.Format(
                CultureInfo.InvariantCulture,
                "   Op {0,40}: {1,8}",
                nameof(BitSetSupport.Operation.BSOP_AddElemD),
                1_000_000);

            Assert.That(BitSetSupport.OpNames.Length, Is.EqualTo((int)BitSetSupport.Operation.BSOP_NUMOPS));
            Assert.That(lines, Has.Length.EqualTo(BitSetSupport.OpNames.Length + 1));
            Assert.That(lines[0], Is.EqualTo("@ 1000000 total ops."));
            Assert.That(lines[1], Is.EqualTo(expectedTopLine));
            Assert.That(lines[^1], Does.Contain(nameof(BitSetSupport.Operation.BSOP_ToString)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

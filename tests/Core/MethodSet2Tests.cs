// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static unsafe class MethodSet2Tests
{
    [Test]
    public static void SmallMethodSetKeepsLinearLookupStorage()
    {
        WithMethodSet(["Example.Type:Method"], methodSet => {
            Assert.That(MethodNames(methodSet), Is.Null);
            Assert.That(MethodHashes(methodSet), Is.Null);
            Assert.That(methodSet.IsInSet("EXAMPLE.TYPE:METHOD"), Is.True);
            Assert.That(methodSet.IsInSet(0), Is.True);
            Assert.That(methodSet.IsInSet("Missing.Type:Method"), Is.False);
        });
    }

    [Test]
    public static void LargeMethodSetIndexesNamesAndHashesWithoutChangingMatching()
    {
        var lines = new string[101];
        lines[0] = "Example.Type:HasHash (MethodHash=42)";
        lines[1] = "Example.Type:NameOnly";

        for (var index = 2; index < lines.Length; index++)
        {
            lines[index] = $"Example.Type:Method{index} (MethodHash={1000 + index})";
        }

        WithMethodSet(lines, methodSet => {
            Assert.That(MethodNames(methodSet), Is.Not.Null);
            Assert.That(MethodHashes(methodSet), Is.Not.Null);
            Assert.That(methodSet.IsInSet("EXAMPLE.TYPE:HASHASH"), Is.True);
            Assert.That(methodSet.IsInSet("example.type:nameonly"), Is.True);
            Assert.That(methodSet.IsInSet(42), Is.True);
            Assert.That(methodSet.IsInSet(0), Is.True);
            Assert.That(methodSet.IsInSet(1100), Is.True);
            Assert.That(methodSet.IsInSet(-1), Is.False);
            Assert.That(methodSet.IsActiveMethod("Unmatched.Type:Method", 42), Is.True);
            Assert.That(methodSet.IsActiveMethod("EXAMPLE.TYPE:NAMEONLY", -1), Is.True);
            Assert.That(methodSet.IsActiveMethod("Missing.Type:Method", -1), Is.False);
        });
    }

    private static void WithMethodSet(string[] lines, Action<MethodSet2> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ryujitsharp-{Guid.NewGuid():N}-methodset.txt");

        try
        {
            File.WriteAllLines(path, lines, Encoding.UTF8);
            var encodedPath = Encoding.UTF8.GetBytes(path + "\0");

            fixed (byte* pFilename = encodedPath)
            {
                var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
                var previousCompiler = JitTls.Compiler;
                using var tls = TestConfiguration.CreateJitTlsWithCompiler(compiler);

                try
                {
                    action(new MethodSet2(pFilename));
                }
                finally
                {
                    JitTls.Compiler = previousCompiler;
                }
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_methodNames")]
    private static extern ref HashSet<string>? MethodNames(MethodSet2 methodSet);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_methodHashes")]
    private static extern ref HashSet<int>? MethodHashes(MethodSet2 methodSet);
}

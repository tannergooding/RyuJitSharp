// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class FuncInfoRangeTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public static void TraversalExcludesRootAndPreservesDescriptorReferences(int funcletCount)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.fgFuncletsCreated = true;
        compiler.compFuncInfoCount = (ushort)(funcletCount + 1);
        compiler.compFuncInfos = new FuncInfoDsc[funcletCount + 2];
        compiler.compFuncInfos[0].funFlags = 91;
        compiler.compFuncInfos[^1].funFlags = 92;

        var index = 1;
        foreach (ref var function in compiler.Funclets())
        {
            function.funFlags = (byte)index;
            index++;
        }

        Assert.That(index, Is.EqualTo(funcletCount + 1));
        index = funcletCount;

        foreach (ref var function in compiler.Funclets().Reverse())
        {
            Assert.That(function.funFlags, Is.EqualTo(index));
            function.funFlags += 10;
            index--;
        }

        Assert.That(index, Is.Zero);
        Assert.That(compiler.compFuncInfos[0].funFlags, Is.EqualTo(91));
        Assert.That(compiler.compFuncInfos[^1].funFlags, Is.EqualTo(92));

        for (index = 1; index <= funcletCount; index++)
        {
            Assert.That(compiler.compFuncInfos[index].funFlags, Is.EqualTo(index + 10));
        }
    }
}

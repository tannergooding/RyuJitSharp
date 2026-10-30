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
        compiler.compFuncInfos[0].funEHIndex = 91;
        compiler.compFuncInfos[^1].funEHIndex = 92;

        var index = 1;
        foreach (ref var function in compiler.Funclets())
        {
            function.funEHIndex = (ushort)index;
            index++;
        }

        Assert.That(index, Is.EqualTo(funcletCount + 1));
        index = funcletCount;

        foreach (ref var function in compiler.Funclets().Reverse())
        {
            Assert.That(function.funEHIndex, Is.EqualTo(index));
            function.funEHIndex += 10;
            index--;
        }

        Assert.That(index, Is.Zero);
        Assert.That(compiler.compFuncInfos[0].funEHIndex, Is.EqualTo(91));
        Assert.That(compiler.compFuncInfos[^1].funEHIndex, Is.EqualTo(92));

        for (index = 1; index <= funcletCount; index++)
        {
            Assert.That(compiler.compFuncInfos[index].funEHIndex, Is.EqualTo(index + 10));
        }
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.RegSet.TEMP_USAGE_TYPE;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class SpillTemporaryTests
{
    [Test]
    public static void PreallocationKeepsDistinctTypesAndNumberingInSharedSlots()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        ref var regSet = ref codeGen.RegSet;

        Assert.That(regSet.HasComputedTmpSize, Is.False);

        regSet.tmpBeginPreAllocateTemps();
        regSet.tmpPreAllocateTemps(TYP_REF, 1);
        regSet.tmpPreAllocateTemps(TYP_LONG, 1);
        regSet.tmpPreAllocateTemps(TYP_REF, 1);

        Assert.That(regSet.tmpTotalSize, Is.EqualTo((2 * TYP_REF.Size) + TYP_LONG.Size));
        Assert.That(regSet.tmpListBeg()?.tdTempNum, Is.EqualTo(-3));

        var longTemp = regSet.tmpGetTemp(TYP_LONG);
        var refTemp = regSet.tmpGetTemp(TYP_REF);

        Assert.That(longTemp.tdTempNum, Is.EqualTo(-2));
        Assert.That(refTemp.tdTempNum, Is.EqualTo(-3));
        Assert.That(regSet.tmpFindNum(-1)?.tdTempType, Is.EqualTo(TYP_REF));
        Assert.That(regSet.tmpFindNum(-2, TEMP_USAGE_USED), Is.SameAs(longTemp));
        Assert.That(regSet.tmpGetTemp(TYP_REF).tdTempNum, Is.EqualTo(-1));

        regSet.tmpRlsTemp(longTemp);
        Assert.That(regSet.tmpGetTemp(TYP_LONG), Is.SameAs(longTemp));
    }

    [Test]
    public static void ReleaseReusesMostRecentlyFreedDescriptorWithoutChangingItsType()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        ref var regSet = ref codeGen.RegSet;

        regSet.tmpBeginPreAllocateTemps();
        regSet.tmpPreAllocateTemps(TYP_INT, 2);
        regSet.tmpPreAllocateTemps(TYP_FLOAT, 1);

        var first = regSet.tmpGetTemp(TYP_UBYTE);
        var second = regSet.tmpGetTemp(TYP_INT);
        var floating = regSet.tmpGetTemp(TYP_FLOAT);

        Assert.That(first.tdTempType, Is.EqualTo(TYP_INT));
        Assert.That(floating.tdTempType, Is.EqualTo(TYP_FLOAT));
        Assert.That(regSet.tmpIsUnknownSizeTemp(first.tdTempNum), Is.False);

        regSet.tmpRlsTemp(first);
        regSet.tmpRlsTemp(second);
        regSet.tmpRlsTemp(floating);

#if DEBUG
        Assert.That(regSet.tmpGetAllFree(), Is.True);
#endif
        Assert.That(regSet.tmpGetTemp(TYP_INT), Is.SameAs(second));
        Assert.That(regSet.tmpGetTemp(TYP_FLOAT), Is.SameAs(floating));
    }
}

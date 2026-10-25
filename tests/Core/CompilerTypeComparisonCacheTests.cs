// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerTypeComparisonCacheTests
{
    private static int s_castComparisonCalls;
    private static int s_equalityComparisonCalls;

    [Test]
    public static void TypeComparisonsCacheOrderedPairsAndCompleteTriStateResults()
    {
        s_castComparisonCalls = 0;
        s_equalityComparisonCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.compareTypesForCast = &CompareTypesForCast;
        vtable.Base.Base.compareTypesForEquality = &CompareTypesForEquality;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstClass = (CORINFO_CLASS_STRUCT_*)0x101;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x102;

        Assert.That(firstCompiler.eeCompareTypesForCast(firstClass, secondClass), Is.EqualTo(TypeCompareState.Must));
        Assert.That(firstCompiler.eeCompareTypesForEquality(firstClass, secondClass), Is.EqualTo(TypeCompareState.May));
        Assert.That(firstCompiler.eeCompareTypesForCast(firstClass, secondClass), Is.EqualTo(TypeCompareState.Must));
        Assert.That(firstCompiler.eeCompareTypesForCast(secondClass, firstClass), Is.EqualTo(TypeCompareState.MustNot));
        Assert.That(firstCompiler.eeCompareTypesForCast(secondClass, firstClass), Is.EqualTo(TypeCompareState.MustNot));
        Assert.That(firstCompiler.eeCompareTypesForCast(firstClass, secondClass), Is.EqualTo(TypeCompareState.Must));
        Assert.That(firstCompiler.eeCompareTypesForEquality(firstClass, secondClass), Is.EqualTo(TypeCompareState.May));
        Assert.That(firstCompiler.eeCompareTypesForEquality(secondClass, firstClass), Is.EqualTo(TypeCompareState.May));
        Assert.That(firstCompiler.eeCompareTypesForEquality(secondClass, firstClass), Is.EqualTo(TypeCompareState.May));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        Assert.That(secondCompiler.eeCompareTypesForCast(firstClass, secondClass), Is.EqualTo(TypeCompareState.Must));
        Assert.That(secondCompiler.eeCompareTypesForEquality(firstClass, secondClass), Is.EqualTo(TypeCompareState.May));

        Assert.Multiple(() => {
            Assert.That(s_castComparisonCalls, Is.EqualTo(4));
            Assert.That(s_equalityComparisonCalls, Is.EqualTo(3));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareTypesForCast(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* fromClass, CORINFO_CLASS_STRUCT_* toClass)
    {
        s_castComparisonCalls++;
        return ((nint)fromClass, (nint)toClass) switch {
            (0x101, 0x102) => TypeCompareState.Must,
            (0x102, 0x101) => TypeCompareState.MustNot,
            _ => TypeCompareState.May,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareTypesForEquality(
        ICorJitInfo* _, CORINFO_CLASS_STRUCT_* firstClass, CORINFO_CLASS_STRUCT_* secondClass)
    {
        s_equalityComparisonCalls++;
        return ((nint)firstClass, (nint)secondClass) switch {
            (0x101, 0x102) => TypeCompareState.May,
            (0x102, 0x101) => TypeCompareState.May,
            _ => TypeCompareState.MustNot,
        };
    }
}

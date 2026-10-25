// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CompilerStringCharacterCacheTests
{
    private static int s_getStringCharCalls;

    [Test]
    public static void StringCharactersCacheSuccessfulObjectAndIndexPairsOnly()
    {
        s_getStringCharCalls = 0;

        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getStringChar = &GetStringChar;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        var firstCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        firstCompiler.info.compCompHnd = &jitInfo;

        var firstString = (CORINFO_OBJECT_STRUCT_*)0x101;
        var secondString = (CORINFO_OBJECT_STRUCT_*)0x102;

        ushort character = 0;
        Assert.That(firstCompiler.eeGetStringChar(firstString, 0, &character), Is.True);
        Assert.That(character, Is.EqualTo((ushort)'A'));
        character = ushort.MaxValue;
        Assert.That(firstCompiler.eeGetStringChar(firstString, 0, &character), Is.True);
        Assert.That(character, Is.EqualTo((ushort)'A'));

        character = 0;
        Assert.That(firstCompiler.eeGetStringChar(firstString, 1, &character), Is.True);
        Assert.That(character, Is.EqualTo((ushort)'B'));
        character = 0;
        Assert.That(firstCompiler.eeGetStringChar(secondString, 0, &character), Is.True);
        Assert.That(character, Is.EqualTo((ushort)'Z'));

        character = 0x1234;
        Assert.That(firstCompiler.eeGetStringChar(firstString, 8, &character), Is.False);
        Assert.That(character, Is.EqualTo((ushort)0x1234));
        character = 0x5678;
        Assert.That(firstCompiler.eeGetStringChar(firstString, 8, &character), Is.False);
        Assert.That(character, Is.EqualTo((ushort)0x5678));

        var secondCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        secondCompiler.info.compCompHnd = &jitInfo;
        character = 0;
        Assert.That(secondCompiler.eeGetStringChar(firstString, 0, &character), Is.True);
        Assert.That(character, Is.EqualTo((ushort)'A'));

        Assert.That(s_getStringCharCalls, Is.EqualTo(6));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetStringChar(
        ICorJitInfo* _, CORINFO_OBJECT_STRUCT_* stringHandle, int index, ushort* value)
    {
        s_getStringCharCalls++;
        if (index >= 8)
        {
            return 0;
        }

        *value = (nint)stringHandle == 0x101
               ? (ushort)('A' + index)
               : (ushort)'Z';
        return 1;
    }
}

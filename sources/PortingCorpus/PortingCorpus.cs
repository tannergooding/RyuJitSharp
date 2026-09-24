// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace RyuJitSharp;

internal static class PortingCorpus
{
    public static unsafe int Main()
    {
        if (Add(4, 7) != 11)
        {
            return 1;
        }

        if ((Branch(-2) != 2) || (Branch(3) != 4))
        {
            return 2;
        }

        if (Locals(5) != 36)
        {
            return 3;
        }

        if (Call(6) != 13)
        {
            return 4;
        }

        if (InlineCaller(6) != 13)
        {
            return 5;
        }

        if (IndirectCall(6) != 13)
        {
            return 6;
        }

        if (FoldConstants() != 27)
        {
            return 7;
        }

        if (BitConverter.DoubleToInt64Bits(FoldFloating(-0.0)) != long.MinValue)
        {
            return 8;
        }

        if (FoldInteger(0x1234) != 4712)
        {
            return 9;
        }

        if (FoldHardware(5) != 12)
        {
            return 10;
        }

        if (SynchronizedReturn(5) != 6)
        {
            return 11;
        }

        if (GenericCatch<InvalidOperationException>(new InvalidOperationException()) != 17)
        {
            return 12;
        }

        if (OperatingSystem.IsWindows() && (PInvokeCall() == 0))
        {
            return 13;
        }

        delegate* unmanaged<int, int> reversePInvoke = &ReversePInvoke;

        if (reversePInvoke(5) != 6)
        {
            return 14;
        }

        if ((ManyReturns(0) != 10) || (ManyReturns(4) != 50) || (ManyReturns(9) != 60))
        {
            return 15;
        }

        if (LocalAddressStore(0x12340000) != 0x12340007)
        {
            return 16;
        }

        if (LocalAddressDifference(5) != 3)
        {
            return 17;
        }

        if (ImplicitByRefArgument(new Triple { First = 1, Second = 2, Third = 3 }) != 6)
        {
            return 18;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Add(int left, int right) => left + right;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Branch(int value) => value < 0 ? -value : value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Locals(int value)
    {
        var first = value + 1;
        var second = value - 2;

        return (first * first) + second - 3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Call(int value) => Add(value, value + 1);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int InlineCaller(int value) => InlineCandidate(value) + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe int IndirectCall(int value)
    {
        delegate* managed<int, int, int> target = &Add;
        return target(value, value + 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long FoldConstants()
    {
        // BitConverter keeps these constants in IL until the JIT imports the intrinsics.
        return ((long)BitConverter.Int32BitsToSingle(0x40F00000) + (int)BitConverter.Int64BitsToDouble(0x4004000000000000)) * 3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double FoldFloating(double value) => (((value + -0.0) * 1.0) - 0.0) / 1.0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FoldInteger(int value) => (value & 0xFF) + (value / 1) + (value * 0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int FoldHardware(int value) => (Vector128.Create(3) + Vector128.Create(4)).ToScalar() + value;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.Synchronized)]
    public static int SynchronizedReturn(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int GenericCatch<TException>(Exception exception) where TException : Exception
    {
        try
        {
            throw exception;
        }
        catch (TException)
        {
            return 17;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static uint PInvokeCall() => GetCurrentProcessId();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetCurrentProcessId();

    [UnmanagedCallersOnly]
    public static int ReversePInvoke(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ManyReturns(int value)
    {
        switch (value)
        {
            case 0:
            {
                return 10;
            }

            case 1:
            {
                return 20;
            }

            case 2:
            {
                return 30;
            }

            case 3:
            {
                return 40;
            }

            case 4:
            {
                return 50;
            }

            default:
            {
                return 60;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InlineCandidate(int value) => value * 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe int LocalAddressStore(int value)
    {
        var local = value;
        var address = (short*)&local;
        *address = 7;
        return local;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe long LocalAddressDifference(int value)
    {
        var local = value;
        var address = (byte*)&local;
        return address + 3 - address;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ImplicitByRefArgument(Triple value) => value.First + value.Second + value.Third;

    private struct Triple
    {
        public long First;
        public long Second;
        public long Third;
    }
}

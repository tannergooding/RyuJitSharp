// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Numerics;
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

        if (VectorDivide(new Vector<int>(8), new Vector<int>(2))[0] != 4)
        {
            return 22;
        }

        if (PartialVectorCreate(5).GetElement(0) != 1)
        {
            return 21;
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

        if (LoopSum(8) != 28)
        {
            return 19;
        }

        if (IndexedArray(3) != 14)
        {
            return 20;
        }

        if (RepeatedFields(3) != 46)
        {
            return 22;
        }

        if (GenericMetadataReuse(3) != 32)
        {
            return 23;
        }

        if (RepeatedCalls(3) != 60)
        {
            return 24;
        }

        if ((CriticalEdgeJoin(4) != 12) || (CriticalEdgeJoin(3) != 14) || (CriticalEdgeJoin(-2) != -6))
        {
            return 25;
        }

        if ((NestedLoopExits(3) != 13) || (NestedLoopExits(4) != 24))
        {
            return 26;
        }

        if ((NestedExceptionFlow(2) != 11) || (NestedExceptionFlow(3) != 24))
        {
            return 27;
        }

        if ((SwitchSuccessors(4) != 14) || (SwitchSuccessors(5) != 25) ||
            (SwitchSuccessors(6) != 36) || (SwitchSuccessors(7) != 47))
        {
            return 28;
        }

        if ((CriticalEdgeRegisterPressure(-1) != 66) || (CriticalEdgeRegisterPressure(1) != 180))
        {
            return 29;
        }

        if ((LoopExitWithPreheaderJoin(4) != 10) ||
            (LoopExitWithPreheaderJoin(2) != 5) ||
            (LoopExitWithPreheaderJoin(-3) != 0))
        {
            return 30;
        }

        if ((MultipleLoopExitEdges(6) != 30) || (MultipleLoopExitEdges(5) != 11))
        {
            return 31;
        }

        if ((NestedLoopSharedExit(3) != 23) || (NestedLoopSharedExit(2) != 22))
        {
            return 32;
        }

        if ((DisjointLoops(6) != 21) || (DisjointLoops(5) != 15))
        {
            return 33;
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector<int> VectorDivide(Vector<int> dividend, Vector<int> divisor) => dividend / divisor;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Vector128<int> PartialVectorCreate(int value) => Vector128.Create(1, 2, value, 4);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LoopSum(int count)
    {
        var sum = 0;
        for (var index = 0; index < count; index++)
        {
            sum += index;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int IndexedArray(int seed)
    {
        var values = new int[5];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = seed + (index * 2);
        }

        var selectedIndex = seed & 3;
        return values[selectedIndex] + values[4 - selectedIndex];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int RepeatedFields(int value)
    {
        var item = new MetadataFields
        {
            Number = value,
            Wide = value + 1,
            Reference = value,
        };
        var values = new[] { item, item };
        var boxedValue = (int)item.Reference!;

        var sum = item.Number + item.Number + item.Number + item.Number;
        sum += (int)item.Wide + (int)item.Wide + (int)item.Wide + (int)item.Wide;
        sum += values[0].Number + values[1].Number + values[0].Number + values[1].Number;
        sum += boxedValue + boxedValue;

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int GenericMetadataReuse(int value)
    {
        var integerBox = new GenericMetadataBox<int>
        {
            Value = value,
            Tag = value + 1,
        };
        var stringBox = new GenericMetadataBox<string>
        {
            Value = "x",
            Tag = value + 2,
        };
        var integerBoxes = new[] { integerBox, integerBox };

        var sum = integerBox.Value + integerBox.Value + integerBoxes[0].Value + integerBoxes[1].Value;
        sum += integerBox.Tag + integerBox.Tag + stringBox.Tag + stringBox.Tag;
        sum += stringBox.Value.Length + stringBox.Value.Length;

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int RepeatedCalls(int value)
    {
        return Add(value, 1) + Add(value, 2) + Add(value, 3) + Add(value, 4) +
            Add(value, 5) + Add(value, 6) + Add(value, 7) + Add(value, 8);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int CriticalEdgeJoin(int value)
    {
        var result = value * 2;
        if ((value & 1) == 0)
        {
            goto Join;
        }

        result += 5;

    Join:
        return result + value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int CriticalEdgeRegisterPressure(int value)
    {
        var first = value + 1;
        var second = value + 2;
        var third = value + 3;
        var fourth = value + 4;
        var fifth = value + 5;
        var sixth = value + 6;
        var seventh = value + 7;
        var eighth = value + 8;
        var ninth = value + 9;
        var tenth = value + 10;
        var eleventh = value + 11;
        var twelfth = value + 12;

        var sum = 0;
        if (value < 0)
        {
            goto Join;
        }

        sum = Add(first, second) + Add(third, fourth) + Add(fifth, sixth) +
            Add(seventh, eighth) + Add(ninth, tenth) + Add(eleventh, twelfth);

    Join:
        return sum + first + second + third + fourth + fifth + sixth +
            seventh + eighth + ninth + tenth + eleventh + twelfth;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NestedLoopExits(int count)
    {
        var sum = 0;
        for (var outer = 0; outer < count; outer++)
        {
            for (var inner = 0; inner < 5; inner++)
            {
                if (inner == outer)
                {
                    continue;
                }

                if (inner > (outer + 1))
                {
                    break;
                }

                sum += inner + 1;
            }
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LoopExitWithPreheaderJoin(int count)
    {
        var sum = 0;
        if (count < 0)
        {
            sum = -count;
            goto Exit;
        }

        var index = 0;
        while (index < count)
        {
            sum += index + 1;
            if (index == 2)
            {
                goto Exit;
            }

            index++;
        }

    Exit:
        return sum + count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int MultipleLoopExitEdges(int count)
    {
        var result = 0;
        for (var index = 0; index < count; index++)
        {
            if (((count & 1) != 0) && (index == 1))
            {
                result += 10;
                break;
            }

            if (((count & 1) == 0) && (index == 4))
            {
                result += 20;
                break;
            }

            result += index + 1;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NestedLoopSharedExit(int rows)
    {
        var total = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                if ((row >= 1) && (column >= 2))
                {
                    goto Exit;
                }

                total += row + column + 1;
            }
        }

    Exit:
        return total + rows;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DisjointLoops(int count)
    {
        var sum = 0;
        var split = count / 2;
        for (var index = 0; index < split; index++)
        {
            sum += index + 1;
        }

        for (var index = split; index < count; index++)
        {
            sum += index + 1;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NestedExceptionFlow(int value)
    {
        var sum = value;
        try
        {
            sum += 4;
            try
            {
                if ((value & 1) == 0)
                {
                    throw new InvalidOperationException();
                }

                sum *= 2;
            }
            finally
            {
                sum += 3;
            }
        }
        catch (InvalidOperationException)
        {
            sum -= 5;
        }
        finally
        {
            sum += 7;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SwitchSuccessors(int value)
    {
        var result = value;
        switch (value & 3)
        {
            case 0:
            {
                result += 10;
                break;
            }
            case 1:
            {
                result += 20;
                break;
            }
            case 2:
            {
                result += 30;
                break;
            }
            default:
            {
                result += 40;
                break;
            }
        }

        return result;
    }

    private struct Triple
    {
        public long First;
        public long Second;
        public long Third;
    }

    private struct MetadataFields
    {
        public int Number;
        public long Wide;
        public object? Reference;
    }

    private struct GenericMetadataBox<T>
    {
        public T Value;
        public int Tag;
    }
}

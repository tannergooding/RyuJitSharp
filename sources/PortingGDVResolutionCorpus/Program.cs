// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RyuJitSharp;

internal interface IWorker
{
    int Compute(int value);
}

internal class Worker : IWorker
{
    public virtual int Compute(int value) => value + 1;
}

internal sealed class DerivedWorker : Worker
{
    public override int Compute(int value) => value + 7;
}

internal static class GDVCases
{
#pragma warning disable CA1859 // Base return types keep dispatch virtual until the factory is inlined.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IWorker CreateInterfaceWorker()
    {
        return new DerivedWorker();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Worker CreateVirtualWorker()
    {
        return new DerivedWorker();
    }
#pragma warning restore CA1859

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int InterfaceCaller(int value)
    {
        var worker = CreateInterfaceWorker();
        return worker.Compute(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int VirtualCaller(int value)
    {
        var worker = CreateVirtualWorker();
        return worker.Compute(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int UnknownReceiver(IWorker worker, int value)
    {
        return worker.Compute(value);
    }
}

internal static class Program
{
    public static int Main()
    {
        IWorker worker = new DerivedWorker();
        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 30_000; i++)
            {
                if (GDVCases.InterfaceCaller(i) != i + 7 ||
                    GDVCases.VirtualCaller(i) != i + 7 ||
                    GDVCases.UnknownReceiver(worker, i) != i + 7)
                {
                    return 1;
                }
            }

            Thread.Sleep(500);
        }

        if (GDVCases.InterfaceCaller(40) != 47 ||
            GDVCases.VirtualCaller(40) != 47 ||
            GDVCases.UnknownReceiver(worker, 40) != 47)
        {
            return 2;
        }

#pragma warning disable CA1303 // Fixed output identifies successful native oracle execution.
        Console.WriteLine("GDV corpus: interface and virtual results verified");
#pragma warning restore CA1303
        return 0;
    }
}

// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public struct SideEffectSet
{
    private GenTreeFlags _sideEffectFlags;
    private ExceptionSetFlags _preciseExceptions;
    private AliasSet _aliasSet;

    public SideEffectSet(Compiler compiler, GenTree node)
    {
        AddNode(compiler, node);
    }

    public void AddNode(Compiler compiler, GenTree node)
    {
        _sideEffectFlags |= node.OperEffects(compiler, out var exceptions);
        _preciseExceptions |= exceptions;
        _aliasSet.AddNode(compiler, node);
    }

    private readonly bool InterferesWithEffects(GenTreeFlags otherFlags, ExceptionSetFlags otherExceptions,
        bool otherWrites, bool strict)
    {
        var producesException = (_sideEffectFlags & GTF_EXCEPT) != 0;
        var otherProducesException = (otherFlags & GTF_EXCEPT) != 0;
        if (strict)
        {
            if (((_sideEffectFlags & GTF_ORDER_SIDEEFF) != 0) && ((otherFlags & (GTF_GLOB_REF | GTF_ORDER_SIDEEFF)) != 0))
            {
                return true;
            }
            if (((otherFlags & GTF_ORDER_SIDEEFF) != 0) && ((_sideEffectFlags & (GTF_GLOB_REF | GTF_ORDER_SIDEEFF)) != 0))
            {
                return true;
            }
            if (producesException && otherProducesException &&
                ((((_preciseExceptions | otherExceptions) & ExceptionSetFlags.UnknownException) != ExceptionSetFlags.None) ||
                 (BitOperations.PopCount((uint)_preciseExceptions) > 1) || (_preciseExceptions != otherExceptions)))
            {
                return true;
            }
        }
        return (producesException && otherWrites) || (otherProducesException && _aliasSet.WritesAnyLocation);
    }

    public readonly bool InterferesWith(in SideEffectSet other, bool strict)
        => InterferesWithEffects(other._sideEffectFlags, other._preciseExceptions, other._aliasSet.WritesAnyLocation, strict) ||
           _aliasSet.InterferesWith(other._aliasSet);

    public readonly bool InterferesWith(Compiler compiler, GenTree node, bool strict)
    {
        var effects = node.OperEffects(compiler, out var exceptions);
        var aliasInfo = new AliasSet.NodeInfo(compiler, node);
        return InterferesWithEffects(effects, exceptions, aliasInfo.WritesAnyLocation, strict) ||
            _aliasSet.InterferesWith(aliasInfo);
    }

    public bool IsLirInvariantInRange(Compiler compiler, GenTree node, GenTree endExclusive, GenTreeFlags ignoreFlagsOnNode = GTF_EMPTY)
        => IsLirInvariantInRange(compiler, node, endExclusive, null, ignoreFlagsOnNode);

    public bool IsLirInvariantInRange(Compiler compiler, GenTree node, GenTree endExclusive, GenTree? ignoreNode,
        GenTreeFlags ignoreFlagsOnNode = GTF_EMPTY)
    {
        if ((node.Next == endExclusive) || ((ignoreNode is not null) && (node.Next == ignoreNode) && (ignoreNode.Next == endExclusive)))
        {
            return true;
        }
        if (node.Oper.ConsumesFlags)
        {
            return false;
        }
        Clear();
        AddNode(compiler, node);
        _sideEffectFlags &= ~ignoreFlagsOnNode;
        for (var current = node.Next; current != endExclusive; current = current.Next)
        {
            assert(current is not null, "Expected first node to precede end node");
            if ((current != ignoreNode) && InterferesWith(compiler, current, true))
            {
                return false;
            }
        }
        return true;
    }

    public bool IsLirRangeInvariantInRange(Compiler compiler, GenTree rangeStart, GenTree rangeEnd,
        GenTree endExclusive, GenTree? ignoreNode)
    {
        if ((rangeEnd.Next == endExclusive) ||
            ((ignoreNode is not null) && (rangeEnd.Next == ignoreNode) && (ignoreNode.Next == endExclusive)))
        {
            return true;
        }
        if (rangeStart.Oper.ConsumesFlags)
        {
            return false;
        }
        Clear();
        var current = rangeStart;
        while (true)
        {
            AddNode(compiler, current);
            if (current == rangeEnd)
            {
                break;
            }
            current = current.Next;
            assert(current is not null, "Expected rangeStart to precede rangeEnd");
        }
        for (current = rangeEnd.Next; current != endExclusive; current = current.Next)
        {
            assert(current is not null, "Expected first node to precede end node");
            if ((current != ignoreNode) && InterferesWith(compiler, current, true))
            {
                return false;
            }
        }
        return true;
    }

    public readonly bool WritesLocal(int localNumber) => _aliasSet.WritesLocal(localNumber);

    public void Clear()
    {
        _sideEffectFlags = GTF_EMPTY;
        _preciseExceptions = ExceptionSetFlags.None;
        _aliasSet.Clear();
    }
}

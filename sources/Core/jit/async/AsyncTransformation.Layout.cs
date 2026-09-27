// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

internal readonly record struct AsyncReturnTypeInfo(var_types ReturnType, ClassLayout? ReturnLayout);

internal sealed class AsyncReturnInfo(AsyncReturnTypeInfo type)
{
    public AsyncReturnTypeInfo Type { get; } = type;
    public int Alignment { get; set; }
    public int Offset { get; set; }
    public int Size { get; set; }
    public int HeapAlignment => Math.Min(Alignment, TARGET_POINTER_SIZE);
}

internal sealed class AsyncLiveLocalInfo(int lclNum)
{
    public int LclNum { get; } = lclNum;
    public int Alignment { get; set; }
    public int Offset { get; set; }
    public int Size { get; set; }
    public int HeapAlignment => Math.Min(Alignment, TARGET_POINTER_SIZE);
}

internal sealed class AsyncContinuationLayout
{
    public int Size { get; set; }
    public int OSRAddressOffset { get; set; } = -1;
    public int ExceptionOffset { get; set; } = -1;
    public int ContinuationContextOffset { get; set; } = -1;
    public int KeepAliveOffset { get; set; } = -1;
    public int ExecutionContextOffset { get; set; } = -1;
    public List<AsyncLiveLocalInfo> Locals { get; } = [];
    public List<AsyncReturnInfo> Returns { get; } = [];
    public List<int> ContinuationMemberOffsets { get; } = [];
    public unsafe CORINFO_CLASS_HANDLE ClassHnd { get; set; }

#if DEBUG
    public void Dump(Compiler compiler, int indent)
    {
        if (!compiler.verbose)
        {
            return;
        }

        var prefix = new string(' ', indent);
        JITDUMP($"{prefix}Continuation layout ({Size} bytes):\n");
        if (OSRAddressOffset >= 0)
        {
            JITDUMP($"{prefix}  +{OSRAddressOffset:D3} OSR address\n");
        }

        if (ExecutionContextOffset >= 0)
        {
            JITDUMP($"{prefix}  +{ExecutionContextOffset:D3} Execution context\n");
        }

        if (ContinuationContextOffset >= 0)
        {
            JITDUMP($"{prefix}  +{ContinuationContextOffset:D3} Continuation context\n");
        }

        if (ExceptionOffset >= 0)
        {
            JITDUMP($"{prefix}  +{ExceptionOffset:D3} Exception\n");
        }

        if (KeepAliveOffset >= 0)
        {
            JITDUMP($"{prefix}  +{KeepAliveOffset:D3} Keep alive object\n");
        }

        for (var i = 0; i < ContinuationMemberOffsets.Count; i++)
        {
            if (ContinuationMemberOffsets[i] >= 0)
            {
                JITDUMP($"{prefix}  +{ContinuationMemberOffsets[i]:D3} ");
                compiler.GetContinuationMember(i).Print();
                JITDUMP("\n");
            }
        }

        foreach (var info in Locals)
        {
            JITDUMP($"{prefix}  +{info.Offset:D3} V{info.LclNum:D2}: {info.Size} bytes\n");
        }

        foreach (var ret in Returns)
        {
            var type = ret.Type.ReturnType is TYP_STRUCT
                ? (ret.Type.ReturnLayout ??
                    throw new InvalidOperationException("Struct return has no layout")).ClassName
                : ret.Type.ReturnType.Name;
            JITDUMP($"{prefix}  +{ret.Offset:D3} {ret.Size} bytes for {type} return\n");
        }
    }
#endif

    public unsafe AsyncReturnInfo FindReturn(Compiler compiler, GenTreeCall call)
    {
        var layout = call._returnType is TYP_STRUCT ? compiler.typGetObjLayout(call.RetClsHnd) : null;

        foreach (var ret in Returns)
        {
            if ((ret.Type.ReturnType == call._returnType) &&
                ((call._returnType is not TYP_STRUCT) || ClassLayout.AreCompatible(ret.Type.ReturnLayout, layout)))
            {
                return ret;
            }
        }

        throw new InvalidOperationException("Could not find return for call");
    }
}

internal sealed class AsyncContinuationLayoutBuilder(Compiler compiler)
{
    private readonly Compiler _compiler = compiler;
    private readonly List<AsyncReturnTypeInfo> _returns = [];
    private readonly List<int> _locals = [];

    public bool NeedsOSRAddress { get; set; }
    public bool NeedsException { get; set; }
    public bool NeedsContinuationContext { get; set; }
    public bool NeedsKeepAlive { get; set; }
    public bool NeedsExecutionContext { get; set; }
    public IReadOnlyList<int> Locals => _locals;
    public IReadOnlyList<AsyncReturnTypeInfo> Returns => _returns;

    public void AddReturn(AsyncReturnTypeInfo info)
    {
        foreach (var ret in _returns)
        {
            if (ret.ReturnType != info.ReturnType)
            {
                continue;
            }

            if ((ret.ReturnType is TYP_STRUCT) && !ClassLayout.AreCompatible(ret.ReturnLayout, info.ReturnLayout))
            {
                continue;
            }

            return;
        }

        _returns.Add(info);
    }

    public void AddLocal(int lclNum)
    {
        assert((_locals.Count == 0) || (lclNum > _locals[^1]));
        _locals.Add(lclNum);
    }

    public bool ContainsLocal(int lclNum) => _locals.BinarySearch(lclNum) >= 0;

    public static bool Equals(AsyncContinuationLayoutBuilder a, AsyncContinuationLayoutBuilder b)
    {
        if ((a.NeedsOSRAddress != b.NeedsOSRAddress) ||
            (a.NeedsException != b.NeedsException) ||
            (a.NeedsContinuationContext != b.NeedsContinuationContext) ||
            (a.NeedsKeepAlive != b.NeedsKeepAlive) ||
            (a.NeedsExecutionContext != b.NeedsExecutionContext) ||
            (a._returns.Count != b._returns.Count) ||
            (a._locals.Count != b._locals.Count))
        {
            return false;
        }

        for (var i = 0; i < a._returns.Count; i++)
        {
            var lhs = a._returns[i];
            var rhs = b._returns[i];
            if ((lhs.ReturnType != rhs.ReturnType) ||
                ((lhs.ReturnType is TYP_STRUCT) && !ClassLayout.AreCompatible(lhs.ReturnLayout, rhs.ReturnLayout)))
            {
                return false;
            }
        }

        for (var i = 0; i < a._locals.Count; i++)
        {
            if (a._locals[i] != b._locals[i])
            {
                return false;
            }
        }

        return true;
    }

    public static AsyncContinuationLayoutBuilder CreateSharedLayout(
        Compiler compiler, IReadOnlyList<AsyncContinuationLayoutBuilder> states)
    {
        var shared = new AsyncContinuationLayoutBuilder(compiler);
        var locals = new SortedSet<int>();

        foreach (var layout in states)
        {
            shared.NeedsOSRAddress |= layout.NeedsOSRAddress;
            shared.NeedsException |= layout.NeedsException;
            shared.NeedsContinuationContext |= layout.NeedsContinuationContext;
            shared.NeedsKeepAlive |= layout.NeedsKeepAlive;
            shared.NeedsExecutionContext |= layout.NeedsExecutionContext;

            foreach (var local in layout._locals)
            {
                _ = locals.Add(local);
            }

            foreach (var ret in layout._returns)
            {
                shared.AddReturn(ret);
            }
        }

        foreach (var local in locals)
        {
            shared.AddLocal(local);
        }

        return shared;
    }

    public unsafe AsyncContinuationLayout Create(IReadOnlyList<GenTree> continuationMemberOffsets)
    {
        var layout = new AsyncContinuationLayout();
        var memberCount = _compiler.GetContinuationMemberCount();
        for (var i = 0; i < memberCount; i++)
        {
            layout.ContinuationMemberOffsets.Add(-1);
        }

        foreach (var lclNum in _locals)
        {
            ref var dsc = ref _compiler.lvaGetDesc(lclNum);
            var info = new AsyncLiveLocalInfo(lclNum);

            if ((dsc.Type is TYP_STRUCT) || dsc.IsImplicitByRef)
            {
                var localLayout = dsc.Layout ?? throw new InvalidOperationException("Struct local has no layout");
                assert(!localLayout.HasGCByRef());
                info.Alignment = localLayout.GetAlignmentRequirement(_compiler);
                info.Size = checked((int)localLayout.Size);
            }
            else if (dsc.Type is TYP_REF)
            {
                info.Alignment = TARGET_POINTER_SIZE;
                info.Size = TARGET_POINTER_SIZE;
            }
            else
            {
                assert(dsc.Type is not TYP_BYREF);
                info.Alignment = dsc.Type.Alignment;
                info.Size = dsc.Type.Size;
            }

            layout.Locals.Add(info);
        }

        layout.Locals.Sort((lhs, rhs) => {
            var lhsIsRef = _compiler.lvaGetDesc(lhs.LclNum).Type is TYP_REF;
            var rhsIsRef = _compiler.lvaGetDesc(rhs.LclNum).Type is TYP_REF;
            if (lhsIsRef != rhsIsRef)
            {
                return lhsIsRef ? -1 : 1;
            }

            var alignment = rhs.HeapAlignment.CompareTo(lhs.HeapAlignment);
            return alignment != 0 ? alignment : lhs.LclNum.CompareTo(rhs.LclNum);
        });

        foreach (var ret in _returns)
        {
            var info = new AsyncReturnInfo(ret);
            if (ret.ReturnType is TYP_STRUCT)
            {
                assert(ret.ReturnLayout is not null);
                info.Size = checked((int)ret.ReturnLayout.Size);
                info.Alignment = ret.ReturnLayout.GetAlignmentRequirement(_compiler);
            }
            else
            {
                info.Size = ret.ReturnType.Size;
                info.Alignment = info.Size;
            }

            layout.Returns.Add(info);
        }

        int Allocate(int alignment, int size)
        {
            layout.Size = roundUp(layout.Size, alignment);
            var offset = layout.Size;
            layout.Size = checked(layout.Size + size);
            return offset;
        }

        if (NeedsOSRAddress)
        {
            layout.OSRAddressOffset = Allocate(TARGET_POINTER_SIZE, TARGET_POINTER_SIZE);
        }

        if (NeedsExecutionContext)
        {
            layout.ExecutionContextOffset = Allocate(TARGET_POINTER_SIZE, TARGET_POINTER_SIZE);
        }

        if (NeedsContinuationContext)
        {
            layout.ContinuationContextOffset = Allocate(TARGET_POINTER_SIZE, TARGET_POINTER_SIZE);
        }

        if (NeedsException)
        {
            layout.ExceptionOffset = Allocate(TARGET_POINTER_SIZE, TARGET_POINTER_SIZE);
        }

        foreach (var ret in layout.Returns)
        {
            layout.Size = roundUp(layout.Size, TARGET_POINTER_SIZE);
            ret.Offset = Allocate(ret.HeapAlignment, ret.Size);
        }

        foreach (var offsetNode in continuationMemberOffsets)
        {
            var memberIndex = checked((int)offsetNode.AsVal().Val1);
            assert((uint)memberIndex < (uint)memberCount);
            if (layout.ContinuationMemberOffsets[memberIndex] >= 0)
            {
                continue;
            }

            var member = _compiler.GetContinuationMember(memberIndex);
            var storageType = member.GetStorageType(out var memberLayout);
            var alignment = memberLayout?.GetAlignmentRequirement(_compiler) ?? storageType.Alignment;
            var size = memberLayout is null ? storageType.Size : checked((int)memberLayout.Size);
            layout.ContinuationMemberOffsets[memberIndex] = Allocate(Math.Min(alignment, TARGET_POINTER_SIZE), size);
        }

        // A suspension tail captures all three members of each live inline frame together.
        for (var i = 0; i < memberCount; i++)
        {
            if (layout.ContinuationMemberOffsets[i] < 0)
            {
                continue;
            }

            var liveMember = _compiler.GetContinuationMember(i);
            if (!liveMember.IsInlineFrameMember)
            {
                continue;
            }

            for (var j = 0; j < memberCount; j++)
            {
                if (layout.ContinuationMemberOffsets[j] >= 0)
                {
                    continue;
                }

                var member = _compiler.GetContinuationMember(j);
                if (!member.IsInlineFrameMember || (member.InlineDepth != liveMember.InlineDepth))
                {
                    continue;
                }

                var storageType = member.GetStorageType(out var memberLayout);
                assert(memberLayout is null);
                layout.ContinuationMemberOffsets[j] =
                    Allocate(Math.Min((int)storageType.Alignment, TARGET_POINTER_SIZE), storageType.Size);
            }
        }

        if (NeedsKeepAlive)
        {
            layout.KeepAliveOffset = Allocate(TARGET_POINTER_SIZE, TARGET_POINTER_SIZE);
        }

        foreach (var info in layout.Locals)
        {
            info.Offset = Allocate(info.HeapAlignment, info.Size);
        }

        layout.Size = roundUp(layout.Size, TARGET_POINTER_SIZE);
#if DEBUG
        layout.Dump(_compiler, 2);
#endif
        var gcSlots = new bool[layout.Size / TARGET_POINTER_SIZE];
        void SetReference(int offset)
        {
            if (offset >= 0)
            {
                assert((offset % TARGET_POINTER_SIZE) == 0);
                assert(offset < layout.Size);
                var slot = offset / TARGET_POINTER_SIZE;
                assert(!gcSlots[slot]);
                gcSlots[slot] = true;
            }
        }

        void SetType(int offset, var_types type, ClassLayout? classLayout)
        {
            if (type is TYP_REF)
            {
                SetReference(offset);
            }
            else if (type is TYP_STRUCT)
            {
                assert(classLayout is not null);
                for (var slot = 0; slot < classLayout.SlotCount; slot++)
                {
                    if (classLayout.IsGCRef(slot))
                    {
                        SetReference(offset + slot * TARGET_POINTER_SIZE);
                    }
                }
            }
        }

        SetReference(layout.ExecutionContextOffset);
        SetReference(layout.ContinuationContextOffset);
        SetReference(layout.ExceptionOffset);
        SetReference(layout.KeepAliveOffset);

        foreach (var info in layout.Locals)
        {
            ref var dsc = ref _compiler.lvaGetDesc(info.LclNum);
            var isStruct = (dsc.Type is TYP_STRUCT) || dsc.IsImplicitByRef;
            SetType(info.Offset, isStruct ? TYP_STRUCT : dsc.Type, isStruct ? dsc.Layout : null);
        }

        foreach (var ret in layout.Returns)
        {
            SetType(ret.Offset, ret.Type.ReturnType, ret.Type.ReturnLayout);
        }

        for (var i = 0; i < memberCount; i++)
        {
            if (layout.ContinuationMemberOffsets[i] >= 0)
            {
                var member = _compiler.GetContinuationMember(i);
                var storageType = member.GetStorageType(out var memberLayout);
                SetType(layout.ContinuationMemberOffsets[i], storageType, memberLayout);
            }
        }

#if DEBUG
        if (_compiler.verbose)
        {
            var referenceCount = 0;
            foreach (var slot in gcSlots)
            {
                if (slot)
                {
                    referenceCount++;
                }
            }

            JITDUMP($"  Getting continuation layout size = {layout.Size}, numGCRefs = {referenceCount}\n");
            for (var i = 0; i < gcSlots.Length;)
            {
                if (!gcSlots[i])
                {
                    i++;
                    continue;
                }

                var start = i;
                do
                {
                    i++;
                }
                while ((i < gcSlots.Length) && gcSlots[i]);

                JITDUMP($"    [{start * TARGET_POINTER_SIZE,3}..{i * TARGET_POINTER_SIZE,3}) obj refs\n");
            }
        }
#endif
        fixed (bool* refs = gcSlots)
        {
            layout.ClassHnd = _compiler.info.compCompHnd->getContinuationType(layout.Size, refs, gcSlots.Length);
        }

#if DEBUG
        if (_compiler.verbose)
        {
            JITDUMP($"  Result = {_compiler.eeGetClassName(layout.ClassHnd)}\n");
        }
#endif
        return layout;
    }
}

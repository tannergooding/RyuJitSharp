// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    private Dictionary<(VNFunc Func, ValueNum Arg0, ValueNum Arg1, ValueNum Arg2), ValueNum>? _vnFunc3Map;
    private Dictionary<(VNFunc Func, ValueNum Arg0, ValueNum Arg1, ValueNum Arg2, ValueNum Arg3), ValueNum>? _vnFunc4Map;

    public ValueNum VNForFunc(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN, ValueNum arg2VN)
    {
        assert((arg0VN != NoVN) && (arg1VN != NoVN) && (arg2VN != NoVN));
        assert(VNFuncArity(func) is 0 or 3);
        assert((arg0VN == VNNormalValue(arg0VN)) &&
            (arg1VN == VNNormalValue(arg1VN)) &&
            (arg2VN == VNNormalValue(arg2VN)));

        _vnFunc3Map ??= [];
        ref var result = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _vnFunc3Map, (func, arg0VN, arg1VN, arg2VN), out var exists);
        if (!exists)
        {
            result = NoVN;
        }

        if (result == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func3);
            var offset = chunk.AllocVN();
            var record = chunk.FuncApp(offset, 3).Span;
            record[0] = (int)func;
            record[1] = arg0VN;
            record[2] = arg1VN;
            record[3] = arg2VN;
            result = unchecked(chunk.BaseVN + offset);
        }

        return result;
    }

    public ValueNum VNForFunc(var_types type, VNFunc func, ValueNum arg0VN, ValueNum arg1VN,
        ValueNum arg2VN, ValueNum arg3VN)
    {
        assert((arg0VN != NoVN) && (arg1VN != NoVN) && (arg2VN != NoVN) &&
            ((arg3VN != NoVN) || (func == VNF_MapStore)));
        assert((arg0VN == VNNormalValue(arg0VN)) &&
            (arg1VN == VNNormalValue(arg1VN)) &&
            (arg2VN == VNNormalValue(arg2VN)));
        assert((func == VNF_MapStore) || (arg3VN == VNNormalValue(arg3VN)));
        assert(VNFuncArity(func) is 0 or 4);

        _vnFunc4Map ??= [];
        ref var result = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _vnFunc4Map, (func, arg0VN, arg1VN, arg2VN, arg3VN), out var exists);
        if (!exists)
        {
            result = NoVN;
        }

        if (result == NoVN)
        {
            var chunk = GetAllocChunk(type, ChunkExtraAttribs.CEA_Func4);
            var offset = chunk.AllocVN();
            var record = chunk.FuncApp(offset, 4).Span;
            record[0] = (int)func;
            record[1] = arg0VN;
            record[2] = arg1VN;
            record[3] = arg2VN;
            record[4] = arg3VN;
            result = unchecked(chunk.BaseVN + offset);
        }

        return result;
    }
}

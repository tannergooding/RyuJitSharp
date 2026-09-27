// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, fgprofile.cpp.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Compiler;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed unsafe class HandleHistogramProbeInstrumentor(Compiler compiler) : Instrumentor(compiler)
{
    public override bool ShouldProcess(BasicBlock block)
        => block.HasFlag(BBF_IMPORTED) && !block.HasFlag(BBF_INTERNAL);

    public override void Prepare(bool preImport)
    {
#if DEBUG
        if (!preImport)
        {
            foreach (var block in Compiler.Blocks)
            {
                block.bbHandleHistogramSchemaIndex = -1;
            }
        }
#endif
    }

    public override void BuildSchemaElements(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema)
    {
        if (!block.HasFlag(BBF_HAS_HISTOGRAM_PROFILE))
        {
            return;
        }

        block.bbHandleHistogramSchemaIndex = schema.Count;
        var visitor = new HistogramProbeVisitor(Compiler, schema, value: false);
        foreach (var stmt in block.Statements)
        {
            _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        }

        SchemaCountValue += visitor.SchemaCount;
    }

    public override void Instrument(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema, byte* profileMemory)
    {
        if (!block.HasFlag(BBF_HAS_HISTOGRAM_PROFILE))
        {
            return;
        }

        JITDUMP($"Scanning for calls to profile in {FMT_BB(block.bbNum)}\n");
        var index = block.bbHandleHistogramSchemaIndex;
        assert(index >= 0 && index < schema.Count);
        var visitor = new HistogramProbeVisitor(Compiler, schema, value: false, profileMemory, index);
        foreach (var stmt in block.Statements)
        {
            _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        }

        InstrCountValue += visitor.InstrCount;
    }
}

public sealed unsafe class ValueInstrumentor(Compiler compiler) : Instrumentor(compiler)
{
    public override bool ShouldProcess(BasicBlock block)
        => block.HasFlag(BBF_IMPORTED) && !block.HasFlag(BBF_INTERNAL);

    public override void Prepare(bool preImport)
    {
#if DEBUG
        if (!preImport)
        {
            foreach (var block in Compiler.Blocks)
            {
                block.bbValueHistogramSchemaIndex = -1;
            }
        }
#endif
    }

    public override void BuildSchemaElements(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema)
    {
        if (!block.HasFlag(BBF_HAS_VALUE_PROFILE))
        {
            return;
        }

        block.bbValueHistogramSchemaIndex = schema.Count;
        var visitor = new HistogramProbeVisitor(Compiler, schema, value: true);
        foreach (var stmt in block.Statements)
        {
            _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        }

        SchemaCountValue += visitor.SchemaCount;
    }

    public override void Instrument(BasicBlock block, List<ICorJitInfo.PgoInstrumentationSchema> schema, byte* profileMemory)
    {
        if (!block.HasFlag(BBF_HAS_VALUE_PROFILE))
        {
            return;
        }

        var index = block.bbValueHistogramSchemaIndex;
        assert(index >= 0 && index < schema.Count);
        var visitor = new HistogramProbeVisitor(Compiler, schema, value: true, profileMemory, index);
        foreach (var stmt in block.Statements)
        {
            _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
        }

        InstrCountValue += visitor.InstrCount;
    }
}

internal unsafe struct HistogramProbeVisitor : IGenTreeVisitor<HistogramProbeVisitor>
{
    private readonly Compiler _compiler;
    private readonly List<ICorJitInfo.PgoInstrumentationSchema> _schema;
    private readonly bool _value;
    private readonly byte* _profileMemory;
    private readonly GenTreeStack _ancestors = [];
    private int _schemaIndex;

    public static bool DoPreOrder => true;
    public static bool ComputeStack => true;
    public int SchemaCount { get; private set; }
    public int InstrCount { get; private set; }

    public HistogramProbeVisitor(Compiler compiler, List<ICorJitInfo.PgoInstrumentationSchema> schema,
        bool value, byte* profileMemory = null, int schemaIndex = 0)
    {
        _compiler = compiler;
        _schema = schema;
        _value = value;
        _profileMemory = profileMemory;
        _schemaIndex = schemaIndex;
    }

    public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
    {
        if (use is not GenTreeCall call)
        {
            return WALK_CONTINUE;
        }

        if (_value)
        {
            if (!call.IsSpecialIntrinsic())
            {
                return WALK_CONTINUE;
            }

            var intrinsic = _compiler.lookupNamedIntrinsic(call._callMethHnd);
            if (intrinsic is not (NI_System_SpanHelpers_Memmove or NI_System_SpanHelpers_SequenceEqual))
            {
                return WALK_CONTINUE;
            }
        }
        else if (_compiler.compClassifyGDVProbeType(call) is GDVProbeType.None)
        {
            return WALK_CONTINUE;
        }

        assert(call._handleHistogramProfileCandidateInfo is not null);
        if (_profileMemory is null)
        {
            if (_value)
            {
                BuildValueSchema(call);
            }
            else
            {
                BuildHandleSchema(call);
            }
        }
        else if (_value)
        {
            InsertValueProbe(call);
        }
        else
        {
            InsertHandleProbe(call);
        }

        return WALK_CONTINUE;
    }

    public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

    public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
        => IGenTreeVisitor<HistogramProbeVisitor>.WalkTree(ref this, ref use, user, _ancestors);

    private void AddPair(GenTreeCall call, ICorJitInfo.PgoInstrumentationKind countKind,
        ICorJitInfo.PgoInstrumentationKind histogramKind, int other)
    {
        var entry = new ICorJitInfo.PgoInstrumentationSchema {
            Count = 1,
            Other = other,
            InstrumentationKind = countKind,
            ILOffset = call._handleHistogramProfileCandidateInfo!.ilOffset,
            Offset = 0,
        };
        _schema.Add(entry);
        SchemaCount++;
        entry.InstrumentationKind = histogramKind;
        entry.Count = ICorJitInfo.HandleHistogram32.SIZE;
        _schema.Add(entry);
        SchemaCount++;
    }

    private void BuildHandleSchema(GenTreeCall call)
    {
        var kind = _compiler.compClassifyGDVProbeType(call);
        if (kind is GDVProbeType.ClassProfile or GDVProbeType.MethodAndClassProfile)
        {
            var other = ICorJitInfo.HandleHistogram32.CLASS_FLAG;
            if (call.IsVirtualStub)
            {
                other |= ICorJitInfo.HandleHistogram32.INTERFACE_FLAG;
            }
            else if (call.IsDelegateInvoke)
            {
                other |= ICorJitInfo.HandleHistogram32.DELEGATE_FLAG;
            }

            AddPair(call, _compiler.opts.compCollect64BitCounts
                ? ICorJitInfo.PgoInstrumentationKind.HandleHistogramLongCount
                : ICorJitInfo.PgoInstrumentationKind.HandleHistogramIntCount,
                ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes, other);
        }

        if (kind is GDVProbeType.MethodProfile or GDVProbeType.MethodAndClassProfile)
        {
            var other = call.IsVirtualStub ? ICorJitInfo.HandleHistogram32.INTERFACE_FLAG
                : call.IsDelegateInvoke ? ICorJitInfo.HandleHistogram32.DELEGATE_FLAG : 0;
            AddPair(call, _compiler.opts.compCollect64BitCounts
                ? ICorJitInfo.PgoInstrumentationKind.HandleHistogramLongCount
                : ICorJitInfo.PgoInstrumentationKind.HandleHistogramIntCount,
                ICorJitInfo.PgoInstrumentationKind.HandleHistogramMethods, other);
        }
    }

    private void BuildValueSchema(GenTreeCall call)
    {
        AddPair(call, _compiler.opts.compCollect64BitCounts
            ? ICorJitInfo.PgoInstrumentationKind.ValueHistogramLongCount
            : ICorJitInfo.PgoInstrumentationKind.ValueHistogramIntCount,
            ICorJitInfo.PgoInstrumentationKind.ValueHistogram, 0);
    }

    private byte* ReadHandleHistogram(IL_OFFSET ilOffset, bool types, out bool is32)
    {
        is32 = false;
        if (_schemaIndex >= _schema.Count)
        {
            return null;
        }

        var count = _schema[_schemaIndex];
        is32 = count.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.HandleHistogramIntCount;
        var is64 = count.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.HandleHistogramLongCount;
        if ((!is32 && !is64) || count.ILOffset != ilOffset)
        {
            return null;
        }

        assert(_schemaIndex + 2 <= _schema.Count);
        var table = _schema[_schemaIndex + 1];
        assert(table.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes
            or ICorJitInfo.PgoInstrumentationKind.HandleHistogramMethods);
        if ((table.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes) != types)
        {
            return null;
        }

        _schemaIndex += 2;
        var histogram = _profileMemory + count.Offset;
#if DEBUG
        var tableOffset = is32
            ? (int)System.Runtime.InteropServices.Marshal.OffsetOf<ICorJitInfo.HandleHistogram32>(
                nameof(ICorJitInfo.HandleHistogram32.HandleTable))
            : (int)System.Runtime.InteropServices.Marshal.OffsetOf<ICorJitInfo.HandleHistogram64>(
                nameof(ICorJitInfo.HandleHistogram64.HandleTable));
        assert(count.Offset + tableOffset == table.Offset);
#endif
        return histogram;
    }

    private void InsertHandleProbe(GenTreeCall call)
    {
        var candidate = call._handleHistogramProfileCandidateInfo!;
#if DEBUG
        JITDUMP($"Found call [{call.TreeId:D6}] with probe index {candidate.probeIndex} " +
            $"and ilOffset 0x{candidate.ilOffset:X}\n");
#endif

        var typeHistogram = ReadHandleHistogram(candidate.ilOffset, types: true, out var type32);
        var methodHistogram = ReadHandleHistogram(candidate.ilOffset, types: false, out var method32);
        assert(typeHistogram is not null || methodHistogram is not null);
        if (typeHistogram is not null && methodHistogram is not null)
        {
            assert(type32 == method32);
        }

        var is32 = typeHistogram is not null ? type32 : method32;
        assert(!call.Args.AreArgsComplete);
        var objArg = _compiler.impIsCastHelperEligibleForClassProbe(call)
            ? call.Args.GetArgByIndex(1) : call.Args.ThisArg;
        assert(objArg is not null && objArg.EarlyNode is not null && objArg.EarlyNode.Type is TYP_REF);
        var temp = _compiler.lvaGrabTemp(true, "handle histogram profile tmp");
        _compiler.lvaTable[temp].Type = TYP_REF;
        GenTree? helper = null;

        if (typeHistogram is not null)
        {
            helper = _compiler.gtNewHelperCallNode(TYP_VOID,
                is32 ? CORINFO_HELP_CLASSPROFILE32 : CORINFO_HELP_CLASSPROFILE64,
                _compiler.gtNewLclvNode(TYP_REF, temp),
                _compiler.gtNewIconNode(TYP_I_IMPL, (nint)typeHistogram));
        }

        if (methodHistogram is not null)
        {
            var address = _compiler.gtNewIconNode(TYP_I_IMPL, (nint)methodHistogram);
            GenTree methodCall;
            if (call.IsDelegateInvoke)
            {
                methodCall = _compiler.gtNewHelperCallNode(TYP_VOID,
                    is32 ? CORINFO_HELP_DELEGATEPROFILE32 : CORINFO_HELP_DELEGATEPROFILE64,
                    _compiler.gtNewLclvNode(TYP_REF, temp), address);
            }
            else
            {
                assert(call.IsVirtualVtable);
                methodCall = _compiler.gtNewHelperCallNode(TYP_VOID,
                    is32 ? CORINFO_HELP_VTABLEPROFILE32 : CORINFO_HELP_VTABLEPROFILE64,
                    _compiler.gtNewLclvNode(TYP_REF, temp),
                    _compiler.gtNewIconEmbMethHndNode(call._callMethHnd), address);
            }

            helper = helper is null ? methodCall : _compiler.gtNewCommaNode(TYP_REF, helper, methodCall);
        }

        assert(helper is not null);
        var comma = _compiler.gtNewCommaNode(TYP_REF, helper,
            _compiler.gtNewLclvNode(TYP_REF, temp));
        objArg.EarlyNode = _compiler.gtNewCommaNode(TYP_REF,
            _compiler.gtNewStoreLclVarNode(temp, objArg.Node), comma);
        UpdateSideEffects(call);
        JITDUMP("Modified call is now\n");
        DISPTREE(call);
        InstrCount++;
    }

    private void InsertValueProbe(GenTreeCall call)
    {
        assert(call.IsSpecialIntrinsic(_compiler, NI_System_SpanHelpers_Memmove) ||
            call.IsSpecialIntrinsic(_compiler, NI_System_SpanHelpers_SequenceEqual));
        if (_schemaIndex >= _schema.Count)
        {
            return;
        }

        var count = _schema[_schemaIndex];
        if (count.ILOffset != call._handleHistogramProfileCandidateInfo!.ilOffset)
        {
            return;
        }

        var is32 = count.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogramIntCount;
        var is64 = count.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogramLongCount;
        if (!is32 && !is64)
        {
            return;
        }

        assert(_schemaIndex + 2 <= _schema.Count);
        assert(_schema[_schemaIndex + 1].InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogram);
        var histogram = _profileMemory + count.Offset;
        _schemaIndex += 2;

        var lengthArg = call.Args.GetUserArgByIndex(2);
        assert(lengthArg is not null && lengthArg.EarlyNode is not null);
        var length = lengthArg.EarlyNode;
        var temp = _compiler.lvaGrabTemp(true, "length histogram profile tmp");
        var actualType = length.Type.ActualType;
        var store = _compiler.gtNewTempStore(temp, length);
        var local = _compiler.gtNewLclvNode(actualType, temp);
        var lengthNode = _compiler.gtNewCommaNode(actualType, store, local);
        var address = _compiler.gtNewIconNode(TYP_I_IMPL, (nint)histogram);
        var helper = _compiler.gtNewHelperCallNode(TYP_VOID,
            is32 ? CORINFO_HELP_VALUEPROFILE32 : CORINFO_HELP_VALUEPROFILE64, lengthNode, address);
        lengthArg.EarlyNode = _compiler.gtNewCommaNode(actualType,
            helper, _compiler.gtCloneExpr(local)!);
        UpdateSideEffects(call);
        InstrCount++;
    }

    private readonly void UpdateSideEffects(GenTree node)
    {
        _compiler.gtUpdateNodeSideEffects(node);
        var effects = node.Flags & GTF_ALL_EFFECT;
        var first = true;
        foreach (var ancestor in _ancestors)
        {
            if (first)
            {
                first = false;
                continue;
            }

            ancestor.Flags |= effects;
        }
    }
}

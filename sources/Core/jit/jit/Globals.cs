// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Globals
{
#if HOST_X86
#if HOST_ARM
#error Cannot define both HOST_X86 and HOST_ARM
#endif
#if HOST_AMD64
#error Cannot define both HOST_X86 and HOST_AMD64
#endif
#if HOST_ARM64
#error Cannot define both HOST_X86 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_X86 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_X86 and HOST_RISCV64
#endif
#elif HOST_AMD64
#if HOST_X86
#error Cannot define both HOST_AMD64 and HOST_X86
#endif
#if HOST_ARM
#error Cannot define both HOST_AMD64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_AMD64 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_AMD64 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_AMD64 and HOST_RISCV64
#endif
#elif HOST_ARM
#if HOST_X86
#error Cannot define both HOST_ARM and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_ARM and HOST_AMD64
#endif
#if HOST_ARM64
#error Cannot define both HOST_ARM and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_ARM and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_ARM and HOST_RISCV64
#endif
#elif HOST_ARM64
#if HOST_X86
#error Cannot define both HOST_ARM64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_ARM64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_ARM64 and HOST_ARM
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_ARM64 and HOST_LOONGARCH64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_ARM64 and HOST_RISCV64
#endif
#elif HOST_LOONGARCH64
#if HOST_X86
#error Cannot define both HOST_LOONGARCH64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_LOONGARCH64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_LOONGARCH64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_LOONGARCH64 and HOST_ARM64
#endif
#if HOST_RISCV64
#error Cannot define both HOST_LOONGARCH64 and HOST_RISCV64
#endif
#elif HOST_RISCV64
#if HOST_X86
#error Cannot define both HOST_RISCV64 and HOST_X86
#endif
#if HOST_AMD64
#error Cannot define both HOST_RISCV64 and HOST_AMD64
#endif
#if HOST_ARM
#error Cannot define both HOST_RISCV64 and HOST_ARM
#endif
#if HOST_ARM64
#error Cannot define both HOST_RISCV64 and HOST_ARM64
#endif
#if HOST_LOONGARCH64
#error Cannot define both HOST_RISCV64 and HOST_LOONGARCH64
#endif
#else
#error Unsupported or unset host architecture
#endif

#if TARGET_X86
#if TARGET_ARM
#error Cannot define both TARGET_X86 and TARGET_ARM
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_X86 and TARGET_AMD64
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_X86 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_X86 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_X86 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_X86 and TARGET_WASM32
#endif
#elif TARGET_AMD64
#if TARGET_X86
#error Cannot define both TARGET_AMD64 and TARGET_X86
#endif
#if TARGET_ARM
#error Cannot define both TARGET_AMD64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_AMD64 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_AMD64 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_AMD64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_AMD64 and TARGET_WASM32
#endif
#elif TARGET_ARM
#if TARGET_X86
#error Cannot define both TARGET_ARM and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_ARM and TARGET_AMD64
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_ARM and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_ARM and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_ARM and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_ARM and TARGET_WASM32
#endif
#elif TARGET_ARM64
#if TARGET_X86
#error Cannot define both TARGET_ARM64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_ARM64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_ARM64 and TARGET_ARM
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_ARM64 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_ARM64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_ARM64 and TARGET_WASM32
#endif
#elif TARGET_LOONGARCH64
#if TARGET_X86
#error Cannot define both TARGET_LOONGARCH64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_LOONGARCH64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_ARM64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_LOONGARCH64 and TARGET_RISCV64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_LOONGARCH64 and TARGET_WASM32
#endif
#elif TARGET_RISCV64
#if TARGET_X86
#error Cannot define both TARGET_RISCV64 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_RISCV64 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_RISCV64 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_RISCV64 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_RISCV64 and TARGET_LOONGARCH64
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_RISCV64 and TARGET_WASM32
#endif

#elif TARGET_WASM32
#if TARGET_X86
#error Cannot define both TARGET_WASM32 and TARGET_X86
#endif
#if TARGET_AMD64
#error Cannot define both TARGET_WASM32 and TARGET_AMD64
#endif
#if TARGET_ARM
#error Cannot define both TARGET_WASM32 and TARGET_ARM
#endif
#if TARGET_ARM64
#error Cannot define both TARGET_WASM32 and TARGET_ARM64
#endif
#if TARGET_LOONGARCH64
#error Cannot define both TARGET_WASM32 and TARGET_LOONGARCH64
#endif
#if TARGET_RISCV64
#error Cannot define both TARGET_WASM32 and TARGET_RISCV64
#endif
#else
#error Unsupported or unset target architecture
#endif

#if TARGET_64BIT
#if TARGET_X86
#error Cannot define both TARGET_X86 and TARGET_64BIT
#endif
#if TARGET_ARM
#error Cannot define both TARGET_ARM and TARGET_64BIT
#endif
#if TARGET_WASM32
#error Cannot define both TARGET_WASM32 and TARGET_64BIT
#endif
#endif

#if UNIX_AMD64_ABI && !TARGET_AMD64
#error When UNIX_AMD64_ABI is defined, you must define TARGET_AMD64 as well.
#endif

#if UNIX_X86_ABI && !TARGET_X86
#error When UNIX_X86_ABI is defined, you must define TARGET_X86 as well.
#endif

#if USE_COREDISTOOLS && !LATE_DISASM
#error When USE_COREDISTOOLS is defined, you must define LATE_DISASM as well.
#endif

    public const int REGEN_SHORTCUTS = 0;

    public const int REGEN_CALLPAT = 0;

    /// <summary>Did Jit or Inline succeeded?</summary>
    public const int INFO6 = LL_INFO10000;

    /// <summary>NYI stuff.</summary>
    public const int INFO7 = LL_INFO100000;

    /// <summary>Weird failures.</summary>
    public const int INFO8 = LL_INFO1000000;

    public static unsafe CORINFO_OBJECT_HANDLE NO_OBJECT_HANDLE => null;

    public static unsafe CORINFO_CLASS_HANDLE NO_CLASS_HANDLE => null;

    public static unsafe CORINFO_FIELD_HANDLE NO_FIELD_HANDLE => null;

    public static unsafe CORINFO_METHOD_HANDLE NO_METHOD_HANDLE => null;

    public const IL_OFFSET BAD_IL_OFFSET = -1;

    public const int BAD_VAR_NUM = -1;

    public const ushort BAD_LCL_OFFSET = ushort.MaxValue;

    // For the following specially handled FIELD_HANDLES we need
    //   values that are negative and have the low two bits zero
    // See eeFindJitDataOffs and eeGetJitDataOffs in Compiler.hpp
    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_DS => unchecked((CORINFO_FIELD_HANDLE)(-4));

    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_FS => unchecked((CORINFO_FIELD_HANDLE)(-8));

    public static unsafe CORINFO_FIELD_HANDLE FLD_GLOBAL_GS => unchecked((CORINFO_FIELD_HANDLE)(-12));

    // offset of vtable pointer from obj ptr
    public const int VPTR_OFFS = 0;

#if MEASURE_CLRAPI_CALLS
#if FEATURE_JIT_METHOD_PERF
#error Can't time these calls without METHOD_PERF.
#endif
#if DEBUG
#error No point in measuring DEBUG code.
#endif
#if !HOST_X86 && !HOST_AMD64
#error Cycle counters only hooked up on x86/x64.
#endif
#endif

#if FEATURE_TAILCALL_OPT_SHARED_RETURN && !FEATURE_TAILCALL_OPT
#error When FEATURE_TAILCALL_OPT_SHARED_RETURN is defined, you must define FEATURE_TAILCALL_OPT as well.
#endif

    public const int CLFLG_REGVAR = 0x00008;

    public const int CLFLG_TREETRANS = 0x00100;

    public const int CLFLG_INLINING = 0x00200;

#if FEATURE_STRUCTPROMOTE
    public const int CLFLG_STRUCTPROMOTE = 0x00400;
#else
    public const int CLFLG_STRUCTPROMOTE = 0x00000;
#endif

    public const int CLFLG_MAXOPT = CLFLG_REGVAR | CLFLG_TREETRANS | CLFLG_INLINING | CLFLG_STRUCTPROMOTE;

    public const int CLFLG_MINOPT = CLFLG_TREETRANS;

#if DEBUG
    public static bool VERBOSE
    {
        get
        {
            var compiler = JitTls.Compiler;
            return (compiler is not null) && compiler.verbose;
        }
    }
#else
    public const bool VERBOSE = false;
#endif

    [Conditional("DEBUG")]
    public static void DISPNODE(GenTree tree)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.gtDispTree(tree, msg: "", topOnly: true);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void DISPTREE(GenTree tree)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.gtDispTree(tree);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void DISPSTMT(Statement stmt)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.gtDispStmt(stmt);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void DISPRANGE(LIR.ReadOnlyRange range)
    {

#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.gtDispRange(range);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void DISPTREERANGE(LIR.Range range, GenTree tree)
    {

#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.gtDispTreeRange(range, tree);
        }
#endif
    }

    public static string dspBool(bool b) => b ? "true" : "false";

    public static nint dspOffset(nint offs)
    {
        var compiler = JitTls.Compiler;
        assert(compiler is not null);
        return compiler.dspOffset(offs);
    }

    public static unsafe nint dspPtr(void* ptr)
    {
        var compiler = JitTls.Compiler;
        assert(compiler is not null);
        return compiler.dspPtr(ptr);
    }

#if DEBUG
    /// <summary>Helper for <see cref="dumpSingleInstr" /> to dump hex bytes of an IL stream, aligning up to a minimum alignment width.</summary>
    /// <param name="codeAddr">Pointer to IL byte stream to display.</param>
    /// <param name="codeSize">Number of bytes of IL byte stream to display.</param>
    /// <param name="alignSize">Pad out to this many characters, if fewer than this were written.</param>
    public static unsafe void dumpILBytes(byte* codeAddr, int codeSize, int alignSize)
    {
        for (var offs = 0; offs < codeSize; offs++)
        {
            jitprintf($" {codeAddr[offs]:X2}");
        }

        var charsWritten = 3 * codeSize;

        for (var i = charsWritten; i < alignSize; i++)
        {
            jitprintf(" ");
        }
    }

    /// <summary>Display a range of IL instructions from an IL instruction stream.</summary>
    /// <param name="codeAddr">Pointer to IL byte stream to display.</param>
    /// <param name="codeSize">Number of bytes of IL byte stream to display.</param>
    public static unsafe void dumpILRange(byte* codeAddr, int codeSize)
    {
        var offs = 0;

        while (offs < codeSize)
        {
            var codeBytesDumped = dumpSingleInstr(codeAddr, offs, $"IL_{offs:X4}");
            offs += codeBytesDumped;
        }
    }

    /// <summary>Display a single IL instruction.</summary>
    /// <param name="codeAddr">Base pointer to a stream of IL instructions.</param>
    /// <param name="offs">Offset from codeAddr of the IL instruction to display.</param>
    /// <param name="prefix">Optional string to prefix the IL instruction with</param>
    /// <returns>Size of the displayed IL instruction in the instruction stream, in bytes. (Add this to 'offs' to get to the next instruction.)</returns>
    public static unsafe int dumpSingleInstr(byte* codeAddr, IL_OFFSET offs, string prefix = "")
    {
        // assume 3 characters * (1 byte opcode + 4 bytes data + 1 prefix byte) for most things
        const int ALIGN_WIDTH = 3 * 6;

        var opcodePtr = codeAddr + offs;
        var startOpcodePtr = opcodePtr;

        if (prefix.Length != 0)
        {
            jitprintf(prefix);
        }

        OPCODE opcode = (OPCODE)(opcodePtr[0]);
        opcodePtr += sizeof(byte);

        if (opcode == CEE_PREFIX1)
        {
            opcode = (OPCODE)(opcodePtr[0] + 0x0100);
            opcodePtr += sizeof(byte);
        }

        if (opcode >= CEE_COUNT)
        {
            jitprintf($"\nIllegal opcode: {(ushort)(opcode):X2}\n");
            return (IL_OFFSET)(opcodePtr - startOpcodePtr);
        }

        // Get the size of additional parameters

        var sz = opcode.Size;
        var argKind = opcode.ArgKind;
        var name = opcode.Name;
        var totalSize = (IL_OFFSET)(opcodePtr - startOpcodePtr) + sz;
        var baseOffs = (IL_OFFSET)(opcodePtr - codeAddr) + sz;
        var operand = "";

        switch (argKind)
        {
            case InlineNone:
            {
                break;
            }

            case ShortInlineVar:
            {
                var iOp =opcodePtr[0];
                operand = $" 0x{iOp:X}";
                break;
            }

            case ShortInlineI:
            {
                var iOp = unchecked((sbyte)(opcodePtr[0]));
                operand = $" 0x{iOp:X}";
                break;
            }

            case InlineVar:
            {
                var iOp = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 2));
                operand = $" 0x{iOp:X}";
                break;
            }

            case InlineTok:
            case InlineMethod:
            case InlineField:
            case InlineType:
            case InlineString:
            case InlineSig:
            case InlineI:
            {
                var iOp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                operand = $" 0x{iOp:X}";
                break;
            }

            case InlineI8:
            {
                var iOp = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 8));
                operand = $" 0x{iOp:X}";
                break;
            }

            case ShortInlineR:
            {
                var dOp = BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                operand = $" {dOp}";
                break;
            }

            case InlineR:
            {
                var dOp = BinaryPrimitives.ReadDoubleLittleEndian(new ReadOnlySpan<byte>(opcodePtr, 8));
                operand = $" {dOp}";
                break;
            }

            case ShortInlineBrTarget:
            {
                var jOp = unchecked((sbyte)(opcodePtr[0]));
                operand = $" {jOp} (IL_{baseOffs + jOp:X4})";
                break;
            }

            case InlineBrTarget:
            {
                var jOp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                operand = $" {jOp} (IL_{baseOffs + jOp:X4})";
                break;
            }

            case InlineSwitch:
            {
                // Jump over the table
                var cOp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                opcodePtr += 4 + (cOp * 4);
                break;
            }

            case InlinePhi:
            {
                // Jump over the table
                var cOp = opcodePtr[0];
                opcodePtr += 1 + (cOp * 2);
                break;
            }

            default:
            {
                NO_WAY("Bad argKind");
                break;
            }
        }

        dumpILBytes(startOpcodePtr, totalSize, ALIGN_WIDTH);
        jitprintf($" {name,-12}{operand}");

        opcodePtr += sz;

        jitprintf("\n");
        return (IL_OFFSET)(opcodePtr - startOpcodePtr);
    }
#endif

    [Conditional("DEBUG")]
    public static void LABELEDDISPTREERANGE(string label, LIR.Range range, GenTree tree)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            logf($"{label}:\n");
            compiler.gtDispTreeRange(range, tree);
            logf("\n");
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void DISPBLOCK(BasicBlock block)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            compiler.fgTableDispBasicBlock(block);
        }
#endif
    }

    [Conditional("DEBUG")]
    public static void JITDUMP(string message)
    {
#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        if (compiler.verbose)
        {
            logf(message);
        }
#endif
    }

    /// <summary>find class profile data for an IL offset, and return the most likely classes</summary>
    /// <param name="likelyClasses">array of likely classes sorted by likelihood (descending). It must be at least of 'maxLikelyClasses' (next argument) length. The array consists of pairs "clsHandle - likelihood" ordered by likelihood (descending) where likelihood can be any value in [0..100] range. clsHandle is never null for [0..&lt;return value of this function&gt;) range, Items in [&lt;return value of this function&gt;..maxLikelyClasses) are zeroed if the number of classes seen is less than maxLikelyClasses provided.</param>
    /// <param name="schema">profile schema</param>
    /// <param name="pInstrumentationData">associated data</param>
    /// <param name="ilOffset">il offset of the callvirt</param>
    /// <returns>Estimated number of classes seen at runtime</returns>
    /// <remarks>
    ///   <para>A "monomorphic" call site will return likelihood 100 and number of entries = 1.</para>
    ///   <para>This is used by the devirtualization logic below, and by crossgen2 when producing the R2R image (to reduce the sizecost of carrying the type histogram)</para>
    ///   <para>This code can runs without a jit instance present, so JITDUMP and related cannot be used.</para>
    /// </remarks>
    public static unsafe int getLikelyClasses(Span<LikelyClassMethodRecord> likelyClasses, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schema, byte* pInstrumentationData, IL_OFFSET ilOffset)
    {
        return getLikelyClassesOrMethods(likelyClasses, schema, pInstrumentationData, ilOffset, types: true);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(getLikelyMethods))]
    public static unsafe int getLikelyClasses(LikelyClassMethodRecord* pLikelyClasses, int maxLikelyClasses, ICorJitInfo.PgoInstrumentationSchema* schema, int countSchemaItems, byte* pInstrumentationData, int ilOffset)
    {
        return getLikelyClasses(new Span<LikelyClassMethodRecord>(pLikelyClasses, maxLikelyClasses), new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(schema, countSchemaItems), pInstrumentationData, ilOffset);
    }

    /// <summary>Find class/method profile data for an IL offset, and return the most likely classes/methods.</summary>
    /// <param name="likelyEntries"></param>
    /// <param name="schemas"></param>
    /// <param name="pInstrumentationData"></param>
    /// <param name="ilOffset"></param>
    /// <param name="types"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>This is a common entrypoint for getLikelyClasses and getLikelyMethods.</para>
    ///   <para>See documentation for those for more information.</para>
    /// </remarks>
    public static unsafe int getLikelyClassesOrMethods(Span<LikelyClassMethodRecord> likelyEntries, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schemas, byte* pInstrumentationData, IL_OFFSET ilOffset, bool types)
    {
        if (likelyEntries.Length is 0)
        {
            return 0;
        }

        var histogramKind = types ? ICorJitInfo.PgoInstrumentationKind.HandleHistogramTypes : ICorJitInfo.PgoInstrumentationKind.HandleHistogramMethods;
        var compressedKind = types ? ICorJitInfo.PgoInstrumentationKind.GetLikelyClass : ICorJitInfo.PgoInstrumentationKind.GetLikelyMethod;

        likelyEntries.Clear();

        if (schemas.Length is 0)
        {
            return 0;
        }

        for (var i = 0; i < schemas.Length; i++)
        {
            ref readonly var schema = ref schemas[i];

            if (schema.ILOffset != ilOffset)
            {
                continue;
            }

            if ((schema.InstrumentationKind == compressedKind) && (schema.Count is 1))
            {
                var result = ((nint*)(pInstrumentationData + schema.Offset))[0];

                if (ICorJitInfo.IsUnknownHandle(result))
                {
                    return 0;
                }

                // we don't expect zero in GetLikelyClass/GetLikelyMethod
                assert(result is not 0);

                ref var likelyEntry = ref likelyEntries[0];

                likelyEntry.likelihood = (schema.Other & 0xFF);
                likelyEntry.handle = result;

                return 1;
            }

            var isHistogramCount = (schema.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.HandleHistogramIntCount or ICorJitInfo.PgoInstrumentationKind.HandleHistogramLongCount);

            if (isHistogramCount && (schema.Count is 1) && ((i + 1) < schemas.Length))
            {
                ref readonly var nextSchema = ref schemas[i + 1];

                if (nextSchema.InstrumentationKind == histogramKind)
                {
                    // Form a histogram
                    var histogramEntries = new Span<nint>(((nint*)(pInstrumentationData + nextSchema.Offset)), nextSchema.Count);
                    var h  = new LikelyClassMethodHistogram(histogramEntries);

                    // Use histogram count as number of classes estimate
                    // Report back what we've learned
                    // (perhaps, use count to augment likelihood?)

                    switch (h.countHistogramElements)
                    {
                        case 0:
                        {
                            return 0;
                        }

                        case 1:
                        {
                            var hist0 = h.HistogramEntryAt(0);

                            if (ICorJitInfo.IsUnknownHandle(hist0._handle))
                            {
                                // Fast path for monomorphic cases
                                return 0;
                            }

                            ref var likelyEntry = ref likelyEntries[0];

                            likelyEntry.likelihood = 100;
                            likelyEntry.handle = hist0._handle;

                            return 1;
                        }

                        case 2:
                        {
                            // Fast path for two classes
                            var hist0 = h.HistogramEntryAt(0);
                            var hist1 = h.HistogramEntryAt(1);

                            if ((hist0._count >= hist1._count) && !ICorJitInfo.IsUnknownHandle(hist0._handle))
                            {
                                ref var likelyEntry = ref likelyEntries[0];

                                likelyEntry.likelihood = (100 * hist0._count) / h._totalCount;
                                likelyEntry.handle = hist0._handle;

                                if ((likelyEntries.Length > 1) && !ICorJitInfo.IsUnknownHandle(hist1._handle))
                                {
                                    likelyEntry = ref likelyEntries[1];

                                    likelyEntry.likelihood = (100 * hist1._count) / h._totalCount;
                                    likelyEntry.handle = hist1._handle;

                                    return 2;
                                }
                                return 1;
                            }

                            if (!ICorJitInfo.IsUnknownHandle(hist1._handle))
                            {
                                ref var likelyEntry = ref likelyEntries[0];

                                likelyEntry.likelihood = (100 * hist1._count) / h._totalCount;
                                likelyEntry.handle = hist1._handle;

                                if ((likelyEntries.Length > 1) && !ICorJitInfo.IsUnknownHandle(hist0._handle))
                                {
                                    likelyEntry = ref likelyEntries[1];

                                    likelyEntry.likelihood = (100 * hist0._count) / h._totalCount;
                                    likelyEntry.handle = hist0._handle;

                                    return 2;
                                }
                                return 1;
                            }
                            return 0;
                        }

                        default:
                        {
                            Unsafe.SkipInit(out InlineArrayHistogramMaxSizeCount<LikelyClassMethodHistogramEntry> inlineSortedEntries);
                            var sortedEntries = (Span<LikelyClassMethodHistogramEntry>)(inlineSortedEntries);

                            // Since this method can be invoked without a jit instance we can't use any existing allocators
                            var knownHandles = 0;
                            var containsUnknownHandles = false;

                            for (var m = 0; m < h.countHistogramElements; m++)
                            {
                                var hist = h.HistogramEntryAt(m);

                                if (!ICorJitInfo.IsUnknownHandle(hist._handle))
                                {
                                    sortedEntries[knownHandles++] = hist;
                                }
                                else
                                {
                                    containsUnknownHandles = true;
                                }
                            }

                            if (knownHandles == 0)
                            {
                                // We don't have known handles
                                return 0;
                            }

                            // sort by m_count (descending)
                            sortedEntries[0..knownHandles].Sort((h1, h2) => h2._count.CompareTo(h1._count));

                            var numberOfClasses = int.Min(knownHandles, likelyEntries.Length);
                            var totalLikelihood = 0;

                            for (var hIdx = 0; hIdx < numberOfClasses; hIdx++)
                            {
                                var hc = sortedEntries[hIdx];
                                ref var likelyEntry = ref likelyEntries[hIdx];

                                likelyEntry.handle = hc._handle;
                                likelyEntry.likelihood = (hc._count * 100) / h._totalCount;

                                totalLikelihood += likelyEntry.likelihood;
                            }

                            assert(totalLikelihood <= 100);

                            // Distribute the rounding error and just apply it to the first entry.
                            // Assume that there is no error If we have unknown handles.

                            if (!containsUnknownHandles)
                            {
                                assert(numberOfClasses > 0);
                                assert(totalLikelihood > 0);

                                ref var likelyEntry = ref likelyEntries[0];
                                likelyEntry.likelihood += 100 - totalLikelihood;

                                assert(likelyEntry.likelihood <= 100);
                            }
                            return numberOfClasses;
                        }
                    }
                }
            }
        }

        // Failed to find histogram data for this method
        return 0;
    }

    /// <summary>find method profile data for an IL offset, and return the most likely methods</summary>
    /// <param name="likelyMethods"></param>
    /// <param name="schemas"></param>
    /// <param name="pInstrumentationData"></param>
    /// <param name="ilOffset"></param>
    /// <returns></returns>
    /// <remarks>See documentation on getLikelyClasses above.</remarks>
    public static unsafe int getLikelyMethods(Span<LikelyClassMethodRecord> likelyMethods, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schemas, byte* pInstrumentationData, IL_OFFSET ilOffset)
    {
        return getLikelyClassesOrMethods(likelyMethods, schemas, pInstrumentationData, ilOffset, types: false);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(getLikelyMethods))]
    public static unsafe int getLikelyMethods(LikelyClassMethodRecord* pLikelyMethods, int maxLikelyMethods, ICorJitInfo.PgoInstrumentationSchema* schema, int countSchemaItems, byte* pInstrumentationData, int ilOffset)
    {
        return getLikelyMethods(new Span<LikelyClassMethodRecord>(pLikelyMethods, maxLikelyMethods), new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(schema, countSchemaItems), pInstrumentationData, ilOffset);
    }

    /// <summary>find a value profile data for an IL offset</summary>
    /// <param name="likelyValues">array of likely values sorted by likelihood (descending). It must be at least of 'maxLikelyValues' (next argument) length. The array consists of pairs "value - likelihood" ordered by likelihood (descending) where likelihood can be any value in [0..100] range.</param>
    /// <param name="schemas">profile schema</param>
    /// <param name="pInstrumentationData">associated data</param>
    /// <param name="ilOffset">il offset of the node of interest</param>
    /// <returns>Estimated number of different constants seen at runtime</returns>
    public static unsafe int getLikelyValues(Span<LikelyValueRecord> likelyValues, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schemas, byte* pInstrumentationData, int ilOffset)
    {
        if ((likelyValues.Length == 0) || schemas.IsEmpty)
        {
            return 0;
        }
        likelyValues.Clear();

        for (var i = 0; i < schemas.Length; i++)
        {
            ref readonly var schema = ref schemas[i];

            if (schema.ILOffset != ilOffset)
            {
                continue;
            }

            // We currently re-use existing infrastructure for type handles for simplicity.
            //
            var isIntHistogramCount = (schema.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogramIntCount);
            var isLongHistogramCount = (schema.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogramLongCount);
            var isHistogramCount = isIntHistogramCount || isLongHistogramCount;

            if (isHistogramCount && (schema.Count == 1) && ((i + 1) < schemas.Length))
            {
                ref readonly var nextSchema = ref schemas[i + 1];

                if (nextSchema.InstrumentationKind is ICorJitInfo.PgoInstrumentationKind.ValueHistogram)
                {
                    var h = isIntHistogramCount
                          ? new LikelyClassMethodHistogram(new Span<int>((int*)(pInstrumentationData + nextSchema.Offset), nextSchema.Count))
                          : new LikelyClassMethodHistogram(new Span<nint>((nint*)(pInstrumentationData + nextSchema.Offset), nextSchema.Count));

                    Unsafe.SkipInit(out InlineArrayHistogramMaxSizeCount<LikelyClassMethodHistogramEntry> inlineSortedEntries);
                    var sortedEntries = (Span<LikelyClassMethodHistogramEntry>)(inlineSortedEntries);

                    if (h.countHistogramElements is 0)
                    {
                        return 0;
                    }

                    for (var hi = 0; hi < h.countHistogramElements; hi++)
                    {
                        var hist = h.HistogramEntryAt(hi);
                        sortedEntries[hi] = hist;
                    }

                    // sort by m_count (descending)
                    // jitstd::sort(sortedEntries, sortedEntries + h.countHistogramElements,
                    //              [](const LikelyClassMethodHistogramEntry&h1,
                    //                     const LikelyClassMethodHistogramEntry&h2) -> bool {
                    //     return h1.m_count > h2.m_count;
                    // });

                    var numberOfLikelyConst = int.Min(h.countHistogramElements, likelyValues.Length);
                    var totalLikelihood = 0;

                    for (var hIdx = 0; hIdx < numberOfLikelyConst; hIdx++)
                    {
                        var hc = sortedEntries[hIdx];

                        ref var likelyValue = ref likelyValues[hIdx];

                        likelyValue.value = hc._handle;
                        likelyValue.likelihood = (hc._count * 100) / h._totalCount;

                        totalLikelihood += likelyValue.likelihood;
                    }

                    assert(totalLikelihood <= 100);

                    // Distribute the rounding error and just apply it to the first entry.
                    assert(numberOfLikelyConst > 0);
                    assert(totalLikelihood > 0);

                    likelyValues[0].likelihood += (100 - totalLikelihood);
                    assert(likelyValues[0].likelihood <= 100);

                    return numberOfLikelyConst;
                }
            }
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(getLikelyMethods))]
    public static unsafe int getLikelyValues(LikelyValueRecord* pLikelyValues, int maxLikelyValues, ICorJitInfo.PgoInstrumentationSchema* schema, int countSchemaItems, byte* pInstrumentationData, int ilOffset)
    {
        return getLikelyValues(new Span<LikelyValueRecord>(pLikelyValues, maxLikelyValues), new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(schema, countSchemaItems), pInstrumentationData, ilOffset);
    }

    public static int roundUp(int size, int mult)
    {
        assert(int.IsPow2(mult));
        return (size + (mult - 1)) & ~(mult - 1);
    }
}

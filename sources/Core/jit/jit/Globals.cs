// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Globals
{
    static Globals()
    {
        Configuration.EnsureSupportedHostArchitecture();
        ValidateTargetConfiguration();

#if MEASURE_CLRAPI_CALLS
        if (!Configuration.HOST_X86 && !Configuration.HOST_AMD64)
        {
            throw new FatalJitException("Cycle counters only hooked up on x86/x64.");
        }
#endif

        if (Configuration.TARGET_LOONGARCH64 && Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException("SIMD Unimplemented yet LOONGARCH");
        }
    }

    private static void ValidateTargetConfiguration()
    {
        var targetCount =
            (Configuration.TARGET_X86 ? 1 : 0) +
            (Configuration.TARGET_AMD64 ? 1 : 0) +
            (Configuration.TARGET_ARM ? 1 : 0) +
            (Configuration.TARGET_ARM64 ? 1 : 0) +
            (Configuration.TARGET_LOONGARCH64 ? 1 : 0) +
            (Configuration.TARGET_RISCV64 ? 1 : 0) +
            (Configuration.TARGET_WASM32 ? 1 : 0);

        if (targetCount != 1)
        {
            throw new FatalJitException("Exactly one target architecture must be selected.");
        }

        var targetIs64Bit =
            Configuration.TARGET_AMD64 ||
            Configuration.TARGET_ARM64 ||
            Configuration.TARGET_LOONGARCH64 ||
            Configuration.TARGET_RISCV64;

        if (Configuration.TARGET_64BIT != targetIs64Bit)
        {
            throw new FatalJitException("Target bitness does not match the selected architecture.");
        }

        if (Configuration.UNIX_AMD64_ABI && !Configuration.TARGET_AMD64)
        {
            throw new FatalJitException("When UNIX_AMD64_ABI is enabled, TARGET_AMD64 must be enabled.");
        }

        if (Configuration.UNIX_X86_ABI && !Configuration.TARGET_X86)
        {
            throw new FatalJitException("When UNIX_X86_ABI is enabled, TARGET_X86 must be enabled.");
        }
    }

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
#if DEBUG
#error No point in measuring DEBUG code.
#endif
#endif

    public const int CLFLG_REGVAR = 0x00008;

    public const int CLFLG_TREETRANS = 0x00100;

    public const int CLFLG_INLINING = 0x00200;

    public const int CLFLG_STRUCTPROMOTE = 0x00400;

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
    public static void DISPNODE(GenTree? tree)
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
#if DEBUG
        if (offs != 0)
        {
            var compiler = JitTls.Compiler;
            assert(compiler is not null);
            return compiler.dspOffset(offs);
        }
#endif
        return offs;
    }

    public static unsafe nint dspPtr(void* ptr)
    {
#if DEBUG
        if (ptr != null)
        {
            var compiler = JitTls.Compiler;
            assert(compiler is not null);
            return compiler.dspPtr(ptr);
        }
#endif
        return unchecked((nint)ptr);
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
            jitprintf($" {codeAddr[offs]:x2}");
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
            var codeBytesDumped = dumpSingleInstr(codeAddr, offs, $"IL_{offs:x4} ");
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
                long iOp = unchecked((sbyte)(opcodePtr[0]));
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
                long iOp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
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
                double dOp = BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                operand = $" {formatFloat(dOp, "F6")}";
                break;
            }

            case InlineR:
            {
                var dOp = BinaryPrimitives.ReadDoubleLittleEndian(new ReadOnlySpan<byte>(opcodePtr, 8));
                operand = $" {formatFloat(dOp, "F6")}";
                break;
            }

            case ShortInlineBrTarget:
            {
                var jOp = unchecked((sbyte)(opcodePtr[0]));
                operand = FormattableString.Invariant($" {jOp} (IL_{baseOffs + jOp:x4})");
                break;
            }

            case InlineBrTarget:
            {
                var jOp = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(opcodePtr, 4));
                operand = FormattableString.Invariant($" {jOp} (IL_{baseOffs + jOp:x4})");
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

        var totalSize = (IL_OFFSET)(opcodePtr - startOpcodePtr) + sz;
        dumpILBytes(startOpcodePtr, totalSize, ALIGN_WIDTH);
        jitprintf($" {name,-12}{operand}");

        opcodePtr += sz;

        jitprintf("\n");
        return (IL_OFFSET)(opcodePtr - startOpcodePtr);
    }

#endif

    internal static string formatFloat(double value, string format)
    {
        // Preserve CRT spellings independently of the finite-number precision.
        if (double.IsFinite(value))
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        var sign = double.IsNegative(value) ? "-" : "";

        if (double.IsInfinity(value))
        {
            return sign + "inf";
        }

        if (Configuration.HOST_WINDOWS)
        {
            var bits = BitConverter.DoubleToUInt64Bits(value);

            if (bits == 0xFFF8000000000000)
            {
                return "-nan(ind)";
            }

            if ((bits & 0x0008000000000000) == 0)
            {
                return sign + "nan(snan)";
            }
        }

        return sign + "nan";
    }

    internal static string formatFloatWithTrailingZeros(double value, int precision)
    {
        var text = formatFloat(value, "g" + precision.ToString(CultureInfo.InvariantCulture));

        if (!double.IsFinite(value))
        {
            return text;
        }

        // C's %#.*g retains the decimal point and all significant trailing zeroes.
        var exponent = text.IndexOf('e', StringComparison.Ordinal);
        var end = exponent < 0 ? text.Length : exponent;
        var digits = 0;
        var hasPoint = false;

        for (var i = 0; i < end; i++)
        {
            var c = text[i];
            hasPoint |= c == '.';

            if (char.IsAsciiDigit(c) && ((c != '0') || (digits != 0)))
            {
                digits++;
            }
        }

        digits = Math.Max(digits, 1);
        var padding = (hasPoint ? "" : ".") + new string('0', precision - digits);

        return text.Insert(end, padding);
    }

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
    /// <returns>Number of likely class records written to <paramref name="likelyClasses" />.</returns>
    /// <remarks>
    ///   <para>A "monomorphic" call site will return likelihood 100 and number of entries = 1.</para>
    ///   <para>Returned likelihoods reflect the observed proportions and are not guaranteed to sum to 100.</para>
    ///   <para>This is used by the devirtualization logic below, and by crossgen2 when producing the R2R image (to reduce the sizecost of carrying the type histogram)</para>
    ///   <para>This code can runs without a jit instance present, so JITDUMP and related cannot be used.</para>
    /// </remarks>
    public static unsafe int getLikelyClasses(Span<LikelyClassMethodRecord> likelyClasses, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schema, byte* pInstrumentationData, IL_OFFSET ilOffset)
    {
        return getLikelyClassesOrMethods(likelyClasses, schema, pInstrumentationData, ilOffset, types: true);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(getLikelyClasses))]
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

                            // Distribute the rounding error only if the returned entries represent the entire histogram.
                            if ((numberOfClasses == knownHandles) && !containsUnknownHandles)
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
    /// <returns>Number of likely value records written to <paramref name="likelyValues" />.</returns>
    /// <remarks>Returned likelihoods reflect the observed proportions and are not guaranteed to sum to 100.</remarks>
    public static unsafe int getLikelyValues(Span<LikelyValueRecord> likelyValues, ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema> schemas, byte* pInstrumentationData, int ilOffset)
    {
        if (likelyValues.Length == 0)
        {
            return 0;
        }

        likelyValues.Clear();

        if (schemas.IsEmpty)
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
                    sortedEntries[..h.countHistogramElements].Sort((h1, h2) => h2._count.CompareTo(h1._count));

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

                    // Distribute the rounding error only if the returned entries represent the entire histogram.
                    if (numberOfLikelyConst == h.countHistogramElements)
                    {
                        assert(numberOfLikelyConst > 0);
                        assert(totalLikelihood > 0);

                        likelyValues[0].likelihood += 100 - totalLikelihood;
                        assert(likelyValues[0].likelihood <= 100);
                    }

                    return numberOfLikelyConst;
                }
            }
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)], EntryPoint = nameof(getLikelyValues))]
    public static unsafe int getLikelyValues(LikelyValueRecord* pLikelyValues, int maxLikelyValues, ICorJitInfo.PgoInstrumentationSchema* schema, int countSchemaItems, byte* pInstrumentationData, int ilOffset)
    {
        if (schema is null)
        {
            return 0;
        }

        return getLikelyValues(new Span<LikelyValueRecord>(pLikelyValues, maxLikelyValues), new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(schema, countSchemaItems), pInstrumentationData, ilOffset);
    }

    public static int roundUp(int size, int mult)
    {
        assert(int.IsPow2(mult));
        return (size + (mult - 1)) & ~(mult - 1);
    }

    public static uint roundUp(uint size, uint mult)
    {
        assert(uint.IsPow2(mult));
        return unchecked(size + (mult - 1)) & ~(mult - 1);
    }
}

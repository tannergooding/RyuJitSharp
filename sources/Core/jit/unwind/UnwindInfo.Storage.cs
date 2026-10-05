// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed partial class UnwindInfo
{
#if TARGET_ARM
    private const byte UWC_END = 0xFF;
#else
    private const byte UWC_END = 0xE4;
#endif

#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
    private const byte UWC_END_C = 0xE5;
#endif

#if TARGET_ARM
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 19;
    private const uint UW_MAX_CODE_WORDS_COUNT = 15;
    private const uint UW_MAX_EPILOG_START_INDEX = 0xFF;
    private const uint MAX_PROLOG_SIZE_BYTES = 44;
    private const uint MAX_EPILOG_SIZE_BYTES = 44;
#elif TARGET_RISCV64
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 19;
    private const uint UW_MAX_CODE_WORDS_COUNT = 31;
    private const uint UW_MAX_EPILOG_START_INDEX = 0x3FF;
    private const uint MAX_PROLOG_SIZE_BYTES = 200;
    private const uint MAX_EPILOG_SIZE_BYTES = 200;
#elif TARGET_LOONGARCH64
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 20;
    private const uint UW_MAX_CODE_WORDS_COUNT = 31;
    private const uint UW_MAX_EPILOG_START_INDEX = 0x3FF;
    private const uint MAX_PROLOG_SIZE_BYTES = 200;
    private const uint MAX_EPILOG_SIZE_BYTES = 200;
#else
    private const uint UW_MAX_FRAGMENT_SIZE_BYTES = 1u << 20;
    private const uint UW_MAX_CODE_WORDS_COUNT = 31;
    private const uint UW_MAX_EPILOG_START_INDEX = 0x3FF;
    private const uint MAX_PROLOG_SIZE_BYTES = 100;
    private const uint MAX_EPILOG_SIZE_BYTES = 100;
#endif

    private const uint UW_MAX_EPILOG_COUNT = 31;
    private const uint UW_MAX_EXTENDED_CODE_WORDS_COUNT = 0xFF;
    private const uint UW_MAX_EXTENDED_EPILOG_COUNT = 0xFFFF;
    private const uint UW_MAX_EPILOG_START_OFFSET = 0x3FFFF;

    private Compiler? m_compiler;
    private UnwindFragmentInfo? uwiFragmentFirst;
    private UnwindFragmentInfo? uwiFragmentLast;
    private emitLocation? uwiEndLoc;
    private emitLocation? uwiCurLoc;

#if DEBUG
    private bool uwiInitialized;
#endif

#if DEBUG && TARGET_RISCV64
    private static string DebugReference(object? value)
    {
        return value is null ? "NULL" : $"managed#{RuntimeHelpers.GetHashCode(value):x8}";
    }

    private static string DebugIndent(int indent)
    {
        return new string(' ', indent);
    }
#endif

    public void InitUnwindInfo(Compiler compiler, emitLocation? startLoc, emitLocation? endLoc)
    {
        m_compiler = compiler;
        uwiFragmentFirst = new UnwindFragmentInfo(compiler, startLoc, false);
        uwiFragmentLast = uwiFragmentFirst;
        uwiEndLoc = endLoc;
        uwiCurLoc = null;

#if DEBUG
        uwiInitialized = true;
        uwiAddingNOP = false;
#endif
    }

    public emitLocation? GetCurrentEmitterLocation()
    {
        return uwiCurLoc;
    }

    public void AddCode(byte b1)
    {
        CheckInitialized();
        CheckOpsize(b1);
        uwiFragmentLast!.AddCode(b1);
        CaptureLocation();
    }

    public void AddCode(byte b1, byte b2)
    {
        CheckInitialized();
        CheckOpsize(b1);
        uwiFragmentLast!.AddCode(b1, b2);
        CaptureLocation();
    }

    public void AddCode(byte b1, byte b2, byte b3)
    {
        CheckInitialized();
        CheckOpsize(b1);
        uwiFragmentLast!.AddCode(b1, b2, b3);
        CaptureLocation();
    }

    public void AddCode(byte b1, byte b2, byte b3, byte b4)
    {
        CheckInitialized();
        CheckOpsize(b1);
        uwiFragmentLast!.AddCode(b1, b2, b3, b4);
        CaptureLocation();
    }

    public void AddEpilog()
    {
        CheckInitialized();
        uwiFragmentLast!.AddEpilog();
        CaptureLocation();
    }

    public void CaptureLocation()
    {
        CheckInitialized();
        uwiCurLoc = new emitLocation(GetCompiler().GetEmitter());
    }

    public void HotColdSplitCodes(UnwindInfo hotUnwindInfo)
    {
        CheckInitialized();
        hotUnwindInfo.CheckInitialized();
        var first = uwiFragmentFirst!;
        var hotFirst = hotUnwindInfo.uwiFragmentFirst!;
        noway_assert(ReferenceEquals(first, uwiFragmentLast));
        noway_assert(ReferenceEquals(hotFirst, hotUnwindInfo.uwiFragmentLast));
        noway_assert(first.ufiNext is null);
        noway_assert(hotFirst.ufiNext is null);

        first.ufiHasPhantomProlog = true;
        first.CopyPrologCodes(hotFirst);
        first.SplitEpilogCodes(first.ufiEmitLoc!.Value, hotFirst);
    }

    public unsafe void Split()
    {
        var compiler = GetCompiler();
        var first = uwiFragmentFirst!;
        noway_assert(ReferenceEquals(first, uwiFragmentLast));
        noway_assert(first.ufiNext is null);

        var maxFragmentSize = UW_MAX_FRAGMENT_SIZE_BYTES;
#if DEBUG
        var splitFunctionSize = unchecked((uint)JitConfig.JitSplitFunctionSize);
#if TARGET_ARMARCH
        if (splitFunctionSize == 0)
        {
            if (compiler.compStressCompile(Compiler.STRESS_UNWIND, 10))
            {
                splitFunctionSize = compiler.compStressCompile(Compiler.STRESS_UNWIND, 5) ? 4u : 200u;
            }
        }
#endif
        if ((splitFunctionSize != 0) && (splitFunctionSize < maxFragmentSize))
        {
            maxFragmentSize = splitFunctionSize;
        }
#endif

        var emitter = compiler.GetEmitter();
        var startOffset = first.ufiEmitLoc?.CodeOffset(emitter) ?? 0;
        var endOffset = uwiEndLoc?.CodeOffset(emitter)
            ?? unchecked((uint)(compiler.info.compTotalHotCodeSize + compiler.info.compTotalColdCodeSize));
        assert(endOffset > startOffset);
        var codeSize = unchecked(endOffset - startOffset);
        var numberOfFragments = unchecked((codeSize + maxFragmentSize - 1) / maxFragmentSize);
        assert(numberOfFragments > 0);
        if (numberOfFragments == 1)
        {
            return;
        }

        var handle = GCHandle.Alloc(this);
        try
        {
            emitter.emitSplit(first.ufiEmitLoc, uwiEndLoc, maxFragmentSize,
                GCHandle.ToIntPtr(handle).ToPointer(), EmitSplitCallback);
        }
        finally
        {
            handle.Free();
        }

#if DEBUG
        var fragmentCount = 0u;
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragmentCount++;
        }

        if (fragmentCount < numberOfFragments)
        {
            if (compiler.verbose)
            {
                jitprintf($"WARNING: asked the emitter for {numberOfFragments} fragments, but only got {fragmentCount}\n");
            }

            assert(maxFragmentSize != UW_MAX_FRAGMENT_SIZE_BYTES);
        }
#endif
    }

    public void Reserve(bool isFunclet, bool isHotCode)
    {
        CheckInitialized();
        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.Reserve(isFunclet, isHotCode);
        }
    }

    public unsafe void Allocate(CorJitFuncKind functionKind, void* hotCode, void* coldCode, bool isHotCode)
    {
        CheckInitialized();
        var compiler = GetCompiler();
        var endOffset = uwiEndLoc?.CodeOffset(compiler.GetEmitter())
            ?? unchecked((uint)compiler.info.compNativeCodeSize);

        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.FinalizeOffset();
        }

        for (var fragment = uwiFragmentFirst; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.Allocate(functionKind, hotCode, coldCode, endOffset, isHotCode);
        }
    }

#if DEBUG && TARGET_RISCV64
    public void Dump(bool isHotCode, int indent = 0)
    {
        var first = uwiFragmentFirst
            ?? throw new InvalidOperationException("Unwind information has not been initialized.");
        var fragmentCount = 0;
        for (var fragment = first; fragment is not null; fragment = fragment.ufiNext)
        {
            fragmentCount++;
        }

        var indentation = DebugIndent(indent);
        jitprintf($"{indentation}UnwindInfo {(isHotCode ? "" : "COLD ")}@{DebugReference(this)}:\n");
        jitprintf($"{indentation}  m_compiler: {DebugReference(m_compiler)}\n");
        jitprintf($"{indentation}  {fragmentCount} fragment{(fragmentCount != 1 ? "s" : "")}\n");
        jitprintf($"{indentation}  uwiFragmentLast: {DebugReference(uwiFragmentLast)}\n");
        jitprintf($"{indentation}  uwiEndLoc: {DebugReference(uwiEndLoc)}\n");
        jitprintf($"{indentation}  uwiInitialized: 0x{(uwiInitialized ? 0x0FACADE0u : 0u):x8}\n");

        for (var fragment = first; fragment is not null; fragment = fragment.ufiNext)
        {
            fragment.Dump(indent + 2);
        }
    }
#endif

#if TARGET_ARM
    internal uint GetInstructionSize()
    {
        CheckInitialized();
        return GetCompiler().GetEmitter().emitGetInstructionSize(uwiCurLoc.GetValueOrDefault());
    }
#endif

    private Compiler GetCompiler()
    {
        return m_compiler ?? throw new InvalidOperationException("Unwind information has not been initialized.");
    }

    private void CheckInitialized()
    {
#if DEBUG
        assert(uwiInitialized);
#endif
        noway_assert(uwiFragmentLast is not null);
    }

    private void CheckOpsize(byte firstCodeByte)
    {
#if DEBUG && TARGET_ARM
        if (uwiAddingNOP)
        {
            return;
        }

        var opcodeSize = GetArmOpcodeSize(firstCodeByte);
        var instructionSize = GetInstructionSize();
        assert(opcodeSize == instructionSize);
#endif
    }

    private static unsafe void EmitSplitCallback(void* context, emitLocation location)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        var unwindInfo = (UnwindInfo?)handle.Target
            ?? throw new InvalidOperationException("The unwind split callback lost its owner.");
        unwindInfo.AddFragment(location);
    }

    private void AddFragment(emitLocation emitLoc)
    {
        CheckInitialized();
        var last = uwiFragmentLast!;
        var newFragment = new UnwindFragmentInfo(GetCompiler(), emitLoc, true);
#if DEBUG && TARGET_RISCV64
        newFragment.SetNumberAfter(last);
#endif
        newFragment.CopyPrologCodes(uwiFragmentFirst!);
        newFragment.SplitEpilogCodes(emitLoc, last);
        last.ufiNext = newFragment;
        uwiFragmentLast = newFragment;
    }

#if DEBUG
    internal static uint GetUnwindCodeSize(byte header)
    {
#if TARGET_ARM
        if (header <= 0x7F || (header >= 0xC0 && header <= 0xE7) || header >= 0xF0)
        {
            return header switch
            {
                >= 0xF5 and <= 0xF6 => 2,
                0xF7 or 0xF9 => 3,
                0xF8 or 0xFA => 4,
                _ => 1,
            };
        }

        return header <= 0xBF || (header >= 0xE8 && header <= 0xEF) ? 2u : 1u;
#elif TARGET_LOONGARCH64
        return GetUnwindSizeFromUnwindHeader(header);
#else
        return Globals.GetUnwindSizeFromUnwindHeader(header);
#endif
    }
#endif

#if DEBUG && TARGET_ARM
    internal static uint GetArmOpcodeSize(byte header)
    {
        var size = header switch
        {
            <= 0x7F => 2,
            <= 0xBF => 4,
            <= 0xD7 => 2,
            <= 0xEB => 4,
            <= 0xEE => 2,
            0xEF => 4,
            >= 0xF5 and <= 0xF6 => 4,
            >= 0xF7 and <= 0xF8 => 2,
            >= 0xF9 and <= 0xFA => 4,
            0xFB => 2,
            0xFC => 4,
            0xFD => 2,
            0xFE => 4,
            _ => 0,
        };
        assert(size == 2 || size == 4);
        return unchecked((uint)size);
    }
#endif

    private abstract class UnwindCodesStorage
    {
        protected static bool IsEndCode(byte code)
        {
#if TARGET_ARM
            return code >= 0xFD;
#else
            return code == UWC_END;
#endif
        }

        public abstract byte[] Codes { get; }
        public abstract int CodeStart { get; }
        public abstract int Size();
        public abstract void AddCode(byte b1);
        public abstract void AddCode(byte b1, byte b2);
        public abstract void AddCode(byte b1, byte b2, byte b3);
        public abstract void AddCode(byte b1, byte b2, byte b3, byte b4);

#if DEBUG
        public uint GetCodeSizeFromUnwindCodes(bool isProlog)
        {
            var codes = Codes;
            var start = CodeStart;
            var index = start;
            var size = 0u;
            for (;;)
            {
                var code = codes[index];
                if (IsEndCode(code))
                {
#if TARGET_ARM
                    if (!isProlog && (code == 0xFD || code == 0xFE))
                    {
                        size += GetArmOpcodeSize(code);
                    }
#endif
                    break;
                }

#if TARGET_ARM
                size += GetArmOpcodeSize(code);
#else
                size += 4;
#endif
                index = unchecked(index + (int)GetUnwindCodeSize(code));
                assert(index - start < 256);
            }

            return size;
        }
#endif
    }

    private sealed partial class UnwindPrologCodes : UnwindCodesStorage
    {
        private const int UPC_LOCAL_COUNT = 24;
        private readonly Compiler m_compiler;
        private byte[] upcMem = new byte[UPC_LOCAL_COUNT];
        private int upcMemSize = UPC_LOCAL_COUNT;
        private int upcCodeSlot = UPC_LOCAL_COUNT;
        private int upcHeaderSlot = -1;
        private int upcEpilogSlot = -1;
        private int upcUnwindBlockSlot;

        public UnwindPrologCodes(Compiler compiler)
        {
            m_compiler = compiler;
            PushByte(UWC_END);
            PushByte(UWC_END);
            PushByte(UWC_END);
            PushByte(UWC_END);
        }

        public override byte[] Codes => upcMem;
        public override int CodeStart => upcCodeSlot;

        public override void AddCode(byte b1)
        {
            PushByte(b1);
        }

        public override void AddCode(byte b1, byte b2)
        {
            PushByte(b2);
            PushByte(b1);
        }

        public override void AddCode(byte b1, byte b2, byte b3)
        {
            PushByte(b3);
            PushByte(b2);
            PushByte(b1);
        }

        public override void AddCode(byte b1, byte b2, byte b3, byte b4)
        {
            PushByte(b4);
            PushByte(b3);
            PushByte(b2);
            PushByte(b1);
        }

        public override int Size()
        {
            return upcMemSize - upcCodeSlot - 3;
        }

        private void PushByte(byte value)
        {
            if (upcCodeSlot == 0)
            {
                EnsureSize(upcMemSize + 1);
            }

            --upcCodeSlot;
            noway_assert((0 <= upcCodeSlot) && (upcCodeSlot < upcMemSize));
            upcMem[upcCodeSlot] = value;
        }

        private void EnsureSize(int requiredSize)
        {
            if (requiredSize <= upcMemSize)
            {
                return;
            }

            noway_assert((requiredSize & 0xC0000000) == 0);
            var newSize = unchecked(upcMemSize << 1);
            while (newSize < requiredSize)
            {
                newSize = unchecked(newSize << 1);
            }

            var newMemory = new byte[newSize];
            var shift = newSize - upcMemSize;
            Array.Copy(upcMem, 0, newMemory, shift, upcMemSize);
            upcMem = newMemory;
            upcCodeSlot += shift;
            upcMemSize = newSize;
        }

        public void SetFinalSize(int headerBytes, int epilogBytes)
        {
#if DEBUG
            assert(GetCodeSizeFromUnwindCodes(true) <= MAX_PROLOG_SIZE_BYTES);
#endif
            var prologBytes = Size();
            EnsureSize(headerBytes + prologBytes + epilogBytes + 3);
            upcUnwindBlockSlot = upcCodeSlot - headerBytes - epilogBytes;
            assert(upcMemSize == upcUnwindBlockSlot + headerBytes + prologBytes + epilogBytes + 3);
            upcHeaderSlot = upcUnwindBlockSlot - 1;
            assert(upcHeaderSlot >= -1);

            if (epilogBytes > 0)
            {
                Array.Copy(upcMem, upcCodeSlot, upcMem, upcUnwindBlockSlot + headerBytes, prologBytes);
                upcEpilogSlot = upcUnwindBlockSlot + headerBytes + prologBytes;
                upcCodeSlot = upcUnwindBlockSlot + headerBytes;
            }
        }

        public void AddHeaderWord(uint value)
        {
            assert(upcHeaderSlot >= -1);
            assert(upcHeaderSlot + 4 < upcCodeSlot);
            upcMem[++upcHeaderSlot] = unchecked((byte)value);
            upcMem[++upcHeaderSlot] = unchecked((byte)(value >> 8));
            upcMem[++upcHeaderSlot] = unchecked((byte)(value >> 16));
            upcMem[++upcHeaderSlot] = unchecked((byte)(value >> 24));
        }

        public void AppendEpilog(UnwindEpilogInfo epilog)
        {
            assert(upcEpilogSlot != -1);
            var epilogSize = epilog.Size();
            Array.Copy(epilog.GetCodes(), epilog.CodeStart, upcMem, upcEpilogSlot, epilogSize);
            assert(epilog.GetStartIndex() == upcEpilogSlot - upcCodeSlot);
            upcEpilogSlot += epilogSize;
            assert(upcEpilogSlot <= upcMemSize - 3);
        }

        public int Match(UnwindEpilogInfo epilog)
        {
            if (Size() < epilog.Size())
            {
                return -1;
            }

#if TARGET_LOONGARCH64 || TARGET_RISCV64
            var matchIndex = 0;
            var prologIndex = upcCodeSlot;
            var epilogCodes = epilog.GetCodes();
            var epilogIndex = epilog.CodeStart;

            if (epilog.Size() > 0)
            {
                if (upcMem[prologIndex] == 0xE1)
                {
                    prologIndex++;
                    if (epilogCodes[epilogIndex] == 0xE1)
                    {
                        epilogIndex++;
                    }
                    else
                    {
                        matchIndex = 1;
                    }
                }
                else if (upcMem[prologIndex] == 0xE2)
                {
                    prologIndex += 3;
                    if (epilogCodes[epilogIndex] == 0xE1)
                    {
                        epilogIndex += 3;
                    }
                    else
                    {
                        matchIndex = 3;
                    }
                }
            }

            for (var index = 0; index < epilog.Size(); index++)
            {
                if (upcMem[prologIndex + index] != epilogCodes[epilogIndex + index])
                {
                    return -1;
                }
            }

            return matchIndex;
#else
            var matchIndex = Size() - epilog.Size();
            var epilogCodes = epilog.GetCodes();
            for (var index = 0; index < epilog.Size(); index++)
            {
                if (upcMem[upcCodeSlot + matchIndex + index] != epilogCodes[epilog.CodeStart + index])
                {
                    return -1;
                }
            }

            return matchIndex;
#endif
        }

        public void CopyFrom(UnwindPrologCodes source)
        {
            noway_assert(ReferenceEquals(m_compiler, source.m_compiler));
            assert(upcMemSize == UPC_LOCAL_COUNT);
            assert(upcHeaderSlot == -1);
            assert(upcEpilogSlot == -1);
            EnsureSize(source.upcMemSize);
            Array.Copy(source.upcMem, upcMem, source.upcMemSize);
            upcCodeSlot = source.upcCodeSlot;
            upcHeaderSlot = source.upcHeaderSlot;
            upcEpilogSlot = source.upcEpilogSlot;
            upcUnwindBlockSlot = source.upcUnwindBlockSlot;
        }

        public (byte[] Memory, int Offset, int Size) GetFinalInfo()
        {
            assert(upcHeaderSlot + 1 == upcCodeSlot);
            var size = upcMemSize - upcUnwindBlockSlot - 3;
            size = (size + 3) & ~3;
            return (upcMem, upcUnwindBlockSlot, size);
        }

#if DEBUG && TARGET_RISCV64
        public void Dump(int indent = 0)
        {
            var indentation = DebugIndent(indent);
            jitprintf($"{indentation}UnwindPrologCodes @{DebugReference(this)}:\n");
            jitprintf($"{indentation}  m_compiler: {DebugReference(m_compiler)}\n");
            jitprintf($"{indentation}  upcMem: {DebugReference(upcMem)}\n");
            jitprintf($"{indentation}  upcMemSize: {upcMemSize}\n");
            jitprintf($"{indentation}  upcCodeSlot: {upcCodeSlot}\n");
            jitprintf($"{indentation}  upcHeaderSlot: {upcHeaderSlot}\n");
            jitprintf($"{indentation}  upcEpilogSlot: {upcEpilogSlot}\n");
            jitprintf($"{indentation}  upcUnwindBlockSlot: {upcUnwindBlockSlot}\n");

            if (upcMemSize > 0)
            {
                jitprintf($"{indentation}  codes:");
                for (var index = 0; index < upcMemSize; index++)
                {
                    jitprintf($" {upcMem[index]:x2}");
                    if (index == upcCodeSlot)
                    {
                        jitprintf(" <-C");
                    }
                    else if (index == upcHeaderSlot)
                    {
                        jitprintf(" <-H");
                    }
                    else if (index == upcEpilogSlot)
                    {
                        jitprintf(" <-E");
                    }
                    else if (index == upcUnwindBlockSlot)
                    {
                        jitprintf(" <-U");
                    }
                }

                jitprintf("\n");
            }
        }
#endif
    }

    private sealed partial class UnwindEpilogCodes : UnwindCodesStorage
    {
        private const int UEC_LOCAL_COUNT = 4;
        private byte[] uecMem = new byte[UEC_LOCAL_COUNT];
        private byte firstByteOfLastCode;
        private int uecMemSize = UEC_LOCAL_COUNT;
        private int uecCodeSlot = -1;
        private bool uecFinalized;

        public UnwindEpilogCodes()
        {
        }

        public override byte[] Codes => uecMem;
        public override int CodeStart => 0;

        public override void AddCode(byte b1)
        {
            AppendByte(b1);
            firstByteOfLastCode = b1;
        }

        public override void AddCode(byte b1, byte b2)
        {
            AppendByte(b1);
            AppendByte(b2);
            firstByteOfLastCode = b1;
        }

        public override void AddCode(byte b1, byte b2, byte b3)
        {
            AppendByte(b1);
            AppendByte(b2);
            AppendByte(b3);
            firstByteOfLastCode = b1;
        }

        public override void AddCode(byte b1, byte b2, byte b3, byte b4)
        {
            AppendByte(b1);
            AppendByte(b2);
            AppendByte(b3);
            AppendByte(b4);
            firstByteOfLastCode = b1;
        }

        public override int Size()
        {
            return uecFinalized ? uecCodeSlot + 1 : uecCodeSlot + 2;
        }

        private void AppendByte(byte value)
        {
            if (uecCodeSlot == uecMemSize - 1)
            {
                EnsureSize(uecMemSize + 1);
            }

            ++uecCodeSlot;
            noway_assert((0 <= uecCodeSlot) && (uecCodeSlot < uecMemSize));
            uecMem[uecCodeSlot] = value;
        }

        private void EnsureSize(int requiredSize)
        {
            if (requiredSize <= uecMemSize)
            {
                return;
            }

            noway_assert((requiredSize & 0xC0000000) == 0);
            var newSize = unchecked(uecMemSize << 1);
            while (newSize < requiredSize)
            {
                newSize = unchecked(newSize << 1);
            }

            var newMemory = new byte[newSize];
            Array.Copy(uecMem, newMemory, uecMemSize);
            uecMem = newMemory;
            uecMemSize = newSize;
        }

        public void FinalizeCodes()
        {
            assert(!uecFinalized);
            noway_assert((0 <= uecCodeSlot) && (uecCodeSlot < uecMemSize));
            if (!IsEndCode(firstByteOfLastCode))
            {
                AppendByte(UWC_END);
                firstByteOfLastCode = UWC_END;
            }

            uecFinalized = true;

#if DEBUG && !TARGET_RISCV64
            assert(GetCodeSizeFromUnwindCodes(false) <= MAX_EPILOG_SIZE_BYTES);
#endif
        }

#if DEBUG && TARGET_RISCV64
        public void Dump(Compiler compiler, int indent = 0)
        {
            var indentation = DebugIndent(indent);
            jitprintf($"{indentation}UnwindEpilogCodes @{DebugReference(this)}:\n");
            jitprintf($"{indentation}  m_compiler: {DebugReference(compiler)}\n");
            jitprintf($"{indentation}  uecMem: {DebugReference(uecMem)}\n");
            jitprintf($"{indentation}  uecMemSize: {uecMemSize}\n");
            jitprintf($"{indentation}  uecCodeSlot: {uecCodeSlot}\n");
            jitprintf($"{indentation}  uecFinalized: {dspBool(uecFinalized)}\n");

            if (uecMemSize > 0)
            {
                jitprintf($"{indentation}  codes:");
                for (var index = 0; index < uecMemSize; index++)
                {
                    jitprintf($" {uecMem[index]:x2}");
                    if (index == uecCodeSlot)
                    {
                        jitprintf(" <-C");
                    }
                }

                jitprintf("\n");
            }
        }
#endif
    }

    private sealed partial class UnwindEpilogInfo
    {
        private const uint EPI_ILLEGAL_OFFSET = uint.MaxValue;
        public UnwindEpilogInfo? epiNext;
        public emitLocation? epiEmitLocation;
        public readonly UnwindEpilogCodes epiCodes = new();
        private uint epiStartOffset = EPI_ILLEGAL_OFFSET;
        private bool epiMatches;
        private int epiStartIndex = -1;
        private readonly Compiler m_compiler;

        public UnwindEpilogInfo(Compiler compiler)
        {
            m_compiler = compiler;
        }

        public void CaptureEmitLocation()
        {
            noway_assert(epiEmitLocation is null);
            epiEmitLocation = new emitLocation(m_compiler.GetEmitter());
        }

        public void FinalizeOffset()
        {
            epiStartOffset = epiEmitLocation!.Value.CodeOffset(m_compiler.GetEmitter());
        }

        public void FinalizeCodes()
        {
            epiCodes.FinalizeCodes();
        }

        public uint GetStartOffset()
        {
            assert(epiStartOffset != EPI_ILLEGAL_OFFSET);
            return epiStartOffset;
        }

        public int GetStartIndex()
        {
            assert(epiStartIndex != -1);
            return epiStartIndex;
        }

        public void SetStartIndex(int index)
        {
            assert(epiStartIndex == -1);
            epiStartIndex = index;
        }

        public void SetMatches()
        {
            epiMatches = true;
        }

        public bool Matches()
        {
            return epiMatches;
        }

        public int Size()
        {
            return epiCodes.Size();
        }

        public byte[] GetCodes()
        {
            return epiCodes.Codes;
        }

        public int CodeStart => epiCodes.CodeStart;

        public int Match(UnwindEpilogInfo other)
        {
            if (Matches() || (Size() < other.Size()))
            {
                return -1;
            }

            var matchIndex = Size() - other.Size();
            var otherCodes = other.GetCodes();
            var codes = GetCodes();
            for (var index = 0; index < other.Size(); index++)
            {
                if (codes[CodeStart + matchIndex + index] != otherCodes[other.CodeStart + index])
                {
                    return -1;
                }
            }

            return matchIndex;
        }

#if DEBUG && TARGET_RISCV64
        public void Dump(int indent = 0)
        {
            var indentation = DebugIndent(indent);
            jitprintf($"{indentation}UnwindEpilogInfo @{DebugReference(this)}:\n");
            jitprintf($"{indentation}  m_compiler: {DebugReference(m_compiler)}\n");
            jitprintf($"{indentation}  epiNext: {DebugReference(epiNext)}\n");
            jitprintf($"{indentation}  epiEmitLocation: {DebugReference(epiEmitLocation)}\n");
            jitprintf($"{indentation}  epiStartOffset: 0x{epiStartOffset:x}\n");
            jitprintf($"{indentation}  epiMatches: {dspBool(epiMatches)}\n");
            jitprintf($"{indentation}  epiStartIndex: {epiStartIndex}\n");

            epiCodes.Dump(m_compiler, indent + 2);
        }
#endif
    }

    private sealed partial class UnwindFragmentInfo
    {
        private const uint UFI_ILLEGAL_OFFSET = uint.MaxValue;
        private readonly Compiler m_compiler;
        public UnwindFragmentInfo? ufiNext;
        public readonly emitLocation? ufiEmitLoc;
        public bool ufiHasPhantomProlog;
        private readonly UnwindPrologCodes ufiPrologCodes;
        private readonly UnwindEpilogInfo ufiEpilogFirst;
        private UnwindEpilogInfo? ufiEpilogList;
        private UnwindEpilogInfo? ufiEpilogLast;
        private UnwindCodesStorage? ufiCurCodes;
        private uint ufiSize;
        private bool ufiSetEBit;
        private bool ufiNeedExtendedCodeWordsEpilogCount;
        private uint ufiCodeWords;
        private uint ufiEpilogScopes;
        private uint ufiStartOffset = UFI_ILLEGAL_OFFSET;
#if DEBUG
        private bool ufiInProlog = true;
#endif
#if DEBUG && TARGET_RISCV64
        private uint ufiNum;
        private uint ufiInitialized;
#endif

        public UnwindFragmentInfo(Compiler compiler, emitLocation? emitLoc, bool hasPhantomProlog)
        {
            m_compiler = compiler;
            ufiEmitLoc = emitLoc;
            ufiHasPhantomProlog = hasPhantomProlog;
            ufiPrologCodes = new UnwindPrologCodes(compiler);
            ufiEpilogFirst = new UnwindEpilogInfo(compiler);
            ufiCurCodes = ufiPrologCodes;
#if DEBUG && TARGET_RISCV64
            ufiNum = 1;
            ufiInitialized = 0x0FACADE0;
#endif
        }

#if DEBUG && TARGET_RISCV64
        public void SetNumberAfter(UnwindFragmentInfo previous)
        {
            ufiNum = unchecked(previous.ufiNum + 1);
        }
#endif

        public uint GetStartOffset()
        {
            assert(ufiStartOffset != UFI_ILLEGAL_OFFSET);
            return ufiStartOffset;
        }

        public void AddCode(byte b1) => ufiCurCodes!.AddCode(b1);
        public void AddCode(byte b1, byte b2) => ufiCurCodes!.AddCode(b1, b2);
        public void AddCode(byte b1, byte b2, byte b3) => ufiCurCodes!.AddCode(b1, b2, b3);
        public void AddCode(byte b1, byte b2, byte b3, byte b4) => ufiCurCodes!.AddCode(b1, b2, b3, b4);

        public void AddEpilog()
        {
#if DEBUG
            if (ufiInProlog)
            {
                assert(ufiEpilogList is null);
                ufiInProlog = false;
            }
            else
            {
                assert(ufiEpilogList is not null);
            }
#endif
            var newEpilog = ufiEpilogList is null
                ? ufiEpilogList = ufiEpilogFirst
                : new UnwindEpilogInfo(m_compiler);
            ufiEpilogLast?.epiNext = newEpilog;

            ufiEpilogLast = newEpilog;
            newEpilog.CaptureEmitLocation();
            ufiCurCodes = newEpilog.epiCodes;
        }

        public void FinalizeOffset()
        {
            ufiStartOffset = ufiEmitLoc?.CodeOffset(m_compiler.GetEmitter()) ?? 0;
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilog.FinalizeOffset();
            }
        }

        public void CopyPrologCodes(UnwindFragmentInfo source)
        {
            ufiPrologCodes.CopyFrom(source.ufiPrologCodes);
#if TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            ufiPrologCodes.AddCode(UWC_END_C);
#endif
        }

        public void SplitEpilogCodes(emitLocation splitLocation, UnwindFragmentInfo source)
        {
            var splitOffset = splitLocation.CodeOffset(m_compiler.GetEmitter());
            UnwindEpilogInfo? previous = null;
            var epilog = source.ufiEpilogList;
            while (epilog is not null)
            {
                epilog.FinalizeOffset();
                if (epilog.GetStartOffset() >= splitOffset)
                {
                    ufiEpilogList = epilog;
                    ufiEpilogLast = source.ufiEpilogLast;
                    source.ufiEpilogLast = previous;
                    if (source.ufiEpilogLast is null)
                    {
                        source.ufiEpilogList = null;
                    }
                    else
                    {
                        source.ufiEpilogLast.epiNext = null;
                    }

                    source.ufiCurCodes = null;
                    ufiCurCodes = null;
                    break;
                }

                previous = epilog;
                epilog = epilog.epiNext;
            }
        }

        public bool IsAtFragmentEnd(UnwindEpilogInfo epilog)
        {
            return m_compiler.GetEmitter().emitIsFuncEnd(
                epilog.epiEmitLocation!.Value, ufiNext?.ufiEmitLoc);
        }

        public uint Size()
        {
            assert(ufiSize != 0);
            return ufiSize;
        }

        public void MergeCodes()
        {
            uint epilogCount = 0;
            uint epilogCodeBytes = 0;
            var epilogIndex = unchecked((uint)ufiPrologCodes.Size());
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilogCount++;
                epilog.FinalizeCodes();
                var matchIndex = ufiPrologCodes.Match(epilog);
                if (matchIndex != -1)
                {
                    epilog.SetMatches();
                    epilog.SetStartIndex(matchIndex);
                    continue;
                }

                var matched = false;
                for (var previous = ufiEpilogList; previous != epilog; previous = previous!.epiNext)
                {
                    matchIndex = previous!.Match(epilog);
                    if (matchIndex != -1)
                    {
                        epilog.SetMatches();
                        epilog.SetStartIndex(previous.GetStartIndex() + matchIndex);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    epilog.SetStartIndex(unchecked((int)epilogIndex));
                    epilogCodeBytes = unchecked(epilogCodeBytes + (uint)epilog.Size());
                    epilogIndex = unchecked(epilogIndex + (uint)epilog.Size());
                }
            }

            var codeBytes = unchecked((uint)ufiPrologCodes.Size() + epilogCodeBytes);
            codeBytes = unchecked((codeBytes + 3) & ~3u);
            var codeWords = codeBytes / 4;
            ufiNeedExtendedCodeWordsEpilogCount =
                (codeWords > UW_MAX_CODE_WORDS_COUNT) || (epilogCount > UW_MAX_EPILOG_COUNT);

            var setEBit = false;
            var epilogScopes = epilogCount;
            if (epilogCount == 1)
            {
                var onlyEpilog = ufiEpilogList!;
                assert(onlyEpilog.epiNext is null);
                if (onlyEpilog.Matches() && (onlyEpilog.GetStartIndex() == 0)
                    && !ufiNeedExtendedCodeWordsEpilogCount && IsAtFragmentEnd(onlyEpilog))
                {
                    epilogScopes = 0;
                    setEBit = true;
                }
            }

            var headerBytes = unchecked((int)((1u
                + (ufiNeedExtendedCodeWordsEpilogCount ? 1u : 0u)
                + epilogScopes) * 4));
            ufiSize = unchecked((uint)headerBytes + codeBytes);
            ufiPrologCodes.SetFinalSize(headerBytes, unchecked((int)epilogCodeBytes));

            if (epilogCodeBytes != 0)
            {
                for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
                {
                    if (!epilog.Matches())
                    {
                        ufiPrologCodes.AppendEpilog(epilog);
                    }
                }
            }

            ufiSetEBit = setEBit;
            ufiCodeWords = codeWords;
            ufiEpilogScopes = epilogScopes;
        }

        public void Finalize(uint functionLength)
        {
#if TARGET_ARM
            noway_assert((functionLength & 1) == 0);
            var headerFunctionLength = functionLength / 2;
#elif TARGET_RISCV64
            noway_assert((functionLength & 1) == 0);
            var headerFunctionLength = functionLength / 2;
#else
            noway_assert((functionLength & 3) == 0);
            var headerFunctionLength = functionLength / 4;
#endif
            var headerEBit = ufiSetEBit ? 1u : 0u;
            var headerEpilogCount = ufiSetEBit ? unchecked((uint)ufiEpilogList!.GetStartIndex()) : ufiEpilogScopes;
            var headerCodeWords = ufiCodeWords;
            var headerExtendedEpilogCount = 0u;
            var headerExtendedCodeWords = 0u;

            if (!ufiSetEBit && ufiNeedExtendedCodeWordsEpilogCount)
            {
                headerEpilogCount = 0;
                headerCodeWords = 0;
                headerExtendedEpilogCount = ufiEpilogScopes;
                headerExtendedCodeWords = ufiCodeWords;
            }

            noway_assert(headerFunctionLength <= 0x3FFFF);
            if ((headerEpilogCount > UW_MAX_EPILOG_COUNT) || (headerCodeWords > UW_MAX_CODE_WORDS_COUNT))
            {
                IMPL_LIMITATION("unwind data too large");
            }

#if TARGET_ARM
            var headerFBit = ufiHasPhantomProlog ? 1u : 0u;
            var header = headerFunctionLength | (headerEBit << 21) | (headerFBit << 22)
                | (headerEpilogCount << 23) | (headerCodeWords << 28);
#else
            var header = headerFunctionLength | (headerEBit << 21)
                | (headerEpilogCount << 22) | (headerCodeWords << 27);
#endif
            ufiPrologCodes.AddHeaderWord(header);

            if (ufiNeedExtendedCodeWordsEpilogCount)
            {
                noway_assert(headerEBit == 0);
                noway_assert(headerEpilogCount == 0);
                noway_assert(headerCodeWords == 0);
                noway_assert((headerExtendedEpilogCount > UW_MAX_EPILOG_COUNT)
                    || (headerExtendedCodeWords > UW_MAX_CODE_WORDS_COUNT));
                if ((headerExtendedEpilogCount > UW_MAX_EXTENDED_EPILOG_COUNT)
                    || (headerExtendedCodeWords > UW_MAX_EXTENDED_CODE_WORDS_COUNT))
                {
                    IMPL_LIMITATION("unwind data too large");
                }

                ufiPrologCodes.AddHeaderWord(headerExtendedEpilogCount | (headerExtendedCodeWords << 16));
            }

            if (!ufiSetEBit)
            {
                for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
                {
                    assert(epilog.GetStartOffset() >= GetStartOffset());
                    var epilogStartOffset = unchecked(epilog.GetStartOffset() - GetStartOffset());
#if TARGET_ARM
                    noway_assert((epilogStartOffset & 1) == 0);
                    epilogStartOffset /= 2;
#elif TARGET_RISCV64
                    noway_assert((epilogStartOffset & 1) == 0);
                    epilogStartOffset /= 2;
#else
                    noway_assert((epilogStartOffset & 3) == 0);
                    epilogStartOffset /= 4;
#endif
                    var epilogStartIndex = unchecked((uint)epilog.GetStartIndex());
                    if ((epilogStartOffset > UW_MAX_EPILOG_START_OFFSET)
                        || (epilogStartIndex > UW_MAX_EPILOG_START_INDEX))
                    {
                        IMPL_LIMITATION("unwind data too large");
                    }

#if TARGET_ARM
                    const uint headerCondition = 0xE;
                    var epilogScopeWord = epilogStartOffset | (headerCondition << 20) | (epilogStartIndex << 24);
#else
                    var epilogScopeWord = epilogStartOffset | (epilogStartIndex << 22);
#endif
                    ufiPrologCodes.AddHeaderWord(epilogScopeWord);
                }
            }
        }

        public void Reserve(bool isFunclet, bool isHotCode)
        {
            MergeCodes();
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            assert(isHotCode || !isFunclet);
#endif
            m_compiler.unwindReserveEEInfo(isFunclet, !isHotCode, unchecked((int)Size()));
        }

        public unsafe void Allocate(CorJitFuncKind functionKind, void* hotCode, void* coldCode,
            uint functionEndOffset, bool isHotCode)
        {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            noway_assert(isHotCode || functionKind == CorJitFuncKind.CORJIT_FUNC_ROOT);
#endif
            var startOffset = GetStartOffset();
            var endOffset = ufiNext is null ? functionEndOffset : ufiNext.GetStartOffset();
            var eeStartOffset = startOffset;
            var eeEndOffset = endOffset;
            assert(endOffset > startOffset);
            Finalize(unchecked(endOffset - startOffset));
            var finalInfo = ufiPrologCodes.GetFinalInfo();

            if (isHotCode)
            {
#if DEBUG && TARGET_ARMARCH
                if ((JitConfig.JitFakeProcedureSplitting != 0) && (coldCode is not null))
                {
                    assert(endOffset <= unchecked((uint)m_compiler.info.compNativeCodeSize));
                }
                else
#endif
                {
                    assert(endOffset <= unchecked((uint)m_compiler.info.compTotalHotCodeSize));
                }

                coldCode = null;
            }
            else
            {
                assert(startOffset >= unchecked((uint)m_compiler.info.compTotalHotCodeSize));
                eeStartOffset = unchecked(startOffset - (uint)m_compiler.info.compTotalHotCodeSize);
                eeEndOffset = unchecked(endOffset - (uint)m_compiler.info.compTotalHotCodeSize);
            }

            fixed (byte* storage = finalInfo.Memory)
            {
#if DEBUG && (TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64)
                if (m_compiler.opts.dspUnwind)
                {
                    Globals.DumpUnwindInfo(m_compiler, isHotCode, startOffset, endOffset,
                        storage + finalInfo.Offset, unchecked((uint)finalInfo.Size));
                }
#endif
                m_compiler.unwindAllocEEInfo((byte*)hotCode, (byte*)coldCode,
                    unchecked((int)eeStartOffset), unchecked((int)eeEndOffset), finalInfo.Size,
                    storage + finalInfo.Offset, functionKind);
            }
        }

#if DEBUG && TARGET_RISCV64
        public void Dump(int indent = 0)
        {
            var epilogCount = 0;
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilogCount++;
            }

            var indentation = DebugIndent(indent);
            jitprintf($"{indentation}UnwindFragmentInfo #{ufiNum}, @{DebugReference(this)}:\n");
            jitprintf($"{indentation}  m_compiler: {DebugReference(m_compiler)}\n");
            jitprintf($"{indentation}  ufiNext: {DebugReference(ufiNext)}\n");
            jitprintf($"{indentation}  ufiEmitLoc: {DebugReference(ufiEmitLoc)}\n");
            jitprintf($"{indentation}  ufiHasPhantomProlog: {dspBool(ufiHasPhantomProlog)}\n");
            jitprintf($"{indentation}  {epilogCount} epilog{(epilogCount != 1 ? "s" : "")}\n");
            jitprintf($"{indentation}  ufiEpilogList: {DebugReference(ufiEpilogList)}\n");
            jitprintf($"{indentation}  ufiEpilogLast: {DebugReference(ufiEpilogLast)}\n");
            jitprintf($"{indentation}  ufiCurCodes: {DebugReference(ufiCurCodes)}\n");
            jitprintf($"{indentation}  ufiSize: {ufiSize}\n");
            jitprintf($"{indentation}  ufiSetEBit: {dspBool(ufiSetEBit)}\n");
            jitprintf($"{indentation}  ufiNeedExtendedCodeWordsEpilogCount: " +
                $"{dspBool(ufiNeedExtendedCodeWordsEpilogCount)}\n");
            jitprintf($"{indentation}  ufiCodeWords: {ufiCodeWords}\n");
            jitprintf($"{indentation}  ufiEpilogScopes: {ufiEpilogScopes}\n");
            jitprintf($"{indentation}  ufiStartOffset: 0x{ufiStartOffset:x}\n");
            jitprintf($"{indentation}  ufiInProlog: {dspBool(ufiInProlog)}\n");
            jitprintf($"{indentation}  ufiInitialized: 0x{ufiInitialized:x8}\n");

            ufiPrologCodes.Dump(indent + 2);
            for (var epilog = ufiEpilogList; epilog is not null; epilog = epilog.epiNext)
            {
                epilog.Dump(indent + 2);
            }
        }
#endif
    }
}
#endif

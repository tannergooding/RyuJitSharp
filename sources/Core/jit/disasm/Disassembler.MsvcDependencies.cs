// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if LATE_DISASM && (TARGET_XARCH || TARGET_ARM64)
using System.IO;

namespace RyuJitSharp;

public partial struct Disassembler
{
    // These are semantic keys. The external adapter must map DISX86/DISARM64's native enums,
    // preserve DIS::addrNil, and pass DIS::REGA unchanged (it is not the JIT's regNumber).
    internal enum MsTermination
    {
        Unknown,
        FallThrough,
        JmpShort,
        JmpCcShort,
        JmpNear,
        JmpCcNear,
        JmpFar,
        JmpInd,
        CallNear16,
        CallNear32,
        CallFar,
        CallInd,
        Trap,
        TrapCc,
        Bra,
        BraCase,
        BraCc,
        BraCcCase,
        BraCcInd,
        BraInd,
        Call,
        CallCc,
        CallCcInd,
        Other,
    }

    internal enum MsArchitecture
    {
        X86,
        X8664,
        Arm64,
    }

    internal readonly record struct MsInstruction(bool IsAddress, uint OperandCount,
        bool Operand1IsImmediate, bool Operand1IsAddress, ulong Operand1);

    // B527: DIS is a native C++ interface. This boundary does not pretend a managed struct
    // has its vtable, client-pointer lifetime, __stdcall callbacks, or secure CRT ABI.
    internal abstract unsafe class MsDisassembler
    {
        internal abstract MsTermination Termination { get; }
        internal abstract int NativeTermination { get; }
        internal abstract ulong Address { get; }
        internal abstract nuint InstructionSize { get; }
        internal abstract ulong Target { get; }
        internal abstract ulong NilAddress { get; }
        internal abstract ulong AddressAddress(uint operand);
        internal abstract bool Decode(out MsInstruction instruction);
        internal abstract nuint Disassemble(ulong address, byte* bytes, nuint size);
        internal abstract string FormatInstruction(nuint capacity);
        internal abstract string FormatBytes(nuint capacity, out nuint written);
        internal abstract string FormatAddress(ulong address, nuint capacity);
        internal abstract nuint FormatBytesMax();
        internal abstract void SetAddress64(bool enabled);
        internal abstract void SetClient(ref Disassembler client);
        internal abstract void SetAddressCallback(ref Disassembler client);
        internal abstract void SetFixupCallback(ref Disassembler client);
        internal abstract void SetRegisterRelativeCallback(ref Disassembler client);
        internal abstract void SetRegisterCallback(ref Disassembler client);
        internal abstract void Delete();
        internal abstract void WriteCallbackText(char* output, nuint capacity, string text);
        internal abstract string RegisterName(uint register);
        internal abstract string ReadDeferredFunctionName(ref Disassembler client);
        internal abstract int WriteAddressPrefix(StreamWriter output, string formattedAddress);
    }

    private static MsDisassembler? NewMsDisassembler(MsArchitecture architecture)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Late disassembly requires DIS::PdisNew and the MSVCDIS C++ callback ABI.");
    }
}
#endif

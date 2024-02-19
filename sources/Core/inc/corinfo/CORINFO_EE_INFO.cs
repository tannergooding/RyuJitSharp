// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// For some highly optimized paths, the JIT must generate code that directly
// manipulates internal EE data structures. The getEEInfo() helper returns
// this structure containing the needed offsets and values.
public struct CORINFO_EE_INFO
{
    // Information about the InlinedCallFrame structure layout
    public InlinedCallFrameInfo inlinedCallFrameInfo;

    //
    // Offsets into the Thread structure
    //

    // offset of the current Frame
    public uint offsetOfThreadFrame;

    // offset of the preemptive/cooperative state of the Thread
    public uint offsetOfGCState;

    // Delegate offsets
    public uint offsetOfDelegateInstance;

    public uint offsetOfDelegateFirstTarget;

    // Wrapper delegate offsets
    public uint offsetOfWrapperDelegateIndirectCell;

    // Reverse PInvoke offsets
    public uint sizeOfReversePInvokeFrame;

    // OS Page size
    public nuint osPageSize;

    // Null object offset
    public nuint maxUncheckedOffsetForNullObject;

    // Target ABI. Combined with target architecture and OS to determine
    // GC, EH, and unwind styles.
    public CORINFO_RUNTIME_ABI targetAbi;

    public CORINFO_OS osType;

    public struct InlinedCallFrameInfo
    {
        // Size of the Frame structure
        public uint size;

        public uint offsetOfGSCookie;

        public uint offsetOfFrameVptr;

        public uint offsetOfFrameLink;

        public uint offsetOfCallSiteSP;

        public uint offsetOfCalleeSavedFP;

        public uint offsetOfCallTarget;

        public uint offsetOfReturnAddress;

        // This offset is used only for ARM
        public uint offsetOfSPAfterProlog;
    }
}

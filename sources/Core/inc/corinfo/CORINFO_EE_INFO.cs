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
    public int offsetOfThreadFrame;

    // offset of the preemptive/cooperative state of the Thread
    public int offsetOfGCState;

    // Delegate offsets
    public int offsetOfDelegateInstance;

    public int offsetOfDelegateFirstTarget;

    // Wrapper delegate offsets
    public int offsetOfWrapperDelegateIndirectCell;

    // Reverse PInvoke offsets
    public int sizeOfReversePInvokeFrame;

    // OS Page size
    public nint osPageSize;

    // Null object offset
    public nint maxUncheckedOffsetForNullObject;

    // Target ABI. Combined with target architecture and OS to determine
    // GC, EH, and unwind styles.
    public CORINFO_RUNTIME_ABI targetAbi;

    public CORINFO_OS osType;

    public struct InlinedCallFrameInfo
    {
        // Size of the Frame structure
        public int size;

        // Size of the Frame structure when it also contains the secret stub arg
        public int sizeWithSecretStubArg;

        public int offsetOfFrameLink;

        public int offsetOfCallSiteSP;

        public int offsetOfCalleeSavedFP;

        public int offsetOfCallTarget;

        public int offsetOfReturnAddress;

        public int offsetOfSecretStubArg;

        // This offset is used only for ARM
        public int offsetOfSPAfterProlog;
    }
}

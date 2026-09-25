// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitAsyncResumeTable(uint numEntries, out uint dataSecOffs, out dataSection dataSec)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Async resume table allocation requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        var emittedSize = unchecked((uint)sizeof(CORINFO_AsyncResumeInfo) * numEntries);
        var secOffs = roundUp(emitConsDsc.dsdOffs, TARGET_POINTER_SIZE);
        emitConsDsc.dsdOffs = unchecked(secOffs + emittedSize);

        // Locations are captured now; their final code addresses are resolved
        // only when emitting the two-pointer CORINFO_AsyncResumeInfo entries.
        var secDesc = new dataSection
        {
            dsType = dataSection.sectionType.asyncResumeInfo,
            Locations = new emitLocation[numEntries],
            dsSize = emittedSize,
            dsAlignment = TARGET_POINTER_SIZE,
            dsOffset = secOffs,
            dsDataType = TYP_UNKNOWN,
        };

        if (emitConsDsc.dsdLast is dataSection last)
        {
            last.dsNext = secDesc;
        }
        else
        {
            emitConsDsc.dsdList = secDesc;
        }
        emitConsDsc.dsdLast = secDesc;

        dataSecOffs = secOffs;
        dataSec = secDesc;

        // Fetch the EE stub before later table display and materialization.
        if (emitAsyncResumeStub == NO_METHOD_HANDLE)
        {
            fixed (void** entryPoint = &emitAsyncResumeStubEntryPoint)
            {
                emitAsyncResumeStub = emitCmpHandle->getAsyncResumptionStub(entryPoint);
            }
        }
#endif
    }
}

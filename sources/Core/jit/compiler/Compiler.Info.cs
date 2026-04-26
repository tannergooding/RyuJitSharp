// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.Metrics;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct Info
    {
        public unsafe COMP_HANDLE compCompHnd;

        public unsafe CORINFO_MODULE_HANDLE compScopeHnd;

        public unsafe CORINFO_CLASS_HANDLE compClassHnd;

        public unsafe CORINFO_METHOD_HANDLE compMethodHnd;

        public unsafe CORINFO_METHOD_INFO* compMethodInfo;

        public bool hasCircularClassConstraints;

        public bool hasCircularMethodConstraints;

#if DEBUG || LATE_DISASM || DUMP_FLOWGRAPHS || DUMP_GC_TABLES
        public string compMethodName = "";

        public string compClassName = "";

        public string compFullName = "";

        // useful when debugging under SuperPMI
        public int compMethodSuperPMIIndex = -1;
#endif

#if DEBUG
        // Method hash is logically const, but computed on first demand.
        public uint compMethodHashPrivate;

        /// <summary>get hash code for currently jitted method</summary>
        /// <returns>Hash based on method's full name</returns>
        public uint compMethodHash()
        {
            if (compMethodHashPrivate == 0)
            {
                assert(compFullName is not null);
                assert(compFullName.Length != 0);

                // Use compFullName to generate the hash, as it contains the signature and return type
                var hash = (uint)(compFullName.GetHashCode(StringComparison.Ordinal));
                compMethodHashPrivate = hash;
            }
            return compMethodHashPrivate;
        }
#endif

        // The following holds the FLG_xxxx flags for the method we're compiling.
        public uint compFlags;

        // The following holds the class attributes for the method we're compiling.
        public uint compClassAttr;

        public unsafe byte* compCode;

        // The IL code size
        public IL_OFFSET compILCodeSize;         

        // Estimated amount of IL actually imported
        public IL_OFFSET compILImportSize;

        // The IL entry point (normally 0)
        public IL_OFFSET compILEntry;

        // Patchpoint data for OSR (normally nullptr)
        public unsafe PatchpointInfo* compPatchpointInfo;

        // The native code size, after instructions are issued.
        // This is less than (compTotalHotCodeSize + compTotalColdCodeSize) only if:
        //   (1) the code is not hot/cold split, and we issued less code than we expected, or
        //   (2) the code is hot/cold split, and we issued less code than we expected in the cold section (the hot section will always be padded out to compTotalHotCodeSize).
        public UNATIVE_OFFSET compNativeCodeSize;

        private byte _bitfield;

        // Is the method static (no 'this' pointer)?
        public bool compIsStatic
        {
            readonly get
            {
                return ((_bitfield  >> 0) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 0)) | ((value ? 1 : 0) << 0));
            }
        }

        // Does the method have varargs parameters?
        public bool compIsVarArgs
        {
            readonly get
            {
                return ((_bitfield >> 1) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 1)) | ((value ? 1 : 0) << 1));
            }
        }

        // Is the CORINFO_OPT_INIT_LOCALS bit set in the method info options?
        public bool compInitMem
        {
            readonly get
            {
                return ((_bitfield >> 2) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 2)) | ((value ? 1 : 0) << 2));
            }
        }

        // JIT inserted a profiler Enter callback
        public bool compProfilerCallback
        {
            readonly get
            {
                return ((_bitfield >> 3) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 3)) | ((value ? 1 : 0) << 3));
            }
        }

        // EAX captured in prolog will be available through an intrinsic
        public bool compPublishStubParam
        {
            readonly get
            {
                return ((_bitfield >> 4) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 4)) | ((value ? 1 : 0) << 4));
            }
        }

        // The NextCallReturnAddress intrinsic is used.
        public bool compHasNextCallRetAddr
        {
            readonly get
            {
                return ((_bitfield >> 5) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 5)) | ((value ? 1 : 0) << 5));
            }
        }

        // The AsyncCallContinuation intrinsic is used.
        public bool compUsesAsyncContinuation
        {
            readonly get
            {
                return ((_bitfield >> 6) & 0x1) != 0;
            }

            set
            {
                _bitfield = (byte)((_bitfield & ~(0x1 << 6)) | ((value ? 1 : 0) << 6));
            }
        }

        // Return type of the method as declared in IL (including SIMD normalization)
        public var_types compRetType;

        // Normalized return type as per target arch ABI
        public var_types compRetNativeType;

        // Number of arguments (incl. implicit but not hidden)
        public uint compILargsCount;

        // Number of arguments (incl. implicit and     hidden)
        public uint compArgsCount;

        // position of hidden return param var (0, 1) (BAD_VAR_NUM means not present);
        public uint compRetBuffArg;

        // position of hidden param for type context for generic code (CORINFO_CALLCONV_PARAMTYPE)
        public uint compTypeCtxtArg;

        // position of implicit this pointer param (not to be confused with lvaArg0Var)
        public uint compThisArg;

        // Number of vars : args + locals (incl. implicit but not hidden)
        public uint compILlocalsCount;

        // Number of vars : args + locals (incl. implicit and     hidden)
        public uint compLocalsCount;

        public uint compMaxStack;

        // Total number of bytes of Hot Code in the method
        public UNATIVE_OFFSET compTotalHotCodeSize = 0;

        // Total number of bytes of Cold Code in the method
        public UNATIVE_OFFSET compTotalColdCodeSize = 0;

        // count of unmanaged calls with GC transition.
        public uint compUnmanagedCallCountWithGCTransition;

        // The entry-point calling convention for this method.
        public CorInfoCallConvExtension compCallConv;

        // lclNum for the Frame root
        public uint compLvFrameListRoot = BAD_VAR_NUM;

        // Number of exception-handling clauses read in the method's IL.
        // You should generally use compHndBBtabCount instead: it is the current number of EH clauses (after additions like synchronized methods and funclets, and removals like unreachable code deletion).
        public uint compXcptnsCount;                   

        public Target.ArgOrder compArgOrder;

        // true if the VM is "matched": either the JIT is a cross-compiler and the VM expects that, or the JIT is a "self-host" compiler (e.g., x86 hosted targeting x86) and the VM expects that.
        public bool compMatchedVM = true;

        // The following holds IL scope information about local variables.

        public uint compVarScopesCount;

        public unsafe VarScopeDsc* compVarScopes;

        // The following holds information about instr offsets for which we need to report IP-mappings

        // sorted
        public unsafe IL_OFFSET* compStmtOffsets;

        public uint compStmtOffsetsCount;

        public ICorDebugInfo.BoundaryTypes compStmtOffsetsImplicit;

        // Number of class profile probes in this method
        public uint compHandleHistogramProbeCount;

#if TARGET_ARM64
        public bool compNeedsConsecutiveRegisters;
#endif

        public Info()
        {
        }
    }
}

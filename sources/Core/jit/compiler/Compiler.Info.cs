// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

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

        /// <summary>useful when debugging under SuperPMI</summary>
        public int compMethodSpmiIndex = -1;
#endif

#if DEBUG
        private int compMethodHashPrivate;

        /// <summary>get hash code for currently jitted method</summary>
        /// <returns>Hash based on method's full name</returns>
        public int compMethodHash()
        {
            if (compMethodHashPrivate == 0)
            {
                // Use compFullName to generate the hash, as it contains the signature and return type
                compMethodHashPrivate = compFullName.GetHashCode(StringComparison.Ordinal);
            }
            return compMethodHashPrivate;
        }
#endif

        /// <summary>The following holds the FLG_xxxx flags for the method we're compiling.</summary>
        public CorInfoFlag compFlags;

        /// <summary>The following holds the class attributes for the method we're compiling.</summary>
        public CorInfoFlag compClassAttr;

        public unsafe byte* compCode;

        /// <summary>The IL code size</summary>
        public IL_OFFSET compILCodeSize;

        /// <summary>Estimated amount of IL actually imported</summary>
        public IL_OFFSET compILImportSize;

        /// <summary>The IL entry point (normally 0)</summary>
        public IL_OFFSET compILEntry;

        /// <summary>Patchpoint data for OSR (normally null)</summary>
        public unsafe PatchpointInfo* compPatchpointInfo;

        /// <summary>The native code size, after instructions are issued.</summary>
        /// <remarks>
        ///   <para>This is less than (<see cref="compTotalHotCodeSize" /> + <see cref="compTotalColdCodeSize" />) only if:</para>
        ///   <list type="number">
        ///     <item>the code is not hot/cold split, and we issued less code than we expected, or</item>
        ///     <item>the code is hot/cold split, and we issued less code than we expected in the cold section (the hot section will always be padded out to <see cref="compTotalHotCodeSize" />).</item>
        ///   </list>
        /// </remarks>
        public NATIVE_OFFSET compNativeCodeSize;

        private InfoFlags _flags;

        /// <summary>Is the method static (no 'this' pointer)?</summary>
        public bool compIsStatic
        {
            readonly get
            {
                return (_flags & InfoFlags.IsStatic) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.IsStatic) | (value ? InfoFlags.IsStatic : InfoFlags.None);
            }
        }

        /// <summary>Does the method have varargs parameters?</summary>
        public bool compIsVarArgs
        {
            readonly get
            {
                return (_flags & InfoFlags.IsVarArgs) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.IsVarArgs) | (value ? InfoFlags.IsVarArgs : InfoFlags.None);
            }
        }

        /// <summary>Is the <see cref="CORINFO_OPT_INIT_LOCALS" /> bit set in the method info options?</summary>
        public bool compInitMem
        {
            readonly get
            {
                return (_flags & InfoFlags.InitMem) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.InitMem) | (value ? InfoFlags.InitMem : InfoFlags.None);
            }
        }

        /// <summary>JIT inserted a profiler Enter callback</summary>
        public bool compProfilerCallback
        {
            readonly get
            {
                return (_flags & InfoFlags.ProfilerCallback) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.ProfilerCallback) | (value ? InfoFlags.ProfilerCallback : InfoFlags.None);
            }
        }

        /// <summary>EAX captured in prolog will be available through an intrinsic</summary>
        public bool compPublishStubParam
        {
            readonly get
            {
                return (_flags & InfoFlags.PublishStubParam) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.PublishStubParam) | (value ? InfoFlags.PublishStubParam : InfoFlags.None);
            }
        }

        /// <summary>The NextCallReturnAddress intrinsic is used.</summary>
        public bool compHasNextCallRetAddr
        {
            readonly get
            {
                return (_flags & InfoFlags.HasNextCallRetAddr) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.HasNextCallRetAddr) | (value ? InfoFlags.HasNextCallRetAddr : InfoFlags.None);
            }
        }

        /// <summary>The AsyncCallContinuation intrinsic is used.</summary>
        public bool compUsesAsyncContinuation
        {
            readonly get
            {
                return (_flags & InfoFlags.UsesAsyncContinuation) != 0;
            }

            set
            {
                _flags = (_flags & ~InfoFlags.UsesAsyncContinuation) | (value ? InfoFlags.UsesAsyncContinuation : InfoFlags.None);
            }
        }

        /// <summary>Return type of the method as declared in IL (including simd normalization)</summary>
        public var_types compRetType;

        /// <summary>Normalized return type as per target arch ABI</summary>
        public var_types compRetNativeType;

        /// <summary>Number of arguments (incl. implicit but not hidden)</summary>
        public int compILargsCount;

        /// <summary>Number of arguments (incl. implicit and     hidden)</summary>
        public int compArgsCount;

        /// <summary>position of hidden return param var (0, 1) (BAD_VAR_NUM means not present);</summary>
        public int compRetBuffArg;

        /// <summary>position of hidden param for type context for generic code (<see cref="CORINFO_CALLCONV_PARAMTYPE" />)</summary>
        public int compTypeCtxtArg;

        /// <summary>position of implicit this pointer param (not to be confused with lvaArg0Var)</summary>
        public int compThisArg;

        /// <summary>Number of vars : args + locals (incl. implicit but not hidden)</summary>
        public int compILlocalsCount;

        /// <summary>Number of vars : args + locals (incl. implicit and     hidden)</summary>
        public int compLocalsCount;

        public int compMaxStack;

        /// <summary>Total number of bytes of Hot Code in the method</summary>
        public NATIVE_OFFSET compTotalHotCodeSize = 0;

        /// <summary>Total number of bytes of Cold Code in the method</summary>
        public NATIVE_OFFSET compTotalColdCodeSize = 0;

        /// <summary>count of unmanaged calls with GC transition.</summary>
        public int compUnmanagedCallCountWithGCTransition;

        /// <summary>The entry-point calling convention for this method.</summary>
        public CorInfoCallConvExtension compCallConv;

        /// <summary>lclNum for the Frame root</summary>
        public int compLvFrameListRoot = BAD_VAR_NUM;

        /// <summary>Number of exception-handling clauses read in the method's IL.</summary>
        /// <remarks>You should generally use compHndBBtabCount instead: it is the current number of EH clauses (after additions like synchronized methods and funclets, and removals like unreachable code deletion).</remarks>
        public ushort compXcptnsCount;                   

        public Target.ArgOrder compArgOrder;

        /// <summary>true if the VM is "matched": either the JIT is a cross-compiler and the VM expects that, or the JIT is a "self-host" compiler (e.g., x86 hosted targeting x86) and the VM expects that.</summary>
        public bool compMatchedVM = true;

        // The following holds IL scope information about local variables.

        public int compVarScopesCount;

        public VarScopeDsc[] compVarScopes = [];

        // The following holds information about instr offsets for which we need to report IP-mappings

        // sorted
        public IL_OFFSET[] compStmtOffsets = [];

        public int compStmtOffsetsCount;

        public ICorDebugInfo.BoundaryTypes compStmtOffsetsImplicit;

        /// <summary>Number of class profile probes in this method</summary>
        public int compHandleHistogramProbeCount;

#if TARGET_ARM64
        public bool compNeedsConsecutiveRegisters;
#endif

        public Info()
        {
        }
    }
}

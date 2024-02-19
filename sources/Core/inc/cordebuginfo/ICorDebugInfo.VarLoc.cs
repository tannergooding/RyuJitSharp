// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct ICorDebugInfo
{
    public struct VarLoc
    {
        public VarLocType vlType;

        private _Anonymous_e__Union _anonymous;

        [UnscopedRef]
        public ref vlReg vlReg => ref _anonymous.vlReg;

        [UnscopedRef]
        public ref vlStk vlStk => ref _anonymous.vlStk;

        [UnscopedRef]
        public ref vlRegReg vlRegReg => ref _anonymous.vlRegReg;

        [UnscopedRef]
        public ref vlRegStk vlRegStk => ref _anonymous.vlRegStk;

        [UnscopedRef]
        public ref vlStkReg vlStkReg => ref _anonymous.vlStkReg;

        [UnscopedRef]
        public ref vlStk2 vlStk2 => ref _anonymous.vlStk2;

        [UnscopedRef]
        public ref vlFPstk vlFPstk => ref _anonymous.vlFPstk;

        [UnscopedRef]
        public ref vlFixedVarArg vlFixedVarArg => ref _anonymous.vlFixedVarArg;

        [UnscopedRef]
        public ref vlMemory vlMemory => ref _anonymous.vlMemory;


        [StructLayout(LayoutKind.Explicit)]
        private struct _Anonymous_e__Union
        {
            [FieldOffset(0)]
            public vlReg vlReg;

            [FieldOffset(0)]
            public vlStk vlStk;

            [FieldOffset(0)]
            public vlRegReg vlRegReg;

            [FieldOffset(0)]
            public vlRegStk vlRegStk;

            [FieldOffset(0)]
            public vlStkReg vlStkReg;

            [FieldOffset(0)]
            public vlStk2 vlStk2;

            [FieldOffset(0)]
            public vlFPstk vlFPstk;

            [FieldOffset(0)]
            public vlFixedVarArg vlFixedVarArg;

            [FieldOffset(0)]
            public vlMemory vlMemory;
        }
    }
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Globals
{
    public const int LF_GC = 0x00000001;
    public const int LF_GCINFO = 0x00000002;
    public const int LF_STUBS = 0x00000004;
    public const int LF_JIT = 0x00000008;
    public const int LF_LOADER = 0x00000010;
    public const int LF_METADATA = 0x00000020;
    public const int LF_SYNC = 0x00000040;
    public const int LF_EEMEM = 0x00000080;
    public const int LF_GCALLOC = 0x00000100;
    public const int LF_CORDB = 0x00000200;
    public const int LF_CLASSLOADER = 0x00000400;
    public const int LF_CORPROF = 0x00000800;
    public const int LF_DIAGNOSTICS_PORT = 0x00001000;
    public const int LF_DBGALLOC = 0x00002000;
    public const int LF_EH = 0x00004000;
    public const int LF_ENC = 0x00008000;
    public const int LF_ASSERT = 0x00010000;
    public const int LF_VERIFIER = 0x00020000;
    public const int LF_THREADPOOL = 0x00040000;
    public const int LF_GCROOTS = 0x00080000;
    public const int LF_INTEROP = 0x00100000;
    public const int LF_MARSHALER = 0x00200000;
    public const int LF_TIEREDCOMPILATION = 0x00400000;
    public const int LF_ZAP = 0x00800000;
    public const int LF_STARTUP = 0x01000000;
    public const int LF_APPDOMAIN = 0x02000000;
    public const int LF_CODESHARING = 0x04000000;
    public const int LF_STORE = 0x08000000;
    public const int LF_SECURITY = 0x10000000;
    public const int LF_LOCKS = 0x20000000;
    public const int LF_BCL = 0x40000000;
}
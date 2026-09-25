// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.IS_INFO;

using System;

namespace RyuJitSharp;

[Flags]
public enum IS_INFO
{
    IS_NONE = 0,                            // no scheduling information
    IS_R1_RD = 1 << 0,                      // has a reg1  op that is read-only
    IS_R1_WR = 1 << 1,                      // has a reg1  op that is write-only
    IS_R1_RW = 1 << 2,                      // has a reg1  op that is read-write
    IS_R2_RD = 1 << 3,                      // has a reg2  op that is read-only
    IS_R2_WR = 1 << 4,                      // has a reg2  op that is write-only
    IS_R2_RW = 1 << 5,                      // has a reg2  op that is read-write
    IS_R3_RD = 1 << 6,                      // has a reg3  op that is read-only
    IS_R3_WR = 1 << 7,                      // has a reg3  op that is write-only
    IS_R3_RW = 1 << 8,                      // has a reg3  op that is read-write
    IS_R4_RD = 1 << 9,                      // has a reg4  op that is read-only
    IS_R4_WR = 1 << 10,                     // has a reg4  op that is write-only
    IS_R4_RW = 1 << 11,                     // has a reg4  op that is read-write
    IS_GM_RD = 1 << 12,                     // has a [mem] op that is read-only
    IS_GM_WR = 1 << 13,                     // has a [mem] op that is write-only
    IS_GM_RW = 1 << 14,                     // has a [mem] op that is read-write
    IS_SF_RD = 1 << 15,                     // has a [stk] op that is read-only
    IS_SF_WR = 1 << 16,                     // has a [stk] op that is write-only
    IS_SF_RW = 1 << 17,                     // has a [stk] op that is read-write
    IS_AM_RD = 1 << 18,                     // has a [adr] op that is read-only
    IS_AM_WR = 1 << 19,                     // has a [adr] op that is write-only
    IS_AM_RW = 1 << 20,                     // has a [adr] op that is read-write
}
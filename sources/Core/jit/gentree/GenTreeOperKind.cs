// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.GenTreeOperKind;
using System;

namespace RyuJitSharp;

// The following enum defines a set of bit flags that can be used
// to classify expression tree nodes.
[Flags]
public enum GenTreeOperKind : byte
{
    // special operator
    GTK_SPECIAL = 0x00, 

    // leaf    operator
    GTK_LEAF = 0x01, 

    // unary   operator
    GTK_UNOP = 0x02,

    // binary  operator
    GTK_BINOP = 0x04,

    // operator kind mask
    GTK_KINDMASK = (GTK_SPECIAL | GTK_LEAF | GTK_UNOP | GTK_BINOP),

    GTK_SMPOP = (GTK_UNOP | GTK_BINOP),

    // commutative  operator
    GTK_COMMUTE = 0x08,

    // Indicates that an oper for a node type that extends GenTreeOp (or GenTreeUnOp) by adding non-node fields to unary or binary operator.
    GTK_EXOP = 0x10,

    // node does not produce a value
    GTK_NOVALUE = 0x20,

    // node represents a store
    GTK_STORE = 0x40,

    GTK_MASK = 0xFF
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    public struct lvaStructPromotionInfo
    {
        public unsafe CORINFO_CLASS_HANDLE typeHnd;
        public bool canPromote;
        public bool containsHoles;
        public bool fieldsSorted;
        public byte fieldCnt;
        public fieldsInlineArray fields;

        public unsafe lvaStructPromotionInfo(CORINFO_CLASS_HANDLE typeHnd)
        {
            this.typeHnd = typeHnd;
        }

        [InlineArray(MAX_NumOfFieldsInPromotableStruct)]
        public struct fieldsInlineArray
        {
            public lvaStructFieldInfo e0;
        }
    }
}

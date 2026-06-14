// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
    private ref partial struct MaskDataWalker : IGenTreeVisitor<MaskDataWalker>
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly Stack<GenTree> _ancestors;
        private readonly ref optCSE_MaskData _maskData;

        public MaskDataWalker(Compiler compiler, ref optCSE_MaskData maskData)
        {
            _compiler = compiler;
            _ancestors = [];
            _maskData = ref maskData;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            var tree = use;

            if (IS_CSE_INDEX(tree._cseNum))
            {
                assert(_compiler.cseMaskTraits is not null);
                var cseIndex = GET_CSE_INDEX(tree._cseNum);

                // Note that we DO NOT use getCSEAvailBit() here, for the CSE_defMask/CSE_useMask
                var cseBit = genCseNum2Bit(cseIndex);

                if (IS_CSE_DEF(tree._cseNum))
                {
                    BitVecOps.AddElemD(_compiler.cseMaskTraits, _maskData.CSE_defMask, cseBit);
                }
                else
                {
                    BitVecOps.AddElemD(_compiler.cseMaskTraits, _maskData.CSE_useMask, cseBit);
                }
            }
            return fgWalkResult.WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user) => IGenTreeVisitor<MaskDataWalker>.WalkTree(ref this, ref use, user, _ancestors);
    }
}

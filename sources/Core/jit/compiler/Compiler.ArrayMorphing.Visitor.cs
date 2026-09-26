// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, morph.cpp.

using System;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct MorphMDArrayVisitor(Compiler compiler, MorphMDArrayTempCache tempCache
#if DEBUG
        , BasicBlock block
#endif
    )
        : IGenTreeVisitor<MorphMDArrayVisitor>
    {
        private readonly GenTreeStack _ancestors = [];

        public static bool DoPostOrder => true;

        public bool Changed { get; private set; }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if (use is not GenTreeArrElem arrElem)
            {
                return WALK_CONTINUE;
            }

#if DEBUG
            JITDUMP($"Morphing GT_ARR_ELEM [{arrElem.TreeId:D6}] in {FMT_BB(block.bbNum)} of '{compiler.info.compFullName}'\n");
#endif
            DISPTREE(arrElem);

            assert((arrElem.ArrRank >= 2) && (arrElem.ArrRank <= GenTreeArrElem.MaxRank));
            assert(arrElem.ArrObj.Type is TYP_REF);
            assert(arrElem.Type is TYP_BYREF);
            var rank = arrElem.ArrRank;
            var indices = arrElem.ArrInds;
            GenTree[] indicesToUse = new GenTree[GenTreeArrElem.MaxRank];
            Span<int> indicesToCopy = stackalloc int[GenTreeArrElem.MaxRank];
            var anyIndexWithSideEffects = false;

            // Index expressions must run before the first array metadata access.
            for (var i = 0; i < rank; i++)
            {
                var index = indices[i];
                assert(index.Type.ActualType is TYP_INT);
                if ((index.Flags & GTF_ALL_EFFECT) == 0)
                {
                    indicesToUse[i] = index;
                    indicesToCopy[i] = BAD_VAR_NUM;
                }
                else
                {
                    var indexLocal = tempCache.GrabTemp(index.Type);
                    indicesToUse[i] = compiler.gtNewLclvNode(index.Type.ActualType, indexLocal);
                    indicesToCopy[i] = indexLocal;
                    anyIndexWithSideEffects = true;
                }
            }

            var array = arrElem.ArrObj;
            var arrayLocal = array.Oper is GT_LCL_VAR
                ? array.AsLclVar().LclNum
                : tempCache.GrabTemp(TYP_REF);
            var newArrayLocal = array.Oper is GT_LCL_VAR ? BAD_VAR_NUM : arrayLocal;

            GenTree? fullTree = null;
            for (var i = 0; i < rank; i++)
            {
                var index = indicesToUse[i];
                assert((index.Flags & GTF_ALL_EFFECT) == 0);
                var lowerBound = compiler.gtNewMDArrLowerBound(
                    compiler.gtNewLclvNode(TYP_REF, arrayLocal), i, rank);
                var effectiveIndexLocal = tempCache.GrabTemp(TYP_INT);
                var effectiveIndex = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, index, lowerBound);
                var effectiveIndexStore = compiler.gtNewTempStore(effectiveIndexLocal, effectiveIndex);
                var length = compiler.gtNewMDArrLen(
                    compiler.gtNewLclvNode(TYP_REF, arrayLocal), i, rank);
                var boundsCheck = new GenTreeBoundsChk(
                    compiler.gtNewLclvNode(TYP_INT, effectiveIndexLocal), length, SCK_RNGCHK_FAIL);
                var checkedIndex = compiler.gtNewCommaNode(TYP_INT, boundsCheck,
                    compiler.gtNewLclvNode(TYP_INT, effectiveIndexLocal));
                var indexComma = compiler.gtNewCommaNode(TYP_INT, effectiveIndexStore, checkedIndex);

                if (i > 0)
                {
                    assert(fullTree is not null);
                    var scaleLength = compiler.gtNewMDArrLen(
                        compiler.gtNewLclvNode(TYP_REF, arrayLocal), i, rank);
                    var scaled = compiler.gtNewBinaryNode(GT_MUL, TYP_INT, fullTree, scaleLength);
                    fullTree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, scaled, indexComma);
                }
                else
                {
                    fullTree = indexComma;
                }
            }

            assert(fullTree is not null);
#if TARGET_64BIT
            fullTree = compiler.gtNewCastNode(TYP_I_IMPL, fullTree, true, TYP_I_IMPL);
#else
            throw new PlatformNotSupportedException("MD-array morphing requires the Windows AMD64 path.");
#endif
            var elementSize = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)(uint)arrElem.ArrElemSize));
            var scale = compiler.gtNewBinaryNode(GT_MUL, TYP_I_IMPL, fullTree, elementSize);
            var dataOffset = compiler.gtNewIconNode(TYP_I_IMPL, eeGetMDArrayDataOffset(rank));
            var scalePlusOffset = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, scale, dataOffset);
            GenTree expansion = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, scalePlusOffset,
                compiler.gtNewLclvNode(TYP_REF, arrayLocal));

            if (anyIndexWithSideEffects)
            {
                for (var i = rank; i > 0; i--)
                {
                    if (indicesToCopy[i - 1] != BAD_VAR_NUM)
                    {
                        var store = compiler.gtNewTempStore(indicesToCopy[i - 1], indices[i - 1]);
                        expansion = compiler.gtNewCommaNode(expansion.Type, store, expansion);
                    }
                }
            }

            if (newArrayLocal != BAD_VAR_NUM)
            {
                var store = compiler.gtNewTempStore(newArrayLocal, array);
                expansion = compiler.gtNewCommaNode(expansion.Type, store, expansion);
            }

            JITDUMP("fgMorphArrayOpsStmt (before remorph):\n");
            DISPTREE(expansion);
            use = expansion;
            Changed = true;
            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<MorphMDArrayVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}

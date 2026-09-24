// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private GenTree? LowerArrLength(GenTreeArrCommon node)
    {
        var array = node.ArrRef;
        int offset;
        switch (node.Oper)
        {
            case GT_ARR_LENGTH:
            {
                offset = node.AsArrLen().ArrLenOffset;
                assert(offset is OFFSETOF__CORINFO_Array__length or OFFSETOF__CORINFO_String__stringLen);
                break;
            }

            case GT_MDARR_LENGTH:
            {
                var mdArray = node.AsMDArr();
                offset = Compiler.eeGetMDArrayLengthOffset(mdArray.Rank, mdArray.Dim);
                break;
            }

            case GT_MDARR_LOWER_BOUND:
            {
                var mdArray = node.AsMDArr();
                offset = Compiler.eeGetMDArrayLowerBoundOffset(mdArray.Rank, mdArray.Dim);
                break;
            }

            default:
            {
                unreached();
                throw new System.InvalidOperationException("Expected an array metadata node.");
            }
        }

        assert(array.Next == node);
        GenTree address;
        if ((array.Oper is GT_CNS_INT) && (array.AsIntCon().IconValue == 0))
        {
            // Preserve the null fault without introducing an ADD of two constants.
            address = array;
        }
        else
        {
            var constant = CompilerInstance.gtNewIconNode(TYP_I_IMPL, offset);
            address = CompilerInstance.gtNewBinaryNode(GT_ADD, TYP_BYREF, array, constant);
            BlockRange().InsertAfter(array, constant, address);
        }

        var indirection = new GenTreeIndir(GT_IND, node.Type, address, null, node, NodeThreading.LIR);
        BlockRange().ReplaceNode(node, indirection);
        return array.Next;
    }
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class LinearScan
{
    private void writeRegisters(RefPosition currentRefPosition, GenTree tree)
    {
        LsraGlobals.lsraAssignRegToTree(tree, currentRefPosition.assignedReg(), currentRefPosition.getMultiRegIdx());
    }

    private void insertCopyOrReload(BasicBlock block, GenTree tree, uint multiRegIdx, RefPosition refPosition)
    {
#if !((TARGET_AMD64 && WINDOWS_AMD64_ABI) || TARGET_ARM64)
        throw new FatalJitException("LSRA copy or reload insertion is not ported for this target.");
#else
        var foundUse = block.TryGetUse(tree, out var treeUse);
        assert(foundUse);
        if (!foundUse)
        {
            throw new FatalJitException("Copy or reload requires an owning LIR use.");
        }

        var parent = treeUse.User();
        var oper = refPosition.reload ? GT_RELOAD : GT_COPY;
#if DEBUG
        if (!refPosition.reload)
        {
            updateLsraStat(LsraStat.STAT_COPY_REG, checked((uint)block.bbNum));
        }
#endif

        if (parent.Oper.IsCopyOrReload)
        {
            if (!tree.IsMultiRegNode)
            {
                throw new FatalJitException("A grouped LSRA copy or reload requires a multi-register source.");
            }

            var copyOrReload = parent.AsCopyOrReload();
            if (copyOrReload.GetRegNumByIdx(checked((byte)multiRegIdx)) != REG_NA)
            {
                throw new FatalJitException("The LSRA copy or reload register is already assigned.");
            }

            copyOrReload.SetRegNumByIdx(refPosition.assignedReg(), checked((byte)multiRegIdx));
        }
        else
        {
            var regType = tree.Type;
            if ((regType is TYP_STRUCT) && !tree.IsMultiRegNode)
            {
                assert(_compiler.compEnregStructLocals);
                assert(tree.Oper.IsLocal);
                var local = tree.AsLclVarCommon();
                regType = _compiler.lvaGetDesc(local.LclNum).GetRegisterType(local);
                assert(regType is not TYP_UNDEF);
            }

            var newNode = new GenTreeCopyOrReload(oper, regType, tree);
            assert(refPosition.registerAssignment != SRBM_NONE);
#if DEBUG
            newNode._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
            newNode.SetRegNumByIdx(refPosition.assignedReg(), checked((byte)multiRegIdx));
            if (refPosition.copyReg)
            {
                assert(isCandidateLocalRef(tree) || tree.IsMultiRegLclVar);
                newNode.SetLastUse(checked((int)multiRegIdx), true);
            }

            block.InsertAfter(tree, newNode);
            treeUse.ReplaceWith(newNode);
        }
#endif
    }
}

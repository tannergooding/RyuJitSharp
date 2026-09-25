// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class LinearScan
{
    private void writeRegisters(RefPosition currentRefPosition, GenTree tree)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
        throw new FatalJitException("LSRA register writeback is not ported for this target.");
#else
        var reg = currentRefPosition.assignedReg();
        var regIdx = currentRefPosition.getMultiRegIdx();

        if (regIdx == 0)
        {
            tree.RegNum = reg;
        }
        else if (tree.Oper is GT_COPY)
        {
            assert(regIdx == 1);
            tree.AsCopyOrReload().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
        else if (tree.Oper is GT_HWINTRINSIC)
        {
            tree.AsHWIntrinsic().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
        else if (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
        {
            tree.AsLclVar().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
        else
        {
            // The remaining native case is a multi-register call. Windows AMD64 disables
            // FEATURE_MULTIREG_RET and permits only one call return register.
            throw new FatalJitException("Unsupported multi-register LSRA writeback node.");
        }
#endif
    }

    private void insertCopyOrReload(BasicBlock block, GenTree tree, uint multiRegIdx, RefPosition refPosition)
    {
#if !TARGET_AMD64 || UNIX_AMD64_ABI
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

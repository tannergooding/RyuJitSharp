// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genUpdateVarReg(ref LclVarDsc local, GenTree tree)
    {
        assert((tree.Oper.IsScalarLocal && !tree.IsMultiRegLclVar) || (tree.Oper is GT_COPY));
        local.RegNum = tree.RegNum;
    }

    public void genUpdateVarReg(ref LclVarDsc local, GenTree tree, byte registerIndex)
    {
        assert(_compiler.lvaEnregMultiRegVars);
        assert(tree.IsMultiRegLclVar || (tree.Oper is GT_COPY));
        local.RegNum = tree.GetRegByIndex(registerIndex);
    }

#if !TARGET_WASM
    public regMaskTP genGetRegMask(in LclVarDsc local)
    {
        assert(local.lvIsInReg);
        var reg = local.RegNum;
#if TARGET_ARM
        if (reg.IsFltReg)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "ARM floating register-variable masks are not implemented.");
        }
#endif
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    public regMaskTP genGetRegMask(GenTree tree)
    {
        assert(tree.Oper is GT_LCL_VAR);
        var mask = RBM_NONE;
        ref var local = ref _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum);
        if (local.lvPromoted)
        {
            for (var index = local.lvFieldLclStart; index < local.lvFieldLclStart + local.lvFieldCnt; index++)
            {
                ref var field = ref _compiler.lvaGetDesc(index);
                noway_assert(field.lvIsStructField);
                if (field.lvIsInReg)
                {
                    mask |= genGetRegMask(in field);
                }
            }
        }
        else if (local.lvIsInReg)
        {
            mask = genGetRegMask(in local);
        }

        return mask;
    }
#endif

    public void genUpdateRegLife(in LclVarDsc local, bool isBorn, bool isDying
#if DEBUG
        , GenTree? tree
#endif
    )
    {
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        var mask = genGetRegMask(in local);
#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf($"\t\t\t\t\t\t\tV{_compiler.lvaGetLclNum(in local):D2} in reg ");
            local.PrintVarReg();
            jitprintf($" is becoming {(isDying ? "dead" : "live")}  ");
            Compiler.printTreeId(tree);
            jitprintf("\n");
        }
#endif

        // A minopts dead store may be born and dying at the same node.
        if (isDying)
        {
            _regSet.RemoveMaskVars(mask);
        }
        else
        {
            assert(local.IsAlwaysAliveInMemory || (_regSet.GetMaskVars() & mask).IsEmpty);
            _regSet.AddMaskVars(mask);
        }
#endif
    }
}

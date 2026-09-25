// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private TreeLifeUpdater? treeLifeUpdater;
    private nint[] genLastLiveSet = [];
    private regMaskTP genLastLiveMask;
    private List<EmittedCallReturnInfo>? emittedCallReturnInfo;
    private BasicBlock? genPendingCallLabel;

    private struct EmittedCallReturnInfo
    {
        public IL_OFFSET callILOffset;
        public emitLocation returnLocation;
        public siVarLoc returnValueLoc;
    }

    public void genPrepForCompiler()
    {
        treeLifeUpdater = new TreeLifeUpdater(_compiler, forCodeGen: true);
        _gcInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(_compiler);

        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);

            if (varDsc.lvTracked || varDsc.lvIsRegCandidate)
            {
                if (!varDsc.lvRegister && _compiler.lvaIsGCTracked(in varDsc))
                {
                    VarSetOps.AddElemD(_compiler, _gcInfo.gcTrkStkPtrLcls, varDsc._varIndex);
                }
            }
        }

        genLastLiveSet = VarSetOps.MakeEmpty(_compiler);
        genLastLiveMask = RBM_NONE;
        _compiler.Metrics.BasicBlocksAtCodegen = _compiler.fgBBcount;
    }

    public void genInitializeRegisterState()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Code-generation register initialization outside AMD64 is not implemented.");
#else
        _regSet.rsSpillBeg();

        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            ref var varDsc = ref _compiler.lvaGetDesc(varNum);
            if (!varDsc.lvIsParam || !varDsc.lvRegister)
            {
                continue;
            }

            assert(_compiler.fgFirstBB is not null);
            if (!VarSetOps.IsMember(_compiler, _compiler.fgFirstBB.bbLiveIn, varDsc._varIndex))
            {
                continue;
            }

            if (varDsc.IsAddressExposed)
            {
                continue;
            }

            var reg = varDsc.RegNum;
            if (genIsValidIntReg(reg))
            {
                _regSet.verifyRegUsed(reg);
            }
        }
#endif
    }

    public void genInitialize()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Block-list code-generation initialization outside AMD64 is not implemented.");
#else
        if (_compiler.opts.compScopeInfo)
        {
            siInit();
        }

        initializeVariableLiveKeeper();
        emittedCallReturnInfo = [];
        genPendingCallLabel = null;
        _gcInfo.gcRegPtrSetInit();
        _gcInfo.gcVarPtrSetInit();
        genInitializeRegisterState();
        _compiler.compCurLife = VarSetOps.MakeEmpty(_compiler);
        SetStackLevel(0);
#endif
    }

    public void genUpdateLife(GenTree tree)
    {
        assert(treeLifeUpdater is not null);
        treeLifeUpdater.UpdateLife(tree, generalLclAddrHandling: false);
    }

    private void SetStackLevel(uint newStackLevel)
    {
#if DEBUG
        if ((genStackLevel != newStackLevel) && _compiler.verbose)
        {
            jitprintf($"Setting stack level from {unchecked((int)genStackLevel)} to {unchecked((int)newStackLevel)}\n");
        }
#endif
        genStackLevel = newStackLevel;
    }
}

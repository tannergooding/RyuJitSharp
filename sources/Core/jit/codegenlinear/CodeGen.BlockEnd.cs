// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEmitEndBlock(BasicBlock block)
    {
        var emitNopBeforeEHRegion = false;
        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
            // Keep return addresses inside their EH region when an eliminated jump follows a call.
            // Calls immediately before OS epilogs are handled separately by the emitter.
            if (Emitter.emitIsLastInsCall() &&
                (block.IsLast || !BasicBlock.sameEHRegion(block, block.Next)))
            {
                switch (block.Kind)
                {
                    case BBJ_ALWAYS:
                    {
                        emitNopBeforeEHRegion = true;
                        break;
                    }

                    case BBJ_THROW:
                    case BBJ_CALLFINALLY:
                    case BBJ_EHCATCHRET:
                    case BBJ_RETURN:
                    case BBJ_EHFINALLYRET:
                    case BBJ_EHFAULTRET:
                    case BBJ_EHFILTERRET:
                    {
                        break;
                    }

                    case BBJ_COND:
                    case BBJ_SWITCH:
                    default:
                    {
                        assert(false, conditionExpression: "Unexpected block kind after a call at an EH boundary.");
                        break;
                    }
                }
            }
        }
        void SetLoopAlignBackEdge(BasicBlock source, BasicBlock target)
        {
            if (target.isLoopAlign && Emitter.emitSetLoopBackEdge(target) && !source.IsLast)
            {
                // End the backedge group here so later instructions do not inflate the loop size.
                JITDUMP($"Mark {FMT_BB(source.Next.bbNum)} as label: alignment end-of-loop\n");
                source.Next.SetFlags(BBF_HAS_LABEL);
            }
        }
        var removedJmp = false;
        switch (block.Kind)
        {
            case BBJ_RETURN:
            {
                genExitCode(block);
                break;
            }

            case BBJ_THROW:
            {
                if (Configuration.TARGET_WASM)
                {
                    Emitter.emitIns(INS_unreachable);
                    if (block.IsLast || _compiler.bbIsFuncletBeg(block.Next))
                    {
                        genEmitFunctionEndWasm(emitTerminalUnreachable: false);
                    }
                }
                else
                {
                    // The unreachable breakpoint keeps the unwinder's return address in the right region.
                    if (block.IsLast || !BasicBlock.sameEHRegion(block, block.Next) ||
                        (!IsFramePointerUsed && _compiler.fgIsThrowHlpBlk(block.Next)) ||
                        _compiler.bbIsFuncletBeg(block.Next) || block.IsLastHotBlock(_compiler))
                    {
                        instGen(INS_BREAKPOINT);
                    }
                    else
                    {
                        var call = block.LastNode;
                        if ((call is GenTreeCall callNode) && callNode.IsNoReturn)
                        {
                            instGen(INS_BREAKPOINT);
                        }
                    }
                }
                break;
            }

            case BBJ_CALLFINALLY:
            {
                genCallFinally(block);
                break;
            }

            case BBJ_EHCATCHRET:
            {
                genEHCatchRet(block);
                goto case BBJ_EHFINALLYRET;
            }

            case BBJ_EHFINALLYRET:
            case BBJ_EHFAULTRET:
            case BBJ_EHFILTERRET:
            {
                genReserveFuncletEpilog(block);
                break;
            }

            case BBJ_SWITCH:
            {
                break;
            }

            case BBJ_ALWAYS:
            {
                if (Configuration.DEBUG)
                {
                    var call = block.LastNode;
                    if (call is GenTreeCall callNode)
                    {
                        assert(!callNode.IsNoReturn);
                    }
                }
                if (block.CanRemoveJumpToNext(_compiler))
                {
                    if (emitNopBeforeEHRegion)
                    {
                        instGen(INS_nop);
                    }
                    if (Configuration.FEATURE_LOOP_ALIGN)
                    {
                        removedJmp = true;
                    }
                    break;
                }
                if (Configuration.TARGET_WASM)
                {
                    inst_JMP_Wasm(EJ_jmp, block.Target);
                }
                else if (Configuration.TARGET_XARCH)
                {
                    var isRemovableJmpCandidate = !_compiler.fgInDifferentRegions(block, block.Target);
                    inst_JMP(EJ_jmp, block.Target, isRemovableJmpCandidate);
                }
                else
                {
                    inst_JMP(EJ_jmp, block.Target);
                }
                if (Configuration.FEATURE_LOOP_ALIGN)
                {
                    SetLoopAlignBackEdge(block, block.Target);
                }
                if (Configuration.TARGET_WASM && (block.IsLast || _compiler.bbIsFuncletBeg(block.Next)))
                {
                    genEmitFunctionEndWasm(emitTerminalUnreachable: true);
                }
                break;
            }

            case BBJ_COND:
            {
                if (Configuration.FEATURE_LOOP_ALIGN)
                {
                    SetLoopAlignBackEdge(block, block.TrueTarget);
                    SetLoopAlignBackEdge(block, block.FalseTarget);
                }
                break;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage095
                );
                return;
            }
        }
        if (Configuration.FEATURE_LOOP_ALIGN)
        {
            if (block.hasAlign)
            {
                assert(ShouldAlignLoops);
                assert(!block.isBBCallFinallyPairTail);
                assert(block.Kind != BBJ_CALLFINALLY);
                Emitter.emitLoopAlignment(
                    (block.Kind == BBJ_ALWAYS) && !removedJmp
                    );
            }

            if (!block.IsLast && block.Next.isLoopAlign && _compiler.opts.compJitHideAlignBehindJmp)
            {
                Emitter.emitConnectAlignInstrWithCurIG();
            }
        }
    }

    private void inst_JMP_Wasm(emitJumpKind jump, BasicBlock target)
    {
        var instruction = Emitter.emitJumpKindToIns(jump);
        var depth = unchecked(findTargetDepth(target) + wasmExtraControlFlowDepth);
        GetEmitter().emitIns_J(instruction, EA_4BYTE, depth, target);
    }
}

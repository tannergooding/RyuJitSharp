// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genEmitEndBlock(BasicBlock block)
    {
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
        var emitNopBeforeEHRegion = false;
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
#endif
#if FEATURE_LOOP_ALIGN
        void SetLoopAlignBackEdge(BasicBlock source, BasicBlock target)
        {
            if (target.isLoopAlign && Emitter.emitSetLoopBackEdge(target) && !source.IsLast)
            {
                // End the backedge group here so later instructions do not inflate the loop size.
                JITDUMP($"Mark {FMT_BB(source.Next.bbNum)} as label: alignment end-of-loop\n");
                source.Next.SetFlags(BBF_HAS_LABEL);
            }
        }
#endif
#if DEBUG && FEATURE_LOOP_ALIGN
        var removedJmp = false;
#endif
        switch (block.Kind)
        {
            case BBJ_RETURN:
            {
                genExitCode(block);
                break;
            }

            case BBJ_THROW:
            {
#if TARGET_WASM
                Emitter.emitIns(INS_unreachable);
                if (block.IsLast || _compiler.bbIsFuncletBeg(block.Next))
                {
                    genEmitFunctionEnd(emitTerminalUnreachable: false);
                }
#else
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
#endif
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
#if DEBUG
                var call = block.LastNode;
                if (call is GenTreeCall callNode)
                {
                    assert(!callNode.IsNoReturn);
                }
#endif
                if (block.CanRemoveJumpToNext(_compiler))
                {
#if TARGET_AMD64
                    if (emitNopBeforeEHRegion)
                    {
                        instGen(INS_nop);
                    }
#endif
#if DEBUG && FEATURE_LOOP_ALIGN
                    removedJmp = true;
#endif
                    break;
                }
#if TARGET_XARCH
                var isRemovableJmpCandidate = !_compiler.fgInDifferentRegions(block, block.Target);
                inst_JMP(EJ_jmp, block.Target, isRemovableJmpCandidate);
#else
                inst_JMP(EJ_jmp, block.Target);
#endif
#if FEATURE_LOOP_ALIGN
                SetLoopAlignBackEdge(block, block.Target);
#endif
#if TARGET_WASM
                if (block.IsLast || _compiler.bbIsFuncletBeg(block.Next))
                {
                    genEmitFunctionEnd();
                }
#endif
                break;
            }

            case BBJ_COND:
            {
#if FEATURE_LOOP_ALIGN
                SetLoopAlignBackEdge(block, block.TrueTarget);
                SetLoopAlignBackEdge(block, block.FalseTarget);
#endif
                break;
            }

            default:
            {
                throw new FatalJitException("Unexpected block kind at the end of code generation.");
            }
        }
#if FEATURE_LOOP_ALIGN
        if (block.hasAlign)
        {
            assert(ShouldAlignLoops);
            assert(!block.isBBCallFinallyPairTail);
            assert(block.Kind != BBJ_CALLFINALLY);
            Emitter.emitLoopAlignment(
#if DEBUG
                (block.Kind == BBJ_ALWAYS) && !removedJmp
#endif
            );
        }
        if (!block.IsLast && block.Next.isLoopAlign && _compiler.opts.compJitHideAlignBehindJmp)
        {
            Emitter.emitConnectAlignInstrWithCurIG();
        }
#endif
    }

#if TARGET_WASM
    private void inst_JMP(emitJumpKind jump, BasicBlock target)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm jump instruction generation is not ported.");
    }

    private void genEmitFunctionEnd(bool emitTerminalUnreachable = true)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Wasm function-end emission is not ported.");
    }
#endif
}

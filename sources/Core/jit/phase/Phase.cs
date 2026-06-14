// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public abstract partial class Phase
{
    private Compiler _compiler;
    private string _name;
    private Phases _phase;

    protected Phase(Compiler compiler, Phases phase)
    {
        _compiler = compiler;
        _name = phase.Name;
        _phase = phase;
    }

    /// <summary>execute a phase and any before and after actions</summary>
    public virtual void Run()
    {
        var observations = new Observations(_compiler);
        PrePhase();
        var status = DoPhase();
        PostPhase(status);
        observations.Check(status);
    }

    protected abstract PhaseStatus DoPhase();

    /// <summary>perform dumps and checks after a phase executes</summary>
    /// <param name="status">status from the DoPhase call for this phase</param>
    protected virtual void PostPhase(PhaseStatus status)
    {
        var compiler = _compiler;
        compiler.EndPhase(_phase);

#if DEBUG
#if DUMP_FLOWGRAPHS
        _ = compiler.fgDumpFlowGraph(_phase, Compiler.PhasePosition.PostPhase);
#endif

        // Don't dump or check post phase unless the phase made changes.
        var doPostPhase = status != PhaseStatus.MODIFIED_NOTHING;
        var doPostPhaseChecks = compiler.activePhaseChecks != PhaseChecks.CHECK_NONE;
        var doPostPhaseDumps = compiler.activePhaseDumps == PhaseDumps.DUMP_ALL;

        var statusMessage = doPostPhase ? "" : " [no changes]";

        if (VERBOSE)
        {
            if (compiler.compIsForInlining)
            {
                assert(compiler.impInlineInfo.iciCall is not null);
                jitprintf($"\n*************** Inline @[{compiler.impInlineInfo.iciCall.TreeId:D6}] Finishing PHASE {_name}{statusMessage}\n");
            }
            else
            {
                if (compiler.opts.optRepeatActive)
                {
                    jitprintf($"\n*************** Finishing PHASE {_name}{statusMessage} (OptRepeat iteration {compiler.opts.optRepeatIteration} of {compiler.opts.optRepeatCount})\n");
                }
                else
                {
                    jitprintf($"\n*************** Finishing PHASE {_name}{statusMessage}\n");
                }
            }

            if (doPostPhase && doPostPhaseDumps)
            {
                jitprintf($"Trees after {_name}\n");
                compiler.fgDispBasicBlocks(true);
            }
        }

        if (doPostPhase && doPostPhaseChecks)
        {
            var checks = compiler.activePhaseChecks;

            if ((checks & PhaseChecks.CHECK_UNIQUE) != 0)
            {
                compiler.fgDebugCheckNodesUniqueness();
            }

            if ((checks & PhaseChecks.CHECK_FG) != 0)
            {
                compiler.fgDebugCheckBBlist();
            }

            if ((checks & PhaseChecks.CHECK_FG_INIT_BLOCK) != 0)
            {
                compiler.fgDebugCheckInitBB();
            }

            if ((checks & PhaseChecks.CHECK_IR) != 0)
            {
                compiler.fgDebugCheckLinks();
            }

            if ((checks & PhaseChecks.CHECK_EH) != 0)
            {
                compiler.fgVerifyHandlerTab();
            }

            if ((checks & PhaseChecks.CHECK_LOOPS) != 0)
            {
                compiler.fgDebugCheckLoops();
            }

            if ((checks & PhaseChecks.CHECK_PROFILE) != 0 || (checks & PhaseChecks.CHECK_LIKELIHOODS) != 0)
            {
                compiler.fgDebugCheckProfile(checks);
            }

            if ((checks & PhaseChecks.CHECK_LINKED_LOCALS) != 0)
            {
                compiler.fgDebugCheckLinkedLocals();
            }

            compiler.fgDebugCheckFlowGraphAnnotations();
        }
#endif
    }

    /// <summary>perform dumps and checks before a phase executes</summary>
    protected virtual void PrePhase()
    {
        var compiler = _compiler;
        compiler.BeginPhase(_phase);

#if DEBUG
        if (VERBOSE)
        {
            if (compiler.compIsForInlining)
            {
                assert(compiler.impInlineInfo.iciCall is not null);
                jitprintf($"\n*************** Inline @[{compiler.impInlineInfo.iciCall.TreeId:D6}] Starting PHASE {_name}\n");
            }
            else
            {
                if (compiler.opts.optRepeatActive)
                {
                    jitprintf($"\n*************** Starting PHASE {_name} (OptRepeat iteration {compiler.opts.optRepeatIteration} of {compiler.opts.optRepeatCount})\n");
                }
                else
                {
                    jitprintf($"\n*************** Starting PHASE {_name}\n");
                }
            }
        }
#endif

#if DUMP_FLOWGRAPHS
        compiler.fgDumpFlowGraph(_phase, Compiler.PhasePosition.PrePhase);
#endif
    }
}

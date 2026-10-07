// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_WASM
    private sealed class WasmSpillSlot
    {
        public int LocalNumber { get; }

        public bool IsByRef { get; }

        public bool InUse { get; set; }

        public WasmSpillSlot(int localNumber, bool isByRef)
        {
            LocalNumber = localNumber;
            IsByRef = isByRef;
        }
    }

    private List<WasmSpillSlot>? _wasmSpillSlots;

    public PhaseStatus fgWasmSpillRefs()
    {
        var anyChanges = false;
        var defs = new List<GenTree>();
        var spillSlotsToZeroAtEndOfBlock = new List<int>();

        foreach (var block in Blocks)
        {
            // LIR values cannot span blocks, so only references defined in this block can reach a call.
            assert(defs.Count == 0);
            defs.Clear();

            if (_wasmSpillSlots is not null)
            {
                foreach (var slot in _wasmSpillSlots)
                {
                    slot.InUse = false;
                }
            }

            foreach (var tree in block)
            {
                if (tree.Oper.IsCall && (defs.Count != 0))
                {
                    // Keep live references in pinned stack slots so the GC cannot move them across the call.
                    _wasmSpillSlots ??= [];

                    JITDUMP($"Spilling {defs.Count} live ref(s) for call\n");
                    DISPNODE(tree);

                    foreach (var def in defs)
                    {
                        JITDUMP("    ");
                        DISPNODE(def);

                        var spillSlot = -1;
                        foreach (var slot in _wasmSpillSlots)
                        {
                            if (slot.InUse || (slot.IsByRef != (def.Type is TYP_BYREF)))
                            {
                                continue;
                            }

                            spillSlot = slot.LocalNumber;
                            slot.InUse = true;
                            break;
                        }

                        if (spillSlot == -1)
                        {
                            spillSlot = lvaGrabTemp(false, "WasmSpillRefs spill slot");
                            ref var varDsc = ref lvaGetDesc(spillSlot);
                            varDsc.Type = def.Type;
                            varDsc.lvPinned = true;
                            varDsc.lvMustInit = true;
                            lvaSetVarDoNotEnregister(spillSlot, DoNotEnregisterReason.WasmGCVisibility);

                            var slot = new WasmSpillSlot(spillSlot, def.Type is TYP_BYREF)
                            {
                                InUse = true,
                            };
                            _wasmSpillSlots.Add(slot);
                        }

                        var spill = gtNewStoreLclVarNode(spillSlot, def);
                        var reload = gtNewLclVarNode(def.Type, spillSlot);
                        noway_assert(block.TryGetUse(def, out var use));
                        block.InsertAfter(def, spill);
                        block.InsertAfter(spill, reload);
                        use.ReplaceWith(reload);

                        if ((def._lirFlags & LIR.Flags.MultiplyUsed) != LIR.Flags.None)
                        {
#if DEBUG
                            JITDUMP($"Transferring multiply-used flag from [{def.TreeId:D6}] to [{reload.TreeId:D6}] for spill\n");
#endif
                            def._lirFlags &= ~LIR.Flags.MultiplyUsed;
                            reload._lirFlags |= LIR.Flags.MultiplyUsed;
                        }

                        spillSlotsToZeroAtEndOfBlock.Add(spillSlot);
                        anyChanges = true;
                    }

                    defs.Clear();
                }

                void RemoveUse(GenTree operand)
                {
                    if (!operand.IsValue)
                    {
                        return;
                    }

                    if (operand.IsContained)
                    {
                        // A contained value is part of its parent; only the underlying operands are live values.
                        _ = operand.VisitOperands(innerOperand =>
                        {
                            RemoveUse(innerOperand);
                            return GenTree.VisitResult.Continue;
                        });
                        return;
                    }

                    if (operand.Type is not (TYP_REF or TYP_BYREF))
                    {
                        return;
                    }

                    for (var i = defs.Count; i > 0; i--)
                    {
                        if (ReferenceEquals(operand, defs[i - 1]))
                        {
                            defs[i - 1] = defs[^1];
                            defs.RemoveAt(defs.Count - 1);
                            break;
                        }
                    }
                }

                if (tree.IsContained)
                {
                    continue;
                }

                _ = tree.VisitOperands(operand =>
                {
                    RemoveUse(operand);
                    return GenTree.VisitResult.Continue;
                });

                if (!tree.IsValue || tree.IsUnusedValue || tree.IsInvariant || (tree.Type is not (TYP_REF or TYP_BYREF)))
                {
                    continue;
                }

                if (tree.OperIs(GT_LCL_VAR))
                {
                    var local = tree.AsLclVarCommon();
                    // Non-address-exposed locals cannot be mutated between their definition and this use.
                    if (!lvaGetDesc(local.LclNum).IsAddressExposed)
                    {
                        continue;
                    }
                }

                defs.Add(tree);
            }

            if (spillSlotsToZeroAtEndOfBlock.Count != 0)
            {
                // Keep dead spill slots from extending object lifetimes beyond this block.
                if (block.Kind is not BBJ_RETURN)
                {
                    foreach (var localNumber in spillSlotsToZeroAtEndOfBlock)
                    {
                        var zero = gtNewZeroConNode(TYP_I_IMPL);
                        var store = gtNewStoreLclVarNode(localNumber, zero);
                        LIR.InsertBeforeTerminator(block, LIR.SeqTree(this, store));
                    }
                }

                spillSlotsToZeroAtEndOfBlock.Clear();
            }
        }

        if (_wasmSpillSlots is not null)
        {
            JITDUMP($"Total allocated spill slot count was {_wasmSpillSlots.Count}\n");
        }

        return anyChanges ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING;
    }
#endif
}

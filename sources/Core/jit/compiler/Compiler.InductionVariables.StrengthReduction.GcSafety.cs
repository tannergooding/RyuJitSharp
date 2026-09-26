// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, inductionvariableopts.cpp.

using System.Collections.Generic;
using static RyuJitSharp.Globals;
using static RyuJitSharp.VNFunc;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed partial class StrengthReductionContext
    {
        private unsafe bool StaysWithinManagedObject(List<CursorInfo> cursors, ScevAddRec addRec)
        {
            var start = _scevContext.MaterializeVN(addRec.Start);
            if (!start.BothDefined())
            {
                return false;
            }

            var baseValue = start;
            var store = _compiler.vnStore
                ?? throw new FatalJitException("GC-pointer strength reduction requires value numbering.");
            store.PeelOffsets(ref baseValue.LiberalAddr, out var liberalOffset);
            store.PeelOffsets(ref baseValue.ConservativeAddr, out var conservativeOffset);
            if (liberalOffset != conservativeOffset)
            {
                return false;
            }

            if ((store.TypeOfVN(baseValue.Conservative) is not TYP_REF) ||
                (store.TypeOfVN(baseValue.Liberal) is not TYP_REF))
            {
                // Span-like byrefs need a separate range proof; only object bases are supported.
                return false;
            }

            GenTreeArrAddr? arrayAddress = null;
            foreach (var cursor in cursors)
            {
                var current = cursor.Tree;
                while ((current is not null) && (current.Oper is not GT_ARR_ADDR))
                {
                    current = _compiler.optFindIVParent(cursor.Stmt, current);
                }
                if (current is GenTreeArrAddr address)
                {
                    arrayAddress = address;
                    break;
                }
            }
            if (arrayAddress is null)
            {
                return false;
            }

            var elementSize = arrayAddress.ElemType is TYP_STRUCT
                ? _compiler.typGetObjLayout(arrayAddress.ElemClassHandle).Size
                : (uint)arrayAddress.ElemType.Size;
            if (!addRec.Step.GetConstantValue(_compiler, out var step) ||
                (unchecked((uint)step) > elementSize))
            {
                return false;
            }

            var preheader = _loop.EntryEdge(0).SourceBlock;
            if (!_compiler.optAssertionVNIsNonNull(baseValue.Conservative, preheader.bbAssertionOut))
            {
                return false;
            }
            if ((liberalOffset < 0) || (liberalOffset > arrayAddress.FirstElemOffset))
            {
                return false;
            }

            var lengthVN = store.VNForFunc(TYP_INT, VNF_ARR_LENGTH, baseValue.Liberal);
            foreach (var bound in _backEdgeBounds)
            {
                if (bound.Type is not TYP_INT)
                {
                    continue;
                }

                var boundVN = _scevContext.MaterializeVN(bound);
                if (boundVN.Liberal != ValueNumStore.NoVN)
                {
                    var relop = store.VNForFunc(TYP_INT, VNF_LT_UN, boundVN.Liberal, lengthVN);
                    if (_scevContext.EvaluateRelop(relop) is RelopEvaluationResult.True)
                    {
                        return true;
                    }
                }

                var boundBase = bound.PeelAdditions(out var boundOffset);
                boundOffset = unchecked((int)boundOffset);
                if (boundOffset >= 0)
                {
                    continue;
                }

                // A negative backedge-count offset and boundBase <= length keep the update in range.
                var baseVN = _scevContext.MaterializeVN(boundBase);
                if (baseVN.Liberal != ValueNumStore.NoVN)
                {
                    var relop = store.VNForFunc(TYP_INT, VNF_LE, baseVN.Liberal, lengthVN);
                    if (_scevContext.EvaluateRelop(relop) is RelopEvaluationResult.True)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

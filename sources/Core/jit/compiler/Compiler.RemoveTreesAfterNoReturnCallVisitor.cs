// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct RemoveTreesAfterNoReturnCallVisitor : IGenTreeVisitor<RemoveTreesAfterNoReturnCallVisitor>
    {
        private readonly Compiler _compiler;
        private readonly BasicBlock _block;
        private readonly Statement _statement;
        private readonly GenTreeStack _ancestors;
        private readonly List<GenTreeUse> _useStack;

        public RemoveTreesAfterNoReturnCallVisitor(Compiler compiler, BasicBlock block, Statement statement)
        {
            _compiler = compiler;
            _block = block;
            _statement = statement;
            _ancestors = [];
            _useStack = [];
        }

        public static bool DoPreOrder => true;

        public static bool DoPostOrder => true;

        public static bool UseExecutionOrder => true;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            assert(use.Oper is not GT_QMARK);
            _useStack.Add(GenTreeUse.FromUse(ref use, user));
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            if ((use.Oper is not GT_CALL) || !use.AsCall().IsNoReturn)
            {
                while (!Unsafe.AreSame(ref _useStack[^1].GetUse(_statement), ref use))
                {
                    _useStack.RemoveAt(_useStack.Count - 1);
                }

                return WALK_CONTINUE;
            }

#if DEBUG
            JITDUMP($"Removing trees after no-return call [{use.TreeId:D6}]\n");
#endif

            // Extract side effects of all siblings and ancestor's siblings.
            for (var i = 0; i < _useStack.Count - 1; i++)
            {
                var useInfo = _useStack[i];
                ref var currentUse = ref useInfo.GetUse(_statement);

                if (Unsafe.AreSame(ref currentUse, ref use))
                {
                    // Future uses are operands of the no-return call.
                    break;
                }

                // Equal users identify a sibling, not an ancestor of the call.
                if (_useStack[i + 1].User == useInfo.User)
                {
#if DEBUG
                    JITDUMP($"Extracting side effects of (ancestor) sibling [{currentUse.TreeId:D6}]:");
#endif

                    GenTree? sideEffects = null;
                    _compiler.gtExtractSideEffList(currentUse, ref sideEffects);

                    if (sideEffects is not null)
                    {
                        var newStatement = _compiler.fgNewStmtFromTree(sideEffects);
                        _compiler.fgInsertStmtBefore(_block, _statement, newStatement);
                        JITDUMP("\n");
                        DISPSTMT(newStatement);
                    }
                    else
                    {
                        JITDUMP(" none\n");
                    }
                }
            }

            _statement.RootNode = use;
            JITDUMP("New final statement:\n");
            DISPSTMT(_statement);

            return WALK_ABORT;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<RemoveTreesAfterNoReturnCallVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}

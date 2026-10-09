// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime, forwardsub.cpp.

using System.Collections.Generic;
using System.Numerics;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;

namespace RyuJitSharp;

public partial class Compiler
{
    private struct ForwardSubVisitor(Compiler compiler, int lclNum) : IGenTreeVisitor<ForwardSubVisitor>
    {
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly GenTreeStack _ancestors = [];
        private readonly int _parentLclNum = compiler.lvaGetDesc(lclNum).lvIsStructField
            ? compiler.lvaGetDesc(lclNum).lvParentLcl : BAD_VAR_NUM;
        private GenTree? _node;
        private GenTree? _parentNode;
        private GenTreeFlags _useFlags;
        private GenTreeFlags _accumulatedFlags;
        private ExceptionSetFlags _accumulatedExceptions;
        private ExceptionSetFlags _useExceptions;
        private uint _treeSize;
#if DEBUG
        private uint _useCount;
        public readonly uint UseCount => _useCount;
#endif

        public readonly GenTree? Node => _node;
        public readonly GenTree? ParentNode => _parentNode;
        public readonly GenTreeFlags Flags => _useFlags;
        public readonly ExceptionSetFlags Exceptions => _useExceptions;
        public readonly bool IsCallArg => _parentNode is GenTreeCall;
        public readonly uint Complexity => _treeSize;

        public readonly bool IsUse(int number)
        {
            if ((number == lclNum) || (number == _parentLclNum))
            {
                return true;
            }

            ref var descriptor = ref compiler.lvaGetDesc(number);
            return descriptor.lvIsStructField && (descriptor.lvParentLcl == lclNum);
        }

        public readonly bool IsLastUse(GenTreeFlags flags)
        {
            var deathFlags = compiler.lvaGetDesc(lclNum).FullDeathFlags;
            return (flags & deathFlags) == deathFlags;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            _treeSize++;
            var node = use;

            if ((node.Oper is GT_LCL_VAR) && (node.AsLclVarCommon().LclNum == lclNum))
            {
                var isCallTarget = user is GenTreeCall call &&
                    (call._callType is CT_INDIRECT) && (call.ControlExpr == node);
                if (!isCallTarget && IsLastUse(node.Flags))
                {
                    _node = node;
                    _useFlags = _accumulatedFlags;
                    _useExceptions = _accumulatedExceptions;
                    _parentNode = user;
                }
            }

            if (node.Oper.IsLocal)
            {
#if DEBUG
                if (IsUse(node.AsLclVarCommon().LclNum))
                {
                    _useCount++;
                }
#endif
                if (compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).IsAddressExposed)
                {
                    _accumulatedFlags |= GTF_GLOB_REF;
                }
            }

            _accumulatedFlags |= node.Flags & GTF_GLOB_EFFECT;
            if ((node.Flags & GTF_EXCEPT) != 0 &&
                BitOperations.PopCount((uint)_accumulatedExceptions) <= 1 &&
                (_accumulatedExceptions & ExceptionSetFlags.UnknownException) == 0)
            {
                _accumulatedExceptions |= node.Exceptions(compiler);
            }

            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ForwardSubVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private struct ForwardSubEffectsVisitor(Compiler compiler) : IGenTreeVisitor<ForwardSubEffectsVisitor>
    {
        public static bool DoPostOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly GenTreeStack _ancestors = [];
        private GenTreeFlags _flags;
        public readonly GenTreeFlags Flags => _flags;

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user)
        {
            _flags |= use.Flags & GTF_ALL_EFFECT;
            if (use.Oper.IsLocal && compiler.lvaGetDesc(use.AsLclVarCommon().LclNum).IsAddressExposed)
            {
                _flags |= GTF_GLOB_REF;
            }
            return WALK_CONTINUE;
        }

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ForwardSubEffectsVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private struct ForwardSubCollectVisitor(int lclNum) : IGenTreeVisitor<ForwardSubCollectVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly GenTreeStack _ancestors = [];
        private readonly List<GenTreeLclVar> _uses = [];
        public readonly List<GenTreeLclVar> Uses => _uses;
        public bool Bail { get; private set; }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if ((use.Oper is GT_LCL_VAR) && (use.AsLclVarCommon().LclNum == lclNum))
            {
                if (user is GenTreeCall call && (call._callType is CT_INDIRECT) &&
                    (call.ControlExpr == use))
                {
                    Bail = true;
                    return WALK_ABORT;
                }
                _uses.Add(use.AsLclVar());
            }
            return WALK_CONTINUE;
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<ForwardSubCollectVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }
}

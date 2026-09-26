// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public readonly struct CopyPropSsaDef
    {
        private readonly Compiler _compiler;
        private readonly int _lclNum;
        private readonly int _ssaNum;
#if DEBUG
        private readonly GenTreeLclVarCommon _defNode;
#endif

        public CopyPropSsaDef(Compiler compiler, int lclNum, int ssaNum, GenTreeLclVarCommon defNode)
        {
            _compiler = compiler;
            _lclNum = lclNum;
            _ssaNum = ssaNum;
#if DEBUG
            _defNode = defNode;
#endif
        }

        public int SsaNum => _ssaNum;

        public ref LclSsaVarDsc GetSsaDef() => ref _compiler.lvaGetDesc(_lclNum).GetPerSsaData(_ssaNum);

#if DEBUG
        public GenTreeLclVarCommon GetDefNode() => _defNode;
#endif
    }
}

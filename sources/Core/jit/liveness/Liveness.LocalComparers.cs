// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    internal interface ILocalLess
    {
        bool Less(int first, int second);
    }

    internal readonly struct SmallCodeLess(Compiler compiler) : ILocalLess
    {
        private readonly LclVarDsc[] _locals = compiler.lvaTable;
        private readonly RefCountState _state = compiler.lvaRefCountState;
#if DEBUG
        private readonly int _localCount = compiler.lvaCount;
#endif

        public bool Less(int first, int second)
        {
#if DEBUG
            assert((uint)first < (uint)_localCount);
            assert((uint)second < (uint)_localCount);
#endif
            ref readonly var firstLocal = ref _locals[first];
            ref readonly var secondLocal = ref _locals[second];
            assert(firstLocal.lvTracked && secondLocal.lvTracked);
            assert(!firstLocal.lvRegister && !secondLocal.lvRegister);

            uint firstWeight = firstLocal.lvRefCnt(_state);
            uint secondWeight = secondLocal.lvRefCnt(_state);
#if !TARGET_ARM
            var firstIsFloat = varTypeUsesFloatReg(firstLocal.Type);
            var secondIsFloat = varTypeUsesFloatReg(secondLocal.Type);
            if (firstIsFloat != secondIsFloat)
            {
                if ((secondWeight != 0) && firstIsFloat)
                {
                    return false;
                }

                if ((firstWeight != 0) && secondIsFloat)
                {
                    return true;
                }
            }
#endif
            if (firstWeight != secondWeight)
            {
                return firstWeight > secondWeight;
            }

            if (firstLocal.lvRefCntWtd() != secondLocal.lvRefCntWtd())
            {
                return firstLocal.lvRefCntWtd() > secondLocal.lvRefCntWtd();
            }

            // Native breaks exact count/weight ties with register-argument and
            // GC preferences expressed in units of the unweighted count.
            if (firstWeight != 0)
            {
                if (firstLocal.lvIsRegArg)
                {
                    firstWeight += 2 * BB_UNITY_WEIGHT_UNSIGNED;
                }

                if (varTypeIsGC(firstLocal.Type))
                {
                    firstWeight += BB_UNITY_WEIGHT_UNSIGNED / 2;
                }
            }

            if (secondWeight != 0)
            {
                if (secondLocal.lvIsRegArg)
                {
                    secondWeight += 2 * BB_UNITY_WEIGHT_UNSIGNED;
                }

                if (varTypeIsGC(secondLocal.Type))
                {
                    secondWeight += BB_UNITY_WEIGHT_UNSIGNED / 2;
                }
            }

            if (firstWeight != secondWeight)
            {
                return firstWeight > secondWeight;
            }

            return first < second;
        }
    }

    private readonly struct BlendedCodeLess(Compiler compiler) : ILocalLess
    {
        private readonly LclVarDsc[] _locals = compiler.lvaTable;
        private readonly RefCountState _state = compiler.lvaRefCountState;
#if DEBUG
        private readonly int _localCount = compiler.lvaCount;
#endif

        public bool Less(int first, int second)
        {
#if DEBUG
            assert((uint)first < (uint)_localCount);
            assert((uint)second < (uint)_localCount);
#endif
            ref readonly var firstLocal = ref _locals[first];
            ref readonly var secondLocal = ref _locals[second];
            assert(firstLocal.lvTracked && secondLocal.lvTracked);
            assert(!firstLocal.lvRegister && !secondLocal.lvRegister);

            var firstWeight = firstLocal.lvRefCntWtd(_state);
            var secondWeight = secondLocal.lvRefCntWtd(_state);
#if !TARGET_ARM
            var firstIsFloat = varTypeUsesFloatReg(firstLocal.Type);
            var secondIsFloat = varTypeUsesFloatReg(secondLocal.Type);
            if (firstIsFloat != secondIsFloat)
            {
                if (!Compiler.fgProfileWeightsEqual(secondWeight, 0) && firstIsFloat)
                {
                    return false;
                }

                if (!Compiler.fgProfileWeightsEqual(firstWeight, 0) && secondIsFloat)
                {
                    return true;
                }
            }
#endif
            if (!Compiler.fgProfileWeightsEqual(firstWeight, 0) && firstLocal.lvIsRegArg)
            {
                firstWeight += 2 * BB_UNITY_WEIGHT;
            }

            if (!Compiler.fgProfileWeightsEqual(secondWeight, 0) && secondLocal.lvIsRegArg)
            {
                secondWeight += 2 * BB_UNITY_WEIGHT;
            }

            if (!Compiler.fgProfileWeightsEqual(firstWeight, secondWeight))
            {
                return firstWeight > secondWeight;
            }

            if (firstLocal.lvRefCnt(_state) != secondLocal.lvRefCnt(_state))
            {
                return firstLocal.lvRefCnt(_state) > secondLocal.lvRefCnt(_state);
            }

            if (varTypeIsGC(firstLocal.Type) != varTypeIsGC(secondLocal.Type))
            {
                return varTypeIsGC(firstLocal.Type);
            }

            return first < second;
        }
    }
}

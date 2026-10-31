// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    // Four entries covered 104 of 111 repeated getArgType queries in the pinned corpus.
    private const int SignatureArgumentTypeCacheCapacity = 4;

    // Larger EE signatures and instantiations bypass the cache to bound copied key storage.
    private const int MaxCachedSignatureBytes = 256;
    private const int MaxCachedSignatureInstantiationCount = 16;
    private const ulong SignatureArgumentHashPrime = 1099511628211UL;

    private SignatureArgumentTypeCacheEntry?[]? _signatureArgumentTypeCache;
    private int _signatureArgumentTypeCacheCount;
    private SignatureArgumentClassCacheEntry? _signatureArgumentClassCache;

    internal static unsafe CorInfoTypeWithMod eeGetArgTypeFromJitInfo(
        ref ICorJitInfo jitInfo,
        Compiler? compiler,
        CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_HANDLE args,
        CORINFO_CLASS_HANDLE* valueClass)
    {
        var jitInfoPointer = (ICorJitInfo*)(Unsafe.AsPointer(ref jitInfo));

        if ((compiler is not null) &&
            (compiler.info.compCompHnd == jitInfoPointer) &&
            SignatureArgumentCacheProbe.TryCreate(sig, args, hasValueClassOutput: valueClass != null, out var probe))
        {
            if (compiler.TryGetSignatureArgumentType(probe, out var cachedType, out var cachedValueClass))
            {
                if (valueClass != null)
                {
                    *valueClass = (CORINFO_CLASS_HANDLE)(cachedValueClass);
                }

                return cachedType;
            }

            var key = probe.CreateKey();
            var result = jitInfo.lpVtbl->Base.Base.getArgType(jitInfoPointer, sig, args, valueClass);
            var classResult = valueClass == null ? 0 : (nint)(*valueClass);
            compiler.StoreSignatureArgumentType(key, result, classResult);
            return result;
        }

        return jitInfo.lpVtbl->Base.Base.getArgType(jitInfoPointer, sig, args, valueClass);
    }

    internal static unsafe CORINFO_CLASS_HANDLE eeGetArgClassFromJitInfo(
        ref ICorJitInfo jitInfo,
        Compiler? compiler,
        CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_HANDLE args)
    {
        var jitInfoPointer = (ICorJitInfo*)(Unsafe.AsPointer(ref jitInfo));

        if ((compiler is not null) &&
            (compiler.info.compCompHnd == jitInfoPointer) &&
            SignatureArgumentCacheProbe.TryCreate(sig, args, hasValueClassOutput: false, out var probe))
        {
            if (compiler.TryGetSignatureArgumentClass(probe, out var cachedClass))
            {
                return (CORINFO_CLASS_HANDLE)(cachedClass);
            }

            var key = probe.CreateKey();
            var result = jitInfo.lpVtbl->Base.Base.getArgClass(jitInfoPointer, sig, args);
            compiler.StoreSignatureArgumentClass(key, (nint)result);
            return result;
        }

        return jitInfo.lpVtbl->Base.Base.getArgClass(jitInfoPointer, sig, args);
    }

    private bool TryGetSignatureArgumentType(
        SignatureArgumentCacheProbe probe,
        out CorInfoTypeWithMod result,
        out nint valueClass)
    {
        var cache = _signatureArgumentTypeCache;
        if (cache is not null)
        {
            for (var index = 0; index < _signatureArgumentTypeCacheCount; index++)
            {
                var entry = cache[index];
                if ((entry is null) || !entry.Key.Matches(probe))
                {
                    continue;
                }

                MoveSignatureArgumentTypeCacheEntryToFront(cache, index, entry);
                result = entry.Result;
                valueClass = entry.ValueClass;
                return true;
            }
        }

        result = default;
        valueClass = 0;
        return false;
    }

    private void StoreSignatureArgumentType(
        SignatureArgumentCacheKey key,
        CorInfoTypeWithMod result,
        nint valueClass)
    {
        var cache = _signatureArgumentTypeCache ??= new SignatureArgumentTypeCacheEntry[SignatureArgumentTypeCacheCapacity];
        var entry = new SignatureArgumentTypeCacheEntry(key, result, valueClass);
        var count = _signatureArgumentTypeCacheCount;

        if (count < SignatureArgumentTypeCacheCapacity)
        {
            count++;
        }

        for (var index = count - 1; index > 0; index--)
        {
            cache[index] = cache[index - 1];
        }

        cache[0] = entry;
        _signatureArgumentTypeCacheCount = count;
    }

    private static void MoveSignatureArgumentTypeCacheEntryToFront(
        SignatureArgumentTypeCacheEntry?[] cache,
        int index,
        SignatureArgumentTypeCacheEntry entry)
    {
        for (var current = index; current > 0; current--)
        {
            cache[current] = cache[current - 1];
        }

        cache[0] = entry;
    }

    private bool TryGetSignatureArgumentClass(SignatureArgumentCacheProbe probe, out nint result)
    {
        var entry = _signatureArgumentClassCache;
        if ((entry is not null) && entry.Key.Matches(probe))
        {
            result = entry.Result;
            return true;
        }

        result = 0;
        return false;
    }

    private void StoreSignatureArgumentClass(SignatureArgumentCacheKey key, nint result)
    {
        _signatureArgumentClassCache = new SignatureArgumentClassCacheEntry(key, result);
    }

    private sealed class SignatureArgumentTypeCacheEntry
    {
        public readonly SignatureArgumentCacheKey Key;
        public readonly CorInfoTypeWithMod Result;
        public readonly nint ValueClass;

        public SignatureArgumentTypeCacheEntry(
            SignatureArgumentCacheKey key,
            CorInfoTypeWithMod result,
            nint valueClass)
        {
            Key = key;
            Result = result;
            ValueClass = valueClass;
        }
    }

    private sealed class SignatureArgumentClassCacheEntry
    {
        public readonly SignatureArgumentCacheKey Key;
        public readonly nint Result;

        public SignatureArgumentClassCacheEntry(SignatureArgumentCacheKey key, nint result)
        {
            Key = key;
            Result = result;
        }
    }

    private sealed unsafe class SignatureArgumentCacheKey
    {
        private readonly ulong _hash;
        private readonly uint _callConv;
        private readonly CorInfoType _returnType;
        private readonly CorInfoSigInfoFlags _flags;
        private readonly ushort _numArgs;
        private readonly nint _returnTypeClass;
        private readonly nint _returnTypeSigClass;
        private readonly nint _scope;
        private readonly uint _token;
        private readonly int _argsStartOffset;
        private readonly int _argumentOffset;
        private readonly bool _hasValueClassOutput;
        private readonly byte[] _signatureBytes;
        private readonly nint[] _classInstantiation;
        private readonly nint[] _methodInstantiation;

        public SignatureArgumentCacheKey(SignatureArgumentCacheProbe probe)
        {
            var sig = probe.Signature;
            _hash = probe.Hash;
            _callConv = unchecked((uint)sig->callConv);
            _returnType = sig->retType;
            _flags = sig->flags;
            _numArgs = sig->numArgs;
            _returnTypeClass = (nint)sig->retTypeClass;
            _returnTypeSigClass = (nint)sig->retTypeSigClass;
            _scope = (nint)sig->scope;
            _token = unchecked((uint)sig->token);
            _argsStartOffset = probe.ArgsStartOffset;
            _argumentOffset = probe.ArgumentOffset;
            _hasValueClassOutput = probe.HasValueClassOutput;
            _signatureBytes = new ReadOnlySpan<byte>((byte*)sig->pSig, probe.SignatureLength).ToArray();
            _classInstantiation = CopyHandles(sig->sigInst.classInst, sig->sigInst.classInstCount);
            _methodInstantiation = CopyHandles(sig->sigInst.methInst, sig->sigInst.methInstCount);
        }

        public bool Matches(SignatureArgumentCacheProbe probe)
        {
            var sig = probe.Signature;

            if ((_hash != probe.Hash) ||
                (_callConv != unchecked((uint)sig->callConv)) ||
                (_returnType != sig->retType) ||
                (_flags != sig->flags) ||
                (_numArgs != sig->numArgs) ||
                (_returnTypeClass != (nint)sig->retTypeClass) ||
                (_returnTypeSigClass != (nint)sig->retTypeSigClass) ||
                (_scope != (nint)sig->scope) ||
                (_token != unchecked((uint)sig->token)) ||
                (_argsStartOffset != probe.ArgsStartOffset) ||
                (_argumentOffset != probe.ArgumentOffset) ||
                (_hasValueClassOutput != probe.HasValueClassOutput) ||
                (_signatureBytes.Length != probe.SignatureLength) ||
                (_classInstantiation.Length != probe.ClassInstantiationCount) ||
                (_methodInstantiation.Length != probe.MethodInstantiationCount))
            {
                return false;
            }

            var signatureBytes = new ReadOnlySpan<byte>((byte*)sig->pSig, probe.SignatureLength);
            if (!_signatureBytes.AsSpan().SequenceEqual(signatureBytes))
            {
                return false;
            }

            for (var index = 0; index < _classInstantiation.Length; index++)
            {
                if (_classInstantiation[index] != (nint)sig->sigInst.classInst[index])
                {
                    return false;
                }
            }

            for (var index = 0; index < _methodInstantiation.Length; index++)
            {
                if (_methodInstantiation[index] != (nint)sig->sigInst.methInst[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static nint[] CopyHandles(CORINFO_CLASS_HANDLE* handles, int count)
        {
            if (count == 0)
            {
                return [];
            }

            var result = new nint[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = (nint)handles[index];
            }

            return result;
        }
    }

    private readonly unsafe ref struct SignatureArgumentCacheProbe
    {
        public readonly CORINFO_SIG_INFO* Signature;
        public readonly int SignatureLength;
        public readonly int ArgsStartOffset;
        public readonly int ArgumentOffset;
        public readonly int ClassInstantiationCount;
        public readonly int MethodInstantiationCount;
        public readonly bool HasValueClassOutput;
        public readonly ulong Hash;

        private SignatureArgumentCacheProbe(
            CORINFO_SIG_INFO* signature,
            int signatureLength,
            int argsStartOffset,
            int argumentOffset,
            int classInstantiationCount,
            int methodInstantiationCount,
            bool hasValueClassOutput,
            ulong hash)
        {
            Signature = signature;
            SignatureLength = signatureLength;
            ArgsStartOffset = argsStartOffset;
            ArgumentOffset = argumentOffset;
            ClassInstantiationCount = classInstantiationCount;
            MethodInstantiationCount = methodInstantiationCount;
            HasValueClassOutput = hasValueClassOutput;
            Hash = hash;
        }

        public static bool TryCreate(
            CORINFO_SIG_INFO* sig,
            CORINFO_ARG_LIST_HANDLE args,
            bool hasValueClassOutput,
            out SignatureArgumentCacheProbe probe)
        {
            probe = default;

            if ((sig == null) ||
                (sig->methodSignature != null) ||
                (sig->pSig == null) ||
                (sig->cbSig <= 0) ||
                (sig->cbSig > MaxCachedSignatureBytes))
            {
                return false;
            }

            var classInstantiationCount = sig->sigInst.classInstCount;
            var methodInstantiationCount = sig->sigInst.methInstCount;

            if ((classInstantiationCount < 0) ||
                (classInstantiationCount > MaxCachedSignatureInstantiationCount) ||
                ((classInstantiationCount != 0) && (sig->sigInst.classInst == null)) ||
                (methodInstantiationCount < 0) ||
                (methodInstantiationCount > MaxCachedSignatureInstantiationCount) ||
                ((methodInstantiationCount != 0) && (sig->sigInst.methInst == null)))
            {
                return false;
            }

            var signatureStart = (nuint)sig->pSig;
            var signatureLength = (nuint)sig->cbSig;

            if (signatureLength > (nuint.MaxValue - signatureStart))
            {
                return false;
            }

            var signatureEnd = signatureStart + signatureLength;

            var argsStart = (nuint)sig->args;
            var argument = (nuint)args;

            if ((argsStart < signatureStart) ||
                (argsStart >= signatureEnd) ||
                (argument < argsStart) ||
                (argument >= signatureEnd))
            {
                return false;
            }

            var argsStartOffset = (int)(argsStart - signatureStart);
            var argumentOffset = (int)(argument - signatureStart);
            var hash = ComputeHash(
                sig,
                argsStartOffset,
                argumentOffset,
                hasValueClassOutput,
                classInstantiationCount,
                methodInstantiationCount);

            probe = new SignatureArgumentCacheProbe(
                sig,
                (int)signatureLength,
                argsStartOffset,
                argumentOffset,
                classInstantiationCount,
                methodInstantiationCount,
                hasValueClassOutput,
                hash);
            return true;
        }

        public SignatureArgumentCacheKey CreateKey() => new(this);

        private static ulong ComputeHash(
            CORINFO_SIG_INFO* sig,
            int argsStartOffset,
            int argumentOffset,
            bool hasValueClassOutput,
            int classInstantiationCount,
            int methodInstantiationCount)
        {
            var hash = 14695981039346656037UL;
            hash = AddHash(hash, unchecked((uint)sig->callConv));
            hash = AddHash(hash, unchecked((byte)sig->retType));
            hash = AddHash(hash, unchecked((uint)sig->flags));
            hash = AddHash(hash, sig->numArgs);
            hash = AddHash(hash, unchecked((ulong)(nuint)sig->retTypeClass));
            hash = AddHash(hash, unchecked((ulong)(nuint)sig->retTypeSigClass));
            hash = AddHash(hash, unchecked((ulong)(nuint)sig->scope));
            hash = AddHash(hash, unchecked((uint)sig->token));
            hash = AddHash(hash, unchecked((uint)argsStartOffset));
            hash = AddHash(hash, unchecked((uint)argumentOffset));
            hash = AddHash(hash, hasValueClassOutput ? 1UL : 0UL);
            hash = AddHash(hash, unchecked((uint)classInstantiationCount));

            for (var index = 0; index < classInstantiationCount; index++)
            {
                hash = AddHash(hash, unchecked((ulong)(nuint)sig->sigInst.classInst[index]));
            }

            hash = AddHash(hash, unchecked((uint)methodInstantiationCount));

            for (var index = 0; index < methodInstantiationCount; index++)
            {
                hash = AddHash(hash, unchecked((ulong)(nuint)sig->sigInst.methInst[index]));
            }

            var signatureBytes = new ReadOnlySpan<byte>((byte*)sig->pSig, sig->cbSig);

            foreach (var value in signatureBytes)
            {
                hash = AddHash(hash, value);
            }

            return hash;
        }

        private static ulong AddHash(ulong hash, ulong value)
        {
            for (var index = 0; index < sizeof(ulong); index++)
            {
                hash = unchecked((hash ^ (byte)value) * SignatureArgumentHashPrime);
                value >>= 8;
            }

            return hash;
        }
    }
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    private nint _lastClassMetadataNameCacheClassHandle;
    private nint _lastClassMetadataNameCacheName;
    private nint _lastClassMetadataNameCacheNamespace;
    private bool _lastClassMetadataNameCacheHasNamespaceOutput;
    private bool _hasLastClassMetadataNameCache;

    internal static unsafe byte* eeGetClassNameFromMetadataFromJitInfo(
        ref ICorJitInfo jitInfo,
        Compiler? compiler,
        CORINFO_CLASS_HANDLE classHandle,
        byte** namespaceName)
    {
        var jitInfoPointer = (ICorJitInfo*)(Unsafe.AsPointer(ref jitInfo));
        if ((compiler is not null) && (compiler.info.compCompHnd == jitInfoPointer))
        {
            var classKey = (nint)classHandle;
            var hasNamespaceOutput = namespaceName != null;

            if (compiler.TryGetClassMetadataName(classKey, hasNamespaceOutput, out var cachedName, out var cachedNamespace))
            {
                if (hasNamespaceOutput)
                {
                    *namespaceName = (byte*)cachedNamespace;
                }

                return (byte*)cachedName;
            }

            var className = jitInfo.lpVtbl->Base.Base.getClassNameFromMetadata(jitInfoPointer, classHandle, namespaceName);
            var namespaceResult = hasNamespaceOutput ? (nint)(*namespaceName) : 0;
            compiler.StoreClassMetadataName(classKey, hasNamespaceOutput, (nint)className, namespaceResult);
            return className;
        }

        return jitInfo.lpVtbl->Base.Base.getClassNameFromMetadata(jitInfoPointer, classHandle, namespaceName);
    }

    private bool TryGetClassMetadataName(
        nint classHandle,
        bool hasNamespaceOutput,
        out nint className,
        out nint namespaceName)
    {
        if (_hasLastClassMetadataNameCache &&
            (_lastClassMetadataNameCacheClassHandle == classHandle) &&
            (_lastClassMetadataNameCacheHasNamespaceOutput == hasNamespaceOutput))
        {
            className = _lastClassMetadataNameCacheName;
            namespaceName = _lastClassMetadataNameCacheNamespace;
            return true;
        }

        className = 0;
        namespaceName = 0;
        return false;
    }

    private void StoreClassMetadataName(
        nint classHandle,
        bool hasNamespaceOutput,
        nint className,
        nint namespaceName)
    {
        _lastClassMetadataNameCacheClassHandle = classHandle;
        _lastClassMetadataNameCacheHasNamespaceOutput = hasNamespaceOutput;
        _lastClassMetadataNameCacheName = className;
        _lastClassMetadataNameCacheNamespace = namespaceName;
        _hasLastClassMetadataNameCache = true;
    }
}

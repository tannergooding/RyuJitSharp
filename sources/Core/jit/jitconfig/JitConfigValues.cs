// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.JitConfigValues;
using System.Collections.Frozen;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    private FrozenDictionary<ConfigInteger, int>? _configIntegers;
    private FrozenDictionary<ConfigString, nint>? _configStrings;
    private FrozenDictionary<ConfigMethodSet, MethodSet>? _configMethodSets;

    private bool _isInitialized;

    public readonly int this[ConfigInteger configInteger]
    {
        get
        {
            assert(_isInitialized);
            assert(_configIntegers is not null);
            return _configIntegers[configInteger];
        }
    }

    public readonly MethodSet this[ConfigMethodSet configMethodSet]
    {
        get
        {
            assert(_isInitialized);
            assert(_configMethodSets is not null);
            return _configMethodSets[configMethodSet];
        }
    }

    public readonly unsafe byte* this[ConfigString configString]
    {
        get
        {
            assert(_isInitialized);
            assert(_configStrings is not null);
            return (byte*)(_configStrings[configString]);
        }
    }

    public unsafe void destroy(ICorJitHost* jitHost)
    {
        if (!_isInitialized)
        {
            return;
        }

        assert(_configIntegers is not null);
        assert(_configStrings is not null);
        assert(_configMethodSets is not null);

        foreach (var configString in _configStrings.Values)
        {
            jitHost->freeStringConfigValue((byte*)(configString));
        }

        foreach (var configMethodSet in _configMethodSets.Values)
        {
            configMethodSet.destroy(jitHost);
        }

        _configIntegers = null;
        _configStrings = null;
        _configMethodSets = null;

        _isInitialized = false;
    }

    public unsafe void initialize(ICorJitHost* jitHost)
    {
        assert(!_isInitialized);

        _configIntegers = ConfigIntegerMetadata.ToFrozenDictionary(kvp => kvp.Key, kvp => jitHost->getIntConfigValue((byte*)(kvp.Value.Key), kvp.Value.DefaultValue));
        _configStrings = ConfigStringMetadata.ToFrozenDictionary(kvp => kvp.Key, kvp => unchecked((nint)(jitHost->getStringConfigValue((byte*)(kvp.Value)))));
        _configMethodSets = ConfigMethodSetMetadata.ToFrozenDictionary(kvp => kvp.Key, kvp => new MethodSet(jitHost->getStringConfigValue((byte*)(kvp.Value)), jitHost));

        _isInitialized = true;
    }

    public readonly bool isInitialized() => _isInitialized;
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using System.Text;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    public sealed partial class MethodSet
    {
        private unsafe byte* _listFromConfig;
        private MethodName? _names;

        /// <summary>Initialize the method set by parsing the string</summary>
        /// <param name="listFromConfig">A string containing the list. The string must have come from the host's config, and this class takes ownership of the string.</param>
        /// <param name="jitHost">Pointer to host interface</param>
        public unsafe MethodSet(byte* listFromConfig, ICorJitHost* jitHost)
        {
            assert(_listFromConfig is null);
            assert(_names is null);

            if (listFromConfig is null)
            {
                return;
            }

            _listFromConfig = listFromConfig;

            var patternStart = listFromConfig;
            var patternEnd = patternStart;

            while (patternEnd[0] != 0)
            {
                if (patternEnd[0] == ' ')
                {
                    _names = CommitPattern(_names, patternStart, patternEnd);
                    patternStart = patternEnd + 1;
                }
                patternEnd++;
            }

            _names = CommitPattern(_names, patternStart, patternEnd);

            static MethodName? CommitPattern(MethodName? next, byte* patternStart, byte* patternEnd)
            {
                if (patternEnd <= patternStart)
                {
                    return next;
                }

                var pattern = new ReadOnlySpan<byte>(patternStart, (int)(patternEnd - patternStart));
                var methodNameFlags = MethodNameFlags.None;

                var exclamationIndex = pattern.IndexOf((byte)('!'));

                if (exclamationIndex >= 0)
                {
                    methodNameFlags |= MethodNameFlags.ContainsAssemblyName;
                    pattern = pattern[(exclamationIndex + 1)..];
                }

                var colonIndex = pattern.IndexOf((byte)(':'));

                if (colonIndex >= 0)
                {
                    if (pattern[..colonIndex].Contains((byte)('[')))
                    {
                        methodNameFlags |= MethodNameFlags.ClassNameContainsInstantiation;
                    }

                    methodNameFlags |= MethodNameFlags.ContainsClassName;
                    pattern = pattern[(colonIndex + 1)..];
                }

                var parenIndex = pattern.IndexOf((byte)('('));

                if (parenIndex >= 0)
                {
                    methodNameFlags |= MethodNameFlags.ContainsSignature;
                    pattern = pattern[..parenIndex];
                }

                if (pattern.Contains((byte)('[')))
                {
                    methodNameFlags |= MethodNameFlags.MethodNameContainsInstantiation;
                }

                return new MethodName(
                    next,
                    patternStart,
                    patternEnd,
                    methodNameFlags
                );
            }
        }

        public unsafe bool contains(CORINFO_METHOD_HANDLE methodHandle, CORINFO_CLASS_HANDLE classHandle, CORINFO_SIG_INFO* sigInfo)
        {
            if (isEmpty())
            {
                return false;
            }

            var compiler = JitTls.GetCompiler();
            assert(compiler is not null);

            var stringBuilder = new StringBuilder(1024);
            MethodName? prevPattern = null;

            for (var name = _names; name is not null; name = name.Next)
            {
                if ((prevPattern is null) || (name.ContainsClassName != prevPattern.ContainsClassName) ||
                    (name.ClassNameContainsInstantiation != prevPattern.ClassNameContainsInstantiation) ||
                    (name.MethodNameContainsInstantiation != prevPattern.MethodNameContainsInstantiation) ||
                    (name.ContainsSignature != prevPattern.ContainsSignature))
                {
                    var success = compiler.eeRunFunctorWithSPMIErrorTrap(() =>
                        compiler.eePrintMethod(
                            stringBuilder,
                            classHandle,
                            methodHandle,
                            sigInfo,
                            includeAssembly: name.ContainsAssemblyName,
                            includeClass: name.ContainsClassName,
                            includeClassInstantiation: name.ClassNameContainsInstantiation,
                            includeMethodInstantiation: name.MethodNameContainsInstantiation,
                            includeSignature: name.ContainsSignature,
                            includeReturnType: false,
                            includeThisSpecifier: false
                        )
                    );

                    if (!success)
                    {
                        continue;
                    }
                    prevPattern = name;
                }

                if (MatchGlob(name.Pattern, Encoding.UTF8.GetBytes(stringBuilder.ToString())))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Destroy the method set.</summary>
        /// <param name="jitHost">Pointer to host interface</param>
        public unsafe void destroy(ICorJitHost* jitHost)
        {
            // Free method names, free the list string, and reset our state
            for (MethodName? name = _names, next; name is not null; name = next)
            {
                next = name.Next;
                name.Next = null;
            }

            _listFromConfig = null;
            _names = null;
        }

        public bool isEmpty() => _names is null;

        public unsafe byte* list() => _listFromConfig;
    }
}

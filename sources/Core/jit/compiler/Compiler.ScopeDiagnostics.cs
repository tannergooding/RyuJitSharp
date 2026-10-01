// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public void compDispScopeLists()
    {
        jitprintf(string.Create(CultureInfo.InvariantCulture,
            $"Local variable scopes = {info.compVarScopesCount}\n"));
        if (info.compVarScopesCount != 0)
        {
            jitprintf("    \tVarNum \tLVNum \t      Name \tBeg \tEnd\n");
        }

        jitprintf("Sorted by enter scope:\n");
        for (var index = 0; index < info.compVarScopesCount; index++)
        {
            ref var scope = ref compEnterScopeList(index);
            assert(!Unsafe.IsNullRef(in scope));
            var name = scope.vsdName ?? "UNKNOWN";
            // Native %10s pads by bytes; the dump writer emits UTF-8.
            var paddedName = new string(' ', int.Max(0, 10 - Encoding.UTF8.GetByteCount(name))) + name;
            jitprintf(string.Create(CultureInfo.InvariantCulture,
                $"{index,2}: \t{scope.vsdVarNum:X2}h \t{scope.vsdLVnum:X2}h \t{paddedName} \t{scope.vsdLifeBeg:X3}h   \t{scope.vsdLifeEnd:X3}h"));
            if (compNextEnterScopeIndex == index)
            {
                jitprintf(" <-- next enter scope");
            }
            jitprintf("\n");
        }

        jitprintf("Sorted by exit scope:\n");
        for (var index = 0; index < info.compVarScopesCount; index++)
        {
            ref var scope = ref compExitScopeList(index);
            assert(!Unsafe.IsNullRef(in scope));
            var name = scope.vsdName ?? "UNKNOWN";
            var paddedName = new string(' ', int.Max(0, 10 - Encoding.UTF8.GetByteCount(name))) + name;
            jitprintf(string.Create(CultureInfo.InvariantCulture,
                $"{index,2}: \t{scope.vsdVarNum:X2}h \t{scope.vsdLVnum:X2}h \t{paddedName} \t{scope.vsdLifeBeg:X3}h   \t{scope.vsdLifeEnd:X3}h"));
            if (compNextExitScopeIndex == index)
            {
                jitprintf(" <-- next exit scope");
            }
            jitprintf("\n");
        }
    }
}
#endif

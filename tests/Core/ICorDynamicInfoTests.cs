using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.InfoAccessType;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ICorDynamicInfoTests
{
    [Test]
    public static void GetHelperFtnWritesLookupAndOptionalMethodHandle()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };

        CORINFO_CONST_LOOKUP lookup = default;
        CORINFO_METHOD_STRUCT_* method = null;
        jitInfo.getHelperFtn(CORINFO_HELP_MEMCPY, &lookup, &method);

        Assert.That(lookup.accessType, Is.EqualTo(IAT_PVALUE));
        Assert.That((nint)lookup.addr, Is.EqualTo((nint)0x1234));
        Assert.That((nint)method, Is.EqualTo((nint)0x5678));

        method = null;
        jitInfo.getHelperFtn(CORINFO_HELP_MEMCPY, null, &method);

        Assert.That((nint)method, Is.EqualTo((nint)0x5678));

        lookup = default;
        jitInfo.getHelperFtn(CORINFO_HELP_MEMCPY, &lookup);

        Assert.That(lookup.accessType, Is.EqualTo(IAT_VALUE));
        Assert.That((nint)lookup.addr, Is.EqualTo((nint)0x1234));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        if (lookup is not null)
        {
            lookup->accessType = method is null ? IAT_VALUE : IAT_PVALUE;
            lookup->addr = (void*)0x1234;
        }

        if (method is not null)
        {
            *method = (CORINFO_METHOD_STRUCT_*)0x5678;
        }
    }
}

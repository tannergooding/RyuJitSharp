// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT license.

namespace RyuJitSharp;

public partial class Compiler
{
    public static int GetOutgoingArgByteSize(int sizeWithoutPadding)
        => roundUp(sizeWithoutPadding, TARGET_POINTER_SIZE);

    public unsafe GenTree fgGetStubAddrArg(GenTreeCall call)
    {
        assert(call.IsVirtualStub);
        GenTree? address;
        if (call._callType is CT_INDIRECT)
        {
            assert(call.ControlExpr is not null);
            address = gtClone(call.ControlExpr, true);
        }
        else
        {
            assert(call.IsVirtualStubRelativeIndir);
            address = gtNewIconHandleNode((nint)call.StubCallStubAddr, GTF_ICON_FTN_ADDR);
#if DEBUG
            address.AsIntCon().TargetHandle = (nint)call._callMethHnd;
#endif
        }

        assert(address is not null);
        return address;
    }

    public static bool IsGcSafePoint(GenTreeCall call)
    {
        // Intrinsics can disappear after morph, and fast tail calls have no safepoint here.
        if (!call.IsFastTailCall && !call.IsSpecialIntrinsic())
        {
            if (call.IsUnmanaged && call.IsSuppressGCTransition)
            {
                return false;
            }
            else if (call._callType is CT_INDIRECT)
            {
                return true;
            }
            else if (call._callType is CT_USER_FUNC)
            {
                if ((call._callMoreFlags & GTF_CALL_M_NOGCCHECK) == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void fgAssignSetVarDef(GenTree tree)
    {
        _ = tree.VisitPhysicalLocalDefNodes(this, def => {
            if (tree.IsEntireLocalDef(this, def.AsLclVarCommon()))
            {
                def.Flags |= GTF_VAR_DEF;
            }
            else
            {
                // A partial definition also uses the value from preceding definitions.
                def.Flags |= GTF_VAR_DEF | GTF_VAR_USEASG;
            }

            return GenTree.VisitResult.Continue;
        });
    }
}

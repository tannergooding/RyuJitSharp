// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private uint curNestedAlignment { get; set; }

    public BasicBlock genCreateTempLabel()
    {
        if (Configuration.DEBUG)
        {
            _compiler.fgSafeBasicBlockCreation = true;
        }
        var block = BasicBlock.New(_compiler);
        if (Configuration.DEBUG)
        {
            _compiler.fgSafeBasicBlockCreation = false;
        }
        if (Configuration.DEBUG && _compiler.verbose)
        {
            jitprintf($"Mark {FMT_BB(block.bbNum)} as label: codegen temp block\n");
        }
        block.SetFlags(BBF_HAS_LABEL);
        assert(_compiler.compCurBB is not null);
        block.CopyFlags(_compiler.compCurBB, BBF_COLD);
        if (Configuration.DEBUG)
        {
            if (Configuration.UNIX_X86_ABI)
            {
                block.bbTgtStkDepth = unchecked((int)((genStackLevel - curNestedAlignment) / sizeof(int)));
            }
            else
            {
                block.bbTgtStkDepth = unchecked((int)(genStackLevel / sizeof(int)));
            }
        }
        return block;
    }

    private void genLogLabel(BasicBlock block)
    {
        if (Configuration.DEBUG && _compiler.dspCode)
        {
            jitprintf($"\n      L_M{unchecked((uint)_compiler.compMethodID):D3}_{FMT_BB(block.bbNum)}:\n");
        }
    }

    public void genDefineTempLabel(BasicBlock label)
    {
        genLogLabel(label);
        label.bbEmitCookie = Emitter.emitAddLabel(GCInfo.gcVarPtrSetCur, GCInfo.gcRegGCrefSetCur, GCInfo.gcRegByrefSetCur);
    }

    public void genDefineInlineTempLabel(BasicBlock label)
    {
        genLogLabel(label);
        label.bbEmitCookie = Emitter.emitAddInlineLabel();
    }

    public unsafe void genDefinePendingCallLabel(GenTreeCall call)
    {
        if (genPendingCallLabel is null)
        {
            return;
        }

        if (call.IsHelperCall())
        {
            switch (call.HelperNum)
            {
                case CORINFO_HELP_VALIDATE_INDIRECT_CALL:
                case CORINFO_HELP_VIRTUAL_FUNC_PTR:
                case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
                case CORINFO_HELP_MEMSET:
                case CORINFO_HELP_MEMCPY:
                {
                    return;
                }

                default:
                {
                    break;
                }
            }
        }

        genDefineInlineTempLabel(genPendingCallLabel);
        genPendingCallLabel = null;
    }
}

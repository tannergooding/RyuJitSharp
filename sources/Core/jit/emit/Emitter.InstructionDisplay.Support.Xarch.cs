// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitDispFrameRef(int variable, int displacement, uint ilOffset, bool assembly)
    {
        if (Configuration.TARGET_XARCH)
        {
            emitDispFrameRefXarch(variable, displacement, ilOffset, assembly);
            return;
        }

        if (Configuration.TARGET_ARMARCH)
        {
            if (Configuration.DEBUG)
            {
                emitDispFrameRefArm(variable, displacement, ilOffset);
            }
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.EmitDispFrameRefUnusedOnLoongArch64);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.EmitDispFrameRefUnimplementedUnusedOnRISCV64Yet);
            return;
        }

        FatalJitException.ThrowFatalJitException(FatalJitErrorCode.FrameReferenceDisplayIsNotImplementedForThisTarget);
        return;
    }

    private void emitDispFrameRefXarch(int variable, int displacement, uint ilOffset, bool assembly)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.FrameReferenceDisplayMissingCompiler);
            return;
        }
        jitprintf("[");
        if (!assembly || compiler.lvaDoneFrameLayout == Compiler.NO_FRAME_LAYOUT)
        {
            jitprintf(variable < 0 ? $"TEMP_{-variable:D2}" : $"V{variable:D2}");
            if (displacement < 0)
            {
                jitprintf($"-0x{unchecked((uint)-displacement):X}");
            }
            else if (displacement > 0)
            {
                jitprintf($"+0x{displacement:X}");
            }
        }

        if (compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT)
        {
            if (!assembly)
            {
                jitprintf(" ");
            }
            var address = compiler.lvaFrameAddress(variable, out var fpBased) + displacement;
            if (Configuration.TARGET_X86)
            {
                jitprintf(fpBased ? "ebp" : "esp");
            }
            else
            {
                jitprintf(fpBased
                    ? TargetRuntimeMetadata.FramePointerBaseName
                    : TargetRuntimeMetadata.StackPointerBaseName);
            }
            if (address < 0)
            {
                jitprintf($"-0x{-address:X2}");
            }
            else if (address > 0)
            {
                jitprintf($"+0x{address:X2}");
            }
            if (!Configuration.FEATURE_FIXED_OUT_ARGS && !fpBased && emitCurStackLvl != 0)
            {
                jitprintf($"+0x{unchecked((uint)emitCurStackLvl):X2}");
            }
        }
        jitprintf("]");

        if (Configuration.DEBUG && variable >= 0 && compiler.varNames
            && ilOffset != unchecked((uint)BAD_IL_OFFSET))
        {
            for (var i = 0; i < compiler.info.compVarScopesCount; i++)
            {
                ref var scope = ref compiler.info.compVarScopes[i];
                if (scope.vsdVarNum == variable && ilOffset >= unchecked((uint)scope.vsdLifeBeg)
                    && ilOffset < unchecked((uint)scope.vsdLifeEnd))
                {
                    var name = compiler.GetVarScopeName(i);
                    if (name is not null)
                    {
                        jitprintf($"'{name}");
                        if (displacement != 0)
                        {
                            jitprintf($"{displacement:+0;-0}");
                        }
                        jitprintf("'");
                    }
                    break;
                }
            }
        }
    }

    private void emitDispFrameRefArm(int variable, int displacement, uint ilOffset)
    {
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.FrameReferenceDisplayMissingCompiler);
            return;
        }
        jitprintf("[");
        if (variable < 0)
        {
            var temporary = unchecked(-variable).ToString("D2", CultureInfo.InvariantCulture);
            jitprintf($"TEMP_{temporary}");
        }
        else
        {
            compiler.gtDispLclVar(variable, false);
        }

        if (displacement < 0)
        {
            var magnitude = unchecked(-displacement).ToString("x2", CultureInfo.InvariantCulture);
            jitprintf($"-0x{magnitude}");
        }
        else if (displacement > 0)
        {
            jitprintf($"+0x{displacement.ToString("x2", CultureInfo.InvariantCulture)}");
        }

        jitprintf("]");

        if (Configuration.DEBUG && variable >= 0 && compiler.varNames
            && ilOffset != unchecked((uint)BAD_IL_OFFSET))
        {
            var variableName = compiler.compLocalVarName(variable, unchecked((int)ilOffset));
            if (variableName is not null)
            {
                jitprintf($"'{variableName}");
                if (displacement < 0)
                {
                    jitprintf($"-{unchecked(-displacement).ToString(CultureInfo.InvariantCulture)}");
                }
                else if (displacement > 0)
                {
                    jitprintf($"+{displacement.ToString(CultureInfo.InvariantCulture)}");
                }
                jitprintf("'");
            }
        }
    }

    private unsafe void emitDispClsVar(CORINFO_FIELD_HANDLE field, nint offset, bool reloc)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, FatalJitErrorCode.StaticXarchReferenceDisplayRequiresXarch);
            return;
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.StaticReferenceDisplayMissingCompiler);
            return;
        }
        if (Configuration.TARGET_X86)
        {
            offset = unchecked((int)offset);
        }

        if (compiler.opts.disDiffable && ((offset >> 20) is not 0 and not -1))
        {
            offset = unchecked((nint)0xD1FFAB1E);
        }
        if (field == FLD_GLOBAL_FS)
        {
            jitprintf($"FS:[0x{unchecked((uint)offset):X4}]");
            return;
        }
        if (field == FLD_GLOBAL_GS)
        {
            jitprintf($"GS:[0x{unchecked((uint)offset):X4}]");
            return;
        }
        if (field == FLD_GLOBAL_DS)
        {
            jitprintf($"[0x{unchecked((uint)offset):X4}]");
            return;
        }

        var dataOffset = Compiler.eeGetJitDataOffs(field);
        jitprintf("[");
        if (reloc)
        {
            jitprintf("reloc ");
        }
        if (dataOffset >= 0)
        {
            jitprintf((dataOffset & 1) != 0 ? $"@CNS{dataOffset - 1:D2}" : $"@RWD{dataOffset:D2}");
        }
        else
        {
            jitprintf($"classVar[{FMT_PTR((void*)compiler.dspPtr(field))}]");
        }
        if (offset != 0)
        {
            jitprintf($"{offset:+0;-0}");
        }
        jitprintf("]");
        if (Configuration.DEBUG && compiler.varNames && offset < 0)
        {
            jitprintf($"'{compiler.eeGetFieldName(field, true)}{(offset == 0 ? "" : offset.ToString("+0;-0", CultureInfo.InvariantCulture))}'");
        }
    }
}

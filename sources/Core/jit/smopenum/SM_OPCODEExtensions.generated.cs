// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG || SMGEN_COMPILE
using System;

namespace RyuJitSharp;

public static partial class SM_OPCODEExtensions
{
    private static ReadOnlySpan<SM_OPCODE> s_codeSeqs => [
        // ==== Single opcode states ====
        SM_NOSHOW, CODE_SEQUENCE_END,
        SM_LDARG_0, CODE_SEQUENCE_END,
        SM_LDARG_1, CODE_SEQUENCE_END,
        SM_LDARG_2, CODE_SEQUENCE_END,
        SM_LDARG_3, CODE_SEQUENCE_END,
        SM_LDLOC_0, CODE_SEQUENCE_END,
        SM_LDLOC_1, CODE_SEQUENCE_END,
        SM_LDLOC_2, CODE_SEQUENCE_END,
        SM_LDLOC_3, CODE_SEQUENCE_END,
        SM_STLOC_0, CODE_SEQUENCE_END,
        SM_STLOC_1, CODE_SEQUENCE_END,
        SM_STLOC_2, CODE_SEQUENCE_END,
        SM_STLOC_3, CODE_SEQUENCE_END,
        SM_LDARG_S, CODE_SEQUENCE_END,
        SM_LDARGA_S, CODE_SEQUENCE_END,
        SM_STARG_S, CODE_SEQUENCE_END,
        SM_LDLOC_S, CODE_SEQUENCE_END,
        SM_LDLOCA_S, CODE_SEQUENCE_END,
        SM_STLOC_S, CODE_SEQUENCE_END,
        SM_LDNULL, CODE_SEQUENCE_END,
        SM_LDC_I4_M1, CODE_SEQUENCE_END,
        SM_LDC_I4_0, CODE_SEQUENCE_END,
        SM_LDC_I4_1, CODE_SEQUENCE_END,
        SM_LDC_I4_2, CODE_SEQUENCE_END,
        SM_LDC_I4_3, CODE_SEQUENCE_END,
        SM_LDC_I4_4, CODE_SEQUENCE_END,
        SM_LDC_I4_5, CODE_SEQUENCE_END,
        SM_LDC_I4_6, CODE_SEQUENCE_END,
        SM_LDC_I4_7, CODE_SEQUENCE_END,
        SM_LDC_I4_8, CODE_SEQUENCE_END,
        SM_LDC_I4_S, CODE_SEQUENCE_END,
        SM_LDC_I4, CODE_SEQUENCE_END,
        SM_LDC_I8, CODE_SEQUENCE_END,
        SM_LDC_R4, CODE_SEQUENCE_END,
        SM_LDC_R8, CODE_SEQUENCE_END,
        SM_UNUSED, CODE_SEQUENCE_END,
        SM_DUP, CODE_SEQUENCE_END,
        SM_POP, CODE_SEQUENCE_END,
        SM_CALL, CODE_SEQUENCE_END,
        SM_CALLI, CODE_SEQUENCE_END,
        SM_RET, CODE_SEQUENCE_END,
        SM_BR_S, CODE_SEQUENCE_END,
        SM_BRFALSE_S, CODE_SEQUENCE_END,
        SM_BRTRUE_S, CODE_SEQUENCE_END,
        SM_BEQ_S, CODE_SEQUENCE_END,
        SM_BGE_S, CODE_SEQUENCE_END,
        SM_BGT_S, CODE_SEQUENCE_END,
        SM_BLE_S, CODE_SEQUENCE_END,
        SM_BLT_S, CODE_SEQUENCE_END,
        SM_BNE_UN_S, CODE_SEQUENCE_END,
        SM_BGE_UN_S, CODE_SEQUENCE_END,
        SM_BGT_UN_S, CODE_SEQUENCE_END,
        SM_BLE_UN_S, CODE_SEQUENCE_END,
        SM_BLT_UN_S, CODE_SEQUENCE_END,
        SM_LONG_BRANCH, CODE_SEQUENCE_END,
        SM_SWITCH, CODE_SEQUENCE_END,
        SM_LDIND_I1, CODE_SEQUENCE_END,
        SM_LDIND_U1, CODE_SEQUENCE_END,
        SM_LDIND_I2, CODE_SEQUENCE_END,
        SM_LDIND_U2, CODE_SEQUENCE_END,
        SM_LDIND_I4, CODE_SEQUENCE_END,
        SM_LDIND_U4, CODE_SEQUENCE_END,
        SM_LDIND_I8, CODE_SEQUENCE_END,
        SM_LDIND_I, CODE_SEQUENCE_END,
        SM_LDIND_R4, CODE_SEQUENCE_END,
        SM_LDIND_R8, CODE_SEQUENCE_END,
        SM_LDIND_REF, CODE_SEQUENCE_END,
        SM_STIND_REF, CODE_SEQUENCE_END,
        SM_STIND_I1, CODE_SEQUENCE_END,
        SM_STIND_I2, CODE_SEQUENCE_END,
        SM_STIND_I4, CODE_SEQUENCE_END,
        SM_STIND_I8, CODE_SEQUENCE_END,
        SM_STIND_R4, CODE_SEQUENCE_END,
        SM_STIND_R8, CODE_SEQUENCE_END,
        SM_ADD, CODE_SEQUENCE_END,
        SM_SUB, CODE_SEQUENCE_END,
        SM_MUL, CODE_SEQUENCE_END,
        SM_DIV, CODE_SEQUENCE_END,
        SM_DIV_UN, CODE_SEQUENCE_END,
        SM_REM, CODE_SEQUENCE_END,
        SM_REM_UN, CODE_SEQUENCE_END,
        SM_AND, CODE_SEQUENCE_END,
        SM_OR, CODE_SEQUENCE_END,
        SM_XOR, CODE_SEQUENCE_END,
        SM_SHL, CODE_SEQUENCE_END,
        SM_SHR, CODE_SEQUENCE_END,
        SM_SHR_UN, CODE_SEQUENCE_END,
        SM_NEG, CODE_SEQUENCE_END,
        SM_NOT, CODE_SEQUENCE_END,
        SM_CONV_I1, CODE_SEQUENCE_END,
        SM_CONV_I2, CODE_SEQUENCE_END,
        SM_CONV_I4, CODE_SEQUENCE_END,
        SM_CONV_I8, CODE_SEQUENCE_END,
        SM_CONV_R4, CODE_SEQUENCE_END,
        SM_CONV_R8, CODE_SEQUENCE_END,
        SM_CONV_U4, CODE_SEQUENCE_END,
        SM_CONV_U8, CODE_SEQUENCE_END,
        SM_CALLVIRT, CODE_SEQUENCE_END,
        SM_CPOBJ, CODE_SEQUENCE_END,
        SM_LDOBJ, CODE_SEQUENCE_END,
        SM_LDSTR, CODE_SEQUENCE_END,
        SM_NEWOBJ, CODE_SEQUENCE_END,
        SM_CASTCLASS, CODE_SEQUENCE_END,
        SM_ISINST, CODE_SEQUENCE_END,
        SM_CONV_R_UN, CODE_SEQUENCE_END,
        SM_UNBOX, CODE_SEQUENCE_END,
        SM_THROW, CODE_SEQUENCE_END,
        SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDFLDA, CODE_SEQUENCE_END,
        SM_STFLD, CODE_SEQUENCE_END,
        SM_LDSFLD, CODE_SEQUENCE_END,
        SM_LDSFLDA, CODE_SEQUENCE_END,
        SM_STSFLD, CODE_SEQUENCE_END,
        SM_STOBJ, CODE_SEQUENCE_END,
        SM_OVF_NOTYPE_UN, CODE_SEQUENCE_END,
        SM_BOX, CODE_SEQUENCE_END,
        SM_NEWARR, CODE_SEQUENCE_END,
        SM_LDLEN, CODE_SEQUENCE_END,
        SM_LDELEMA, CODE_SEQUENCE_END,
        SM_LDELEM_I1, CODE_SEQUENCE_END,
        SM_LDELEM_U1, CODE_SEQUENCE_END,
        SM_LDELEM_I2, CODE_SEQUENCE_END,
        SM_LDELEM_U2, CODE_SEQUENCE_END,
        SM_LDELEM_I4, CODE_SEQUENCE_END,
        SM_LDELEM_U4, CODE_SEQUENCE_END,
        SM_LDELEM_I8, CODE_SEQUENCE_END,
        SM_LDELEM_I, CODE_SEQUENCE_END,
        SM_LDELEM_R4, CODE_SEQUENCE_END,
        SM_LDELEM_R8, CODE_SEQUENCE_END,
        SM_LDELEM_REF, CODE_SEQUENCE_END,
        SM_STELEM_I, CODE_SEQUENCE_END,
        SM_STELEM_I1, CODE_SEQUENCE_END,
        SM_STELEM_I2, CODE_SEQUENCE_END,
        SM_STELEM_I4, CODE_SEQUENCE_END,
        SM_STELEM_I8, CODE_SEQUENCE_END,
        SM_STELEM_R4, CODE_SEQUENCE_END,
        SM_STELEM_R8, CODE_SEQUENCE_END,
        SM_STELEM_REF, CODE_SEQUENCE_END,
        SM_LDELEM, CODE_SEQUENCE_END,
        SM_STELEM, CODE_SEQUENCE_END,
        SM_UNBOX_ANY, CODE_SEQUENCE_END,
        SM_CONV_OVF_I1, CODE_SEQUENCE_END,
        SM_CONV_OVF_U1, CODE_SEQUENCE_END,
        SM_CONV_OVF_I2, CODE_SEQUENCE_END,
        SM_CONV_OVF_U2, CODE_SEQUENCE_END,
        SM_CONV_OVF_I4, CODE_SEQUENCE_END,
        SM_CONV_OVF_U4, CODE_SEQUENCE_END,
        SM_CONV_OVF_I8, CODE_SEQUENCE_END,
        SM_CONV_OVF_U8, CODE_SEQUENCE_END,
        SM_REFANYVAL, CODE_SEQUENCE_END,
        SM_CKFINITE, CODE_SEQUENCE_END,
        SM_MKREFANY, CODE_SEQUENCE_END,
        SM_LDTOKEN, CODE_SEQUENCE_END,
        SM_CONV_U2, CODE_SEQUENCE_END,
        SM_CONV_U1, CODE_SEQUENCE_END,
        SM_CONV_I, CODE_SEQUENCE_END,
        SM_CONV_OVF_I, CODE_SEQUENCE_END,
        SM_CONV_OVF_U, CODE_SEQUENCE_END,
        SM_ADD_OVF, CODE_SEQUENCE_END,
        SM_MUL_OVF, CODE_SEQUENCE_END,
        SM_SUB_OVF, CODE_SEQUENCE_END,
        SM_LEAVE_S, CODE_SEQUENCE_END,
        SM_STIND_I, CODE_SEQUENCE_END,
        SM_CONV_U, CODE_SEQUENCE_END,
        SM_PREFIX_N, CODE_SEQUENCE_END,
        SM_ARGLIST, CODE_SEQUENCE_END,
        SM_CEQ, CODE_SEQUENCE_END,
        SM_CGT, CODE_SEQUENCE_END,
        SM_CGT_UN, CODE_SEQUENCE_END,
        SM_CLT, CODE_SEQUENCE_END,
        SM_CLT_UN, CODE_SEQUENCE_END,
        SM_LDFTN, CODE_SEQUENCE_END,
        SM_LDVIRTFTN, CODE_SEQUENCE_END,
        SM_LONG_LOC_ARG, CODE_SEQUENCE_END,
        SM_LOCALLOC, CODE_SEQUENCE_END,
        SM_UNALIGNED, CODE_SEQUENCE_END,
        SM_VOLATILE, CODE_SEQUENCE_END,
        SM_TAILCALL, CODE_SEQUENCE_END,
        SM_INITOBJ, CODE_SEQUENCE_END,
        SM_CONSTRAINED, CODE_SEQUENCE_END,
        SM_CPBLK, CODE_SEQUENCE_END,
        SM_INITBLK, CODE_SEQUENCE_END,
        SM_RETHROW, CODE_SEQUENCE_END,
        SM_SIZEOF, CODE_SEQUENCE_END,
        SM_REFANYTYPE, CODE_SEQUENCE_END,
        SM_READONLY, CODE_SEQUENCE_END,
        SM_LDARGA_S_NORMED, CODE_SEQUENCE_END,
        SM_LDLOCA_S_NORMED, CODE_SEQUENCE_END,

        // ==== Legel prefixed opcode sequences ====
        SM_CONSTRAINED, SM_CALLVIRT, CODE_SEQUENCE_END,
        
        // ==== Interesting patterns ====
        
        // Fetching of object field
        SM_LDARG_0, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_1, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_2, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDARG_3, SM_LDFLD, CODE_SEQUENCE_END,
        
        // Fetching of struct field
        SM_LDARGA_S, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDLOCA_S, SM_LDFLD, CODE_SEQUENCE_END,
        
        // Fetching of struct field from a normed struct
        SM_LDARGA_S_NORMED, SM_LDFLD, CODE_SEQUENCE_END,
        SM_LDLOCA_S_NORMED, SM_LDFLD, CODE_SEQUENCE_END,
        
        // stloc/ldloc --> dup
        SM_STLOC_0, SM_LDLOC_0, CODE_SEQUENCE_END,
        SM_STLOC_1, SM_LDLOC_1, CODE_SEQUENCE_END,
        SM_STLOC_2, SM_LDLOC_2, CODE_SEQUENCE_END,
        SM_STLOC_3, SM_LDLOC_3, CODE_SEQUENCE_END,
        
        // FPU operations
        SM_LDC_R4, SM_ADD, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_SUB, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_MUL, CODE_SEQUENCE_END,
        SM_LDC_R4, SM_DIV, CODE_SEQUENCE_END,
        
        SM_LDC_R8, SM_ADD, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_SUB, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_MUL, CODE_SEQUENCE_END,
        SM_LDC_R8, SM_DIV, CODE_SEQUENCE_END,
        
        SM_CONV_R4, SM_ADD, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_SUB, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_MUL, CODE_SEQUENCE_END,
        SM_CONV_R4, SM_DIV, CODE_SEQUENCE_END,
        
        // {SM_CONV_R8,       SM_ADD,        CODE_SEQUENCE_END},  // Removed since it collides with ldelem.r8 in
        // Math.InternalRound
        // {SM_CONV_R8,       SM_SUB,        CODE_SEQUENCE_END},  // Just remove the SM_SUB as well.
        SM_CONV_R8, SM_MUL, CODE_SEQUENCE_END,
        SM_CONV_R8, SM_DIV, CODE_SEQUENCE_END,
        
        // Constant init constructor:
        //  L_0006: ldarg.0
        //  L_0007: ldc.r8 0
        //  L_0010: stfld float64 raytracer.Vec::x

        SM_LDARG_0, SM_LDC_I4_0, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDC_R4, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDC_R8, SM_STFLD, CODE_SEQUENCE_END,
        
        // Copy constructor:
        //  L_0006: ldarg.0
        //  L_0007: ldarg.1
        //  L_0008: ldfld float64 raytracer.Vec::x
        //  L_000d: stfld float64 raytracer.Vec::x

        SM_LDARG_0, SM_LDARG_1, SM_LDFLD, SM_STFLD, CODE_SEQUENCE_END,
        
        // Field setter:
        //  [DebuggerNonUserCode]
        //  private void CtorClosed(object target, IntPtr methodPtr)
        //  {
        //      if (target == null)
        //      {
        //          this.ThrowNullThisInDelegateToInstance();
        //      }
        //      base._target = target;
        //      base._methodPtr = methodPtr;
        //  }
        //
        //
        //  .method private hidebysig instance void CtorClosed(object target, native int methodPtr) cil managed
        //  {
        //      .custom instance void System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor()
        //      .maxstack 8
        //      L_0000: ldarg.1
        //      L_0001: brtrue.s L_0009
        //      L_0003: ldarg.0
        //      L_0004: call instance void System.MulticastDelegate::ThrowNullThisInDelegateToInstance()
        //
        //      L_0009: ldarg.0
        //      L_000a: ldarg.1
        //      L_000b: stfld object System.Delegate::_target
        //
        //      L_0010: ldarg.0
        //      L_0011: ldarg.2
        //      L_0012: stfld native int System.Delegate::_methodPtr
        //
        //      L_0017: ret
        //  }
        
        SM_LDARG_0, SM_LDARG_1, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDARG_2, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDARG_3, SM_STFLD, CODE_SEQUENCE_END,
        
        // Scale operator:
        //  L_0000: ldarg.0
        //  L_0001: dup
        //  L_0002: ldfld float64 raytracer.Vec::x
        //  L_0007: ldarg.1
        //  L_0008: mul
        //  L_0009: stfld float64 raytracer.Vec::x
        
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_ADD, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_SUB, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_MUL, SM_STFLD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_DUP, SM_LDFLD, SM_LDARG_1, SM_DIV, SM_STFLD, CODE_SEQUENCE_END,
        
        // Add operator
        //  L_0000: ldarg.0
        //  L_0001: ldfld float64 raytracer.Vec::x
        //  L_0006: ldarg.1
        //  L_0007: ldfld float64 raytracer.Vec::x
        //  L_000c: add
        
        SM_LDARG_0, SM_LDFLD, SM_LDARG_1, SM_LDFLD, SM_ADD, CODE_SEQUENCE_END,
        SM_LDARG_0, SM_LDFLD, SM_LDARG_1, SM_LDFLD, SM_SUB, CODE_SEQUENCE_END,
        // No need for mul and div since there is no mathemetical meaning of it.
        
        SM_LDARGA_S, SM_LDFLD, SM_LDARGA_S, SM_LDFLD, SM_ADD, CODE_SEQUENCE_END,
        SM_LDARGA_S, SM_LDFLD, SM_LDARGA_S, SM_LDFLD, SM_SUB, CODE_SEQUENCE_END,
        // No need for mul and div since there is no mathemetical meaning of it.

        // The end:
        CODE_SEQUENCE_END,
    ];

    private static readonly string[] s_names = [
        "noshow", // SM_NOSHOW
        "ldarg.0", // SM_LDARG_0
        "ldarg.1", // SM_LDARG_1
        "ldarg.2", // SM_LDARG_2
        "ldarg.3", // SM_LDARG_3
        "ldloc.0", // SM_LDLOC_0
        "ldloc.1", // SM_LDLOC_1
        "ldloc.2", // SM_LDLOC_2
        "ldloc.3", // SM_LDLOC_3
        "stloc.0", // SM_STLOC_0
        "stloc.1", // SM_STLOC_1
        "stloc.2", // SM_STLOC_2
        "stloc.3", // SM_STLOC_3
        "ldarg.s", // SM_LDARG_S
        "ldarga.s", // SM_LDARGA_S
        "starg.s", // SM_STARG_S
        "ldloc.s", // SM_LDLOC_S
        "ldloca.s", // SM_LDLOCA_S
        "stloc.s", // SM_STLOC_S
        "ldnull", // SM_LDNULL
        "ldc.i4.m1", // SM_LDC_I4_M1
        "ldc.i4.0", // SM_LDC_I4_0
        "ldc.i4.1", // SM_LDC_I4_1
        "ldc.i4.2", // SM_LDC_I4_2
        "ldc.i4.3", // SM_LDC_I4_3
        "ldc.i4.4", // SM_LDC_I4_4
        "ldc.i4.5", // SM_LDC_I4_5
        "ldc.i4.6", // SM_LDC_I4_6
        "ldc.i4.7", // SM_LDC_I4_7
        "ldc.i4.8", // SM_LDC_I4_8
        "ldc.i4.s", // SM_LDC_I4_S
        "ldc.i4", // SM_LDC_I4
        "ldc.i8", // SM_LDC_I8
        "ldc.r4", // SM_LDC_R4
        "ldc.r8", // SM_LDC_R8
        "unused", // SM_UNUSED
        "dup", // SM_DUP
        "pop", // SM_POP
        "call", // SM_CALL
        "calli", // SM_CALLI
        "ret", // SM_RET
        "br.s", // SM_BR_S
        "brfalse.s", // SM_BRFALSE_S
        "brtrue.s", // SM_BRTRUE_S
        "beq.s", // SM_BEQ_S
        "bge.s", // SM_BGE_S
        "bgt.s", // SM_BGT_S
        "ble.s", // SM_BLE_S
        "blt.s", // SM_BLT_S
        "bne.un.s", // SM_BNE_UN_S
        "bge.un.s", // SM_BGE_UN_S
        "bgt.un.s", // SM_BGT_UN_S
        "ble.un.s", // SM_BLE_UN_S
        "blt.un.s", // SM_BLT_UN_S
        "long.branch", // SM_LONG_BRANCH
        "switch", // SM_SWITCH
        "ldind.i1", // SM_LDIND_I1
        "ldind.u1", // SM_LDIND_U1
        "ldind.i2", // SM_LDIND_I2
        "ldind.u2", // SM_LDIND_U2
        "ldind.i4", // SM_LDIND_I4
        "ldind.u4", // SM_LDIND_U4
        "ldind.i8", // SM_LDIND_I8
        "ldind.i", // SM_LDIND_I
        "ldind.r4", // SM_LDIND_R4
        "ldind.r8", // SM_LDIND_R8
        "ldind.ref", // SM_LDIND_REF
        "stind.ref", // SM_STIND_REF
        "stind.i1", // SM_STIND_I1
        "stind.i2", // SM_STIND_I2
        "stind.i4", // SM_STIND_I4
        "stind.i8", // SM_STIND_I8
        "stind.r4", // SM_STIND_R4
        "stind.r8", // SM_STIND_R8
        "add", // SM_ADD
        "sub", // SM_SUB
        "mul", // SM_MUL
        "div", // SM_DIV
        "div.un", // SM_DIV_UN
        "rem", // SM_REM
        "rem.un", // SM_REM_UN
        "and", // SM_AND
        "or", // SM_OR
        "xor", // SM_XOR
        "shl", // SM_SHL
        "shr", // SM_SHR
        "shr.un", // SM_SHR_UN
        "neg", // SM_NEG
        "not", // SM_NOT
        "conv.i1", // SM_CONV_I1
        "conv.i2", // SM_CONV_I2
        "conv.i4", // SM_CONV_I4
        "conv.i8", // SM_CONV_I8
        "conv.r4", // SM_CONV_R4
        "conv.r8", // SM_CONV_R8
        "conv.u4", // SM_CONV_U4
        "conv.u8", // SM_CONV_U8
        "callvirt", // SM_CALLVIRT
        "cpobj", // SM_CPOBJ
        "ldobj", // SM_LDOBJ
        "ldstr", // SM_LDSTR
        "newobj", // SM_NEWOBJ
        "castclass", // SM_CASTCLASS
        "isinst", // SM_ISINST
        "conv.r.un", // SM_CONV_R_UN
        "unbox", // SM_UNBOX
        "throw", // SM_THROW
        "ldfld", // SM_LDFLD
        "ldflda", // SM_LDFLDA
        "stfld", // SM_STFLD
        "ldsfld", // SM_LDSFLD
        "ldsflda", // SM_LDSFLDA
        "stsfld", // SM_STSFLD
        "stobj", // SM_STOBJ
        "ovf.notype.un", // SM_OVF_NOTYPE_UN
        "box", // SM_BOX
        "newarr", // SM_NEWARR
        "ldlen", // SM_LDLEN
        "ldelema", // SM_LDELEMA
        "ldelem.i1", // SM_LDELEM_I1
        "ldelem.u1", // SM_LDELEM_U1
        "ldelem.i2", // SM_LDELEM_I2
        "ldelem.u2", // SM_LDELEM_U2
        "ldelem.i4", // SM_LDELEM_I4
        "ldelem.u4", // SM_LDELEM_U4
        "ldelem.i8", // SM_LDELEM_I8
        "ldelem.i", // SM_LDELEM_I
        "ldelem.r4", // SM_LDELEM_R4
        "ldelem.r8", // SM_LDELEM_R8
        "ldelem.ref", // SM_LDELEM_REF
        "stelem.i", // SM_STELEM_I
        "stelem.i1", // SM_STELEM_I1
        "stelem.i2", // SM_STELEM_I2
        "stelem.i4", // SM_STELEM_I4
        "stelem.i8", // SM_STELEM_I8
        "stelem.r4", // SM_STELEM_R4
        "stelem.r8", // SM_STELEM_R8
        "stelem.ref", // SM_STELEM_REF
        "ldelem", // SM_LDELEM
        "stelem", // SM_STELEM
        "unbox.any", // SM_UNBOX_ANY
        "conv.ovf.i1", // SM_CONV_OVF_I1
        "conv.ovf.u1", // SM_CONV_OVF_U1
        "conv.ovf.i2", // SM_CONV_OVF_I2
        "conv.ovf.u2", // SM_CONV_OVF_U2
        "conv.ovf.i4", // SM_CONV_OVF_I4
        "conv.ovf.u4", // SM_CONV_OVF_U4
        "conv.ovf.i8", // SM_CONV_OVF_I8
        "conv.ovf.u8", // SM_CONV_OVF_U8
        "refanyval", // SM_REFANYVAL
        "ckfinite", // SM_CKFINITE
        "mkrefany", // SM_MKREFANY
        "ldtoken", // SM_LDTOKEN
        "conv.u2", // SM_CONV_U2
        "conv.u1", // SM_CONV_U1
        "conv.i", // SM_CONV_I
        "conv.ovf.i", // SM_CONV_OVF_I
        "conv.ovf.u", // SM_CONV_OVF_U
        "add.ovf", // SM_ADD_OVF
        "mul.ovf", // SM_MUL_OVF
        "sub.ovf", // SM_SUB_OVF
        "leave.s", // SM_LEAVE_S
        "stind.i", // SM_STIND_I
        "conv.u", // SM_CONV_U
        "prefix.n", // SM_PREFIX_N
        "arglist", // SM_ARGLIST
        "ceq", // SM_CEQ
        "cgt", // SM_CGT
        "cgt.un", // SM_CGT_UN
        "clt", // SM_CLT
        "clt.un", // SM_CLT_UN
        "ldftn", // SM_LDFTN
        "ldvirtftn", // SM_LDVIRTFTN
        "long.loc.arg", // SM_LONG_LOC_ARG
        "localloc", // SM_LOCALLOC
        "unaligned", // SM_UNALIGNED
        "volatile", // SM_VOLATILE
        "tailcall", // SM_TAILCALL
        "initobj", // SM_INITOBJ
        "constrained", // SM_CONSTRAINED
        "cpblk", // SM_CPBLK
        "initblk", // SM_INITBLK
        "rethrow", // SM_RETHROW
        "sizeof", // SM_SIZEOF
        "refanytype", // SM_REFANYTYPE
        "readonly", // SM_READONLY
        "ldarga.s.normed", // SM_LDARGA_S_NORMED
        "ldloca.s.normed", // SM_LDLOCA_S_NORMED
    ];
}
#endif
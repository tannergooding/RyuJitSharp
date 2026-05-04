// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public CORINFO_EE_INFO eeInfo;

    public bool eeInfoInitialized;

    public uint eeBoundariesCount;

    /// <summary>Boundaries to report to the EE</summary>
    public unsafe ICorDebugInfo.OffsetMapping* eeBoundaries;

    public uint eeVarsCount;

    public VarResultInfo? eeVars;

    /// <summary>Append the output of one of the JIT-EE 'print' functions to a StringBuilder.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="printFunc">A functor to print the string that follows the conventions of the JIT-EE print* functions.</param>
    public unsafe void eeAppendPrint(StringBuilder stringBuilder, EEPrintFunc printFunc)
    {
        const int DefaultBufferSize = 256;

        var stackBuffer = stackalloc byte[DefaultBufferSize];
        var arrayBuffer = null as byte[];

        nuint requiredBufferSize;
        var printed = printFunc(stackBuffer, DefaultBufferSize, &requiredBufferSize);

        scoped Span<byte> messageUtf8;

        if (requiredBufferSize <= DefaultBufferSize)
        {
            assert(printed == (requiredBufferSize - 1));
            messageUtf8 = new Span<byte>(stackBuffer, unchecked((int)(printed)));
        }
        else
        {
            arrayBuffer = ArrayPool<byte>.Shared.Rent(unchecked((int)(requiredBufferSize)));

            fixed (byte* pArrayBuffer = arrayBuffer)
            {
                printed = printFunc(pArrayBuffer, requiredBufferSize, &requiredBufferSize);
            }

            assert(printed == (requiredBufferSize - 1));
            messageUtf8 = arrayBuffer.AsSpan(0, unchecked((int)(printed)));
        }
        _ = stringBuilder.Append(Encoding.UTF8.GetString(messageUtf8));

        if (arrayBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(arrayBuffer);
        }
    }

    public unsafe CORINFO_CLASS_HANDLE eeGetArgClass(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE argListHandle)
        => info.compCompHnd->getArgClass(sigInfo, argListHandle);

    /// <summary>Get the assembly name of a type.</summary>
    /// <param name="clsHnd">the handle of the class</param>
    /// <returns>The name string.</returns>
    /// <remarks>If missing information (in SPMI), then return a placeholder string.</remarks>
    public unsafe string eeGetClassAssemblyName(CORINFO_CLASS_HANDLE clsHnd)
    {
        var assemblyName = "<unknown assembly>";

        var success = eeRunFunctorWithSpmiErrorTrap(() => {
            var pClassAssemblyNameUtf8 = info.compCompHnd->getClassAssemblyName(clsHnd);
            var classAssemblyNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassAssemblyNameUtf8);
            assemblyName = Encoding.UTF8.GetString(classAssemblyNameUtf8);
        });

        return (assemblyName is not null) ? assemblyName : "<no assembly>";
    }

    /// <summary>Get the name (including namespace and instantiation) of a type.</summary>
    /// <param name="classHandle">the handle of the class</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The name string.</returns>
    /// <remarks>If missing information (in SPMI), then return a placeholder string.</remarks>
    public unsafe string eeGetClassName(CORINFO_CLASS_HANDLE classHandle, StringBuilder? stringBuilder = null)
    {
        stringBuilder ??= new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintType(
                stringBuilder,
                classHandle,
                includeInstantiation: true
            )
        );

        if (!success)
        {
            _ = stringBuilder.Clear();
            _ = stringBuilder.Append("<unknown class>");
        }
        return stringBuilder.ToString();
    }

    public unsafe ref CORINFO_EE_INFO eeGetEEInfo()
    {
        if (!eeInfoInitialized)
        {
            fixed (CORINFO_EE_INFO* pEEInfo = &eeInfo)
            {
                info.compCompHnd->getEEInfo(pEEInfo);
            }
            eeInfoInitialized = true;
        }
        return ref eeInfo;
    }

    public unsafe CorInfoHelpFunc eeGetHelperNum(CORINFO_METHOD_HANDLE method)
    {
        if (((nuint)(method) & 1) is 0)
        {
            // Helpers are marked by the fact that they are odd numbers
            return CORINFO_HELP_UNDEF;
        }
        return unchecked((CorInfoHelpFunc)((nuint)(method) >> 2));
    }

    /// <summary>Get a string describing a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <param name="includeReturnType">Whether to include the return type in the string</param>
    /// <param name="includeThisSpecifier">Whether to include a specifier for whether this is an instance method.</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The string</returns>
    public unsafe string eeGetMethodFullName(CORINFO_METHOD_HANDLE methodHandle, bool includeReturnType = true, bool includeThisSpecifier = true, StringBuilder? stringBuilder = null)
    {
        var helper = eeGetHelperNum(methodHandle);

        if (helper != CORINFO_HELP_UNDEF)
        {
            return helper.ToString();
        }

        stringBuilder ??= new StringBuilder();
        var classHandle = NO_CLASS_HANDLE;

        var success = eeRunFunctorWithSpmiErrorTrap(() => {
            classHandle = info.compCompHnd->getMethodClass(methodHandle);
            eeGetMethodSig(methodHandle, out var sigInfo);
            eePrintMethod(
                stringBuilder,
                classHandle,
                methodHandle,
                &sigInfo,
                includeAssembly: false,
                includeClass: true,
                includeClassInstantiation: true,
                includeMethodInstantiation: true,
                includeSignature: true,
                includeReturnType,
                includeThisSpecifier
            );
        });

        if (success)
        {
            return stringBuilder.ToString();
        }

        // Try without signature
        _ = stringBuilder.Clear();
        return eeGetMethodName(methodHandle, stringBuilder);
    }

    /// <summary>Get the name of a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <param name="stringBuilder">Pre-existing string builder to use or <c>null</c> to create an internal one</param>
    /// <returns>The string</returns>
    /// <remarks>See <see cref="eeGetMethodFullName" /> for documentation about the buffer.</remarks>
    public unsafe string eeGetMethodName(CORINFO_METHOD_HANDLE methodHandle, StringBuilder? stringBuilder = null)
    {
        stringBuilder ??= new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintMethod(
                stringBuilder,
                NO_CLASS_HANDLE,
                methodHandle,
                sigInfo: null,
                includeAssembly: false,
                includeClass: false,
                includeClassInstantiation: false,
                includeMethodInstantiation: false,
                includeSignature: false,
                includeReturnType: false,
                includeThisSpecifier: false
            )
        );

        if (!success)
        {
            _ = stringBuilder.Clear();
            _ = stringBuilder.Append("<unknown method>");
        }
        return stringBuilder.ToString();
    }

    public unsafe void eeGetMethodSig(CORINFO_METHOD_HANDLE methHnd, out CORINFO_SIG_INFO returnSigInfo, CORINFO_CLASS_HANDLE owner = null)
    {
        fixed (CORINFO_SIG_INFO* pReturnSigInfo = &returnSigInfo)
        {
            info.compCompHnd->getMethodSig(methHnd, pReturnSigInfo, owner);
        }
        assert(!varTypeIsComposite(returnSigInfo.retType.VarType) || (returnSigInfo.retTypeClass is not null));
    }

    public unsafe bool eeIsByrefLike(CORINFO_CLASS_HANDLE classHandle)
        => (info.compCompHnd->getClassAttribs(classHandle) & CORINFO_FLG_BYREF_LIKE) is not 0;

    public unsafe bool eeIsFieldStatic(CORINFO_FIELD_HANDLE fieldHandle)
        => info.compCompHnd->isFieldStatic(fieldHandle);

    /// <summary>Print a JIT type.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="corInfoType">the CorInfoType type</param>
    public void eePrintCorInfoType(StringBuilder stringBuilder, CorInfoType corInfoType)
    {
        var corInfoTypeName = corInfoType switch {
            CORINFO_TYPE_UNDEF => "<UNDEF>",
            CORINFO_TYPE_VOID => "void",
            CORINFO_TYPE_BOOL => "bool",
            CORINFO_TYPE_CHAR => "char",
            CORINFO_TYPE_BYTE => "sbyte",
            CORINFO_TYPE_UBYTE => "byte",
            CORINFO_TYPE_SHORT => "short",
            CORINFO_TYPE_USHORT => "ushort",
            CORINFO_TYPE_INT => "int",
            CORINFO_TYPE_UINT => "uint",
            CORINFO_TYPE_LONG => "long",
            CORINFO_TYPE_ULONG => "ulong",
            CORINFO_TYPE_NATIVEINT => "nint",
            CORINFO_TYPE_NATIVEUINT => "nuint",
            CORINFO_TYPE_FLOAT => "float",
            CORINFO_TYPE_DOUBLE => "double",
            CORINFO_TYPE_STRING => "string",
            CORINFO_TYPE_PTR => "ptr",
            CORINFO_TYPE_BYREF => "byref",
            CORINFO_TYPE_VALUECLASS => "struct",
            CORINFO_TYPE_CLASS => "class",
            CORINFO_TYPE_REFANY => "typedbyref",
            CORINFO_TYPE_VAR => "var",
            _ => $"CORINFO_TYPE_INVALID"
        };

        _ = stringBuilder.Append(corInfoTypeName);
    }

    /// <summary>Print a method given by a method handle, its owning class handle and its signature.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="classHandle">Handle for the owning class.</param>
    /// <param name="methodHandle"></param>
    /// <param name="sigInfo">The signature of the method.</param>
    /// <param name="includeAssembly">Whether to print the assembly name.</param>
    /// <param name="includeClass">Whether to print the class name.</param>
    /// <param name="includeClassInstantiation">Whether to print the class instantiation. Only valid when includeClass is passed.</param>
    /// <param name="includeMethodInstantiation">Whether to print the method instantiation. Requires the signature to be passed.</param>
    /// <param name="includeSignature">Whether to print the signature.</param>
    /// <param name="includeReturnType">Whether to include the return type at the end.</param>
    /// <param name="includeThisSpecifier">Whether to include a specifier at the end for whether the method is an instance</param>
    public unsafe void eePrintMethod(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, CORINFO_METHOD_HANDLE methodHandle, CORINFO_SIG_INFO* sigInfo, bool includeAssembly, bool includeClass, bool includeClassInstantiation, bool includeMethodInstantiation, bool includeSignature, bool includeReturnType, bool includeThisSpecifier)
    {
        var helper = eeGetHelperNum(methodHandle);

        if (helper != CORINFO_HELP_UNDEF)
        {
            assert(helper < CORINFO_HELP_COUNT);
            _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"{helper}");
            return;
        }

        if (includeAssembly)
        {
            var pClassAssemblyNameUtf8 = info.compCompHnd->getClassAssemblyName(classHandle);
            var classAssemblyNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassAssemblyNameUtf8);

            _ = stringBuilder.Append(Encoding.UTF8.GetString(classAssemblyNameUtf8));
            _ = stringBuilder.Append('!');
        }

        if (includeClass)
        {
            eePrintType(stringBuilder, classHandle, includeClassInstantiation);
            _ = stringBuilder.Append(':');
        }

        eeAppendPrint(stringBuilder, (buffer, bufferSize, pRequiredBufferSize) =>
            info.compCompHnd->printMethodName(methodHandle, buffer, bufferSize, pRequiredBufferSize)
        );

        if (includeMethodInstantiation && (sigInfo->sigInst.methInstCount > 0))
        {
            _ = stringBuilder.Append('[');
            for (var i = 0u; i < sigInfo->sigInst.methInstCount; i++)
            {
                if (i > 0)
                {
                    _ = stringBuilder.Append(',');
                }

                eePrintTypeOrJitAlias(stringBuilder, sigInfo->sigInst.methInst[i], true);
            }
            _ = stringBuilder.Append(']');
        }

        if (includeSignature)
        {
            _ = stringBuilder.Append('(');

            var argLst = sigInfo->args;
            for (var i = 0u; i < sigInfo->numArgs; i++)
            {
                if (i > 0)
                {
                    _ = stringBuilder.Append(',');
                }

                CORINFO_CLASS_HANDLE vcClsHnd;
                var argTypeWithMod = info.compCompHnd->getArgType(sigInfo, argLst, &vcClsHnd);

                AppendCorInfoTypeWithModModifiers(stringBuilder, argTypeWithMod);
                var type = strip(argTypeWithMod).PreciseVarType;

                switch (type)
                {
                    case TYP_REF:
                    case TYP_STRUCT:
                    {
                        var clsHnd = eeGetArgClass(sigInfo, argLst);

                        // For some SIMD struct types we can get a null back from eeGetArgClass on Linux/X64
                        if (clsHnd != NO_CLASS_HANDLE)
                        {
                            eePrintType(stringBuilder, clsHnd, true);
                            break;
                        }
                        goto default;
                    }

                    default:
                    {
                        eePrintCorInfoType(stringBuilder, strip(argTypeWithMod));
                        break;
                    }
                }
                argLst = info.compCompHnd->getArgNext(argLst);
            }

            _ = stringBuilder.Append(')');

            if (includeReturnType)
            {
                var retType = sigInfo->retType.PreciseVarType;

                if (retType != TYP_VOID)
                {
                    _ = stringBuilder.Append(':');
                    switch (retType)
                    {
                        case TYP_REF:
                        case TYP_STRUCT:
                        {
                            var clsHnd = sigInfo->retTypeClass;

                            if (clsHnd != NO_CLASS_HANDLE)
                            {
                                eePrintType(stringBuilder, clsHnd, true);
                                break;
                            }
                            goto default;
                        }

                        default:
                        {
                            eePrintCorInfoType(stringBuilder, sigInfo->retType);
                            break;
                        }
                    }
                }
            }

            // Does it have a 'this' pointer?
            // Don't count explicit this, which has the this pointer type as the first element of the arg type list
            if (includeThisSpecifier && sigInfo->hasThis() && !sigInfo->hasExplicitThis())
            {
                _ = stringBuilder.Append(":this");
            }
        }

        static void AppendCorInfoTypeWithModModifiers(StringBuilder stringBuilder, CorInfoTypeWithMod corInfoTypeWithMod)
        {
            if ((corInfoTypeWithMod & CORINFO_TYPE_MOD_PINNED) == CORINFO_TYPE_MOD_PINNED)
            {
                _ = stringBuilder.Append("PINNED__");
            }

            if ((corInfoTypeWithMod & CORINFO_TYPE_MOD_COPY_WITH_HELPER) == CORINFO_TYPE_MOD_COPY_WITH_HELPER)
            {
                _ = stringBuilder.Append("COPY_WITH_HELPER__");
            }
        }
    }

    /// <summary>Print a type given by a class handle.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="classHandle">Handle for the class</param>
    /// <param name="includeInstantiation">Whether to print the instantiation of the class</param>
    public unsafe void eePrintType(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, bool includeInstantiation)
    {
        var arrayRank = info.compCompHnd->getArrayRank(classHandle);

        if (arrayRank > 0)
        {
            CORINFO_CLASS_HANDLE childClsHnd;
            var childType = info.compCompHnd->getChildType(classHandle, &childClsHnd);

            if ((childType == CORINFO_TYPE_CLASS) || (childType == CORINFO_TYPE_VALUECLASS))
            {
                eePrintType(stringBuilder, childClsHnd, includeInstantiation);
            }
            else
            {
                eePrintCorInfoType(stringBuilder, childType);
            }

            _ = stringBuilder.Append('[');

            for (var i = 1u; i < arrayRank; i++)
            {
                _ = stringBuilder.Append(',');
            }

            _ = stringBuilder.Append(']');
            return;
        }

        eeAppendPrint(stringBuilder, (buffer, bufferSize, pRequiredBufferSize) =>
            info.compCompHnd->printClassName(classHandle, buffer, bufferSize, pRequiredBufferSize)
        );

        if (!includeInstantiation)
        {
            return;
        }

        var pref = '[';

        for (var typeArgIndex = 0u; ; typeArgIndex++)
        {
            var typeArg = info.compCompHnd->getTypeInstantiationArgument(classHandle, typeArgIndex);

            if (typeArg == NO_CLASS_HANDLE)
            {
                break;
            }

            _ = stringBuilder.Append(pref);
            pref = ',';
            eePrintTypeOrJitAlias(stringBuilder, typeArg, true);
        }

        if (pref != '[')
        {
            _ = stringBuilder.Append(']');
        }
    }

    /// <summary>Print a type given by a class handle. If the type is a primitive type, prints its JIT alias.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="clsHnd">Handle for the class</param>
    /// <param name="includeInstantiation">Whether to print the instantiation of the class</param>
    public unsafe void eePrintTypeOrJitAlias(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE clsHnd, bool includeInstantiation)
    {
        var typ = info.compCompHnd->asCorInfoType(clsHnd);

        if ((typ == CORINFO_TYPE_CLASS) || (typ == CORINFO_TYPE_VALUECLASS))
        {
            eePrintType(stringBuilder, clsHnd, includeInstantiation);
        }
        else
        {
            eePrintCorInfoType(stringBuilder, typ);
        }
    }

    public unsafe bool eeRunWithErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return info.compCompHnd->runWithErrorTrap((errorTrapFunction)(function), parameter);
    }

    public unsafe bool eeRunWithSpmiErrorTrap<TParam>(delegate* unmanaged[Cdecl]<TParam*, void> function, TParam* parameter)
        where TParam : unmanaged
    {
        return info.compCompHnd->runWithSPMIErrorTrap((errorTrapFunction)(function), parameter);
    }

    public unsafe bool eeRunFunctorWithSpmiErrorTrap(Action function)
    {
        var functionHandle = new GCHandle<Action>(function);
        var succeeded = info.compCompHnd->runWithErrorTrap(&NativeShim, (void*)(GCHandle<Action>.ToIntPtr(functionHandle)));

        functionHandle.Dispose();
        return succeeded;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static void NativeShim(void* parameter)
        {
            var functionHandle = GCHandle<Action>.FromIntPtr(unchecked((nint)(parameter)));
            functionHandle.Target();
        }
    }
}

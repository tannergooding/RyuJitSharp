// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public CORINFO_EE_INFO eeInfo;

    public bool eeInfoInitialized;

    public int eeBoundariesCount;

    /// <summary>Boundaries to report to the EE</summary>
    public unsafe ICorDebugInfo.OffsetMapping* eeBoundaries;

    public int eeVarsCount;

    public VarResultInfo? eeVars;

    /// <summary>Append the output of one of the JIT-EE 'print' functions to a StringBuilder.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="printFunc">A functor to print the string that follows the conventions of the JIT-EE print* functions.</param>
    public StringBuilder eeAppendPrint(StringBuilder stringBuilder, EEPrintFunc printFunc)
    {
        const int DefaultBufferSize = 256;

        var stackBuffer = (stackalloc byte[DefaultBufferSize]);
        var arrayBuffer = null as byte[];

        var printed = printFunc(stackBuffer, out var requiredBufferSize);

        scoped Span<byte> messageUtf8;

        if (requiredBufferSize <= DefaultBufferSize)
        {
            assert(printed == (requiredBufferSize - 1));
            messageUtf8 = stackBuffer[..printed];
        }
        else
        {
            arrayBuffer = ArrayPool<byte>.Shared.Rent(requiredBufferSize);
            printed = printFunc(arrayBuffer.AsSpan(0, requiredBufferSize), out requiredBufferSize);

            assert(printed == (requiredBufferSize - 1));
            messageUtf8 = arrayBuffer.AsSpan(0, printed);
        }
        _ = stringBuilder.Append(Encoding.UTF8.GetString(messageUtf8));

        if (arrayBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(arrayBuffer);
        }
        return stringBuilder;
    }

    /// <summary>Convert a tuple of "{ value, pValue }" to "CORINFO_CONST_LOOKUP".</summary>
    /// <param name="value">The direct value (IAT_VALUE)</param>
    /// <param name="pValue">The indirect value (IAT_PVALUE)</param>
    /// <returns>The lookup.</returns>
    public unsafe CORINFO_CONST_LOOKUP eeConvertToLookup(void* value, void* pValue)
    {
        Unsafe.SkipInit(out CORINFO_CONST_LOOKUP lookup);

        if (value is not null)
        {
            assert(pValue is null);
            lookup.accessType = IAT_VALUE;
            lookup.addr = value;
        }
        else
        {
            assert(pValue is not null);
            lookup.accessType = IAT_PVALUE;
            lookup.addr = pValue;
        }
        return lookup;
    }

    public static unsafe CORINFO_METHOD_HANDLE eeFindHelper(CorInfoHelpFunc helpFunc)
    {
        // Helpers are marked by the fact that they are odd numbers
        // force this to be an odd number (will shift it back to extract)

        assert(helpFunc < CORINFO_HELP_COUNT);
        return (CORINFO_METHOD_HANDLE)(((int)(helpFunc) << 2) + 1);
    }

    public unsafe CORINFO_CLASS_HANDLE eeGetArgClass(CORINFO_SIG_INFO* sigInfo, CORINFO_ARG_LIST_HANDLE argListHandle)
        => info.compCompHnd->getArgClass(sigInfo, argListHandle);

    public unsafe void eeGetCallInfo(in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_RESOLVED_TOKEN constrainedToken, CORINFO_CALLINFO_FLAGS flags, out CORINFO_CALL_INFO result)
    {
        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        fixed (CORINFO_RESOLVED_TOKEN* pConstrainedToken = &constrainedToken)
        fixed (CORINFO_CALL_INFO* pResult = &result)
        {
            info.compCompHnd->getCallInfo(pResolvedToken, pConstrainedToken, info.compMethodHnd, flags, pResult);
        }
    }

    public unsafe void eeGetCallSiteSig(int sigTok, CORINFO_MODULE_HANDLE scope, CORINFO_CONTEXT_HANDLE context, out CORINFO_SIG_INFO sigInfo)
    {
        // For varargs we need the number of arguments at the call site

        fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
        {
            info.compCompHnd->findCallSiteSig(scope, sigTok, context, pSigInfo);
        }
        assert(!varTypeIsComposite(sigInfo.retType.VarType) || (sigInfo.retTypeClass is not null));
    }

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

    public unsafe CORINFO_CLASS_HANDLE eeGetClassFromContext(CORINFO_CONTEXT_HANDLE context)
    {
        if (context == METHOD_BEING_COMPILED_CONTEXT())
        {
            return impInlineRoot.info.compClassHnd;
        }

        if (unchecked((nint)(context) & (nint)(CORINFO_CONTEXTFLAGS_MASK)) == (nint)(CORINFO_CONTEXTFLAGS_CLASS))
        {
            return unchecked((CORINFO_CLASS_HANDLE)((nint)(context) & ~(nint)(CORINFO_CONTEXTFLAGS_MASK)));
        }
        else
        {
            return info.compCompHnd->getMethodClass(unchecked((CORINFO_METHOD_HANDLE)((nint)(context) & ~(nint)(CORINFO_CONTEXTFLAGS_MASK))));
        }
    }

    /// <summary>Get the name (including namespace and instantiation) of a type.</summary>
    /// <param name="classHandle">the handle of the class</param>
    /// <returns>The name string.</returns>
    /// <remarks>If missing information (in SPMI), then return a placeholder string.</remarks>
    public unsafe string eeGetClassName(CORINFO_CLASS_HANDLE classHandle)
    {
        var stringBuilder = new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintType(
                stringBuilder,
                classHandle,
                includeInstantiation: true
            )
        );
        return success ? stringBuilder.ToString() : "<unknown class>";
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

    public unsafe void eeGetFieldInfo(in CORINFO_RESOLVED_TOKEN resolvedToken, CORINFO_ACCESS_FLAGS flags, out CORINFO_FIELD_INFO result)
    {
        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        fixed (CORINFO_FIELD_INFO* pResult = &result)
        {
            info.compCompHnd->getFieldInfo(pResolvedToken, info.compMethodHnd, flags, pResult);
        }
    }

    /// <summary>Get a string describing a field.</summary>
    /// <param name="fldHnd">the field handle</param>
    /// <param name="includeType">Whether to prefix the string with &lt;type name&gt;:</param>
    public unsafe string eeGetFieldName(CORINFO_FIELD_HANDLE fldHnd, bool includeType)
    {
        var stringBuilder = new StringBuilder();

        var success = eeRunFunctorWithSpmiErrorTrap(() =>
            eePrintField(
                stringBuilder,
                fldHnd,
                includeType
            )
        );

        if (!success)
        {
            if (includeType)
            {
                _ = stringBuilder.Clear();
                _ = stringBuilder.Append("<unknown class>:");

                success = eeRunFunctorWithSpmiErrorTrap(() =>
                    eePrintField(
                        stringBuilder,
                        fldHnd,
                        includeType: false
                    )
                );

                return success ? stringBuilder.ToString() : "<unknown class>:<unknown field>";
            }
        }
        return success ? stringBuilder.ToString() : "<unknown field>";
    }

    public unsafe var_types eeGetFieldType(CORINFO_FIELD_HANDLE fldHnd, CORINFO_CLASS_HANDLE memberParent = null)
    {
        return info.compCompHnd->getFieldType(fldHnd, structType: null, memberParent).VarType;
    }

    public unsafe var_types eeGetFieldType(CORINFO_FIELD_HANDLE fldHnd, out CORINFO_CLASS_HANDLE structHnd, CORINFO_CLASS_HANDLE memberParent = null)
    {
        fixed (CORINFO_CLASS_HANDLE* pStructHnd = &structHnd)
        {
            return info.compCompHnd->getFieldType(fldHnd, pStructHnd, memberParent).VarType;
        }
    }

    public static unsafe CorInfoHelpFunc eeGetHelperNum(CORINFO_METHOD_HANDLE method)
    {
        var value = (nint)(method);

        if ((value & 1) == 0)
        {
            // Helpers are marked by the fact that they are odd numbers
            return CORINFO_HELP_UNDEF;
        }
        return (CorInfoHelpFunc)((value >>> 2));
    }

    /// <summary>Get a string describing a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <param name="includeReturnType">Whether to include the return type in the string</param>
    /// <param name="includeThisSpecifier">Whether to include a specifier for whether this is an instance method.</param>
    /// <returns>The string</returns>
    public unsafe string eeGetMethodFullName(CORINFO_METHOD_HANDLE methodHandle, bool includeReturnType = true, bool includeThisSpecifier = true)
    {
        var helper = eeGetHelperNum(methodHandle);

        if (helper != CORINFO_HELP_UNDEF)
        {
            return helper.ToString();
        }

        var stringBuilder = new StringBuilder();
        var classHandle = NO_CLASS_HANDLE;

        var success = eeRunFunctorWithSpmiErrorTrap(() => {
            classHandle = info.compCompHnd->getMethodClass(methodHandle);
            eeGetMethodSig(methodHandle, out var sigInfo);
            _ = eePrintMethod(
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
        return eeGetMethodName(methodHandle);
    }

    /// <summary>Get the name of a method.</summary>
    /// <param name="methodHandle">the method handle</param>
    /// <returns>The string</returns>
    /// <remarks>See <see cref="eeGetMethodFullName" /> for documentation about the buffer.</remarks>
    public unsafe string eeGetMethodName(CORINFO_METHOD_HANDLE methodHandle)
    {
        var stringBuilder = new StringBuilder();

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
        return success ? stringBuilder.ToString() : "<unknown method>";
    }

    public unsafe void eeGetMethodSig(CORINFO_METHOD_HANDLE methHnd, out CORINFO_SIG_INFO returnSigInfo, CORINFO_CLASS_HANDLE owner = null)
    {
        fixed (CORINFO_SIG_INFO* pReturnSigInfo = &returnSigInfo)
        {
            info.compCompHnd->getMethodSig(methHnd, pReturnSigInfo, owner);
        }
        assert(!varTypeIsComposite(returnSigInfo.retType.VarType) || (returnSigInfo.retTypeClass is not null));
    }

    public unsafe void eeGetSig(int sigTok, CORINFO_MODULE_HANDLE scope, CORINFO_CONTEXT_HANDLE context, out CORINFO_SIG_INFO sigInfo)
    {
        fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
        {
            info.compCompHnd->findSig(scope, sigTok, context, pSigInfo);
        }
        assert(!varTypeIsComposite(sigInfo.retType.VarType) || (sigInfo.retTypeClass is not null));
    }

    public unsafe void eeGetStmtOffsets()
    {
        int offsetsCount;
        int* offsets;
        ICorDebugInfo.BoundaryTypes offsetsImplicit;

        if (compIsForInlining)
        {
            // We do not get explicit boundaries for inlinees, only implicit ones.
            offsetsImplicit = impInlineRoot.info.compStmtOffsetsImplicit;
            offsetsCount = 0;
            offsets = null;
        }
        else
        {
            info.compCompHnd->getBoundaries(info.compMethodHnd, &offsetsCount, &offsets, &offsetsImplicit);
        }

        // Set the implicit boundaries
        info.compStmtOffsetsImplicit = offsetsImplicit;

        // Process the explicit boundaries
        info.compStmtOffsetsCount = 0;

        if (offsetsCount == 0)
        {
            return;
        }

        info.compStmtOffsets = new IL_OFFSET[offsetsCount];
        var stmtOffsetCount = 0;

        for (var i = 0; i < offsetsCount; i++)
        {
            if (offsets[i] > info.compILCodeSize)
            {
                continue;
            }

            info.compStmtOffsets[stmtOffsetCount] = offsets[i];
            stmtOffsetCount++;
        }

        info.compStmtOffsetsCount = stmtOffsetCount;
        info.compCompHnd->freeArray(offsets);
    }

    public unsafe void eeGetVars()
    {
        ICorDebugInfo.ILVarInfo* varInfoTable;
        int varInfoCount;
        bool extendOthers;

        info.compCompHnd->getVars(info.compMethodHnd, &varInfoCount, &varInfoTable, &extendOthers);

#if DEBUG
        if (verbose)
        {
            jitprintf($"getVars() returned cVars = {varInfoCount}, extendOthers = {extendOthers}\n");
        }
#endif

        // Over allocate in case extendOthers is set.
        var varInfoCountExtra = varInfoCount;

        if (extendOthers)
        {
            varInfoCountExtra += info.compLocalsCount;
        }

        if (varInfoCountExtra == 0)
        {
            return;
        }

        info.compVarScopes = new VarScopeDsc[varInfoCountExtra];
        var localVars = info.compVarScopes.AsSpan();
        var v = varInfoTable;

        for (var i = 0; i < varInfoCount; i++, v++)
        {
#if DEBUG
            if (verbose)
            {
                jitprintf($"var:{v->varNumber} start:{v->startOffset} end:{v->endOffset}\n");
            }
#endif

            if (v->startOffset >= v->endOffset)
            {
                continue;
            }

            assert(v->startOffset <= info.compILCodeSize);
            assert(v->endOffset <= info.compILCodeSize);

            ref var localVar = ref localVars[i];

            localVar.vsdLifeBeg = v->startOffset;
            localVar.vsdLifeEnd = v->endOffset;
            localVar.vsdLVnum = i;
            localVar.vsdVarNum = compMapILvarNum(v->varNumber);

#if DEBUG
            localVar.vsdName = gtGetLclVarName(localVar.vsdVarNum);
#endif

            info.compVarScopesCount++;
        }

        /* If extendOthers is set, then assume the scope of unreported vars
           is the entire method. Note that this will cause fgExtendDbgLifetimes()
           to zero-initialize all of them. This will be expensive if it's used
           for too many variables.
         */
        if (extendOthers)
        {
            // Allocate a bit-array for all the variables and initialize to false
            var varInfoProvided = new bool[info.compLocalsCount];

            // Find which vars have absolutely no varInfo provided
            assert(info.compVarScopesCount == varInfoCount);

            for (var i = 0; i < varInfoCount; i++)
            {
                ref var localVar = ref localVars[i];
                varInfoProvided[localVar.vsdVarNum] = true;
            }

            // Create entries for the variables with no varInfo

            for (var varNum = 0; varNum < info.compLocalsCount; varNum++)
            {
                if (varInfoProvided[varNum])
                {
                    continue;
                }

                // Create a varInfo with scope over the entire method
                ref var localVar = ref localVars[varInfoCount + varNum];

                localVar.vsdLifeBeg = 0;
                localVar.vsdLifeEnd = info.compILCodeSize;
                localVar.vsdVarNum = varNum;
                localVar.vsdLVnum = info.compVarScopesCount;

#if DEBUG
                localVar.vsdName = gtGetLclVarName(localVar.vsdVarNum);
#endif

                info.compVarScopesCount++;
            }
        }

        if (varInfoCount != 0)
        {
            info.compCompHnd->freeArray(varInfoTable);
        }

#if DEBUG
        if (verbose)
        {
            compDispLocalVars();
        }
#endif
    }

    public unsafe bool eeIsByrefLike(CORINFO_CLASS_HANDLE classHandle)
        => (info.compCompHnd->getClassAttribs(classHandle) & CORINFO_FLG_BYREF_LIKE) != 0;

    public unsafe bool eeIsIntrinsic(CORINFO_METHOD_HANDLE ftn)
        => info.compCompHnd->isIntrinsic(ftn);

    public unsafe bool eeIsFieldStatic(CORINFO_FIELD_HANDLE fieldHandle)
        => info.compCompHnd->isFieldStatic(fieldHandle);

    public unsafe bool eeIsSharedInst(CORINFO_CLASS_HANDLE clsHnd)
        => (info.compCompHnd->getClassAttribs(clsHnd) & CORINFO_FLG_SHAREDINST) != 0;

    public unsafe bool eeIsValueClass(CORINFO_CLASS_HANDLE clsHnd)
        => info.compCompHnd->isValueClass(clsHnd);

    /// <summary>Print a JIT type.</summary>
    /// <param name="corInfoType">the CorInfoType type</param>
    public string eeGetCorInfoTypeName(CorInfoType corInfoType) => corInfoType switch {
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
        CORINFO_TYPE_PTR => "ptr",
        CORINFO_TYPE_BYREF => "byref",
        CORINFO_TYPE_VALUECLASS => "struct",
        CORINFO_TYPE_CLASS => "class",
        _ => "CORINFO_TYPE_INVALID"
    };

    /// <summary>Print a field name to a StringPrinter.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="fldHnd">The field</param>
    /// <param name="includeType">Whether to prefix the string by &lt;class name&gt;:</param>
    public unsafe StringBuilder eePrintField(StringBuilder stringBuilder, CORINFO_FIELD_HANDLE fldHnd, bool includeType)
    {
        if (includeType)
        {
            var cls = info.compCompHnd->getFieldClass(fldHnd);
            _ = eePrintType(stringBuilder, cls, includeInstantiation: true);
            _ = stringBuilder.Append(':');
        }

        _ = eeAppendPrint(stringBuilder, (buffer, out requiredBufferSize) => {
            fixed (byte* pBuffer = buffer)
            {
                nint nativeRequiredBufferSize;
                var result = (int)(info.compCompHnd->printFieldName(fldHnd, pBuffer, buffer.Length, &nativeRequiredBufferSize));

                requiredBufferSize = (int)(nativeRequiredBufferSize);
                return result;
            }
        });

        return stringBuilder;
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
    public unsafe StringBuilder eePrintMethod(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, CORINFO_METHOD_HANDLE methodHandle, CORINFO_SIG_INFO* sigInfo, bool includeAssembly, bool includeClass, bool includeClassInstantiation, bool includeMethodInstantiation, bool includeSignature, bool includeReturnType, bool includeThisSpecifier)
    {
        var helper = eeGetHelperNum(methodHandle);

        if (helper != CORINFO_HELP_UNDEF)
        {
            assert(helper < CORINFO_HELP_COUNT);
            return stringBuilder.Append(helper);
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
            _ = eePrintType(stringBuilder, classHandle, includeClassInstantiation);
            _ = stringBuilder.Append(':');
        }

        _ = eeAppendPrint(stringBuilder, (buffer, out requiredBufferSize) => {
            fixed (byte* pBuffer = buffer)
            {
                nint nativeRequiredBufferSize;
                var result = (int)(info.compCompHnd->printMethodName(methodHandle, pBuffer, buffer.Length, &nativeRequiredBufferSize));

                requiredBufferSize = (int)(nativeRequiredBufferSize);
                return result;
            }
        });

        if (includeMethodInstantiation && (sigInfo->sigInst.methInstCount > 0))
        {
            _ = stringBuilder.Append('[');
            for (var i = 0; i < sigInfo->sigInst.methInstCount; i++)
            {
                if (i > 0)
                {
                    _ = stringBuilder.Append(',');
                }

                _ = eePrintTypeOrJitAlias(stringBuilder, sigInfo->sigInst.methInst[i], true);
            }
            _ = stringBuilder.Append(']');
        }

        if (includeSignature)
        {
            _ = stringBuilder.Append('(');

            var argLst = sigInfo->args;
            for (var i = 0; i < sigInfo->numArgs; i++)
            {
                if (i > 0)
                {
                    _ = stringBuilder.Append(',');
                }

                CORINFO_CLASS_HANDLE vcClsHnd;
                var argTypeWithMod = info.compCompHnd->getArgType(sigInfo, argLst, &vcClsHnd);

                _ = AppendCorInfoTypeWithModModifiers(stringBuilder, argTypeWithMod);
                var type = strip(argTypeWithMod).PreciseVarType;

                switch (type)
                {
                    case TYP_REF:
                    case TYP_STRUCT:
                    {
                        var clsHnd = eeGetArgClass(sigInfo, argLst);

                        // For some simd struct types we can get a null back from eeGetArgClass on Linux/X64
                        if (clsHnd != NO_CLASS_HANDLE)
                        {
                            _ = eePrintType(stringBuilder, clsHnd, true);
                            break;
                        }
                        goto default;
                    }

                    default:
                    {
                        _ = stringBuilder.Append(eeGetCorInfoTypeName(strip(argTypeWithMod)));
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
                                _ = eePrintType(stringBuilder, clsHnd, true);
                                break;
                            }
                            goto default;
                        }

                        default:
                        {
                            _ = stringBuilder.Append(eeGetCorInfoTypeName(sigInfo->retType));
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

        return stringBuilder;

        static StringBuilder AppendCorInfoTypeWithModModifiers(StringBuilder stringBuilder, CorInfoTypeWithMod corInfoTypeWithMod)
        {
            if ((corInfoTypeWithMod & CORINFO_TYPE_MOD_PINNED) == CORINFO_TYPE_MOD_PINNED)
            {
                _ = stringBuilder.Append("PINNED__");
            }

            if ((corInfoTypeWithMod & CORINFO_TYPE_MOD_COPY_WITH_HELPER) == CORINFO_TYPE_MOD_COPY_WITH_HELPER)
            {
                _ = stringBuilder.Append("COPY_WITH_HELPER__");
            }

            return stringBuilder;
        }
    }

    /// <summary>Print a type given by a class handle.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="classHandle">Handle for the class</param>
    /// <param name="includeInstantiation">Whether to print the instantiation of the class</param>
    public unsafe StringBuilder eePrintType(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE classHandle, bool includeInstantiation)
    {
        var arrayRank = info.compCompHnd->getArrayRank(classHandle);

        if (arrayRank > 0)
        {
            CORINFO_CLASS_HANDLE childClsHnd;
            var childType = info.compCompHnd->getChildType(classHandle, &childClsHnd);

            if ((childType == CORINFO_TYPE_CLASS) || (childType == CORINFO_TYPE_VALUECLASS))
            {
                _ = eePrintType(stringBuilder, childClsHnd, includeInstantiation);
            }
            else
            {
                _ = stringBuilder.Append(eeGetCorInfoTypeName(childType));
            }

            _ = stringBuilder.Append('[');

            for (var i = 1; i < arrayRank; i++)
            {
                _ = stringBuilder.Append(',');
            }

            _ = stringBuilder.Append(']');
        }

        _ = eeAppendPrint(stringBuilder, (buffer, out requiredBufferSize) => {
            fixed (byte* pBuffer = buffer)
            {
                nint nativeRquiredBufferSize;
                var result = (int)(info.compCompHnd->printClassName(classHandle, pBuffer, buffer.Length, &nativeRquiredBufferSize));

                requiredBufferSize = (int)(nativeRquiredBufferSize);
                return result;
            }
        });

        if (!includeInstantiation)
        {
            return stringBuilder;
        }

        var pref = '[';

        for (var typeArgIndex = 0; ; typeArgIndex++)
        {
            var typeArg = info.compCompHnd->getTypeInstantiationArgument(classHandle, typeArgIndex);

            if (typeArg == NO_CLASS_HANDLE)
            {
                break;
            }

            _ = stringBuilder.Append(pref);
            pref = ',';
            _ = eePrintTypeOrJitAlias(stringBuilder, typeArg, true);
        }

        if (pref != '[')
        {
            _ = stringBuilder.Append(']');
        }
        return stringBuilder;
    }

    /// <summary>Print a type given by a class handle. If the type is a primitive type, prints its JIT alias.</summary>
    /// <param name="stringBuilder">the builder</param>
    /// <param name="clsHnd">Handle for the class</param>
    /// <param name="includeInstantiation">Whether to print the instantiation of the class</param>
    public unsafe StringBuilder eePrintTypeOrJitAlias(StringBuilder stringBuilder, CORINFO_CLASS_HANDLE clsHnd, bool includeInstantiation)
    {
        var typ = info.compCompHnd->asCorInfoType(clsHnd);

        if ((typ == CORINFO_TYPE_CLASS) || (typ == CORINFO_TYPE_VALUECLASS))
        {
            return eePrintType(stringBuilder, clsHnd, includeInstantiation);
        }
        else
        {
            return stringBuilder.Append(eeGetCorInfoTypeName(typ));
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

    public unsafe bool eeRunFunctorWithErrorTrap(Action function)
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

    public unsafe bool eeRunFunctorWithSpmiErrorTrap(Action function)
    {
        var functionHandle = new GCHandle<Action>(function);
        var succeeded = info.compCompHnd->runWithSPMIErrorTrap(&NativeShim, (void*)(GCHandle<Action>.ToIntPtr(functionHandle)));

        functionHandle.Dispose();
        return succeeded;

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        static void NativeShim(void* parameter)
        {
            var functionHandle = GCHandle<Action>.FromIntPtr(unchecked((nint)(parameter)));
            functionHandle.Target();
        }
    }

#if DEBUG
    /// <summary>wraps getClassSize but if doing SuperPMI replay and the value isn't found, use a bogus size.</summary>
    /// <param name="clsHnd"></param>
    /// <returns>Either the actual class size, or (unsigned)-1 if SuperPMI didn't have it.</returns>
    /// <remarks>This is only allowed for JitDump output.</remarks>
    public unsafe int eeTryGetClassSize(CORINFO_CLASS_HANDLE clsHnd)
    {
        var classSize = -1;
        _ = eeRunFunctorWithSpmiErrorTrap(() => classSize = info.compCompHnd->getClassSize(clsHnd));
        return classSize;
    }
#endif
}

// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if SWIFT_SUPPORT
    private unsafe void impPopArgsForSwiftCall(GenTreeCall call, in CORINFO_SIG_INFO sig, ref GenTree? swiftErrorNode)
    {
#if DEBUG
        JITDUMP($"Creating args for Swift call [{call.TreeId:D6}]\n");
#endif

        var swiftErrorIndex = uint.MaxValue;
        var swiftSelfIndex = uint.MaxValue;
        var swiftIndirectResultIndex = uint.MaxValue;
        var selfType = NO_CLASS_HANDLE;
        var spillStack = false;
        var sigArg = sig.args;

        for (var argIndex = 0; argIndex < sig.numArgs; argIndex++, sigArg = info.compCompHnd->getArgNext(sigArg))
        {
            CORINFO_CLASS_HANDLE argClass;
            CorInfoType argType;

            fixed (CORINFO_SIG_INFO* pSig = &sig)
            {
                argType = strip(info.compCompHnd->getArgType(pSig, sigArg, &argClass));
            }

            var argIsByrefOrPtr = argType is CORINFO_TYPE_BYREF or CORINFO_TYPE_PTR;
            if (argIsByrefOrPtr)
            {
                fixed (CORINFO_SIG_INFO* pSig = &sig)
                {
                    argClass = info.compCompHnd->getArgClass(pSig, sigArg);
                }

                argType = info.compCompHnd->getChildType(argClass, &argClass);
            }

            if (argType is not CORINFO_TYPE_VALUECLASS)
            {
                continue;
            }

            if (info.compCompHnd->isIntrinsicType(argClass))
            {
                var className = getClassNameFromMetadata(argClass, out var namespaceName);
                if (namespaceName is "System.Runtime.InteropServices.Swift")
                {
                    if (className is "SwiftError")
                    {
                        if (!argIsByrefOrPtr)
                        {
                            BADCODE("Expected SwiftError pointer/reference, got struct");
                        }

                        if (swiftErrorIndex != uint.MaxValue)
                        {
                            BADCODE("Duplicate SwiftError* parameter");
                        }

                        swiftErrorIndex = (uint)argIndex;
                        spillStack = true;
                    }
                    else if (className is "SwiftSelf")
                    {
                        if (argIsByrefOrPtr)
                        {
                            BADCODE("Expected SwiftSelf struct, got pointer/reference");
                        }

                        if (swiftSelfIndex != uint.MaxValue)
                        {
                            BADCODE("Duplicate SwiftSelf parameter");
                        }

                        swiftSelfIndex = (uint)argIndex;
                    }
                    else if (className is "SwiftSelf`1")
                    {
                        if (argIsByrefOrPtr)
                        {
                            BADCODE("Expected SwiftSelf<T> struct, got pointer/reference");
                        }

                        if (swiftSelfIndex != uint.MaxValue)
                        {
                            BADCODE("Duplicate SwiftSelf parameter");
                        }

                        if (argIndex != sig.numArgs - 1)
                        {
                            BADCODE("SwiftSelf<T> must be the last argument in the signature");
                        }

                        selfType = info.compCompHnd->getTypeInstantiationArgument(argClass, 0);
                        if (info.compCompHnd->asCorInfoType(selfType) is not CORINFO_TYPE_VALUECLASS)
                        {
                            BADCODE("SwiftSelf<T> expects T to be a value class");
                        }

                        swiftSelfIndex = (uint)argIndex;
                    }
                    else if (className is "SwiftIndirectResult")
                    {
                        if (argIsByrefOrPtr)
                        {
                            BADCODE("Expected SwiftIndirectResult struct, got pointer/reference");
                        }

                        if (sig.retType is not CORINFO_TYPE_VOID)
                        {
                            BADCODE("Functions with SwiftIndirectResult arguments must return void");
                        }

                        if (swiftIndirectResultIndex != uint.MaxValue)
                        {
                            BADCODE("Duplicate SwiftIndirectResult argument");
                        }

                        swiftIndirectResultIndex = (uint)argIndex;
                        spillStack = true;
                    }
                }
            }

            if (argIsByrefOrPtr)
            {
                continue;
            }

            var node = impStackTop(sig.numArgs - 1 - argIndex).val;
            if (!node.Oper.IsLocalRead)
            {
                _ = impSpillStackEntry(stackState.esStackDepth - sig.numArgs + argIndex, BAD_VAR_NUM,
                    assertOnRecursion: false, "Swift struct arg with lowering");
            }
        }

        if (spillStack)
        {
            impSpillSideEffects(true, CHECK_SPILL_ALL, "Spill for swift call");
        }

        impPopCallArgs(sig, call);

        JITDUMP("Node after popping args:\n");
        DISPTREE(call);
        JITDUMP("\n");

        var swiftErrorArg = swiftErrorIndex != uint.MaxValue ? call.Args.GetArgByIndex((int)swiftErrorIndex) : null;
        var argIndexAfterPop = 0;

        for (var arg = call.Args.Head; arg is not null; argIndexAfterPop++)
        {
            if (!varTypeIsStruct(arg.SignatureType))
            {
                arg = arg.Next;
                continue;
            }

            if (varTypeIsSimd(arg.SignatureType))
            {
                IMPL_LIMITATION("SIMD types are currently unsupported in Swift calls");
            }

#if DEBUG
            JITDUMP($"  Argument {argIndexAfterPop} is a struct [{arg.Node.TreeId:D6}]\n");
#endif
            assert(arg.Node.Oper.IsLocalRead);
            var structVal = arg.Node.AsLclVarCommon();
            var insertAfter = arg;

            if (((argIndexAfterPop == swiftSelfIndex) && (selfType == NO_CLASS_HANDLE)) ||
                (argIndexAfterPop == swiftIndirectResultIndex))
            {
                var primitiveSelf = gtNewLclFldNode(TYP_I_IMPL, structVal.LclNum, structVal.LclOffs);
                var newArg = NewCallArg.CreateForPrimitive(primitiveSelf, TYP_I_IMPL);

                if (argIndexAfterPop == swiftSelfIndex)
                {
                    insertAfter = call.Args.InsertAfter(insertAfter, newArg.WithWellKnownArg(WellKnownArg.SwiftSelf));
                }
                else
                {
                    _ = call.Args.PushFront(newArg.WithWellKnownArg(WellKnownArg.RetBuffer));
                    call._callMoreFlags |= GTF_CALL_M_RETBUFFARG;
                }
            }
            else
            {
                var argClass = (argIndexAfterPop == swiftSelfIndex) ? selfType : arg.SignatureClassHandle;
                ref readonly var lowering = ref GetSwiftLowering(argClass);

                if (lowering.byReference)
                {
#if DEBUG
                    JITDUMP($"  Argument {argIndexAfterPop} of type {typGetObjLayout(arg.SignatureClassHandle).ClassName} must be passed by reference\n");
#endif

                    var addrNode = gtNewLclAddrNode(TYP_I_IMPL, structVal.LclNum, structVal.LclOffs);
                    var newArg = NewCallArg.CreateForPrimitive(addrNode, TYP_I_IMPL);
                    if (argIndexAfterPop == swiftSelfIndex)
                    {
                        newArg = newArg.WithWellKnownArg(WellKnownArg.SwiftSelf);
                    }

                    JITDUMP("    Passing by reference\n");
                    insertAfter = call.Args.InsertAfter(insertAfter, newArg);
                }
                else
                {
#if DEBUG
                    JITDUMP($"  Argument {argIndexAfterPop} of type {typGetObjLayout(arg.SignatureClassHandle).ClassName} must be passed as {lowering.numLoweredElements} primitive(s)\n");

                    for (var i = 0; i < lowering.numLoweredElements; i++)
                    {
                        JITDUMP($"    [{i}] @ +{lowering.offsets[i]:D2}: {lowering.loweredElements[i].PreciseVarType.Name}\n");
                    }
#endif

                    for (var i = 0; i < lowering.numLoweredElements; i++)
                    {
                        var loweredType = lowering.loweredElements[i].VarType;
                        var offset = lowering.offsets[i];
                        var layout = structVal.GetLayout(this);
                        assert(layout is not null);
                        var sizeToRead = (int)Math.Min(layout.Size - (uint)offset, (uint)loweredType.Size);
                        assert(sizeToRead > 0);

                        GenTree loweredNode;
                        if (sizeToRead == loweredType.Size)
                        {
                            loweredNode = gtNewLclFldNode(loweredType, structVal.LclNum,
                                checked((ushort)(structVal.LclOffs + offset)));
                        }
                        else
                        {
                            GenTree? combined = null;
                            var relOffset = 0;

                            void AddSegment(var_types type)
                            {
                                GenTree value = gtNewLclFldNode(type, structVal.LclNum,
                                    checked((ushort)(structVal.LclOffs + offset + relOffset)));

                                if (loweredType is TYP_LONG)
                                {
                                    value = gtNewCastNode(TYP_LONG, value, fromUnsigned: true, TYP_LONG);
                                }

                                if (relOffset > 0)
                                {
                                    value = gtNewBinaryNode(GT_LSH, loweredType.ActualType, value,
                                        gtNewIconNode(TYP_INT, relOffset * 8));
                                }

                                combined = combined is null ? value :
                                    gtNewBinaryNode(GT_OR, loweredType.ActualType, combined, value);
                                relOffset += type.Size;
                            }

                            if (sizeToRead - relOffset >= 4)
                            {
                                AddSegment(TYP_INT);
                            }

                            if (sizeToRead - relOffset >= 2)
                            {
                                AddSegment(TYP_USHORT);
                            }

                            if (sizeToRead - relOffset >= 1)
                            {
                                AddSegment(TYP_UBYTE);
                            }

                            assert(relOffset == sizeToRead);
                            assert(combined is not null);
                            loweredNode = combined;
                        }

#if DEBUG
                        JITDUMP($"    Adding expanded primitive argument [{loweredNode.TreeId:D6}]\n");
                        DISPTREE(loweredNode);
#endif
                        insertAfter = call.Args.InsertAfter(insertAfter,
                            NewCallArg.CreateForPrimitive(loweredNode, loweredType));
                    }
                }
            }

#if DEBUG
            JITDUMP($"  Removing plain struct argument [{structVal.TreeId:D6}]\n");
#endif
            call.Args.Remove(arg);
            arg = insertAfter.Next;
        }

        if (swiftErrorArg is not null)
        {
            var errorSentinelValueNode = gtNewIconNode(TYP_INT, 0);
            _ = call.Args.InsertAfter(swiftErrorArg,
                NewCallArg.CreateForPrimitive(errorSentinelValueNode).WithWellKnownArg(WellKnownArg.SwiftError));

            swiftErrorNode = swiftErrorArg.Node;
            call.Args.Remove(swiftErrorArg);
        }

#if DEBUG
        if (verbose && call.Type == TYP_STRUCT && sig.retTypeClass != NO_CLASS_HANDLE)
        {
            ref readonly var lowering = ref GetSwiftLowering(sig.retTypeClass);
            if (lowering.byReference)
            {
                jitprintf($"  Call returns {typGetObjLayout(sig.retTypeClass).ClassName} by reference\n");
            }
            else
            {
                jitprintf($"  Call returns {typGetObjLayout(sig.retTypeClass).ClassName} as {lowering.numLoweredElements} primitive(s) in registers\n");
                for (var i = 0; i < lowering.numLoweredElements; i++)
                {
                    jitprintf($"    [{i}] @ +{lowering.offsets[i]:D2}: {lowering.loweredElements[i].PreciseVarType.Name}\n");
                }
            }
        }
#endif

        JITDUMP("Final result after Swift call lowering:\n");
        DISPTREE(call);
        JITDUMP("\n");
        impRetypeUnmanagedCallArgs(call);
    }
#endif
}

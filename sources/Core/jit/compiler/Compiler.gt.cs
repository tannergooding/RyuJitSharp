// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace RyuJitSharp;

public partial class Compiler
{
    public static unsafe fgWalkPreFn gtMarkColonCond;

    public static unsafe fgWalkPreFn gtClearColonCond;

#if DEBUG
    public void gtDispRange(LIR.ReadOnlyRange range)
    {
        foreach (var node in range)
        {
            gtDispLIRNode(node);
        }
    }

    public void gtDispClassLayout(ClassLayout layout, var_types type)
    {
        assert(layout is not null);

        if (layout.IsBlockLayout)
        {
            jitprintf($"<{layout.Size}>");
        }
        else if (varTypeIsSimd(type))
        {
            jitprintf($"<{layout.ShortClassName}>");
        }
        else
        {
            jitprintf($"<{layout.ShortClassName}, {layout.Size}>");
        }
    }

    public void gtDispLclVar(int lclNum, bool padForBiggestDisp = true)
    {
        var name = gtGetLclVarName(lclNum);

        if (name.Length == 0)
        {
            return;
        }

        jitprintf(name);

        if (padForBiggestDisp && (name.Length < LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH))
        {
            jitprintf(new string(' ', int.Max(0, LONGEST_COMMON_LCL_VAR_DISPLAY_LENGTH - name.Length)));
        }
    }

    public void gtDispLclVarStructType(int lclNum)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);
        var type = varDsc.Type;

        if (type is TYP_STRUCT)
        {
            var layout = varDsc.Layout;
            assert(layout is not null);
            gtDispClassLayout(layout, type);
        }
    }

    public void gtDispLIRNode(GenTree node, string prefixMsg = "")
    {
        var indentStack = new IndentStack(this);
        var prefixIndent = 0;

        if (prefixMsg is not null)
        {
            prefixIndent = prefixMsg.Length;
        }

        var nodeIsCall = node.Oper.IsCall;

        // Visit operands
        var operandArc = IIArcTop;

        foreach (var operand in node.Operands)
        {
            if (!operand.IsValue)
            {
                // Either of these situations may happen with calls.
                continue;
            }

            if (nodeIsCall)
            {
                var call = node.AsCall();

                if (operand == call.ControlExpr)
                {
                    DisplayOperand(operand, "control expr", operandArc, ref indentStack, prefixIndent);
                }
                else
                {
                    var curArg = call.Args.FindByNode(operand);
                    assert(curArg is not null);

                    var message = (operand == curArg.EarlyNode) ? gtGetArgMsg(call, curArg) : gtGetLateArgMsg(call, curArg);
                    DisplayOperand(operand, message, operandArc, ref indentStack, prefixIndent);
                }
            }
            else
            {
                DisplayOperand(operand, "", operandArc, ref indentStack, prefixIndent);
            }

            operandArc = IIArc;
        }

        // Visit the operator

        if (prefixMsg is not null)
        {
            jitprintf("%s", prefixMsg);
        }

        const bool topOnly = true;
        const bool isLIR   = true;
        gtDispTree(node, ref indentStack, null, topOnly, isLIR);

        static void DisplayOperand(GenTree operand, string message, IndentInfo operandArc, ref IndentStack indentStack, int prefixIndent)
        {
            assert(operand is not null);
            assert(message is not null);

            if (prefixIndent is not 0)
            {
                jitprintf(new string(' ', prefixIndent));
            }

            // 60 spaces for alignment
            jitprintf(new string(' ', 60));

            indentStack.Push(operandArc);
            indentStack.Print();
            _ = indentStack.Pop();
            operandArc = IIArc;

            jitprintf($"  t{operand.TreeId,-5} {operand.Type.Name,-6} {message}\n");
        }
    }

    public void gtDispStmt(Statement stmt, string? msg = null)
    {
        if (msg is not null)
        {
            jitprintf($"{msg} ");
        }
        jitprintf($"{FMT_STMT(stmt.Id)} ( ");

        ref readonly var di = ref stmt.DebugInfo;

        // For statements in the root we display just the location without the inline context info.
        if ((di.InlineContext is null) || di.InlineContext.IsRoot)
        {
            di.Location.Dump();
        }
        else
        {
            di.Dump(recurse: false);
        }
        jitprintf(" ... ");

        var lastILOffs = stmt.LastILOffset;

        if (lastILOffs == BAD_IL_OFFSET)
        {
            jitprintf("???");
        }
        else
        {
            jitprintf($"0x{lastILOffs:X3}");
        }

        jitprintf(" )");

        if (di.GetParent(out var par))
        {
            jitprintf(" <- ");
            par.Dump(recurse: true);
        }
        jitprintf("\n");

        gtDispTree(stmt.RootNode);
    }

    public void gtDispTree(GenTree tree, string? msg = null, bool topOnly = false, bool isLIR = false)
    {
        var indentStack = new IndentStack(this);
        gtDispTree(tree, ref indentStack, msg, topOnly, isLIR);
    }

    public void gtDispTree(GenTree tree, ref IndentStack indentStack, string? msg = null, bool topOnly = false, bool isLIR = false)
    {
        // TODO: Port Compiler.gtDispTree
    }

    public void gtDispTreeRange(LIR.Range containingRange, GenTree tree)
    {
        gtDispRange(containingRange.GetTreeRangeWithFlags(tree, out _, out _));
    }
#endif

    /// <summary>Extracts side effects from the given expression.</summary>
    /// <param name="expr">the expression tree to extract side effects from</param>
    /// <param name="list">reference to a (possibly null) node</param>
    /// <param name="flags">side effect flags to be considered</param>
    /// <param name="ignoreRoot">ignore side effects on the expression root node</param>
    /// <remarks>
    ///   <para>list is modified such that the original list is executed after all side effects that were extracted.</para>
    ///   <para>The original side effect execution order is preserved.</para>
    /// </remarks>
    public void gtExtractSideEffList(GenTree expr, ref GenTree? list, GenTreeFlags flags = GTF_SIDE_EFFECT, bool ignoreRoot = false)
    {
        var sideEffectExtractor = new SideEffectExtractor(this, flags);

        if (ignoreRoot)
        {
            foreach (ref var operand in expr.UseEdges)
            {
                _ = sideEffectExtractor.WalkTree(ref operand, user: null);
            }
        }
        else
        {
            _ = sideEffectExtractor.WalkTree(ref expr, user: null);
        }

        if (list is not null)
        {
            sideEffectExtractor.Append(list);
        }
        list = sideEffectExtractor.Result;
    }

#if DEBUG
    public string gtGetArgMsg(GenTreeCall call, CallArg arg)
    {
        var stringBuilder = new StringBuilder();
        _ = gtPrintArgPrefix(stringBuilder, call, arg);

        if (arg.LateNode is not null)
        {
            _ = stringBuilder.Append(" setup");
        }
        else if (call.Args.IsAbiInformationDetermined)
        {
            _ = gtPrintABILocation(stringBuilder, arg.AbiInfo);
        }
        return stringBuilder.ToString();
    }

    public string gtGetLateArgMsg(GenTreeCall call, CallArg arg)
    {
        assert(arg.LateNode is not null);
        var stringBuilder = new StringBuilder();

        _ = gtPrintArgPrefix(stringBuilder, call, arg);
        _ = gtPrintABILocation(stringBuilder, arg.AbiInfo);

        return stringBuilder.ToString();
    }

    public StringBuilder gtPrintABILocation(StringBuilder stringBuilder, in AbiPassingInformation abiInfo)
    {
        var firstReg = REG_NA;
        var lastReg  = REG_NA;

        foreach (ref readonly var segment in abiInfo.Segments)
        {
            if (segment.IsPassedInRegister)
            {
#if HAS_FIXED_REGISTER_SET
                var regMsk = segment.RegisterMask;

                while (regMsk != RBM_NONE)
                {
                    var regIdx = int.TrailingZeroCount(regMsk);
                    var reg = (regNumber)(regIdx + segment.RegisterMaskBase);
                    regMsk &= ~(1 << regIdx);

                    if (firstReg == REG_NA)
                    {
                        firstReg = reg;
                        lastReg  = reg;
                    }
                    else if (REG_NEXT(lastReg) == reg)
                    {
                        lastReg = reg;
                    }
                    else
                    {
                        PrintRegs(stringBuilder, firstReg, lastReg);
                        firstReg = reg;
                        lastReg  = reg;
                    }
                }
#else
                var reg = segment.Register;

                if (firstReg == REG_NA)
                {
                    firstReg = reg;
                    lastReg  = reg;
                }
                else if (REG_NEXT(lastReg) == reg)
                {
                    lastReg = reg;
                }
                else
                {
                    PrintRegs(firstReg, lastReg, stringBuilder);
                    firstReg = reg;
                    lastReg  = reg;
                }
#endif
            }
            else
            {
                PrintRegs(stringBuilder, firstReg, lastReg);

#if FEATURE_FIXED_OUT_ARGS
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" out+{segment.StackOffset:X2}");
#else
                _ = stringBuilder.Append(" STK");
#endif
            }
        }

        PrintRegs(stringBuilder, firstReg, lastReg);
        return stringBuilder;

        static void PrintRegs(StringBuilder stringBuilder, regNumber firstReg, regNumber lastReg)
        {
            if (firstReg == REG_NA)
            {
                return;
            }

            var printSeparately = firstReg == lastReg;

#if TARGET_XARCH
            // No numeric arg regs, always print separately
            printSeparately = true;
#endif

            if (printSeparately)
            {
                var reg = firstReg;

                while (true)
                {
                    _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" {reg.Name}");

                    if (reg == lastReg)
                    {
                        break;
                    }
                    reg = REG_NEXT(reg);
                }
            }
            else
            {
                // Numeric arg regs, print as a range
                _ = stringBuilder.Append(CultureInfo.InvariantCulture, $" {firstReg.Name}{(REG_NEXT(firstReg) == lastReg ? ' ' : '-')}{lastReg.Name}");
            }

            firstReg = REG_NA;
            lastReg = REG_NA;
        }
    }

    public StringBuilder gtPrintArgPrefix(StringBuilder stringBuilder, GenTreeCall call, CallArg arg)
    {
        var wellKnownName = gtGetWellKnownArgNameForArgMsg(arg.WellKnownArg);

        if (wellKnownName.Length != 0)
        {
            _ = stringBuilder.Append(wellKnownName);
        }
        else
        {
            var argNum = call.Args.GetIndex(arg);
            _ = stringBuilder.Append(CultureInfo.InvariantCulture, $"arg{argNum}");
        }
        return stringBuilder;
    }

    public string gtGetWellKnownArgNameForArgMsg(WellKnownArg arg) => arg switch {
        WellKnownArg.ThisPointer => "this",
        WellKnownArg.VarArgsCookie => "va cookie",
        WellKnownArg.InstParam => "gctx",
        WellKnownArg.AsyncContinuation => "async",
        WellKnownArg.RetBuffer => "retbuf",
        WellKnownArg.PInvokeFrame => "pinv frame",
        WellKnownArg.WrapperDelegateCell => "wrap cell",
        WellKnownArg.ShiftLow => "shift low",
        WellKnownArg.ShiftHigh => "shift high",
        WellKnownArg.VirtualStubCell => "vsd cell",
        WellKnownArg.PInvokeCookie => "pinv cookie",
        WellKnownArg.PInvokeTarget => "pinv tgt",
        WellKnownArg.R2RIndirectionCell => "r2r cell",
        WellKnownArg.ValidateIndirectCallTarget => "cfg tgt",
        WellKnownArg.DispatchIndirectCallTarget => "cfg tgt",
        WellKnownArg.SwiftError => "swift error",
        WellKnownArg.SwiftSelf => "swift self",
        WellKnownArg.X86TailCallSpecialArg => "tail call",
        WellKnownArg.StackArrayLocal => "&lcl arr",
        WellKnownArg.RuntimeMethodHandle => "meth hnd",
        WellKnownArg.AsyncExecutionContext => "exec ctx",
        WellKnownArg.AsyncSynchronizationContext => "sync ctx",
        WellKnownArg.WasmShadowStackPointer => "wasm sp",
        WellKnownArg.WasmPortableEntryPoint => "wasm pep",
        _ => "",
    };
#endif

    /// <summary>find class handle for elements of an array of ref types</summary>
    /// <param name="array">array to find handle for</param>
    /// <returns>null if element class handle is unknown, otherwise the class handle.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetArrayElementClassHandle(GenTree array)
    {
        var arrayClassHnd = gtGetClassHandle(array, out var isArrayExact, out var isArrayNonNull);

        if (arrayClassHnd is not null)
        {
            // We know the class of the reference
            var attribs = info.compCompHnd->getClassAttribs(arrayClassHnd);

            if ((attribs & CORINFO_FLG_ARRAY) != 0)
            {
                // We know for sure it is an array
                CORINFO_CLASS_HANDLE elemClassHnd;
                var arrayElemType = info.compCompHnd->getChildType(arrayClassHnd, &elemClassHnd);

                if (arrayElemType == CORINFO_TYPE_CLASS)
                {
                    // We know it is an array of ref types
                    return elemClassHnd;
                }
            }
        }
        return null;
    }

    /// <summary>find class handle for a ref type</summary>
    /// <param name="tree">tree to find handle for</param>
    /// <param name="isExact">whether handle is exact type</param>
    /// <param name="isNonNull">whether tree value is known not to be null</param>
    /// <returns>The class handle or <c>null</c> if it is unknown</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetClassHandle(GenTree tree, out bool isExact, out bool isNonNull)
    {
        // Set default values for our out params.
        isNonNull = false;
        isExact = false;

        // Bail out if the tree is not a ref type.
        if (tree.Type is not TYP_REF)
        {
            return NO_CLASS_HANDLE;
        }

        // Tunnel through commas.
        var obj = tree.EffectiveVal;
        var objOp = obj.Oper;
        var objClass = NO_CLASS_HANDLE;

        switch (objOp)
        {
            case GT_COMMA:
            {
                // gtEffectiveVal above means we shouldn't see commas here.
                assert(false, "unexpected GT_COMMA");
                break;
            }

            case GT_LCL_VAR:
            {
                // For locals, pick up type info from the local table.
                var objLcl = obj.AsLclVar().LclNum;

                objClass = lvaTable[objLcl].lvClassHnd;
                isExact = lvaTable[objLcl].lvClassIsExact;
                break;
            }

            case GT_CNS_INT:
            {
                var intCon = obj.AsIntCon();

                if (intCon.IsIconHandle(GTF_ICON_OBJ_HDL))
                {
                    objClass = info.compCompHnd->getObjectType((CORINFO_OBJECT_HANDLE)(intCon.IconValue));

                    if (objClass != NO_CLASS_HANDLE)
                    {
                        // if we managed to get a class handle it's definitely not null
                        isNonNull = true;
                        isExact = true;
                    }
                }
                break;
            }

            case GT_RET_EXPR:
            {
                // If we see a RET_EXPR, recurse through to examine the return value expression.
                var retExpr = obj.AsRetExpr().InlineCandidate;
                objClass = gtGetClassHandle(retExpr, out isExact, out isNonNull);
                break;
            }

            case GT_CALL:
            {
                var call = obj.AsCall();

                if (call.IsSpecialIntrinsic())
                {
                    var ni = lookupNamedIntrinsic(call._callMethHnd);

                    if ((ni == NI_System_Array_Clone) || (ni == NI_System_Object_MemberwiseClone))
                    {
                        var thisArg = call.Args.ThisArg;
                        assert(thisArg is not null);

                        objClass = gtGetClassHandle(thisArg.Node, out isExact, out isNonNull);
                        break;
                    }

                    var specialObjClass = impGetSpecialIntrinsicExactReturnType(call);

                    if (specialObjClass is not null)
                    {
                        objClass = specialObjClass;
                        isExact = true;
                        isNonNull = true;
                        break;
                    }
                }

                if (call.IsInlineCandidate && !call.IsGuardedDevirtualizationCandidate)
                {
                    // For inline candidates, we've already cached the return
                    // type class handle in the inline info (for GDV candidates,
                    // this data is valid only for a correct guess, so we cannot
                    // use it).
                    var inlInfo = call.SingleInlineCandidateInfo;
                    assert(inlInfo is not null);

                    // Grab it as our first cut at a return type.
                    assert(inlInfo.methInfo.args.retType == CORINFO_TYPE_CLASS);
                    objClass = inlInfo.methInfo.args.retTypeClass;

                    // If the method is shared, the above may not capture
                    // the most precise return type information (that is,
                    // it may represent a shared return type and as such,
                    // have instances of __Canon). See if we can use the
                    // context to get at something more definite.
                    //
                    // For now, we do this here on demand rather than when
                    // processing the call, but we could/should apply
                    // similar sharpening to the argument and local types
                    // of the inlinee.
                    if (eeIsSharedInst(objClass))
                    {
                        var context = inlInfo.exactContextHandle;

                        if (context is not null)
                        {
                            var exactClass = eeGetClassFromContext(context);

                            // Grab the signature in this context.
                            eeGetMethodSig(call._callMethHnd, out var sigInfo, exactClass);
                            assert(sigInfo.retType == CORINFO_TYPE_CLASS);
                            objClass = sigInfo.retTypeClass;
                        }
                    }
                }
                else if (call._callType == CT_USER_FUNC)
                {
                    // For user calls, we can fetch the approximate return
                    // type info from the method handle. Unfortunately
                    // we've lost the exact context, so this is the best
                    // we can do for now.

                    var method = call._callMethHnd;
                    eeGetMethodSig(method, out var sigInfo, owner: null);

                    if (sigInfo.retType == CORINFO_TYPE_VOID)
                    {
                        // This is a constructor call.
                        var methodFlags = info.compCompHnd->getMethodAttribs(method);
                        assert((methodFlags & CORINFO_FLG_CONSTRUCTOR) != 0);
                        objClass = info.compCompHnd->getMethodClass(method);
                        isExact = true;
                        isNonNull = true;
                    }
                    else
                    {
                        assert(sigInfo.retType == CORINFO_TYPE_CLASS);
                        objClass = sigInfo.retTypeClass;
                    }
                }
                else if (call.IsHelperCall())
                {
                    objClass = gtGetHelperCallClassHandle(call, out isExact, out isNonNull);
                }

                break;
            }

            case GT_INTRINSIC:
            {
                var intrinsic = obj.AsIntrinsic();

                if (intrinsic.IntrinsicName == NI_System_Object_GetType)
                {
                    var runtimeType = info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE);
                    assert(runtimeType != NO_CLASS_HANDLE);

                    objClass = runtimeType;
                    isNonNull = true;
                }
                break;
            }

            case GT_CNS_STR:
            {
                // For literal strings, we know the class and that the value is not null.
                objClass = impStringClass;
                isExact = true;
                isNonNull = true;
                break;
            }

            case GT_IND:
            {
                var indir = obj.AsIndir();

                var indirBase = indir.Base;
                assert(indirBase is not null);

                // indir(lcl_var_addr) -. lcl
                //
                // This comes up during constrained callvirt on ref types.
                //
                if (indirBase.IsLclVarAddr)
                {
                    var objLcl = indirBase.AsLclVarCommon().LclNum;
                    ref var lvaDsc = ref lvaTable[objLcl];

                    objClass = lvaDsc.lvClassHnd;
                    isExact = lvaDsc.lvClassIsExact;
                }
                else if (indirBase.Oper is GT_INDEX_ADDR or GT_ARR_ELEM)
                {
                    // indir(arr_elem(...)) . array element type

                    if (indirBase.Oper is GT_INDEX_ADDR)
                    {
                        objClass = gtGetArrayElementClassHandle(indirBase.AsIndexAddr().Arr);
                    }
                    else
                    {
                        objClass = gtGetArrayElementClassHandle(indirBase.AsArrElem().ArrObj);
                    }
                }
                else if (indirBase.Oper is GT_ADD)
                {
                    // TODO-VNTypes: use "IsFieldAddr" here instead.

                    // This could be a static field access.
                    //
                    // See if op1 is a static field base helper call
                    // and if so, op2 will have the field info.

                    var indirBaseOp = indirBase.AsOp();

                    var op1 = indirBaseOp.Op1;
                    var op2 = indirBaseOp.Op2;

                    if (op2.Oper.IsCnsIntOrI)
                    {
                        var intCon = op2.AsIntCon();
                        var fieldSeq = intCon.FieldSeq;

                        if ((fieldSeq is not null) && (fieldSeq.Offset == intCon.IconValue))
                        {
                            // No benefit to calling gtGetFieldClassHandle here, as
                            // the exact field being accessed can vary.
                            var fieldHnd = fieldSeq.FieldHandle;
                            var fieldOwner = NO_CLASS_HANDLE;

                            // fieldOwner helps us to get a more exact field class for instance fields
                            if (!fieldSeq.IsStaticField)
                            {
                                fieldOwner = gtGetClassHandle(op1, out var objIsExact, out var objIsNonNull);
                            }

                            if (eeGetFieldType(fieldHnd, out var fieldClass, fieldOwner) == TYP_REF)
                            {
                                objClass = fieldClass;
                            }
                        }
                    }
                }
                else if (indirBase.Oper.IsCnsIntOrI)
                {
                    var intCon = indirBase.AsIntCon();

                    if (intCon.IsIconHandle(GTF_ICON_CONST_PTR) || intCon.IsIconHandle(GTF_ICON_STATIC_HDL))
                    {
                        // Check if we have IND(ICON_HANDLE) that represents a static field
                        var fldSeq = intCon.FieldSeq;

                        if ((fldSeq is not null) && (fldSeq.Offset == intCon.IconValue))
                        {
                            var fldHandle = fldSeq.FieldHandle;
                            objClass = gtGetFieldClassHandle(fldHandle, out isExact, out isNonNull);
                        }
                    }
                }
                else if (indirBase.Oper is GT_FIELD_ADDR)
                {
                    objClass = gtGetFieldClassHandle(indirBase.AsFieldAddr().FldHnd, out isExact, out isNonNull);
                }
                break;
            }

            case GT_BOX:
            {
                // Box should just wrap a local var reference which has
                // the type we're looking for. Also box only represents a
                // non-nullable value type so result cannot be null.
                var box = obj.AsBox();

                var boxTemp = box.BoxOp;
                assert(boxTemp.Oper.IsLocal);

                var boxTempLcl = boxTemp.AsLclVar().LclNum;
                ref var lvaDsc = ref lvaTable[boxTempLcl];
                objClass = lvaDsc.lvClassHnd;
                isExact = lvaDsc.lvClassIsExact;
                isNonNull = true;
                break;
            }

            default:
            {
                break;
            }
        }

        if ((objClass == NO_CLASS_HANDLE) && (vnStore is not null))
        {
            // Try VN if we haven't found a class handle yet
            objClass = vnStore.GetObjectType(tree._vnPair.Conservative, out isExact, out isNonNull);
        }

        if ((objClass != NO_CLASS_HANDLE) && !isExact && (JitConfig[ConfigInteger.JitEnableExactDevirtualization] != 0))
        {
            CORINFO_CLASS_HANDLE exactClass;

            if (info.compCompHnd->getExactClasses(objClass, 1, &exactClass) == 1)
            {
                isExact = true;
                objClass = exactClass;
            }
            else
            {
                isExact = info.compCompHnd->isExactType(objClass);
            }
        }
        return objClass;
    }

    /// <summary>find class handle for a field</summary>
    /// <param name="fieldHnd">field handle for field in question</param>
    /// <param name="isExact">true if type is known exactly</param>
    /// <param name="isNonNull">true if field value is not null</param>
    /// <returns>null if helper call result is not a ref class, or the class handle is unknown, otherwise the class handle.</returns>
    /// <remarks>May examine runtime state of static field instances.</remarks>
    public unsafe CORINFO_CLASS_HANDLE gtGetFieldClassHandle(CORINFO_FIELD_HANDLE fieldHnd, out bool isExact, out bool isNonNull)
    {
        isExact = false;
        isNonNull = false;

        var fieldClass = NO_CLASS_HANDLE;
        var fieldCorType = info.compCompHnd->getFieldType(fieldHnd, &fieldClass);

        if (fieldCorType == CORINFO_TYPE_CLASS)
        {
            // Optionally, look at the actual type of the field's value
            var queryForCurrentClass = true;

#if DEBUG
            queryForCurrentClass = JitConfig[ConfigInteger.JitQueryCurrentStaticFieldClass] > 0;
#endif

            if (queryForCurrentClass)
            {
#if DEBUG
                if (verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0))
                {
                    JITDUMP($"\nQuerying runtime about current class of field {eeGetFieldName(fieldHnd, true)} (declared as {eeGetClassName(fieldClass)})\n");
                }
#endif

                // Is this a fully initialized init-only static field?
                //
                // Note we're not asking for speculative results here, yet.
                var currentClass = info.compCompHnd->getStaticFieldCurrentClass(fieldHnd);

                if (currentClass != NO_CLASS_HANDLE)
                {
                    // Yes! We know the class exactly and can rely on this to always be true.
                    fieldClass = currentClass;

                    isExact = true;
                    isNonNull = true;

#if DEBUG
                    if (verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0))
                    {
                        JITDUMP($"Runtime reports field is init-only and initialized and has class {eeGetClassName(fieldClass)}\n");
                    }
#endif
                }
                else
                {
                    JITDUMP("Field's current class not available\n");
                }
                return fieldClass;
            }
        }
        return NO_CLASS_HANDLE;
    }

    /// <summary>find the compile time class handle from a helper call argument tree</summary>
    /// <param name="tree">tree that passes the handle to the helper</param>
    /// <returns>The compile time class handle if known.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetHelperArgClassHandle(GenTree tree)
    {
        var result = NO_CLASS_HANDLE;

        if (tree.Oper.IsCnsIntOrI && (tree.Type is TYP_I_IMPL))
        {
            // The handle could be a literal constant
            var intCon = tree.AsIntCon();

            assert(intCon.IsIconHandle(GTF_ICON_CLASS_HDL));
            result = (CORINFO_CLASS_HANDLE)(intCon.CompileTimeHandle);
        }
        else if (tree.Oper is GT_RUNTIMELOOKUP)
        {
            // Or the result of a runtime lookup
            result = tree.AsRuntimeLookup().ClassHandle;
        }
        else if (tree.Oper is GT_IND)
        {
            // Or something reached indirectly

            // The handle indirs we are looking for will be marked as non-faulting.
            // Certain others (eg from refanytype) may not be.
            if ((tree.Flags & GTF_IND_NONFAULTING) != 0)
            {
                var handleTreeInternal = tree.AsUnOp().Op1;

                if (handleTreeInternal.Oper.IsCnsIntOrI && (handleTreeInternal.Type is TYP_I_IMPL))
                {
                    // These handle constants should be class handles.
                    var intCon = handleTreeInternal.AsIntCon();

                    assert(intCon.IsIconHandle(GTF_ICON_CLASS_HDL));
                    result = (CORINFO_CLASS_HANDLE)(intCon.CompileTimeHandle);
                }
            }
        }

        return result;
    }

    /// <summary>find the compile time method handle from a helper call argument tree</summary>
    /// <param name="tree">tree that passes the handle to the helper</param>
    /// <returns>The compile time method handle, if known.</returns>
    public unsafe CORINFO_METHOD_HANDLE gtGetHelperArgMethodHandle(GenTree tree)
    {
        var result = NO_METHOD_HANDLE;

        // The handle could be a literal constant
        if (tree.Oper.IsCnsIntOrI && (tree.Type is TYP_I_IMPL))
        {
            var intCon = tree.AsIntCon();
            assert(intCon.IsIconHandle(GTF_ICON_METHOD_HDL));
            result = (CORINFO_METHOD_HANDLE)(intCon.CompileTimeHandle);
        }
        // Or the result of a runtime lookup
        else if (tree.Oper is GT_RUNTIMELOOKUP)
        {
            result = tree.AsRuntimeLookup().MethodHandle;
        }
        // Or something reached indirectly
        else if (tree.Oper is GT_IND)
        {
            // The handle indirs we are looking for will be marked as non-faulting.
            // Certain others (eg from refanytype) may not be.
            if ((tree.Flags & GTF_IND_NONFAULTING) != 0)
            {
                var handleTreeInternal = tree.AsUnOp().Op1;

                if (handleTreeInternal.Oper.IsCnsIntOrI && (handleTreeInternal.Type is TYP_I_IMPL))
                {
                    // These handle constants should be method handles.
                    var intCon = handleTreeInternal.AsIntCon();

                    assert(intCon.IsIconHandle(GTF_ICON_METHOD_HDL));
                    result = (CORINFO_METHOD_HANDLE)(intCon.CompileTimeHandle);
                }
            }
        }

        return result;
    }

    /// <summary>find class handle for return value of a helper call</summary>
    /// <param name="call">helper call to examine</param>
    /// <param name="isExact">true if type is known exactly</param>
    /// <param name="isNonNull">true if return value is not null</param>
    /// <returns>null if helper call result is not a ref class, or the class handle is unknown, otherwise the class handle.</returns>
    public unsafe CORINFO_CLASS_HANDLE gtGetHelperCallClassHandle(GenTreeCall call, out bool isExact, out bool isNonNull)
    {
        assert(call.IsHelperCall());

        isNonNull = false;
        isExact = false;

        CORINFO_CLASS_HANDLE objClass = null;
        var helper = eeGetHelperNum(call._callMethHnd);

        switch (helper)
        {
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL:
            {
                // Note for some runtimes these helpers return exact types.
                // But in those cases the types are also sealed, so there's no need to claim exactness here.

                var runtimeType = info.compCompHnd->getBuiltinClass(CLASSID_RUNTIME_TYPE);
                assert(runtimeType != NO_CLASS_HANDLE);

                objClass = runtimeType;
                isNonNull = helper is CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE;
                break;
            }

            case CORINFO_HELP_BOX:
            case CORINFO_HELP_BOX_NULLABLE:
            {
                var arg = call.Args.GetUserArgByIndex(0);
                assert(arg is not null);

                var typeArg = arg.Node;

                if (typeArg.Oper.IsCnsIntOrI)
                {
                    var intCon = typeArg.AsIntCon();

                    if (intCon.IsIconHandle(GTF_ICON_CLASS_HDL))
                    {
                        var isNullableHelper = helper is CORINFO_HELP_BOX_NULLABLE;
                        objClass = gtGetHelperArgClassHandle(typeArg);

                        if ((objClass != NO_CLASS_HANDLE) && isNullableHelper)
                        {
                            // Nullable<T> is boxed as just T (via CORINFO_HELP_BOX_NULLABLE)
                            objClass = info.compCompHnd->getTypeForBox(objClass);
                        }

                        if (objClass != NO_CLASS_HANDLE)
                        {
                            // CORINFO_HELP_BOX_NULLABLE may return null
                            // CORINFO_HELP_BOX always returns non-null
                            isNonNull = !isNullableHelper;
                            isExact = true;
                        }
                    }
                }
                break;
            }

            case CORINFO_HELP_CHKCASTCLASS:
            case CORINFO_HELP_CHKCASTANY:
            case CORINFO_HELP_CHKCASTARRAY:
            case CORINFO_HELP_CHKCASTINTERFACE:
            case CORINFO_HELP_CHKCASTCLASS_SPECIAL:
            case CORINFO_HELP_ISINSTANCEOFINTERFACE:
            case CORINFO_HELP_ISINSTANCEOFARRAY:
            case CORINFO_HELP_ISINSTANCEOFCLASS:
            case CORINFO_HELP_ISINSTANCEOFANY:
            {
                // Fetch the class handle from the helper call arglist
                var arg = call.Args.GetArgByIndex(0);
                assert(arg is not null);

                var typeArg = arg.Node;
                var castHnd = gtGetHelperArgClassHandle(typeArg);

                // We generally assume the type being cast to is the best type
                // for the result, unless it is an interface type.
                //
                // TODO-CQ: when we have default interface methods then
                // this might not be the best assumption. A similar issue arises when
                // typing the temp in impCastClassOrIsInstToTree, when we
                // expand the cast inline.
                if (castHnd is not null)
                {
                    var attrs = info.compCompHnd->getClassAttribs(castHnd);

                    if ((attrs & CORINFO_FLG_INTERFACE) != 0)
                    {
                        castHnd = null;
                    }
                }

                // If we don't have a good estimate for the type we can use the
                // type from the value being cast instead.
                if (castHnd is null)
                {
                    var valueArg = call.Args.GetArgByIndex(1);
                    assert(valueArg is not null);

                    var valueNode = valueArg.Node;
                    castHnd = gtGetClassHandle(valueNode, out isExact, out isNonNull);
                }

                // We don't know at jit time if the cast will succeed or fail, but if it
                // fails at runtime then an exception is thrown for cast helpers, or the
                // result is set null for instance helpers.
                //
                // So it safe to claim the result has the cast type.
                // Note we don't know for sure that it is exactly this type.
                if (castHnd is not null)
                {
                    objClass = castHnd;
                }
                break;
            }

            case CORINFO_HELP_NEWARR_1_DIRECT:
            case CORINFO_HELP_NEWARR_1_MAYBEFROZEN:
            case CORINFO_HELP_NEWARR_1_PTR:
            case CORINFO_HELP_NEWARR_1_VC:
            case CORINFO_HELP_NEWARR_1_ALIGN8:
            case CORINFO_HELP_READYTORUN_NEWARR_1:
            {
                var arrayHnd = (CORINFO_CLASS_HANDLE)(call.CompileTimeHelperArgumentHandle);

                if (arrayHnd != NO_CLASS_HANDLE)
                {
                    objClass = arrayHnd;
                    isExact = true;
                    isNonNull = true;
                }
                break;
            }

            default:
            {
                break;
            }
        }

        return objClass;
    }

#if DEBUG
    /// <summary>Get the local var name</summary>
    /// <param name="lclNum"></param>
    /// <returns></returns>
    public string gtGetLclVarName(int lclNum)
    {
        gtGetLclVarNameInfo(lclNum, out var ilKind, out var ilName, out var ilNum);

        if (ilName.Length != 0)
        {
            return $"V{lclNum:D2} {ilName}";
        }
        else if (ilKind.Length != 0)
        {
            return $"V{lclNum:D2} {ilKind}{ilNum}";
        }
        else
        {
            return $"V{lclNum:D2}";
        }
    }

    public void gtGetLclVarNameInfo(int lclNum, out string ilKind, out string ilName, out int ilNum)
    {
        var kind = "";
        var name = "";

        var num = compMap2ILvarNum(lclNum);

        if (num == ICorDebugInfo.RETBUF_ILNUM)
        {
            name = "RetBuf";
        }
        else if (num == ICorDebugInfo.VARARGS_HND_ILNUM)
        {
            name = "VarArgHandle";
        }
        else if (num == ICorDebugInfo.TYPECTXT_ILNUM)
        {
            name = "TypeCtx";
        }
        else if (num == ICorDebugInfo.UNKNOWN_ILNUM)
        {
            if (lclNumIsTrueCSE(lclNum))
            {
                kind = "cse";
                num  = lclNum - optCSEstart;
            }
#if TARGET_ARM64
            else if (lclNum == lvaFfrRegister)
            {
                // We introduce this LclVar in lowering, hence special case the printing of
                // it instead of handling it in "rationalizer" below.
                ilName = "FFReg";
            }
#endif
            else if (lclNum >= optCSEstart)
            {
                // Currently any new LclVar's introduced after the CSE phase
                // are believed to be created by the "rationalizer" that is what is meant by the "rat" prefix.
                kind = "rat";
                num  = lclNum - (optCSEstart + optCSEcount);
            }
            else if (lclNum == info.compLvFrameListRoot)
            {
                name = "FramesRoot";
            }
            else if (lclNum == lvaInlinedPInvokeFrameVar)
            {
                name = "PInvokeFrame";
            }
            else if (lclNum == lvaGSSecurityCookie)
            {
                name = "GsCookie";
            }
            else if (lclNum == lvaRetAddrVar)
            {
                name = "ReturnAddress";
            }
#if FEATURE_FIXED_OUT_ARGS
            else if (lclNum == lvaOutgoingArgSpaceVar)
            {
                name = "OutArgs";
            }
#endif
#if JIT32_GCENCODER
            else if (lclNum == lvaLocAllocSPvar)
            {
                ilName = "LocAllocSP";
            }
#endif
            else if (lclNum == lvaAsyncContinuationArg)
            {
                name = "AsyncCont";
            }
#if TARGET_WASM
            else if (lclNum == lvaWasmSpArg)
            {
                ilName = "SP";
            }
#endif
            else
            {
                kind = "tmp";

                if (compIsForInlining)
                {
                    num = lclNum - impInlineInfo.InlinerCompiler.info.compLocalsCount;
                }
                else
                {
                    num = lclNum - info.compLocalsCount;
                }
            }
        }
        else if (lclNum < (compIsForInlining ? impInlineInfo.InlinerCompiler.info.compArgsCount : info.compArgsCount))
        {
            if ((num is 0) && !info.compIsStatic)
            {
                name = "this";
            }
            else
            {
                kind = "arg";
            }
        }
        else
        {
            if (!lvaTable[lclNum].lvIsStructField)
            {
                kind = "loc";
            }
            if (compIsForInlining)
            {
                num -= impInlineInfo.InlinerCompiler.info.compILargsCount;
            }
            else
            {
                num -= info.compILargsCount;
            }
        }

        ilKind = kind;
        ilName = name;
        ilNum  = num;
    }
#endif

    public GenTreeOp gtNewCommaNode(var_types type, GenTree op1, GenTree op2)
    {
        return new GenTreeOp(GT_COMMA, type, op1, op2);
    }

    /// <summary>Create (and check for) a "nothing" node, i.e. a node that doesn't produce any code.</summary>
    /// <returns></returns>
    /// <remarks>We currently use a "GT_NOP" node of type void for this purpose.</remarks>
    public GenTree gtNewNothingNode() => new GenTree(GT_NOP, TYP_VOID);

    /// <summary>Helper to create a null check node.</summary>
    /// <param name="addr">Address to null check</param>
    /// <returns>New GT_NULLCHECK node</returns>
    public GenTreeIndir gtNewNullCheck(GenTree addr)
    {
        assert(fgAddrCouldBeNull(addr));
        optMethodFlags |= OMF_HAS_NULLCHECK;

        var nullCheck = new GenTreeIndir(GT_NULLCHECK, TYP_BYTE, addr);
        nullCheck.Flags |= GTF_EXCEPT;
        return nullCheck;
    }

    public GenTreeLclVar gtNewTempStore(int tmp, GenTree val)
        => gtNewTempStore(tmp, val, out _, CHECK_SPILL_NONE, default, null);

    public GenTreeLclVar gtNewTempStore(int tmp, GenTree val, out Statement pAfterStmt, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
    {
        // TODO: Port Compiler.gtNewTempStore
        Unsafe.SkipInit(out pAfterStmt);
        return null!;
    }

    /// <summary>Return true if the given node (excluding children trees) contains side effects.</summary>
    /// <param name="node"></param>
    /// <param name="flags"></param>
    /// <param name="ignoreCctors"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>Note that it does not recurse, and children need to be handled separately.</para>
    ///   <para>It may return false even if the node has GTF_SIDE_EFFECT (because of its children).</para>
    /// </remarks>
    public bool gtNodeHasSideEffects(GenTree node, GenTreeFlags flags, bool ignoreCctors = false)
    {
        if ((flags & GTF_ASG) != 0)
        {
            if (node.RequiresAsgFlag)
            {
                return true;
            }
        }

        // Are there only GTF_CALL side effects remaining? (and no other side effect kinds)
        if ((flags & GTF_CALL) != 0)
        {
            var potentialCall = node;

            while (potentialCall.Oper is GT_RET_EXPR)
            {
                // We need to preserve return expressions where the underlying call has side effects.
                // Otherwise early folding can result in us dropping the call.
                potentialCall = potentialCall.AsRetExpr().InlineCandidate;
            }

            if (potentialCall.Oper is GT_CALL)
            {
                var call = potentialCall.AsCall();
                var ignoreExceptions = (flags & GTF_EXCEPT) == 0;
                return call.HasSideEffects(this, ignoreExceptions, ignoreCctors);
            }
        }

        if ((flags & GTF_EXCEPT) != 0)
        {
            if (node.MayThrow(this))
            {
                return true;
            }
        }

        // Expressions declared as CSE by (e.g.) hoisting code are considered to have relevant side effects (if we care about GTF_MAKE_CSE).
        return ((flags & GTF_MAKE_CSE) != 0) && ((node.Flags & GTF_MAKE_CSE) != 0);
    }

    /// <inheritdoc cref="gtPeelOffsets(ref GenTree, out long, out FieldSeq)" />
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset)
        => gtPeelOffsets(ref addr, out offset, out Unsafe.NullRef<FieldSeq?>());

    /// <summary>Peel all ADD(addr, CNS_INT(x)) nodes off the specified address node and return the base node and sum of offsets peeled.</summary>
    /// <param name="addr">The address node.</param>
    /// <param name="offset">The sum of offset peeled such that ADD(addr, offset) is equivalent to the original addr.</param>
    /// <param name="fldSeq">The combined field sequence for all the peeled offsets.</param>
    public void gtPeelOffsets(ref GenTree addr, out target_ssize_t offset, out FieldSeq? fldSeq)
    {
        assert(addr.Type is TYP_I_IMPL or TYP_BYREF or TYP_REF);

        Unsafe.SkipInit(out fldSeq);
        offset = 0;

        if (!Unsafe.IsNullRef(in fldSeq))
        {
            fldSeq = null;
        }

        while (true)
        {
            if ((addr.Oper is GT_ADD) && !addr.HasOverflowCheck)
            {
                var addrOp = addr.AsOp();

                var op1 = addrOp.Op1;
                var op2 = addrOp.Op2;

                if (op2.Oper.IsCnsIntOrI && (op2.Type is TYP_I_IMPL))
                {
                    var intCon = op2.AsIntCon();

                    if (!intCon.IsIconHandle())
                    {
                        offset += intCon.IconValue;

                        if (!Unsafe.IsNullRef(in fldSeq))
                        {
                            assert(m_fieldSeqStore is not null);
                            fldSeq = m_fieldSeqStore.Append(fldSeq, intCon.FieldSeq);
                        }

                        addr = op1;
                        continue;
                    }
                }

                if (op1.Oper.IsCnsIntOrI && (op1.Type is TYP_I_IMPL))
                {
                    var intCon = op1.AsIntCon();

                    if (!intCon.IsIconHandle())
                    {
                        offset += intCon.IconValue;

                        if (!Unsafe.IsNullRef(in fldSeq))
                        {
                            assert(m_fieldSeqStore is not null);
                            fldSeq = m_fieldSeqStore.Append(intCon.FieldSeq, fldSeq);
                        }

                        addr = op2;
                        continue;
                    }
                }

                break;
            }
            else if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();

                if (addrMode.HasIndex)
                {
                    break;
                }
                offset += addrMode.Offset;

                assert(addrMode.Base is not null);
                addr = addrMode.Base;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>Given a tree, figure out the order in which its sub-operands should be evaluated.</summary>
    /// <param name="tree"></param>
    /// <returns>Returns the Sethi 'complexity' estimate for this tree (the higher the number, the higher is the tree's resources requirement).</returns>
    /// <remarks>
    ///   <para>If the second operand of a binary operator is more expensive than the first operand, then try to swap the operand trees.Updates the GTF_REVERSE_OPS bit if necessary in this case.</para>
    /// </remarks>
    public int gtSetEvalOrder(GenTree tree)
    {
        // This function sets:
        //   1. GetCostEx() to the execution complexity estimate
        //   2. GetCostSz() to the code size estimate
        //   3. Sometimes sets GTF_ADDRMODE_NO_CSE on nodes in the tree.

        if (opts.OptimizationDisabled)
        {
            return gtSetEvalOrderMinOpts(tree);
        }

        // TODO: Port Compiler.gtSetEvalOrder
        return 0;
    }

    /// <summary>A MinOpts specific version of gtSetEvalOrder. We don't need to set costs, but we're looking for opportunities to swap operands.</summary>
    /// <param name="tree">The tree for which we are setting the evaluation order.</param>
    /// <returns>the Sethi 'complexity' estimate for this tree (the higher the number, the higher is the tree's resources requirement)</returns>
    public int gtSetEvalOrderMinOpts(GenTree tree)
    {
        // TODO: Port Compiler.gtSetEvalOrderMinOpts
        return 0;
    }

    /// <summary>A wrapper for gtSetEvalOrder and gtComputeFPlvls</summary>
    /// <param name="stmt"></param>
    /// <remarks>Necessary because the FP levels may need to be re-computed if we reverse operands</remarks>
    public void gtSetStmtInfo(Statement stmt) => gtSetEvalOrder(stmt.RootNode);

    public bool gtTreeHasSideEffects(GenTree tree, GenTreeFlags flags, bool ignoreCctors = false)
    {
        // These are the side effect flags that we care about for this tree
        var sideEffectFlags = tree.Flags & flags;

        // Does this tree have any Side-effect flags set that we care about?
        if (sideEffectFlags == 0)
        {
            // no it doesn't..
            return false;
        }

        if ((sideEffectFlags is GTF_CALL) && tree.Oper.IsCall && tree.AsCall().IsHelperCall())
        {
            // Generally all trees that contain GT_CALL nodes are considered to have side-effects.
            // However, for some pure helper calls we lie about this.
            if (gtNodeHasSideEffects(tree, flags, ignoreCctors))
            {
                return true;
            }

            // The GTF_CALL may be contributed by an operand, so check for that.
            var hasCallInOperand = false;

            _ = tree.VisitOperands((tree) => {
                if (gtTreeHasSideEffects(tree, GTF_CALL, ignoreCctors))
                {
                    hasCallInOperand = true;
                    return GenTree.VisitResult.Abort;
                }
                return GenTree.VisitResult.Continue;
            });

            return hasCallInOperand;
        }

        return true;
    }
}

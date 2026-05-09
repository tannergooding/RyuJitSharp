// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    // This class is responsible for checking validity and profitability of struct promotion.
    // If it is both legal and profitable, then TryPromoteStructVar promotes the struct and initializes
    // necessary information for fgMorphStructField to use.
    public sealed class StructPromotionHelper
    {
        private Compiler m_compiler;
        private lvaStructPromotionInfo structPromotionInfo;

        public StructPromotionHelper(Compiler compiler)
        {
            m_compiler = compiler;
        }

        /// <summary>checks if the struct type can be promoted.</summary>
        /// <param name="typeHnd">struct handle to check.</param>
        /// <returns>true if the struct type can be promoted.</returns>
        /// <remarks>
        ///   <para>The last analyzed type is memorized to skip the check if we ask about the same time again next.</para>
        ///   <para>However, it was not found profitable to memorize all analyzed types in a map.</para>
        ///   <para>The check initializes only necessary fields in lvaStructPromotionInfo, so if the promotion is rejected early than most fields will be uninitialized.</para>
        /// </remarks>
        public unsafe bool CanPromoteStructType(CORINFO_CLASS_HANDLE typeHnd)
        {
            assert(typeHnd is not null);

            if (!m_compiler.eeIsValueClass(typeHnd))
            {
                // TODO-ObjectStackAllocation: Enable promotion of fields of stack-allocated objects.
                return false;
            }

            if (structPromotionInfo.typeHnd == typeHnd)
            {
                // Asking for the same type of struct as the last time.
                // Nothing need to be done.
                // Fall through ...
                return structPromotionInfo.canPromote;
            }

            // Analyze this type from scratch.
            structPromotionInfo = new lvaStructPromotionInfo(typeHnd);

#if FEATURE_SIMD
            // getMaxVectorByteLength() represents the size of the largest primitive type that we can struct promote.
            var maxSize = MAX_NumOfFieldsInPromotableStruct * int.Max(m_compiler.GetMaxVectorByteLength(), sizeof(double));
#else
            // sizeof(double) represents the size of the largest primitive type that we can struct promote.
            var maxSize = MAX_NumOfFieldsInPromotableStruct * sizeof(double);
#endif

            // lvaStructFieldInfo.fldOffset is byte-sized and offsets start from 0, so the max size can be 256
            assert((byte)(maxSize - 1) == (maxSize - 1));

            var compHandle = m_compiler.info.compCompHnd;
            var structSize = compHandle->getClassSize(typeHnd);

            if (structSize > maxSize)
            {
                return false; // struct is too large
            }

            var typeFlags = compHandle->getClassAttribs(typeHnd);

            if (StructHasOverlappingFields(typeFlags))
            {
                return false;
            }

            if (StructHasIndexableFields(typeFlags))
            {
                return false;
            }

#if TARGET_ARM
            // On ARM, we have a requirement on the struct alignment; see below.
            var structAlignment = roundUp(compHandle->getClassAlignmentRequirement(typeHnd), TARGET_POINTER_SIZE);
#endif

            // At most 1 (root node) + (4 promoted fields) + (each could be a wrapped primitive)
            var numTreeNodes = (nint)(1 + (MAX_NumOfFieldsInPromotableStruct * 2));
            var treeNodes = stackalloc CORINFO_TYPE_LAYOUT_NODE[(int)(numTreeNodes)];
            
            var result = compHandle->getTypeLayout(typeHnd, treeNodes, &numTreeNodes);

            if ((result != GetTypeLayoutResult.Success) || (numTreeNodes <= 1))
            {
                return false;
            }

            assert(treeNodes[0].size == structSize);

            structPromotionInfo.fieldCnt = 0;

            var fieldsSize = 0;

            // Some notes on the following:
            // 1. At most MAX_NumOfFieldsInPromotableStruct fields can be promoted
            // 2. Recursive promotion is not enabled as the rest of the JIT cannot
            //    handle some of the patterns produced efficiently
            // 3. The exception to the above is structs wrapping primitive types; we do
            //    support promoting those, but only through one layer of nesting (as a
            //    quirk -- this can probably be relaxed).

            for (nint i = 1; i < numTreeNodes;)
            {
                if (structPromotionInfo.fieldCnt >= MAX_NumOfFieldsInPromotableStruct)
                {
                    return false;
                }

                ref var node = ref treeNodes[i];
                assert(node.parent == 0);

                ref var promField = ref structPromotionInfo.fields[structPromotionInfo.fieldCnt];

#if DEBUG
                promField.diagFldHnd = node.diagFieldHnd;
#endif

                // Ensured by assertion on size above.
                assert((byte)(node.offset) == node.offset);
                promField.fldOffset = (byte)(node.offset);

                promField.fldOrdinal = structPromotionInfo.fieldCnt;
                promField.fldSize = node.size;

                structPromotionInfo.fieldCnt++;

                if (node.type == CORINFO_TYPE_VALUECLASS)
                {
                    var fldType = TryPromoteValueClassAsPrimitive(treeNodes, numTreeNodes, i);

                    if (fldType == TYP_UNDEF)
                    {
                        return false;
                    }

                    promField.fldType = fldType;
                    promField.fldSIMDTypeHnd = node.simdTypeHnd;
                    AdvanceSubTree(treeNodes, numTreeNodes, ref i);
                }
                else
                {
                    promField.fldType = node.type.VarType;
                    i++;
                }

                fieldsSize += promField.fldSize;

                if ((promField.fldOffset % promField.fldSize) != 0)
                {
                    // The code in Compiler.genPushArgList that reconstitutes
                    // struct values on the stack from promoted fields expects
                    // those fields to be at their natural alignment.
                    return false;
                }

                noway_assert(promField.fldOffset + promField.fldSize <= structSize);

#if TARGET_ARM
                // On ARM, for struct types that don't use explicit layout, the alignment of the struct is
                // at least the max alignment of its fields.  We take advantage of this invariant in struct promotion,
                // so verify it here.
                if (promField.fldSize > structAlignment)
                {
                    // Don't promote vars whose struct types violates the invariant.  (Alignment == size for primitives.)
                    return false;
                }
#endif
            }

            if (fieldsSize != treeNodes[0].size)
            {
                structPromotionInfo.containsHoles = true;

                if (treeNodes[0].hasSignificantPadding)
                {
                    // Struct has significant data not covered by fields we would promote;
                    // this would typically result in dependent promotion, so leave this
                    // struct to physical promotion.
                    return false;
                }
            }

            // Cool, this struct is promotable.

            structPromotionInfo.canPromote = true;
            return true;
        }

        public unsafe void Clear()
        {
            structPromotionInfo.typeHnd = NO_CLASS_HANDLE;
        }

        /// <summary>promote struct var if it is possible and profitable.</summary>
        /// <param name="lclNum">struct number to try.</param>
        /// <returns>true if the struct var was promoted.</returns>
        public bool TryPromoteStructVar(int lclNum)
        {
            if (CanPromoteStructVar(lclNum))
            {
                if (ShouldPromoteStructVar(lclNum))
                {
                    PromoteStructVar(lclNum);
                    return true;
                }
            }
            return false;
        }

        /// <summary>checks if the struct can be promoted.</summary>
        /// <param name="lclNum">struct number to check.</param>
        /// <returns>true if the struct var can be promoted.</returns>
        private unsafe bool CanPromoteStructVar(int lclNum)
        {
            ref var varDsc = ref m_compiler.lvaGetDesc(lclNum);

            assert(varTypeIsStruct(varDsc.Type));
            assert(!varDsc.lvPromoted); // Don't ask again :)

            // If this lclVar is used in a SIMD intrinsic, then we don't want to struct promote it.
            // Note, however, that SIMD lclVars that are NOT used in a SIMD intrinsic may be
            // profitably promoted.
            if (varDsc.lvIsUsedInSimdIntrinsic)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because lvIsUsedInSimdIntrinsic()\n");
                return false;
            }

            // Reject struct promotion of parameters when -GS stack reordering is enabled
            // as we could introduce shadow copies of them.
            if (varDsc.lvIsParam && m_compiler.compGSReorderStackLayout)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because lvIsParam and compGSReorderStackLayout\n");
                return false;
            }

            if (varDsc.lvIsParam && m_compiler.fgNoStructParamPromotion)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled by fgNoStructParamPromotion\n");
                return false;
            }

            if (!m_compiler.lvaEnregMultiRegVars && varDsc.lvIsMultiRegArgOrRet)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because lvIsMultiRegArgOrRet()\n");
                return false;
            }

            // If the local was exposed at Tier0, we currently have to assume it's aliased for OSR.
            //
            if (m_compiler.lvaIsOSRLocal(lclNum) && m_compiler.info.compPatchpointInfo->IsExposed(lclNum))
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because it is an exposed OSR local\n");
                return false;
            }

            if (varDsc.lvDoNotEnregister)
            {
                // Promoting structs that are marked DNER will result in dependent
                // promotion. Allow physical promotion to handle these.
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because it has already been marked DNER\n");
                return false;
            }

            assert(varDsc.Layout is not null);

            if (varDsc.Layout.IsCustomLayout)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because it has custom layout\n");
                return false;
            }

            if (varDsc.lvStackAllocatedObject)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because it is a stack allocated object\n");
                return false;
            }

#if SWIFT_SUPPORT
            // Swift structs are not passed in a way that match their layout and
            // require reassembling on the local stack frame. Skip promotion for these
            // (which would result in dependent promotion anyway).
            if ((m_compiler.info.compCallConv == CorInfoCallConvExtension.Swift) && varDsc.lvIsParam)
            {
                JITDUMP($"  struct promotion of V{lclNum:D2} is disabled because it is a parameter to a Swift function\n");
                return false;
            }
#endif

            var typeHnd = varDsc.Layout.ClassHandle;
            assert(typeHnd != NO_CLASS_HANDLE);

            var canPromote = CanPromoteStructType(typeHnd);

            if (canPromote && varDsc.lvIsMultiRegArgOrRet)
            {
                uint fieldCnt = structPromotionInfo.fieldCnt;
                if (fieldCnt > MAX_MULTIREG_COUNT)
                {
                    canPromote = false;
                }
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
                else
                {
                    for (var i = 0; canPromote && (i < fieldCnt); i++)
                    {
                        var fieldType = structPromotionInfo.fields[i].fldType;

                        // Non-HFA structs are always passed in general purpose registers.
                        // If there are any floating point fields, don't promote for now.
                        // Likewise, since HVA structs are passed in SIMD registers
                        // promotion of non FP or SIMD type fields is disallowed.
                        // TODO-1stClassStructs: add support in Lowering and prolog generation
                        // to enable promoting these types.

                        if (varDsc.lvIsParam && (IsArmHfaParameter(lclNum) != varTypeUsesFloatReg(fieldType)))
                        {
                            canPromote = false;
                        }
#if FEATURE_SIMD
                        // If we have a register-passed struct with mixed non-opaque SIMD types (i.e. with defined fields)
                        // and non-SIMD types, we don't currently handle that case in the prolog, so we can't promote.
                        else if ((fieldCnt > 1) && varTypeIsStruct(fieldType) &&
                                 (structPromotionInfo.fields[i].fldSIMDTypeHnd != NO_CLASS_HANDLE) &&
                                 !m_compiler.isOpaqueSIMDType(structPromotionInfo.fields[i].fldSIMDTypeHnd))
                        {
                            canPromote = false;
                        }
#endif
                    }
                }
#elif UNIX_AMD64_ABI
                else
                {
                    SortStructFields();

                    // Only promote if the field types match the registers, unless we have a single SIMD field.
                    SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc;
                    m_compiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(typeHnd, &structDesc);

                    var regCount = structDesc.eightByteCount;

                    if ((structPromotionInfo.fieldCnt == 1) && varTypeIsSimd(structPromotionInfo.fields[0].fldType))
                    {
                        // Allow the case of promoting a single SIMD field, even if there are multiple registers.
                        // We will fix this up in the prolog.
                    }
                    else if (structPromotionInfo.fieldCnt != regCount)
                    {
                        canPromote = false;
                    }
                    else
                    {
                        for (var i = 0; canPromote && (i < regCount); i++)
                        {
                            ref var fieldInfo = ref structPromotionInfo.fields[i];
                            var fieldType = fieldInfo.fldType;

                            // We don't currently support passing SIMD types in registers.
                            if (varTypeIsSimd(fieldType))
                            {
                                canPromote = false;
                            }
                            else if (varTypeUsesFloatReg(fieldType) !=
                                     (structDesc.eightByteClassifications[i] == SystemVClassificationTypeSSE))
                            {
                                canPromote = false;
                            }
                        }
                    }
                }
#endif
            }
            return canPromote;
        }

        /// <summary>Should a struct var be promoted if it can be promoted?</summary>
        /// <param name="lclNum">struct local number</param>
        /// <returns>true if the struct should be promoted.</returns>
        /// <remarks>This routine mainly performs profitability checks.  Right now it also has some correctness checks due to limitations of down-stream phases.</remarks>
        private unsafe bool ShouldPromoteStructVar(int lclNum)
        {
            ref var varDsc = ref m_compiler.lvaGetDesc(lclNum);

            assert(varTypeIsStruct(varDsc.Type));
            assert(varDsc.Layout is not null);
            assert(varDsc.Layout.ClassHandle == structPromotionInfo.typeHnd);
            assert(structPromotionInfo.canPromote);

            var shouldPromote = true;

            // We *can* promote; *should* we promote?
            // We should only do so if promotion has potential savings.  One source of savings
            // is if a field of the struct is accessed, since this access will be turned into
            // an access of the corresponding promoted field variable.  Even if there are no
            // field accesses, but only block-level operations on the whole struct, if the struct
            // has only one or two fields, then doing those block operations field-wise is probably faster
            // than doing a whole-variable block operation (e.g., a hardware "copy loop" on x86).
            // Struct promotion also provides the following benefits: reduce stack frame size,
            // reduce the need for zero init of stack frame and fine grained constant/copy prop.
            // Asm diffs indicate that promoting structs up to 3 fields is a net size win.
            // So if no fields are accessed independently, and there are four or more fields,
            // then do not promote.
            //
            // TODO: Ideally we would want to consider the impact of whether the struct is
            // passed as a parameter or assigned the return value of a call. Because once promoted,
            // struct copying is done by field by field store instead of a more efficient
            // rep.stos or xmm reg based copy.
            if (structPromotionInfo.fieldCnt > 3 && !varDsc.lvFieldAccessed)
            {
                JITDUMP($"Not promoting promotable struct local V{lclNum:D2}: #fields = {structPromotionInfo.fieldCnt}, fieldAccessed = {varDsc.lvFieldAccessed}.\n");
                shouldPromote = false;
            }
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            else if ((structPromotionInfo.fieldCnt == 2) && (varTypeIsFloating(structPromotionInfo.fields[0].fldType) ||
                                                             varTypeIsFloating(structPromotionInfo.fields[1].fldType)))
            {
                // TODO-LoongArch64 - struct passed by float registers.
                JITDUMP($"Not promoting promotable struct local V{lclNum:D2}: #fields = {structPromotionInfo.fieldCnt} because it is a struct with float field(s).\n");
                shouldPromote = false;
            }
#endif // TARGET_LOONGARCH64 || TARGET_RISCV64
            else if (varDsc.lvIsParam && !m_compiler.lvaIsImplicitByRefLocal(lclNum) && !IsArmHfaParameter(lclNum))
            {
#if FEATURE_MULTIREG_STRUCT_PROMOTE
                // Is this a variable holding a value with exactly two fields passed in
                // multiple registers?
                if (varDsc.lvIsMultiRegArg || IsSysVMultiRegType(varDsc.GetLayout()))
                {
                    if ((structPromotionInfo.fieldCnt != 2) &&
                        ((structPromotionInfo.fieldCnt != 1) || !varTypeIsSimd(structPromotionInfo.fields[0].fldType)))
                    {
                        JITDUMP($"Not promoting multireg struct local V{lclNum:D2}, because lvIsParam is true, #fields != 2 and it's not a single SIMD.\n");
                        shouldPromote = false;
                    }
#if TARGET_LOONGARCH64 || TARGET_RISCV64
                    else if (m_compiler.lvaGetParameterABIInfo(lclNum).IsSplitAcrossRegistersAndStack())
                    {
                        JITDUMP($"Not promoting multireg struct local V{lclNum:D2}, because it is splitted.\n");
                        shouldPromote = false;
                    }
#endif
                }
                else
#endif
                {
                    // TODO-PERF - Implement struct promotion for incoming single-register structs.
                    //             Also the implementation of jmp uses the 4 byte move to store
                    //             byte parameters to the stack, so that if we have a byte field
                    //             with something else occupying the same 4-byte slot, it will
                    //             overwrite other fields.
                    if (structPromotionInfo.fieldCnt != 1)
                    {
                        JITDUMP($"Not promoting promotable struct local V{lclNum:D2}, because lvIsParam is true and #fields = {structPromotionInfo.fieldCnt}.\n");
                        shouldPromote = false;
                    }
                }
            }
            else if ((lclNum == m_compiler.genReturnLocal) && (structPromotionInfo.fieldCnt > 1))
            {
                // TODO-1stClassStructs: a temporary solution to keep diffs small, it will be fixed later.
                shouldPromote = false;
            }
#if DEBUG
            else if (m_compiler.compPromoteFewerStructs(lclNum))
            {
                // Do not promote some structs, that can be promoted, to stress promoted/unpromoted moves.
                JITDUMP($"Not promoting promotable struct local V{lclNum:D2}, because of STRESS_PROMOTE_FEWER_STRUCTS\n");
                shouldPromote = false;
            }
#endif

            //
            // If the lvRefCnt is zero and we have a struct promoted parameter we can end up with an extra store of
            // the incoming register into the stack frame slot.
            // In that case, we would like to avoid promortion.
            // However we haven't yet computed the lvRefCnt values so we can't do that.
            //

            return shouldPromote;
        }

        /// <summary>promote struct variable.</summary>
        /// <param name="lclNum">struct local number</param>
        private unsafe void PromoteStructVar(int lclNum)
        {
            ref var varDsc = ref m_compiler.lvaGetDesc(lclNum);

            // We should never see a reg-sized non-field-addressed struct here.
            assert(!varDsc.lvRegStruct);
            assert(varDsc.Layout is not null);
            assert(varDsc.Layout.ClassHandle == structPromotionInfo.typeHnd);
            assert(structPromotionInfo.canPromote);

            varDsc.lvFieldCnt = structPromotionInfo.fieldCnt;
            varDsc.lvFieldLclStart = m_compiler.lvaCount;
            varDsc.lvPromoted = true;
            varDsc.lvContainsHoles = structPromotionInfo.containsHoles;

#if DEBUG
            // Don't stress this in LCL_FLD stress.
            varDsc.lvKeepType = true;

            if (m_compiler.verbose)
            {
                jitprintf($"\nPromoting struct local V{lclNum:D2} ({varDsc.Layout.ClassName}):");
            }
#endif

            SortStructFields();

            for (var index = 0; index < structPromotionInfo.fieldCnt; index++)
            {
                ref var fieldInfo = ref structPromotionInfo.fields[index];

                if (!varTypeUsesIntReg(fieldInfo.fldType))
                {
                    // Whenever we promote a struct that contains a floating point field
                    // it's possible we transition from a method that originally only had integer
                    // local vars to start having FP.  We have to communicate this through this flag
                    // since LSRA later on will use this flag to determine whether or not to track FP register sets.
                    m_compiler.compFloatingPointUsed = true;
                }

                // Now grab the temp for the field local.
                var reason = "";

#if DEBUG
                reason = $"field V{lclNum:D2}.{m_compiler.eeGetFieldName(fieldInfo.diagFldHnd, includeType: false)} (fldOffset=0x{fieldInfo.fldOffset:X})";

                if (index > 0)
                {
                    noway_assert(fieldInfo.fldOffset > Unsafe.Subtract(ref fieldInfo, 1).fldOffset);
                }
#endif

                // Lifetime of field locals might span multiple BBs, so they must be long lifetime temps.
                var varNum = m_compiler.lvaGrabTemp(false, reason);

                // lvaGrabTemp can reallocate the lvaTable, so
                // refresh the cached varDsc for lclNum.
                varDsc = m_compiler.lvaGetDesc(lclNum);

                ref var fieldVarDsc = ref m_compiler.lvaGetDesc(varNum);
                fieldVarDsc.Type = fieldInfo.fldType;
                fieldVarDsc.lvIsStructField = true;
                fieldVarDsc.lvFldOffset = fieldInfo.fldOffset;
                fieldVarDsc.lvFldOrdinal = fieldInfo.fldOrdinal;
                fieldVarDsc.lvParentLcl = lclNum;
                fieldVarDsc.lvIsParam = varDsc.lvIsParam;
                fieldVarDsc.lvIsOSRLocal = varDsc.lvIsOSRLocal;
                fieldVarDsc.lvIsOSRExposedLocal = varDsc.lvIsOSRExposedLocal;

                if (varDsc.IsSpan && (fieldVarDsc.lvFldOffset == OFFSETOF__CORINFO_Span__length))
                {
                    fieldVarDsc.IsNeverNegative = true;
                }

                // This new local may be the first time we've seen a long typed local.
                if (fieldVarDsc.Type is TYP_LONG)
                {
                    m_compiler.compLongUsed = true;
                }

#if FEATURE_IMPLICIT_BYREFS
                fieldVarDsc.IsImplicitByRef = false;
#endif

                fieldVarDsc.lvIsRegArg = varDsc.lvIsRegArg;

#if FEATURE_SIMD
                if (varTypeIsSimd(fieldInfo.fldType))
                {
                    // We will not recursively promote this, so mark it as 'lvRegStruct' (note that we wouldn't
                    // be promoting this if we didn't think it could be enregistered.
                    fieldVarDsc.lvRegStruct = true;

                    // SIMD types may be HFAs so we need to set the correct state on
                    // the promoted fields to get the right ABI treatment in the
                    // backend.
                    if (GlobalJitOptions.compFeatureHfa && (fieldInfo.fldSize <= MAX_PASS_MULTIREG_BYTES))
                    {
                        // hfaType is set to float, double or SIMD type if it is an HFA, otherwise TYP_UNDEF
                        var hfaType = m_compiler.GetHfaType(fieldInfo.fldSIMDTypeHnd);

                        if (varTypeIsValidHfaType(hfaType))
                        {
                            fieldVarDsc.lvIsMultiRegArg = !varDsc.lvIsMultiRegArg && (fieldVarDsc.lvExactSize > hfaType.Size);
                        }
                    }
                }
#endif

#if DEBUG
                // This temporary should not be converted to a double in stress mode,
                // because we introduce assigns to it after the stress conversion
                fieldVarDsc.lvKeepType = true;
#endif
            }

#if TARGET_ARM
            if (varDsc.lvIsParam)
            {
                // TODO-Cleanup: Allow independent promotion for ARM struct parameters
                m_compiler.lvaSetVarDoNotEnregister(lclNum, DoNotEnregisterReason.IsStructArg);
            }
#endif
        }

        /// <summary>sort the fields according to the increasing order of the field offset.</summary>
        /// <remarks>This is needed because the fields need to be pushed on stack (when referenced as a struct) in offset order.</remarks>
        private void SortStructFields()
        {
            if (!structPromotionInfo.fieldsSorted)
            {
                var fields = (Span<lvaStructFieldInfo>)(structPromotionInfo.fields);
                fields[..structPromotionInfo.fieldCnt].Sort((lhs, rhs) => lhs.fldOffset.CompareTo(rhs.fldOffset));
                structPromotionInfo.fieldsSorted = true;
            }
        }

        /// <summary>Check if a local is an ARM or ARM64 HFA parameter.</summary>
        /// <param name="lclNum">The local</param>
        /// <returns>True if it is an HFA parameter.</returns>
        /// <remarks>This is a quirk to match old promotion behavior.</remarks>
        private unsafe bool IsArmHfaParameter(int lclNum)
        {
            if (!GlobalJitOptions.compFeatureHfa)
            {
                return false;
            }

            var layout = m_compiler.lvaGetDesc(lclNum).Layout;
            assert(layout is not null);

            var hfaType = m_compiler.info.compCompHnd->getHFAType(layout.ClassHandle);
            return hfaType != CORINFO_HFA_ELEM_NONE;
        }

        /// <summary>Check if a type is one that could be passed in 2 registers in some cases.</summary>
        /// <param name="layout">The local</param>
        /// <returns>True if it sometimes may be passed in two registers.</returns>
        /// <remarks>This is a quirk to match old promotion behavior.</remarks>
#if UNIX_AMD64_ABI
        private unsafe bool IsSysVMultiRegType(ClassLayout layout)
        {
            SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR structDesc;
            m_compiler.eeGetSystemVAmd64PassStructInRegisterDescriptor(layout.ClassHandle, &structDesc);
            return structDesc.passedInRegisters && (structDesc.eightByteCount == 2);
        }
#else
        private bool IsSysVMultiRegType(ClassLayout layout) => false;
#endif

        private unsafe var_types TryPromoteValueClassAsPrimitive(CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint maxTreeNodes, nint index)
        {
            // TODO: Port Compiler.StructPromotionHelper.TryPromoteValueClassAsPrimitive
            return TYP_UNDEF;
        }

        /// <summary>Skip over a tree node and all its children.</summary>
        /// <param name="treeNodes">array of type layout nodes, stored in preorder.</param>
        /// <param name="maxTreeNodes">size of 'treeNodes'</param>
        /// <param name="index">Index pointing to root of subtree to skip.</param>
        /// <remarks>Requires the tree nodes to be stored in preorder (as guaranteed by getTypeLayout).</remarks>
        private unsafe void AdvanceSubTree(CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint maxTreeNodes, ref nint index)
        {
            var parIndex = index++;

            while ((index < maxTreeNodes) && (treeNodes[index].parent >= parIndex))
            {
                index++;
            }
        }
    }
}

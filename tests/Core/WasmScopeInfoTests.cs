// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmScopeInfoTests
{
    [SetUp]
    public static void RequireWasmTarget()
    {
        Assume.That(Configuration.TARGET_WASM, "This fixture requires the WebAssembly target.");
    }

    [Test]
    public static void ScopeReportingOpensMethodWideRangesOnlyOnce()
    {
        WithCompiler(compiler => {
            var register = regNumberExtensions.MakeWasmReg(100_000, TYP_INT);
            compiler.lvaCount = 1;
            compiler.lvaTable = [new LclVarDsc {
                Type = TYP_INT,
                lvLRACandidate = true,
                RegNum = register,
            }];
            compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
            compiler.lvaTable[0].setLvRefCnt(1);
            compiler.lvaTrackedCount = 0;
            compiler.info.compLocalsCount = 1;
            compiler.info.compVarScopes = [new() {
                vsdVarNum = 0,
                vsdLVnum = 0,
                vsdLifeBeg = 0,
                vsdLifeEnd = 10,
            }];
            compiler.info.compVarScopesCount = 1;
            compiler.opts.compScopeInfo = true;
            compiler.opts.compDbgInfo = true;
            compiler.opts.compDbgCode = true;
            compiler.compInitScopeLists();

            var codeGen = new CodeGen(compiler)
            {
                IsFramePointerUsed = false,
            };
            compiler.codeGen = codeGen;
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false, TestConfiguration.IsDebug);
            codeGen.initializeVariableLiveKeeper();
            InitializeScopeInfo(codeGen);

            var first = new BasicBlock(null, null) { bbCodeOffs = 0, bbCodeOffsEnd = 2 };
            compiler.fgFirstBB = first;
            codeGen.siBeginBlock(first);
            codeGen.siBeginBlock(new BasicBlock(null, null) { bbCodeOffs = 4, bbCodeOffsEnd = 6 });

            var liveRanges = codeGen.getVariableLiveKeeper().getLiveRangesForVarForBody(0);
            Assert.That(liveRanges, Has.Count.EqualTo(1));
            Assert.That(liveRanges[0].m_VarLocation.vlType, Is.EqualTo(VLT_REG));
            Assert.That(liveRanges[0].m_VarLocation.vlReg.vlrReg,
                Is.EqualTo((ICorDebugInfo.RegNum)(ICorDebugInfo.WASM_LOCAL_REGNUM_BASE + 100_000)));
        });
    }

    [Test]
    public static void HiddenWasmArgumentsMapAsUnknownAndAreRemovedFromLocalIndexes()
    {
        WithCompiler(compiler => {
            compiler.lvaCount = 3;
            compiler.lvaTable = new LclVarDsc[3];
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[1].lvIsParam = true;
            compiler.lvaWasmSpArg = 0;
            compiler.lvaWasmPortableEntryPointArg = 1;
            compiler.info.compLocalsCount = 1;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
            compiler.lvaVarargsHandleArg = BAD_VAR_NUM;
            compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;

            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;

            Assert.Multiple(() => {
                Assert.That(compiler.compMap2ILvarNum(0), Is.EqualTo(ICorDebugInfo.UNKNOWN_ILNUM));
                Assert.That(compiler.compMap2ILvarNum(1), Is.EqualTo(ICorDebugInfo.UNKNOWN_ILNUM));
                Assert.That(compiler.compMap2ILvarNum(2), Is.Zero);
            });
        });
    }

    [Test]
    public static void WasmLocalIndexesUseReservedDebugRegisterNumbering()
    {
        var index = 100_000u;
        var register = regNumberExtensions.MakeWasmReg(index, TYP_INT);
        var location = new CodeGen.siVarLoc();
        location.storeVariableInRegisters(register, REG_NA);

        Assert.That((int)ICorDebugInfo.WASM_LOCAL_REGNUM_BASE,
            Is.EqualTo((int)ICorDebugInfo.RegNum.REGNUM_WASM_AMBIENT_SP + 1));
        Assert.That(location.vlType, Is.EqualTo(VLT_REG));
        Assert.That(location.vlReg.vlrReg,
            Is.EqualTo((ICorDebugInfo.RegNum)(ICorDebugInfo.WASM_LOCAL_REGNUM_BASE + (int)index)));

        Assume.That(TestConfiguration.IsDebug, "This test requires debug diagnostics.");
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));
        var output = TestConfiguration.CaptureDebugOutput(() => codeGen.dumpSiVarLoc(in location));
        Assert.That(output, Is.EqualTo("$100000"));
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaWasmSpArg = BAD_VAR_NUM;
        compiler.lvaWasmResumeIP = BAD_VAR_NUM;
        compiler.lvaWasmPortableEntryPointArg = BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
        compiler.lvaVarargsHandleArg = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.fgFuncletsCreated = true;
        compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT }];
        compiler.compFuncInfoCount = 1;
        compiler.SetMinOpts(true);
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "siInit")]
    private static extern void InitializeScopeInfo(CodeGen codeGen);
}

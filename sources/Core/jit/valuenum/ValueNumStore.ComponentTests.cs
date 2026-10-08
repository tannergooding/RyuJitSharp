// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
namespace RyuJitSharp;

public sealed partial class ValueNumStore
{
    public static void RunTests(Compiler compiler)
    {
        var store = new ValueNumStore(compiler);
        var nullValue = VNForNull();
        assert(nullValue == VNForNull());

        var one = store.VNForIntCon(1);
        assert(one == store.VNForIntCon(1));
        assert(store.TypeOfVN(one) is TYP_INT);
        assert(store.IsVNConstant(one));
        assert(store.ConstantValue<int>(one) == 1);

        var hundred = store.VNForIntCon(100);
        assert(hundred == store.VNForIntCon(100));
        assert(hundred != one);
        assert(store.TypeOfVN(hundred) is TYP_INT);
        assert(store.IsVNConstant(hundred));
        assert(store.ConstantValue<int>(hundred) == 100);

        var oneFloat = store.VNForFloatCon(1.0f);
        assert(oneFloat == store.VNForFloatCon(1.0f));
        assert((oneFloat != one) && (oneFloat != hundred));
        assert(store.TypeOfVN(oneFloat) is TYP_FLOAT);
        assert(store.IsVNConstant(oneFloat));
        assert(store.ConstantValue<float>(oneFloat) == 1.0f);

        var oneDouble = store.VNForDoubleCon(1.0);
        assert(oneDouble == store.VNForDoubleCon(1.0));
        assert((oneDouble != oneFloat) && (oneDouble != one) && (oneDouble != hundred));
        assert(store.TypeOfVN(oneDouble) is TYP_DOUBLE);
        assert(store.IsVNConstant(oneDouble));
        assert(store.ConstantValue<double>(oneDouble) == 1.0);

        var random = store.VNForExpr(null, TYP_INT);
        var sum = store.VNForFunc(TYP_INT, VNF_ADD, one, random);
        assert(sum == store.VNForFunc(TYP_INT, VNF_ADD, one, random));
        assert((sum != oneDouble) && (sum != oneFloat) && (sum != one) && (sum != random));
        assert(store.TypeOfVN(sum) is TYP_INT);
        assert(!store.IsVNConstant(sum));
        assert(store.IsVNFunc(sum));
        var application = new VNFuncApp();
        var found = store.GetVNFunc(sum, ref application);
        assert(found);
        assert((application.Func is VNF_ADD) && (application.Arity == 2) &&
            (application.GetArg(0) == random) && (application.GetArg(1) == one));

        var constantSum = store.VNForFunc(TYP_INT, VNF_ADD, one, hundred);
        assert(constantSum == store.VNForFunc(TYP_INT, VNF_ADD, one, hundred));
        assert((constantSum != oneDouble) && (constantSum != oneFloat) &&
            (constantSum != one) && (constantSum != hundred));
        assert(store.TypeOfVN(constantSum) is TYP_INT);
        assert(store.IsVNConstant(constantSum));
        assert(store.ConstantValue<int>(constantSum) == 101);
    }
}
#endif

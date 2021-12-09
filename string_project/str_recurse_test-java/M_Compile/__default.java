// Class __default
// Dafny class __default compiled into Java
package M_Compile;


@SuppressWarnings({"unchecked", "deprecation"})
public class __default {
  public __default() {
  }
  public static dafny.DafnySequence<? extends Character> replaceRecursive(dafny.DafnySequence<? extends Character> remainingString, dafny.DafnySequence<? extends Character> pattern, dafny.DafnySequence<? extends Character> other)
  {
    dafny.DafnySequence<? extends Character> newString = dafny.DafnySequence.<Character> empty(dafny.TypeDescriptor.CHAR);
    if(true) {
      if (((java.math.BigInteger.valueOf((remainingString).length())).compareTo((java.math.BigInteger.valueOf((pattern).length()))) < 0) || ((java.math.BigInteger.valueOf((remainingString).length())).signum() == 0)) {
        newString = remainingString;
        return newString;
      }
      if ((pattern).isPrefixOf((remainingString))) {
        dafny.DafnySequence<? extends Character> _32_prefixedInner;
        dafny.DafnySequence<? extends Character> _out0;
        _out0 = __default.replaceRecursive((remainingString).drop(java.math.BigInteger.valueOf((pattern).length())), pattern, other);
        _32_prefixedInner = _out0;
        newString = dafny.DafnySequence.<Character>concatenate(other, _32_prefixedInner);
        return newString;
      } else {
        dafny.DafnySequence<? extends Character> _33_first;
        _33_first = (remainingString).take(java.math.BigInteger.ONE);
        dafny.DafnySequence<? extends Character> _34_inner;
        dafny.DafnySequence<? extends Character> _out1;
        _out1 = __default.replaceRecursive((remainingString).drop(java.math.BigInteger.ONE), pattern, other);
        _34_inner = _out1;
        newString = dafny.DafnySequence.<Character>concatenate(_33_first, _34_inner);
        return newString;
      }
    }
    return newString;
  }
  private static final dafny.TypeDescriptor<__default> _TYPE = dafny.TypeDescriptor.referenceWithInitializer(__default.class, () -> (__default) null);
  public static dafny.TypeDescriptor<__default> _typeDescriptor() {
    return (dafny.TypeDescriptor<__default>) (dafny.TypeDescriptor<?>) _TYPE;
  }
}

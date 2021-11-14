// Class __default
// Dafny class __default compiled into Java
package str__recurseUnitTests_Compile;

import M_Compile.*;

@SuppressWarnings({"unchecked", "deprecation"})
public class __default {
  public __default() {
  }
  public static java.math.BigInteger test0()
  {
    java.math.BigInteger r0 = java.math.BigInteger.ZERO;
    if(true) {
      java.math.BigInteger _out0 = java.math.BigInteger.ZERO;
      _out0 = M_Compile.__default.foo(java.math.BigInteger.valueOf(11L), java.math.BigInteger.valueOf(6L));
      r0 = _out0;
    }
    return r0;
  }
  private static final dafny.TypeDescriptor<__default> _TYPE = dafny.TypeDescriptor.referenceWithInitializer(__default.class, () -> (__default) null);
  public static dafny.TypeDescriptor<__default> _typeDescriptor() {
    return (dafny.TypeDescriptor<__default>) (dafny.TypeDescriptor<?>) _TYPE;
  }
}

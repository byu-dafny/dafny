// Class __default
// Dafny class __default compiled into Java
package str__recurseUnitTests_Compile;

import M_Compile.*;
import java.lang.reflect.*;

@SuppressWarnings({ "unchecked", "deprecation" })
public class __default {
  public static void main(String[] args) throws Exception {
    for (Method method : __default.class.getDeclaredMethods()) {
      if (!method.getName().startsWith("test"))
        continue;
      Object result = method.invoke(null, mockParameters(method));
      String resultString = result == null ? "null" : result.toString();
      System.out.println(method.getName() + " " + resultString);
    }
  }

  public static Object[] mockParameters(Method method) {
    Object[] parameters = null;
    try {
      Class<?>[] types = method.getParameterTypes();
      parameters = new Object[types.length];
      for (int i = 0; i < types.length; i++)
        parameters[i] = types[i].getConstructor().newInstance();
    } catch (Exception e) {
      e.printStackTrace();
    }
    return parameters;
  }
  public __default() {
  }
  public static dafny.DafnySequence<? extends Character> test0()
  {
    dafny.DafnySequence<? extends Character> r0 = dafny.DafnySequence.<Character> empty(dafny.TypeDescriptor.CHAR);
    if(true) {
      dafny.DafnySequence<? extends Character> _out2;
      _out2 = M_Compile.__default.replaceRecursive(dafny.DafnySequence.of('a'), dafny.DafnySequence.of('a'), dafny.DafnySequence.of('a'));
      r0 = _out2;
    }
    return r0;
  }
  /* Compilation error: an assume statement cannot be compiled */
  public static dafny.DafnySequence<? extends Character> test2()
  {
    dafny.DafnySequence<? extends Character> r0 = dafny.DafnySequence.<Character> empty(dafny.TypeDescriptor.CHAR);
    if(true) {
      dafny.DafnySequence<? extends Character> _out3;
      _out3 = M_Compile.__default.replaceRecursive(dafny.DafnySequence.of('a'), dafny.DafnySequence.<Character> empty(dafny.TypeDescriptor.CHAR), dafny.DafnySequence.of('a'));
      r0 = _out3;
    }
    return r0;
  }
  /* Compilation error: an assume statement cannot be compiled */
  private static final dafny.TypeDescriptor<__default> _TYPE = dafny.TypeDescriptor.referenceWithInitializer(__default.class, () -> (__default) null);
  public static dafny.TypeDescriptor<__default> _typeDescriptor() {
    return (dafny.TypeDescriptor<__default>) (dafny.TypeDescriptor<?>) _TYPE;
  }
}

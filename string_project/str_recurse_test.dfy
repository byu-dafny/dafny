include "/workspaces/dafny/string_project/str_recurse.dfy"
module str_recurseUnitTests {
import M
method test0() returns (r0:int)  {
r0 := M.foo(11, 6);
}
method test1() returns (r0:int)  {
r0 := M.foo(11, 5);
}
method test3() returns (r0:int)  {
r0 := M.foo(10, 6);
}
}

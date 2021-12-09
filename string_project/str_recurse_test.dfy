include "/workspaces/dafny/string_project/str_recurse.dfy"
module str_recurseUnitTests {
import M
method test0() returns (r0:string)  {
assume false;
r0 := M.replaceRecursive(['a'], ['a'], ['a']);
}
method test2() returns (r0:string)  {
assume false;
r0 := M.replaceRecursive(['a'], [], ['a']);
}
}

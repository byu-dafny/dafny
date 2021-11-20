echo "0) Clearing old work"
#rm -rf /workspaces/dafny/string_project/str_recurse_test-java/*

echo "1) Dafny Compile to Java"
/workspaces/dafny/Scripts/dafny /compileTarget:java /workspaces/dafny/string_project/str_recurse_test.dfy

# Poor hack (because I'm not super familiar with various commands) to insert main and reflection by
# Catting the prefix
# tailing the generated test case
# writing this to a new file so it doesn't overwrite __default while reading
# then overwriting _default with that new file.
echo "2) Insert Java Main"
{ 
  cat /workspaces/dafny/string_project/scripts/java_to_overwrite.txt;
  tail -n +9 /workspaces/dafny/string_project/str_recurse_test-java/str__recurseUnitTests_Compile/__default.java;
} > /workspaces/dafny/string_project/str_recurse_test-java/str__recurseUnitTests_Compile/__default2.java
mv /workspaces/dafny/string_project/str_recurse_test-java/str__recurseUnitTests_Compile/__default2.java /workspaces/dafny/string_project/str_recurse_test-java/str__recurseUnitTests_Compile/__default.java

# generate Java bytecode
echo "3) Compiling Java"
javac -cp /workspaces/dafny/Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime.jar:/workspaces/dafny/string_project/str_recurse_test-java /workspaces/dafny/string_project/str_recurse_test-java/str__recurseUnitTests_Compile/__default.java

# run java code
echo "4) Running Java"
# couldn't get it to work without the cd. Is something wrong with the class path then?
cd /workspaces/dafny/string_project/str_recurse_test-java
# bad: swallow errors, since we have to inject assume false, so redirect them to dev null
java -cp /workspaces/dafny/Source/DafnyRuntime/DafnyRuntimeJava/build/libs/DafnyRuntime.jar:/workspaces/dafny/string_project/str_recurse_test-java str__recurseUnitTests_Compile/__default 2> /dev/null
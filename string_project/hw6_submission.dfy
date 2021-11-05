/******************************************************************
  1. A dafny model for replacing one string with another globally.
 ******************************************************************/
function replaceRecursiveFunc(remainingString:string, pattern:string, other:string) : string 
  requires |pattern| > 0
{
  // base case if string is less than the pattern or 0 size
  if |remainingString| < |pattern| || |remainingString| == 0 then
    remainingString 
  else 
    // if the first set of characters matches 
    if pattern <= remainingString then
      other + replaceRecursiveFunc(remainingString[|pattern|..], pattern, other)
    else
      remainingString[..1] + replaceRecursiveFunc(remainingString[1..], pattern, other)
}
function countOccurences(remainingString:string, pattern:string) : int 
  requires |pattern| > 0
{
  // base case if string is less than the pattern or 0 size
  if |remainingString| < |pattern| || |remainingString| == 0 then
    0 
  else 
    // if the first set of characters matches 
    if pattern <= remainingString then //if remainingString[..|pattern|] == pattern then
      countOccurences(remainingString[|pattern|..], pattern) + 1
    else
      countOccurences(remainingString[1..], pattern)
}


method replaceRecursive(remainingString:string, pattern:string, other:string) returns (newString:string)
  requires |remainingString| >= 0
  requires |pattern| > 0
  requires |other| >= 0
  // optional - no need to test string length since we know the exact return value
  //ensures |newString| == |remainingString| + countOccurences(remainingString, pattern) * (|other| - |pattern|)
  // required to know about each char in the sequence
  ensures newString == replaceRecursiveFunc(remainingString, pattern, other)
{
  // if the remaningString is too small to match (or we are at zero)
  if (|remainingString| < |pattern| || |remainingString| == 0) {
    return remainingString;
  }
  // if the pattern is at the beginning of the string
  if( pattern <= remainingString) {
    var prefixedInner:string := replaceRecursive(remainingString[|pattern|..], pattern, other);
    return other + prefixedInner;
  } else {
    var first:string := remainingString[..1];
    var inner:string := replaceRecursive(remainingString[1..], pattern, other);
    return first + inner;
  }
}

/*
  * Test method for my own sanity - Not related to test generation
  */
method Main ()
{
  // single replacement
  var t1:string := replaceRecursive("c", "c", "b");
  assert t1 == "b";
  // no replacement
  var t2:string := replaceRecursive("a", "c", "b");
  assert t2 == "a";
  // before replacement
  var t3:string := replaceRecursive("ca", "c", "b");
  assert t3 == "ba";
  // after replacement
  var t4:string := replaceRecursive("ac", "c", "b");
  assert t4 == "ab";

  // sequential replacement
  var t5:string := replaceRecursive("cc", "c", "b");
  assert t5 == "bb";
  // non-sequential replacement replacement
  var t6:string := replaceRecursive("cac", "c", "b");
  assert t6 == "bab";
  // complex non-sequential replacement replacement
  var t7:string := replaceRecursive("accacc", "c", "b");
  assert t7 == "abbabb";

  // empty original
  var t8:string := replaceRecursive("", "c", "b");
  assert t8 == "";
  // empty other
  var t9:string := replaceRecursive("ca", "c", "");
  assert t9 == "a";
}

/******************************************************************
  2. Test Generation Category
 ******************************************************************/
/*
White Box Testing to produce test partitions on the ensures and requires clauses.
*/


/******************************************************************
  3. Automated Test Generation
 ******************************************************************/
  // Expected Tests For Partition Testing
  //         Primitive Requires Partitions
  // The code will generate any test that is accept or reject.
  // The code will consider, but not generate any test that is impossible
  // remainingString.length = -10 (impossible)
  // remainingString.length = -1 (impossible)
  // remainingString.length == 0 (accept)
  // remainingString.length = 1 (accept)
  // remainingString.length = 10 (accept)
  // pattern.length = -10 (impossible)
  // pattern.length = -1 (impossible)
  // pattern.length == 0 (reject)
  // pattern.length = 1 (accept)
  // pattern.length = 10 (accept)
  // other.length = -10 (impossible)
  // other.length = -1 (impossible)
  // other.length == 0 (accept)
  // other.length = 1 (accept)
  // other.length = 10 (accept)

  //         Complex Ensures Partitions
  // Included in description is a high level comment about why.
  // ???? is where I understand the oracle is necessary
  
  // recursion base case
  // remainingString.length < pattern.length => ???? + does not call recursion
  // remainingString.length == 0 => ???? + does not call recursion

  // recursion non-base case (1)
  // pattern is prefix of remainingString => ???? + calls recursion
  // pattern is not a prefix of remainingString => ???? + calls recursion

  // deterministic recursion: 
  // a recursive call has a "smaller" input, where smaller approaches a base cases
  // remainingString.length = old(remainingString.length) - 1 || pattern.length = old(pattern.length) + 1

/******************************************************************
  4. Justification
 ******************************************************************/
 /*
 White box testing is interesting since we have access to the source to improve
 our test generation. In this case, we'll use the spec (or raw dafny maybe) to 
 decide how each primitive parameter can be partitioned. In this case, the simple
 ensure clauses can easily be partitioned _and_ also categorized into "accept",
 "reject", and "impossible". Partitions allow for a minimal set of tests, since
 all inputs in a partition should result in the same "accept", "reject", or
 "impossible".

 The cool part of this is figuring our partitions. With simple primitive requires, 
 partitions become easy. When complex ensures, partitions become hard (which is why
 I've targetted them around properties of recursion).
 */
// String algorithm for replacing one string with another string globally
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
  * Test method
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

/**
Ideas for testing
1. fuzz testing (random inputs)
2. pathological testing
3. edge case testing (test least likely paths/inputs/etc)
4. regression changes (where do we expect things to change, what can we test to ensure output is same)
5. deep testing (test paths/conditions that are hard for engineers to think about when writing)
6. recursive identification testing (can we test each recursive path only?)

Questions I want to ask
1. Are we generating tests to validate that our java code (generated from dafny) is correct?
2. Are we generating tests to perform some coverage tests for the generated java?
3. What is purpose of middle part.

 */


  /**
  method replace(original:string, pattern:string, other:string) returns (newString:string)
  requires |original| >= 0
  requires |pattern| > 0
  requires |other| >= 0
  ensures |newString| == |original| + countOccurences(original, pattern) * (|other| - |pattern|)
  {
    var a_original := original;
    var a_pattern := pattern;
    var a_other := other;
    
    newString := "";
    var i	:=	0;
    while (i	<	|original|)	
    {
      // inner match
      var k := 0;
      label inner : while (i + k	<	|original| && k <= |pattern|)	
      {
        var actualIndex := i + k;

        // we have matched pattern to original[i..i+k]
        if(k == |pattern|) {
          // inject other
          newString := newString + other;
          // update i's position, with a hack for termination proving
          i := actualIndex - 1;
          // exit inner loop
          break inner;
        }

        if(original[actualIndex] != pattern[k]) {
          // inject the ones we have passed by
          newString := newString + original[i..actualIndex];
          // update i's position
          i := actualIndex;
          // exit inner loop
          break inner;
        }

        // increment our index
        k := k + 1;
      }
      // increment our index
      i	:=	i	+	1;
    }
    return newString;
  }
  */
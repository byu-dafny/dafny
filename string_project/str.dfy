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

method replaceRecursive2(remainingString:string, pattern:string, other:string) returns (newString:string)
requires |remainingString| >= 0
requires |pattern| > 0
requires |other| >= 0
// optional - no need to test string length since we know the exact return value
//ensures |newString| == |remainingString| + countOccurences(remainingString, pattern) * (|other| - |pattern|)
// required to know about each char in the sequence
ensures newString == replaceRecursiveFunc(remainingString, pattern, other)
{
  // if the remaningString is too small to match (or we are at zero)
  if (|remainingString| == 0) {
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



method replaceIterative(original:string, pattern:string, other:string) returns (newString:string)
  requires |original| >= 0
  requires |pattern| > 0
  requires |other| >= 0
  // optional - no need to test string length since we know the exact return value
  //ensures |newString| == |remainingString| + countOccurences(remainingString, pattern) * (|other| - |pattern|)
  // required to know about each char in the sequence
  ensures newString == replaceRecursiveFunc(original, pattern, other)
  {
    newString := "";
    var i := 0;
    while (i < |original|)	
    {
      // if its a prefix
      if( pattern <= original[i..]) {
        // add the replacement
        newString := newString + other;
        // skip to the end of the match
        i := i + |pattern|;
      }
      else {
        var first:string := original[i..i+1];
        // add the first char
        newString := newString + first;
        // increment our index
        i	:=	i	+	1;
      }
    }
    return newString;
  }

method replaceIterative2(original:string, pattern:string, other:string) returns (newString:string)
  requires |original| >= 0
  requires |pattern| > 0
  requires |other| >= 0
  // optional - no need to test string length since we know the exact return value
  //ensures |newString| == |remainingString| + countOccurences(remainingString, pattern) * (|other| - |pattern|)
  // required to know about each char in the sequence
  ensures newString == replaceRecursiveFunc(original, pattern, other)
  {
    newString := "";
    var original2 := original;
    while (|original2| > 0)	
    {
      // if its a prefix
      if( pattern <= original2) {
        // add the replacement
        newString := newString + other;
        // skip to the end of the match
        original2 := original2[|pattern|..];
      }
      else {
        var first:string := original2[..1];
        // add the first char
        newString := newString + first;
        // increment our index
        original2 := original2[1..];
      }
    }
    return newString;
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
  var t8:string := replaceIterative("", "c", "b");
  assert t8 == "";
  // empty other
  var t9:string := replaceRecursive("ca", "c", "");
  assert t9 == "a";

  // single replacement
  var t1i:string := replaceRecursive("c", "c", "b");
  assert t1i == "b";
  // no replacement
  var t2i:string := replaceRecursive("a", "c", "b");
  assert t2i == "a";
  // before replacement
  var t3i:string := replaceRecursive("ca", "c", "b");
  assert t3i == "ba";
  // after replacement
  var t4i:string := replaceRecursive("ac", "c", "b");
  assert t4i == "ab";

  // sequential replacement
  var t5i:string := replaceRecursive("cc", "c", "b");
  assert t5i == "bb";
  // non-sequential replacement replacement
  var t6i:string := replaceRecursive("cac", "c", "b");
  assert t6i == "bab";
  // complex non-sequential replacement replacement
  var t7i:string := replaceRecursive("accacc", "c", "b");
  assert t7i == "abbabb";

  // empty original
  var t8i:string := replaceRecursive("", "c", "b");
  assert t8i == "";
  // empty other
  var t9i:string := replaceRecursive("ca", "c", "");
  assert t9i == "a";


  // Expected Tests For Partition Testing
  //         Requires Partitions
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

  //         Ensures Partitions
  // The code will generate any test that is accept or reject.
  // The code will consider, but not generate any test that is impossible
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
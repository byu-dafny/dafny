module M {
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
}

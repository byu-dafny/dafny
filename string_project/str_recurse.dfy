module M {
  /*method foo(a:int, b:int) returns (c:int) 
  requires a > 10
  requires b > 5
  ensures c == a * b
  {
    return b * a;
  }*/



  
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
    //assert (|remainingString| >= 0) == false;
    //assert false;
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
/*
class Baz {
  method foo(a:int, b:int) returns (c:int) 
      requires a >= 5
      requires b >= 0
      ensures c == bar(a, b)

  function bar(a:int, b:int):int 
      requires a >= 5
      requires b >= 0
      decreases a
  {
      if a > 5 then           // PIVOTAL CONDITION
          1 + bar(a - 1, b)  // non-base case
      else     
          0                  // base case
  }
}

type MockBaz = Baz


method Main() {
  var b:Baz := new MockBaz>;
}
*/
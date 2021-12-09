method multiply(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  result := a * b;
}

method multiply2(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  if (a > b) {
    result := a * b;
  } else {
    result := b * a;
  }
}
method multiply3(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  if(a == 0) {
    return 0;
  } else {
    var inner := multiply3(a - 1, b);
    return b + inner;
  }
}
method multiply4(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  if(b == 0) {
    return 0;
  } else {
    var inner := multiply3(a, b - 1);
    return a + inner;
  }
}
// russian method
method multiply5(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  if(a == 0) {
    return 0;
  }
  if(a % 2 == 0) {
    var inner := multiply5(a / 2, b);
    return 2 * inner;
  } else {
    var inner := multiply5((a - 1) / 2, b);
    return b + 2 * inner;
  }
}
method multiply6(a:int, b:int) returns (result:int) 
requires a >= 0;
requires b >= 0;
ensures result == a * b;
{
  if(a > 100) {
    var inner := multiply(a, b);
    return inner;
  } else {
    var inner := multiply2(a, b);
    return inner;
  }
}


method Main ()
{
  var m := multiply(1, 2);
  assert m == 2;
  
  m := multiply2(1, 2);
  assert m == 2;
  m := multiply3(1, 2);
  assert m == 2;
  m := multiply4(1, 2);
  assert m == 2;
  m := multiply5(1, 2);
  assert m == 2;
  m := multiply6(1, 2);
  assert m == 2;
}
method Main() {
  assert 1 == 1;
  assert "cats"[0..1] == "c";
  assert "cats"[1..2] == "a";
  assert "c" <= "cats";
  assert "ca" <= "cats";
  assert "cat" <= "cats";
  assert "cats" <= "cats";
  assert "at" <= "cats";



  var original := "aab";
  var pattern := "aa";
  var other := "";

  var newString := "";

  var i := 0;
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

  assert original[i..] == "b";
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
/*
  // if its a prefix
  if( pattern <= original[i..]) {
    // add the replacement
    newString := newString + other;
    // skip to the end of the match
    i := i + |pattern|;
  }
  else {
    var firstC:char := original[i];
    var first := [firstC];
    // add the first char
    newString := newString + first;
    // increment our index
    i	:=	i	+	1;
  }*/

  assert newString == "b";
}
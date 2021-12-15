module M {
    method simple4(x: int, y: int, z: int, t: int) returns (i: bool)
    {
        var q := x + 1;
        var w := y * 3;
        var e := z + y;
        var r := t * x;

        if ((q == 3 && w < 4) 
        || (e >= 6 
        && r != 20 )) 
        {
            i := true;
        } 
        else {
            i := false;
        }
        if (q < 5 || w 
        > 123) && (e == 
        23 || r < 18) 
        {
            i := true;
        } 
        else {
            i := false;
        }
    }
}
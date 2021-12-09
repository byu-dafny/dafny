CheckWellformed$$M.__default.replaceRecursiveFunc
        anon0
                assume {:print "Impl", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", remainingString#0, " | ", pattern#0, " | ", other#0} true;
                assume {:print "Types", " | ", "Seq Box", " | ", "Seq Box", " | ", "Seq Box"} true;
                b$reqreads#0 := true;
                b$reqreads#1 := true;
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20032"} true;
        anon1
                // AddWellformednessCheck for function replaceRecursiveFunc
                assume {:captureState "/workspaces/dafny/string_project/str_recurse.dfy(2,11): initial state"} true;
                $_Frame := (lambda<alpha> $o: ref, $f: Field alpha :: $o != null && read($Heap, $o, alloc) ==> false);
                assume Seq#Length(pattern#0) > 0;
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20034"} true;
        anon11_Then
                assume $Is(M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0), TSeq(TChar));
                assume false;
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20036"} true;
        anon11_Else
                $_Frame := (lambda<alpha> $o: ref, $f: Field alpha :: $o != null && read($Heap, $o, alloc) ==> false);
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20043"} true;
        anon12_Then
                assume {:partition} Seq#Length(pattern#0) <= Seq#Length(remainingString#0);
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20045"} true;
        anon12_Else
                assume {:partition} Seq#Length(remainingString#0) < Seq#Length(pattern#0);
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20047"} true;
        anon5
        anon13_Then
                assume {:partition} Seq#Length(remainingString#0) < Seq#Length(pattern#0) || Seq#Length(remainingString#0) == LitInt(0);
                assume M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0) == remainingString#0;
                assume true;
                // CheckWellformedWithResult: any expression
                assume $Is(M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0), TSeq(TChar));
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20056"} true;
        anon13_Else
                assume {:partition} !(Seq#Length(remainingString#0) < Seq#Length(pattern#0) || Seq#Length(remainingString#0) == LitInt(0));
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20063"} true;
        anon14_Then
                assume {:partition} Seq#Length(pattern#0) <= Seq#Length(remainingString#0) && Seq#SameUntil(pattern#0, remainingString#0, Seq#Length(pattern#0));
                assert 0 <= Seq#Length(pattern#0) && Seq#Length(pattern#0) <= Seq#Length(remainingString#0);
                ##remainingString#0 := Seq#Drop(remainingString#0, Seq#Length(pattern#0));
                // assume allocatedness for argument to function
                assume $IsAlloc(##remainingString#0, TSeq(TChar), $Heap);
                ##pattern#0 := pattern#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##pattern#0, TSeq(TChar), $Heap);
                ##other#0 := other#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##other#0, TSeq(TChar), $Heap);
                assert {:subsumption 0} Seq#Length(##pattern#0) > 0;
                assume Seq#Length(##pattern#0) > 0;
                b$reqreads#0 := (forall<alpha> $o: ref, $f: Field alpha :: false ==> $_Frame[$o, $f]);
                assert Seq#Rank(##remainingString#0) < Seq#Rank(remainingString#0) || (Seq#Rank(##remainingString#0) == Seq#Rank(remainingString#0) && (Seq#Rank(##pattern#0) < Seq#Rank(pattern#0) || (Seq#Rank(##pattern#0) == Seq#Rank(pattern#0) && Seq#Rank(##other#0) < Seq#Rank(other#0))));
                assume M.__default.replaceRecursiveFunc#canCall(Seq#Drop(remainingString#0, Seq#Length(pattern#0)), pattern#0, other#0);
                assume M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0) == Seq#Append(other#0, M.__default.replaceRecursiveFunc($LS($LZ), Seq#Drop(remainingString#0, Seq#Length(pattern#0)), pattern#0, other#0));
                assume M.__default.replaceRecursiveFunc#canCall(Seq#Drop(remainingString#0, Seq#Length(pattern#0)), pattern#0, other#0);
                // CheckWellformedWithResult: any expression
                assume $Is(M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0), TSeq(TChar));
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20065"} true;
        anon14_Else
                assume {:partition} !(Seq#Length(pattern#0) <= Seq#Length(remainingString#0) && Seq#SameUntil(pattern#0, remainingString#0, Seq#Length(pattern#0)));
                assert 0 <= LitInt(1) && LitInt(1) <= Seq#Length(remainingString#0);
                assert 0 <= LitInt(1) && LitInt(1) <= Seq#Length(remainingString#0);
                ##remainingString#1 := Seq#Drop(remainingString#0, LitInt(1));
                // assume allocatedness for argument to function
                assume $IsAlloc(##remainingString#1, TSeq(TChar), $Heap);
                ##pattern#1 := pattern#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##pattern#1, TSeq(TChar), $Heap);
                ##other#1 := other#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##other#1, TSeq(TChar), $Heap);
                assert {:subsumption 0} Seq#Length(##pattern#1) > 0;
                assume Seq#Length(##pattern#1) > 0;
                b$reqreads#1 := (forall<alpha> $o: ref, $f: Field alpha :: false ==> $_Frame[$o, $f]);
                assert Seq#Rank(##remainingString#1) < Seq#Rank(remainingString#0) || (Seq#Rank(##remainingString#1) == Seq#Rank(remainingString#0) && (Seq#Rank(##pattern#1) < Seq#Rank(pattern#0) || (Seq#Rank(##pattern#1) == Seq#Rank(pattern#0) && Seq#Rank(##other#1) < Seq#Rank(other#0))));
                assume M.__default.replaceRecursiveFunc#canCall(Seq#Drop(remainingString#0, LitInt(1)), pattern#0, other#0);
                assume M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0) == Seq#Append(Seq#Take(remainingString#0, LitInt(1)), M.__default.replaceRecursiveFunc($LS($LZ), Seq#Drop(remainingString#0, LitInt(1)), pattern#0, other#0));
                assume M.__default.replaceRecursiveFunc#canCall(Seq#Drop(remainingString#0, LitInt(1)), pattern#0, other#0);
                // CheckWellformedWithResult: any expression
                assume $Is(M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0), TSeq(TChar));
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20067"} true;
        anon10
                assert b$reqreads#0;
                assert b$reqreads#1;
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursiveFunc", " | ", "20069"} true;




Impl: CheckWellformed$$M.__default.replaceRecursive
        anon0
                assume {:print "Impl", " | ", "CheckWellformed$$M.__default.replaceRecursive", " | ", remainingString#0, " | ", pattern#0, " | ", other#0} true;
                assume {:print "Types", " | ", "Seq Box", " | ", "Seq Box", " | ", "Seq Box"} true;
                // AddMethodImpl: replaceRecursive, CheckWellformed$$M.__default.replaceRecursive
                $_Frame := (lambda<alpha> $o: ref, $f: Field alpha :: $o != null && read($Heap, $o, alloc) ==> false);
                assume {:captureState "/workspaces/dafny/string_project/str_recurse.dfy(16,9): initial state"} true;
                assume Seq#Length(remainingString#0) >= LitInt(0);
                assume Seq#Length(pattern#0) > 0;
                assume Seq#Length(other#0) >= LitInt(0);
                havoc $Heap;
                assume (forall $o: ref :: { $Heap[$o] } $o != null && read(old($Heap), $o, alloc) ==> $Heap[$o] == old($Heap)[$o]);
                assume $HeapSucc(old($Heap), $Heap);
                havoc newString#0;
                assume {:captureState "/workspaces/dafny/string_project/str_recurse.dfy(23,20): post-state"} true;
                ##remainingString#0 := remainingString#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##remainingString#0, TSeq(TChar), $Heap);
                ##pattern#0 := pattern#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##pattern#0, TSeq(TChar), $Heap);
                ##other#0 := other#0;
                // assume allocatedness for argument to function
                assume $IsAlloc(##other#0, TSeq(TChar), $Heap);
                assert {:subsumption 0} Seq#Length(##pattern#0) > 0;
                assume Seq#Length(##pattern#0) > 0;
                assume M.__default.replaceRecursiveFunc#canCall(remainingString#0, pattern#0, other#0);
                assume Seq#Equal(newString#0, M.__default.replaceRecursiveFunc($LS($LZ), remainingString#0, pattern#0, other#0));
                assume {:print "Block", " | ", "CheckWellformed$$M.__default.replaceRecursive", " | ", "20339"} true;
// RUN: %baredafny translate "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

method Main() {
    print "hello\n";
}

from pyeda.inter import *
#from graphviz import Source
from pathsearch import *
from random import Random
import sys

class hashabledict(dict):
  def __key(self):
    return tuple((k,self[k]) for k in sorted(self))
  def __hash__(self):
    return hash(frozenset(self))
  def __eq__(self, other):
    return self.__key() == other.__key()

def main():
    f=expr(str(sys.argv[1]))
    f=expr2bdd(f)
    test_cases, _, _ = run_one_pathsearch(f, LongestPath, Random(42))
    test_set = set()
    for _, independence_pair in test_cases.items():
        for test_case in independence_pair:
            test_set.add(convert_dict_to_string(test_case))
    #print(test_case)
    #gv = Source(f.to_dot())
    #gv.render('Example1', view=True)
    print(list(test_set))

def convert_dict_to_string(bdd_dict):
    new_dict = hashabledict()
    for k, v in bdd_dict.items():
        new_dict[str(k)] = v
    return new_dict

main()

#"v0 | (v1 & v2)"
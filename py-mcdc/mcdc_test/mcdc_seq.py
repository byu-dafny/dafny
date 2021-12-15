from pyeda.inter import *
from graphviz import Source
from pathsearch import *
from random import Random

A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z = map(bddvar, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')

def get_sequences(d_conv, all_vars):
    """
    given a converted decision, return a mcdc sequence suite
    :param d_conv: decision with conditions converted to variables
    :return: a list of mcdc sequences (mappings from variables to truth values)
    """
    f = eval(d_conv)
    # print(d_conv)
    # print()
    # print(all_vars)
    # print()
    sequences = []
    sequences_v = set()
    raw_sequences, _, _ = run_one_pathsearch(f, LongestPath, Random(42))
    # print(raw_sequences)
    for c1 in raw_sequences:
        for i in range(2):
            raw_seq = raw_sequences[c1][i]
            seq_v = ''
            seq = {}
            # for c2 in raw_seq:
            #     seq_v += str(raw_seq[c2])
            #     seq[str(c2)] = raw_seq[c2]
            for c2 in all_vars:
                # truth_v = raw_seq[eval(c2)] if
                if eval(c2) in raw_seq:
                    seq_v += str(raw_seq[eval(c2)])
                    seq[c2] = raw_seq[eval(c2)]
                else:
                    seq_v += '2'
                    seq[c2] = 2
            if seq_v not in sequences_v:
                sequences_v.add(seq_v)
                sequences.append(seq)
    # print(sequences)
    return sequences

if __name__ == '__main__':
    print(get_sequences(('(A|B) & (C | D)'),['A','B','C','D']))
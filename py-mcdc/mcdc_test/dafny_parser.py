import re
import copy
import os
import argparse
from mcdc_seq import get_sequences


AVOID_KEYWORDS = ['requires', 'modifies', 'ensures', '//']
ALPHABETA = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'


def main(path):
    # get the original program and all decisions
    # decisions = {decision expression: line in the program}
    program, decisions = get_all_decisions(path)

    # for each decision, convert it  ''a < 7 || b > 8'' -> ''A || B''
    # then generate a mcdc sequence suite for the converted decision
    # lastly annotate the program for each mcdc sequence
    annotation_cnt = 0
    for i, d in enumerate(decisions):
        d_conv, cond_map = convert_decision(d)

        mcdc_sequences = get_sequences(d_conv.replace('&&', '&').replace('||', '|'), list(cond_map.keys()))
        
        for j, seq in enumerate(mcdc_sequences):
            annotate_program(program, decisions[d], seq, cond_map, i, j, annotation_cnt)
            annotation_cnt += 1
    print(f'annotated {annotation_cnt} programs')
    
    # with open('annotation.dfy', 'w') as fh:
    #     fh.writelines(program)


def get_all_decisions(path):
    """
    read the program based on the given path, find all decisions in the program
    todo: for now this function only looks for decisions in if statements
    :param path: path to the program
    :return: program, mapping from decision to its position {decision: line (position of the decision)}
    """
    with open(path, 'r') as fh:
        program = fh.readlines()
        decisions = {}
        for num, line in enumerate(program):
            skip_line = False
            for avoid_keyword in AVOID_KEYWORDS:
                if avoid_keyword in line:
                    skip_line = True
                    break
            if skip_line:
                continue

            if 'if' in line:
                decisions[extract_decision(line)] = num

    return program, decisions


def extract_decision(line):
    """
    extract the decision out from a given line of code
    :param line: str for the line of code
    :return: the decision str without whitespace
    """
    start = line.find('if') + 2
    end = line.find('{')
    return ''.join(line[start: end].split())


def convert_decision(d):
    """
    convert conditions in a decision to variables
    todo: for now this function is based on alphabet and only supports decisions with no more than 26 conditions
    :param d: the decision
    :return: the converted decision and mapping from variable to conditions
    """
    d_no_parentheses = d.replace('(', '').replace(')', '')  # remove '(' and ')' from the decision
    conditions = re.split(r'\|\||\&\&', d_no_parentheses)  # split the decision based on '||' and '&&' operators
    conditions = set(conditions)  # remove duplicated conditions

    # iterate through conditions and build the converted decision and the mapping
    d_conv, mapping = d, {}
    for i, c in enumerate(conditions):
        mapping[ALPHABETA[i]] = c
        d_conv = d_conv.replace(c, ALPHABETA[i])

    return d_conv, mapping


def annotate_program(program, line, seq, cond_map, i, j, annotation_cnt):
    """
    annotate the program by adding an assertion and save the annotation
    :param program: original program
    :param line: the position to add the assertion
    :param seq: the mcdc sequence based on which the assertion is created
    :param cond_map: the mapping from boolean variables to conditions
    :param i: index of the decision
    :param j: index of the sequence
    :param annotation_cnt: the number of assertions that have been added
    :return: none
    """
    # assertion = get_assertion(seq, cond_map)
    # program.insert(line + annotation_cnt, assertion)
   
    assertion = get_assertion(seq, cond_map)
    annotated_prog = copy.deepcopy(program)
    annotated_prog.insert(line, assertion)
    if not os.path.isdir('annotation'):
        os.mkdir('annotation')
    with open(f'annotation/annotation{i}_{j}.dfy', 'w') as fh:
        fh.writelines(annotated_prog)


def get_assertion(seq, cond_map):
    """
    create an assertion based on the mcdc sequence
    :param seq: the mcdc sequence
    :param cond_map: the mapping from boolean variables to conditions
    :return: the assertion str
    """
    annotated_cond = []
    for var in seq:
        if seq[var] == 1:
            annotated_cond.append('!(' +cond_map[var] + ')')
        elif seq[var] == 0:
            annotated_cond.append(cond_map[var])
    assertion = '||'.join(annotated_cond)
    assertion = 'assert ' + assertion + ';\n'
    return assertion


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('rprint_path')
    args = parser.parse_args()

    main(args.rprint_path)

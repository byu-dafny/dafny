#!/bin/bash

prog=$1
echo generating test cases for $prog...
echo 

tmp_rprint="RprintProgram$RANDOM$RANDOM$RANDOM"
echo rprinting the dafny program...
Scripts/dafny $prog /rprint:$tmp_rprint /compile:0
echo 

echo clearing the annotation folder...
for i in `find annotation/annotation*`
do  
    rm $i
done
echo

python3 py-mcdc/mcdc_test/dafny_parser.py $tmp_rprint
echo

for i in `find annotation/annotation*`
do  
    echo "generating a test case for $i ..."
    echo 
    Scripts/dafny $i /generateTestMode:Unchange
    echo
done

echo deleting the rprint version of the program...
rm $tmp_rprint

echo "done"
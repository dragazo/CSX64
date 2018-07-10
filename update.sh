#!/usr/bin/env bash

# this script takes zero or more paths and assembles the asm directory at each location.

exe=csx.exe # name of executable

# ---------------------------

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" # script dir

# ---------------------------

# update all the paths
while [ ! $# -eq 0 ]
do
    # make sure the executable exists
    if [ ! -x "$1/$exe" ]
    then
        echo "FAILED $1 - No $exe found"
        shift
        continue
    fi
    
    # empty its stdlib folder of any object files (or create it)
    if [ -d "$1/stdlib" ]
    then
        find "$1/stdlib" -name "*.o" | xargs -i rm "{}"
    else
        mkdir "$1/stdlib"
    fi
    
    # ---------------------------
    
    # assemble _start
    "$1/$exe" "$root/asm/_start.asm" -ao "$1/_start.o"
    # assemble stdlib
    find "$root/asm/stdlib" -name "*.asm" | xargs -i basename -s ".asm" "{}" | xargs -ri "$1/$exe" "$root/asm/stdlib/{}.asm" -ao "$1/stdlib/{}.o"
    
    echo "updated $1"
    shift
done

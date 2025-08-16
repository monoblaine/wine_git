#!/bin/bash

exec_id=$1
shift
is_input_redirected=$1
shift
path_to_tmp=$(dirname "${BASH_SOURCE[0]}")/tmp
# path_to_tmp=~/wine_git/tmp
path_to_out_file=$path_to_tmp/out_$exec_id
path_to_exc_file=$path_to_tmp/exc_$exec_id

if [[ "$is_input_redirected" == "0" ]]; then
    git "$@" >"$path_to_out_file" 2>&1
    echo $? >"$path_to_exc_file"
else
    path_to_in_file=$path_to_tmp/in_$exec_id
    git "$@" >"$path_to_out_file" 2>&1 <"$path_to_in_file"
    echo $? >"$path_to_exc_file"
    rm "$path_to_in_file"
fi

touch "$path_to_tmp/lock_$exec_id"

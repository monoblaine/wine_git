#!/bin/bash

path_to_wine_git_folder="/home/serhan/Shared/wine_git"
path_to_tmp="$path_to_wine_git_folder/tmp"

exec_id=$1
path_to_out_file="$path_to_tmp/out_$exec_id"
shift
is_input_redirected=$1
shift

if [[ "$is_input_redirected" == "0" ]]; then
    git "$@" >$path_to_out_file
else
    path_to_in_file=$path_to_tmp/in_$exec_id
    git "$@" >$path_to_out_file <"$path_to_in_file"
    rm "$path_to_in_file"
fi

touch "$path_to_tmp/lock_$exec_id"

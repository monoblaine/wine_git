#!/bin/bash

path_to_wine_git_folder="/home/serhan/Shared/wine_git"
path_to_tmp="$path_to_wine_git_folder/tmp"

execId=$1
path_to_out_file="$path_to_tmp/out_$execId"
shift
is_input_redirected=$1
shift

if [[ "$is_input_redirected" == "0" ]]; then
    git "$@" >$path_to_out_file
else
    git "$@" >$path_to_out_file <"$path_to_tmp/in_$execId"
fi

touch "$path_to_tmp/lock_$execId"

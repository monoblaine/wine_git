#!/bin/bash

path_to_wine_git_folder="/home/serhan/Shared/wine_git"
path_to_tmp="$path_to_wine_git_folder/tmp"

execId=$1
path_to_out_file="$path_to_tmp/out_$execId"
shift
is_input_redirected=$1
shift
maybe_command=$1

if [[ "$maybe_command" == "commit" ]]; then
    shift
    f_switch=$1
    shift
    path_to_commit_msg=${1//Z:\//\/}
    path_to_commit_msg=${path_to_commit_msg// /\\ }
    git commit $f_switch "$path_to_commit_msg" >$path_to_out_file
else
    if [[ "$is_input_redirected" == "0" ]]; then
        git "$@" >$path_to_out_file
    else
        git "$@" >$path_to_out_file <"$path_to_tmp/in_$execId"
    fi
fi

touch "$path_to_tmp/lock_$execId"

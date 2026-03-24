#!/usr/bin/env python3
import re

items_blacklist = [
    "magicwand.*",
    "timeswitch.*",
    "textureflipper.*",
    "npcguidestick.*",
    "stackrandomizer.*",
    "lootrandomize.*",
    "something.*",

    "schematic.*",
    "locatormap.*",
    "letter.*",
    "envelope.*",

    "creature.*",
    "butterfly.*",
    "boss.*",

    "jonasframes.*",
    "jonasparts.*",
    "eidolongearbox.*",
    "tobtlocatorpart.*",

    "workitem.*",
    "clayworkitem.*",

    "boatseat.*"
]
blocks_blacklist = []

def compile_regexes():
    """Compiles the regexes in the blacklists for faster matching (not fully necessary)"""
    blacklists = [items_blacklist, blocks_blacklist]
    for bl in blacklists:
        for i in range(len(bl)):
            bl[i] = re.compile(bl[i])
compile_regexes()



def filter(string, item=True):
    """Returns True if the string is not blacklisted, False otherwise"""
    bl = items_blacklist if item else blocks_blacklist
    for pattern in bl:
        if re.match(pattern, string):
            return False
    return True

def structurize(ls):
    """Takes a list as input and returns a structurized dictionary for variants"""
    ret = {}
    for item in ls:
        pre = item.split("-")[0]
        post = '-'.join(item.split("-")[1:])
        ret[pre] = ret.get(pre, []) + [post]
    return ret

def get_counts(dic):
    """Converts a dict of lists to a new dict of counts"""
    counts = {}
    for k, v in dic.items():
        counts[k] = len(v)
    return counts

def simple_debug_counts(filename="all-item-codes"):
    """A simple function to get counts for a file without the filtering and structurizing"""
    with open(filename, "r") as f:
        item_codes = f.read().splitlines()
        item_codes = [code for code in item_codes if filter(code, item=True)]
        item_codes = structurize(item_codes)
        item_codes = get_counts(item_codes)
        print(item_codes)

if __name__ == "__main__":
    item_codes = {}
    block_codes = {}

    # simple_debug_counts("all-item-codes")
    # simple_debug_counts("all-block-codes")

    # TODO: build out the actual patch with structurized item list


#!/usr/bin/env python3
import re
from jinja2 import Template

# type is "block" or "item"
# code is the code of the block or item, with "game:" prefix ("stone-limestone")
# chance is avg percentage in float form, leaving var: 0 for now.
PATCH_FORMAT = Template("""
[
  {
    "file": "game:blocktypes/wood/pan",
    "op": "replace",
    "path": "/attributes/panningDrops/@(sand|gravel|sandwavy)-.*",
    "value": [
    {% for item in drops %}
      {
        "type": "{{item[0]}}",
        "code": "game:{{item[1]}}",
        "chance": { "avg": {{item[2]}}, "var": 0 }
      }
    {% if not loop.last %},{% endif %}
    {% endfor %}
    ]
  }
]
""")

# from jinja2 import Template

# template_string = """
# My favorite fruits are:
# {% for fruit in fruits %}
# - {{ fruit }}
# {% endfor %}
# """
# data = {"fruits": ["apple", "banana", "cherry"]}
# output = Template(template_string).render(**data)
# print(output.strip())



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

def get_filtered_list(filename="all-item-codes", item=True):
    """Gets the filtered list of either items or blocks from a file"""
    with open(filename, "r") as f:
        item_codes = f.read().splitlines()
        item_codes = [code for code in item_codes if filter(code, item=item)]
    return item_codes

def simple_debug_counts(filtered_list):
    """A simple function to get counts for a file without the filtering and structurizing"""
    item_codes = structurize(filtered_list)
    item_codes = get_counts(item_codes)
    print(item_codes)

if __name__ == "__main__":
    item_codes = {}
    block_codes = {}

    item_list = get_filtered_list("all-item-codes", item=True)
    block_list = get_filtered_list("all-block-codes", item=False)

    drops = []

    # simple_debug_counts(item_list)
    # simple_debug_counts(block_list)

    # TODO: build out the actual patch with structurized item list

    print(PATCH_FORMAT.render(drops=[
        ("item", "stone-limestone", 0.30),
        ("block", "metal-parts", 0.70)
    ]))


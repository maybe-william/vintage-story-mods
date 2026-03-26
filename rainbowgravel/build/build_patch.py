#!/usr/bin/env python3
import sys
import re
from jinja2 import Template
from collections import deque

# type is "block" or "item"
# code is the code of the block or item, with "game:" prefix ("stone-limestone")
# chance is avg percentage in float form, leaving var: 0 for now.
PATCH_FORMAT = Template("""
[
  {
    "file": "game:blocktypes/wood/pan",
    "op": "move",
    "fromPath": "/attributes/panningDrops/@(sand|gravel|sandwavy)-.*",
    "path": "/attributes/tempPanningDrops"
  },
  {
    "file": "game:blocktypes/wood/pan",
    "op": "add",
    "path": "/attributes/panningDrops/@(.*gravel.*rainbow.*)",
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
  },
  {
    "file": "game:blocktypes/wood/pan",
    "op": "move",
    "fromPath": "/attributes/tempPanningDrops",
    "path": "/attributes/panningDrops/@(sand|gravel|sandwavy)-.*"
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

    "boatseat.*",

    "shield-.*",
    "ontree-.*"
]
blocks_blacklist = [
    "creative.*",
    "slopetestobj",
    "vertexeater",

    ".*commandblock",
    "tickerblock",
    "conditionalblock",
    "randomizer",
    "meta",
    "worldgenhook",

    "multiblock.*",
    "mpmultiblockwood",
    "pulverizerframe",
    "mppulverizertop",
    "helvehammerbase"

    "jonas.*",
    "tobtlocator",
    "resonator",
    "riftward"

    "dev.*",
    "dpanel",

    "chiseledblock.*",
    "microblock.*",
    "overlay.*",
    "transition.*"
]

#Whitelists for debugging purposes:
items_whitelist = [
]
blocks_whitelist = [
]

def compile_regexes():
    """Compiles the regexes in the blacklists for faster matching (not fully necessary)"""
    lists = [items_blacklist, blocks_blacklist, items_whitelist, blocks_whitelist]
    for l in lists:
        for i in range(len(l)):
            l[i] = re.compile(l[i])
compile_regexes()



def filter(string, item=True, whitelist=False):
    """Returns True if the string is not blacklisted, False otherwise.
    Whitelist is for debugging and testing purposes."""
    if whitelist:
        wl = items_whitelist if item else blocks_whitelist
        for pattern in wl:
            if re.match(pattern, string):
                print(f"INFO: Whitelisting: Pattern {pattern} matched string {string}")
                return True
        return False

    bl = items_blacklist if item else blocks_blacklist
    for pattern in bl:
        if re.match(pattern, string):
            return False
    return True

#! AI Generated
def structurize(codes):
    """
    Build a nested tree.

    Example for:
        stone-limestone
        stone-granite
        gear-temporal

    Produces:
    {
        "stone": {
            "_terminal": False,
            "_children": {
                "limestone": {"_terminal": True, "_children": {}},
                "granite": {"_terminal": True, "_children": {}}
            }
        },
        "gear": {
            "_terminal": False,
            "_children": {
                "temporal": {"_terminal": True, "_children": {}}
            }
        }
    }
    """
    root = {}

    for code in codes:
        parts = code.split("-")
        current_level = root

        for i, part in enumerate(parts):
            if part not in current_level:
                current_level[part] = {
                    "_terminal": False,
                    "_children": {}
                }

            node = current_level[part]

            if i == len(parts) - 1:
                node["_terminal"] = True

            current_level = node["_children"]

    return root

def get_counts(dic):
    """Converts a dict of lists to a new dict of counts"""
    counts = {}
    for k, v in dic.items():
        counts[k] = len(v)
    return counts

def get_filtered_list(filename="all-item-codes", item=True, whitelist=False):
    """Gets the filtered list of either items or blocks from a file"""
    with open(filename, "r") as f:
        item_codes = f.read().splitlines()
        item_codes = [code for code in item_codes if filter(code, item=item, whitelist=whitelist)]
    return item_codes

def simple_debug_counts(filtered_list):
    """A simple function to get counts for a file without the filtering and structurizing"""
    item_codes = structurize(filtered_list)
    item_codes = get_counts(item_codes)
    print(item_codes)

#! AI Generated
def traverse(tree):
    """
    Breadth-first traversal.

    Probability model:
    - Each node splits its incoming probability equally among:
      - one 'stop here' option if the node is terminal
      - each child continuation

    Returns:
        list of (code, probability)
    """
    finalized = []
    queue = deque()

    if not tree:
        return finalized

    root_count = len(tree)
    root_prob = 1.0 / root_count

    for part, node in tree.items():
        queue.append((part, node, root_prob))

    while queue:
        code_so_far, node, prob_so_far = queue.popleft()

        children = node["_children"]
        is_terminal = node["_terminal"]

        num_choices = len(children) + (1 if is_terminal else 0)

        if num_choices == 0:
            continue  # should never happen in a valid tree

        split_prob = prob_so_far / num_choices

        if is_terminal:
            finalized.append((code_so_far, split_prob))

        for child_part, child_node in children.items():
            new_code = f"{code_so_far}-{child_part}"
            queue.append((new_code, child_node, split_prob))

    return finalized
            

if __name__ == "__main__":
    patch_file = sys.argv[1] if len(sys.argv) > 1 else None
    if patch_file is None:
        print("Usage: build_patch.py [patch_file]")
        print("No patch file provided. Aborting.")
        exit(1)

    item_codes = {}
    block_codes = {}

    item_list = get_filtered_list("all-item-codes", item=True)
    block_list = get_filtered_list("all-block-codes", item=False)

    drops = []

    # simple_debug_counts(item_list)
    # simple_debug_counts(block_list)

    # print(PATCH_FORMAT.render(drops=[
    #     ("item", "stone-limestone", 0.30),
    #     ("block", "metal-parts", 0.70)
    # ]))

    # TODO: build out the actual patch with structurized item list

    # print(traverse(structurize(["stone", "stone-limestone", "stone-granite", "gear-temporal", "stone-limestone-stone-slate", "gear-temporal-rusty"])))

    # Halving probabilities because both blocks and items would 
    # add up to 2.0 together
    items = traverse(structurize(item_list))
    for item in items:
        drops.append(("item", item[0], item[1] / 2))

    blocks = traverse(structurize(block_list))
    for block in blocks:
        drops.append(("block", block[0], block[1] / 2))
    
    # print(PATCH_FORMAT.render(drops=drops))

    with open(patch_file, "w") as f:
        f.write(PATCH_FORMAT.render(drops=drops))


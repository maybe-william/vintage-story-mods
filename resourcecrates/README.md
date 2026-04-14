# Resource Crates

A configurable passive resource-generation crate for **Vintage Story 1.21.6**.

This mod adds a special crate block that can be upgraded and configured to generate a specific item over time. It behaves like a hybrid between a machine and a container, with a simple one-slot output inventory.

---

## Overview

A **Resource Crate**:

- Generates a **single configured item type** over time  
- Has a **tier system** that affects generation speed  
- Stores generated output in a **1-slot inventory**  
- Can be configured and upgraded via **shift + right-click interactions**  
- Accumulates progress while loaded, even if the output slot is full  

This is designed as a simple, scalable passive production system.

---

## Core Mechanics

### 1. Crate Tier

Each crate has a **tier** (starting at 0).

You can upgrade the crate by **shift + right-clicking with an upgrade item**:

- Upgrade items map directly to specific tiers
- Upgrades only apply if they increase the current tier
- On upgrade (v0 behavior):
  - Any existing output in the crate **is cleared (deleted)**
  - (Future: items may be popped out instead)

---

### 2. Assigning a Target Item

Each crate can generate **one specific item type**.

To assign the target:

- **Shift + right-click the crate with a valid item**
- The item must be listed in the config as generatable
- The item is **not consumed**

You can also replace the target later using the same action.

---

### 3. Inventory Interaction

- **Right-click (normal)** → Opens the crate GUI  
- The GUI is a **1-slot inventory**
- This slot is used for **output only**

---

### 4. Item Generation

Once a target item is set, the crate begins generating items over time.

- Time is measured in **in-game minutes** (not real-world time)
- Progress accumulates continuously while:
  - the block exists
  - the chunk is loaded

When enough progress is accumulated:

- Items are produced and placed into the output slot
- If the slot is full:
  - progress continues accumulating (up to a cap)
  - items are not inserted until space is available

---

### 5. Progress Storage Limits (v0)

- Progress accumulates even if output is blocked
- Progress is capped at:

  **1,000,000,000 seconds worth of generation**

- No offline progression:
  - **Generation does NOT occur while the chunk is unloaded**

---

## Generation Speed

Generation speed depends on:

- Crate tier
- Target item tier (from config)

### Rules

- Same tier → base rate  
- Lower-tier item → faster  
- Higher-tier item → slower (exponentially)

---

## Default Configuration

### Target Items by Tier

| Tier | Item |
|------|------|
| 0 | Logs (`game:log-grown-oak-ud`) |
| 1 | Planks (`game:plank-oak`) |
| 2 | Copper nuggets |
| 3 | Cassiterite nuggets |
| 4 | Iron bloom |
| 5 | Blister steel ingot |

---

### Upgrade Items

| Item | Sets Crate Tier To |
|------|--------------------|
| Copper ingot | 1 |
| Tin bronze ingot | 2 |
| Iron ingot | 3 |
| Steel ingot | 4 |
| Ilmenite | 5 |

---

### Timing Defaults

- Base rate: **180 in-game minutes per item**
- Lower-tier factor: **10**
- Higher-tier factor: **100**

---

### Example

A tier 2 crate:

- Generating tier 2 item → 180 minutes/item  
- Generating tier 1 item → 18 minutes/item  
- Generating tier 0 item → 1.8 minutes/item  
- Generating tier 3 item → 18,000 minutes/item  

---

## Player Controls Summary

| Action | Result |
|--------|--------|
| Right-click | Open crate inventory |
| Shift + Right-click (upgrade item) | Upgrade crate tier |
| Shift + Right-click (valid item) | Set or replace generation target |

---

## State Persistence

Each crate stores:

- Tier
- Target item
- Generation progress

This data is preserved when:

- The crate is broken and picked up  
- The crate is placed again  

---

## Limitations (v0)

- No generation while chunk is unloaded  
- Output slot is only 1 item stack  
- Upgrading deletes existing output (temporary behavior)  
- No automation rules (e.g., chutes) guaranteed yet  
- No UI for viewing progress or rate  

---

## Design Intent

This mod is intended to provide:

- A **simple passive resource system**
- Strong **tier-based progression**
- Configurable generation rules via JSON
- A foundation for future automation and balancing

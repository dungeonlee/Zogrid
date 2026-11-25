# Zogrid

**Workstation · Server Room · Seat Layout Manager**

Open source · Free · Commercial use allowed · No resale · Fully offline · 7 interface languages

**Other languages:**
[简体中文](README.md) · [繁體中文](README.zh-Hant.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Deutsch](README.de.md)

---

Excel remembers the data but not *who sits by the window on the third row*.
Paper labels mark the seats but have to be torn off every time someone moves.

Zogrid merges the two: the screen shows exactly what is on the wall, and every seat carries its person and its machines.
A few hundred machines in a lab, a whole office floor, or an internet café — one map covers it all.

![Room layout](截图/room.png)

## Who it is for

**School computer labs** — dozens to hundreds of teaching PCs, seating arranged by class. Which model sits where, which IP, which machine is broken and free: all visible at a glance.

**Office floors** — colour-coded departments, leader seats spanning two cells, seat numbers that match reality. Changing a person or a machine means editing one cell, not redrawing the plan. Monitor, IP, MAC and port label all hang off the seat, so the next admin does not have to ask anyone.

**Internet cafés / on-site IT** — seat numbers, machine models, LAN/WAN IPs and MACs on one screen. Idle seats and remaining machines are counted live at the bottom.

---

## Core features

### 1. Draw the room however it is

The screenshot above is the Room properties panel — name, columns and rows, floor colour, grid colour, all in one place.

- Unlimited rooms, named as you like: `3F-Lab A`, `2F-Finance`, `Café ground floor`. One file manages a whole building.
- Set the number of columns and rows; pick floor and grid colours. Light and dark themes both look right.
- **Print room layout**: uses the system printer, black-and-white line art, A4 portrait, auto-scaled and centred, with a model summary in the header. Print it and stick it on the wall.
- Rooms can be locked: the layout is frozen but seat contents remain editable — handy for handovers and audits.

### 2. Build your own seat blocks

![Block library](截图/library.png)

- Built-in 2-seat, 3-seat and "with leader seat" blocks, ready to drag out.
- Design your own: any rows × columns, one cell = one seat. Tick "with leader seat" and the leader seat spans two cells (vertical or horizontal).
- Give each block its own colour to separate departments or zones at a glance.
- Drag entries up and down in the library to reorder them.

### 3. Drop it wherever it belongs

![Drag and drop](截图/layout.png)

- Drag a block from the library into the room; it snaps to the grid. No coordinates to type.
- Move blocks freely: against the wall, with an aisle in the middle, L-shaped, island style.
- Right-click to **pin a block** so a finished layout cannot be dragged by accident.

### 4. Hold Ctrl and drag a seat: people and machines move, the port label stays on the wall

The part that usually goes wrong when rearranging seats is not moving people — it is **whether the port label follows the person**.

Zogrid's rule: **hold Ctrl and drag one seat onto another; the two seats swap their "person + equipment" as a whole, while the port label stays nailed to the wall.**

| Moves with the person | Stays in place (belongs to the wall) |
| --- | --- |
| Name, employee ID, PC model, monitor model, IP, MAC, note | Port label |

Why it is split this way: the port label is where the cable physically comes out of the wall. People change, machines change, the socket does not move. Swapping seats needs no re-patching at the rack and no editing of the ledger — the ledger and the wall always agree.

Details:
- Leader seats, ordinary seats and empty seats can all be dragged onto each other; drag back if you make a mistake.
- Ctrl-swap **still works when the room is locked** (the lock freezes the layout, not staff changes).
- After the swap the status bar says "name/model/monitor/ID/IP/MAC moved along, port label stayed", so nothing is a guess.

### 5. One record card per seat

![Seat details](截图/cell-edit.png)

- Name, port label, PC model (up to 4 per seat), monitor model (often used for an extension or phone number), employee ID.
- Tick **Dual network** to record both LAN and WAN lines on one seat, each with its own IP and MAC.
- Machine details (IP / MAC / note) have Edit and Read-only modes — switch to read-only once the data is verified.
- The seat itself shows only the essentials; everything else lives in the card.

### 6. Find anything

![Search highlight](截图/search.png)

- Search name, IP, MAC, PC model, employee ID or port label as you type.
- Matching seats flash pink so you can spot them instantly.
- Search, model and idle highlights are **mutually exclusive** — turning one on clears the other two.
- Esc clears the search.

### 7. See the whole picture

![Machine stats](截图/stats.png)

- Live summary at the bottom: `4 models · 9 machines · 3 idle`.
- One card per model with its own total and idle count.
- **Click a model** to highlight all of its seats; **click idle** to highlight only seats with a machine and no person.
- Each model can have its own text colour, matching the text inside the seats.

### 8. Fill a whole room from a spreadsheet

![Data menu](截图/template.png)

- **Data → Download blank template**: 8 standard columns — Name / Employee ID / LAN-WAN / MAC / IP / Note (model) / Port Label / Monitor Model.
- In a new room you only type the names; everything else comes from the sheet.
- **Import workbook → Sync to seats** matches by name and fills IP, MAC, model, port label and monitor in seconds.
- Empty cells never overwrite data already verified in the room.
- **Export people table** writes the room back out in the same layout, so you can edit and re-import.

---

## Also worth knowing

- **7 interface languages**: 简体中文, 繁體中文, 日本語, 한국어, English, Français, Deutsch. Switch from "Language ▾" in the toolbar; the app restarts and applies it. Wording follows each language's own UI conventions rather than literal translation.
- **Column names follow the language**: Chinese UI produces Chinese headers, other languages produce English headers — and **both can be imported**, so mixed teams do not hit column mismatch.
- **Fully offline**: no network, no login, no uploads, no telemetry.
- **Single green executable**: one exe, run it from a USB stick if you like.
- **One data file**: `Documents\Zogrid\zogrid.json` — back up or migrate by copying that one file.
- **Dark UI** that is easy on the eyes under machine-room lighting.
- **First-run guidance**: with no config it opens the library and room properties and pulses the "New room" button.

## Three steps to start

1. **Create a room** — F2, name it, set columns and rows.
2. **Place seats** — F1, drag blocks in, arrange them like the real floor, type the names.
3. **Load the data** — Data → Download blank template, fill it in, Import → Sync to seats.

## Shortcuts

| Key | Action |
| --- | --- |
| F1 | Show/hide block library |
| F2 | Show/hide room properties |
| F3 | Show/hide machine stats |
| F4 | Show/hide seat details |
| F5 | Fit the whole room |
| Wheel | Zoom |
| Middle-drag | Pan |
| Arrow keys | Pan by 60 px |
| **Ctrl + drag seat** | Swap two seats' contents |
| Delete | Delete selected block |
| Esc | Clear search / close About |

## Requirements

- Windows 7 or later (Windows 10 / 11 out of the box)
- .NET Framework 4.0+ (bundled with Windows 10/11)
- No installation, no network, no administrator rights

## License

**Copyright © 2026 Michael. All rights reserved by the author.**

- **Open source and free**: the source is public and may be viewed, built and used.
- **Commercial use allowed**: schools, companies and internet cafés may use it free of charge.
- **Resale forbidden**: no fees, sales, bundling or any form of monetisation of this software or a modified version.
- The same restriction applies to later revisions, forks, and Mac or any other platform builds.
- **No warranty**: provided as is; the author is not liable for any loss caused by its use.

Full terms in [LICENSE.txt](LICENSE.txt).

---

Author: Michael · Zogrid · 2026

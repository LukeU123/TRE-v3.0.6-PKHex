# PKHeX-TRE

A fork of [PKHeX](https://github.com/kwsch/PKHeX) (26.10.18) that opens saves from the English translation of
**Pokémon Team Rocket Edition v3.0.6** (a pokeemerald-expansion hack). Normal PKHeX can't read these saves:
the hack uses its own save layout, Pokémon format, species list and item numbering.

## What works (milestone 1)

- Opens the `.sav` (128 KB + RTC footer) and saves it back with valid checksums.
- Party and all 24 boxes: view and edit species (including the hack's own Pokémon), level, nickname,
  moves, held item, ability (all three slots, including hidden), nature, IVs/EVs, shiny, ball, OT, friendship.
- Box names.
- Item and move lists use the translated English names from the ROM.
- Legality checking is switched off for these Pokémon (PKHeX's rules don't apply to the hack).

## Not yet

- Trainer info (money, badges) and the bag: those buttons are hidden for now.
- Met locations show Emerald place names, not the hack's.
- Form names follow PKHeX's lists, which may not match the hack's form order. Some forms may show the wrong sprite.
- The hack's own Pokémon show a "?" sprite.
- Showdown export leaves out the held item.

**Always work on a copy of your save.**

## Building (Visual Studio 2026 / 18.x, ".NET desktop development" workload)

1. Open `PKHeX-TRE.sln`.
2. Set the build to **Release** and make **PKHeX.WinForms** the startup project. (Right-click it, then choose *Set as Startup Project*.)
3. Choose *Build → Build Solution*. The first build downloads NuGet packages, so you need to be online.
4. The program ends up in `PKHeX.WinForms\bin\Release\net10.0-windows\win-x64\PKHeX-TRE.exe`.

## What changed from upstream

- New: `PKHeX.Core/Saves/TRE/`: `SAV3TRE` (save), `PK3TRE` (Pokémon), `PersonalInfoTRE`, `TREData`, and `tre_data.json`
  (species stats, item/move/ability names, generated from the patched English ROM by `tools-tre/gen_tre_data.py`).
- New: `PKHeX.WinForms/Controls/PKM Editor/EditTRE.cs`, which loads and saves TRE Pokémon in the editor.
- Small hooks in `SaveUtil.cs`, `GameStrings.cs`, `FilteredGameDataSource.cs`, `EntitySummary.cs`,
  `LegalityAnalysis.cs`, `PKMEditor.cs` and `SAVEditor.cs`. Search for `TRE` to find them.

Licensed under GPLv3, the same as PKHeX (see `LICENSE`).

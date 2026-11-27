# Changelog

## v0.2 (27 Nov 2026)

Based on PKHeX 26.10.18. Every feature was tested in the game.

- Opens and saves Team Rocket Edition v3.0.6 (English) saves, with valid checksums.
- Party and all 24 boxes (Box tab), plus box names.
- Full Pokémon editing: species (including the hack's own Pokémon), level, nickname, nature, IVs/EVs, friendship,
  shiny, ball, OT, all three ability slots and held items.
- Moves are checked against the hack's learnsets (level-up, TM/tutor, egg and pre-evolutions), and learnable moves are highlighted.
- Met locations use the hack's 359 map names.
- In-game test fixes: EXP no longer corrupts when the Pokémon is saved (the hack keeps EXP in 24 bits),
  nicknames are limited to 10 characters, and the ball list includes the hack's Park Ball slot, so Cherish Ball saves as Cherish.
- Nature changes no longer reroll the PID, which gave random natures (the hack stores nature as a modifier).
  Changing the ability no longer rerolls the PID either.
- **Trainer Info**: name, TID/SID, gender, money and play time.
- **Items**: the hack's 8 bag pockets plus the PC, with sizes and item lists taken from the ROM. TMs and HMs show their move (e.g. "TM235 Crabhammer").
- Trainer Info changes now save (the editor's copy-back skipped the save's block buffers).
- Renamed to PKHeX-TRE (exe, window title, solution). The upstream update prompt is turned off.

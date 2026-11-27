# Changelog

## v0.1.0 (27 Nov 2026)

First release, based on PKHeX 26.10.18.

- Opens and saves Team Rocket Edition v3.0.6 (English) saves, with valid checksums.
- Party and all 24 boxes, plus box names.
- Full Pokémon editing: species (including the hack's own Pokémon), level, nickname, nature, IVs/EVs, friendship,
  shiny, ball, OT, all three ability slots and held items.
- Moves are checked against the hack's learnsets (level-up, TM/tutor, egg and pre-evolutions), and learnable moves are highlighted.
- Met locations use the hack's 359 map names.
- In-game test fixes: EXP no longer corrupts when the Pokémon is saved (the hack keeps EXP in 24 bits),
  nicknames are limited to 10 characters, and the ball list includes the hack's Park Ball slot, so Cherish Ball saves as Cherish.
- Nature changes no longer reroll the PID, which gave random natures (the hack stores nature as a modifier).
  Changing the ability no longer rerolls the PID either.
- Renamed to PKHeX-TRE (exe, window title, solution). The upstream update prompt is turned off.

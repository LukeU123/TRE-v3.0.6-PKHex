using System;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// Team Rocket Edition (pokeemerald-expansion) Pokémon: Gen 3 layout, but with three ability slots.
/// </summary>
public partial class PKMEditor
{
    private void PopulateFieldsTRE()
    {
        if (Entity is not PK3TRE pk)
            throw new FormatException(nameof(Entity));

        L_ExtraBytes.Visible = FLP_ExtraBytes.Visible = false; // no spare bytes in this format
        LoadMisc3(pk);
        LoadMisc1(pk);
        LoadMisc2(pk);

        // Ability slot 0/1/2 (2 = hidden ability)
        var slot = pk.AbilityNumber switch { 4 => 2, 2 => 1, _ => 0 };
        CB_Ability.SelectedIndex = Math.Min(slot, CB_Ability.Items.Count - 1);

        LoadPartyStats(pk);
        UpdateStats();
    }

    private PK3TRE PrepareTRE()
    {
        if (Entity is not PK3TRE pk)
            throw new FormatException(nameof(Entity));

        SaveMisc3(pk);
        SaveMisc2(pk);
        SaveMisc1(pk);

        pk.AbilityNumber = 1 << Math.Clamp(CB_Ability.SelectedIndex, 0, 2);

        SavePartyStats(pk);
        pk.FixMoves();
        pk.RefreshChecksum();
        return pk;
    }
}

using System;
using System.Text.Json;

namespace PKHeX.Core;

/// <summary>
/// Species data for one entry of Team Rocket Edition's species table (base stats, types, abilities...).
/// </summary>
public sealed class PersonalInfoTRE : PersonalInfo
{
    private readonly int[] Abil = new int[3];

    public PersonalInfoTRE(JsonElement s)
    {
        var stats = s.GetProperty("stats");
        HP = stats[0].GetInt32(); ATK = stats[1].GetInt32(); DEF = stats[2].GetInt32();
        SPE = stats[3].GetInt32(); SPA = stats[4].GetInt32(); SPD = stats[5].GetInt32();
        var types = s.GetProperty("types");
        Type1 = TREData.ConvertType(types[0].GetInt32());
        Type2 = TREData.ConvertType(types[1].GetInt32());
        CatchRate = (byte)s.GetProperty("catch").GetInt32();
        BaseEXP = s.GetProperty("exp").GetInt32();
        var ev = s.GetProperty("ev");
        EV_HP = ev[0].GetInt32(); EV_ATK = ev[1].GetInt32(); EV_DEF = ev[2].GetInt32();
        EV_SPE = ev[3].GetInt32(); EV_SPA = ev[4].GetInt32(); EV_SPD = ev[5].GetInt32();
        Gender = (byte)s.GetProperty("gender").GetInt32();
        HatchCycles = (byte)s.GetProperty("egg").GetInt32();
        BaseFriendship = (byte)s.GetProperty("fr").GetInt32();
        EXPGrowth = (byte)Math.Min(5, s.GetProperty("growth").GetInt32());
        var eggs = s.GetProperty("eggs");
        EggGroup1 = eggs[0].GetInt32(); EggGroup2 = eggs[1].GetInt32();
        var a = s.GetProperty("abil");
        for (int i = 0; i < 3; i++)
            Abil[i] = a[i].GetInt32();
    }

    public override byte[] Write() => [];
    public override int HP { get; set; }
    public override int ATK { get; set; }
    public override int DEF { get; set; }
    public override int SPE { get; set; }
    public override int SPA { get; set; }
    public override int SPD { get; set; }
    public override int EV_HP { get; set; }
    public override int EV_ATK { get; set; }
    public override int EV_DEF { get; set; }
    public override int EV_SPE { get; set; }
    public override int EV_SPA { get; set; }
    public override int EV_SPD { get; set; }
    public override byte Type1 { get; set; }
    public override byte Type2 { get; set; }
    public override int EggGroup1 { get; set; }
    public override int EggGroup2 { get; set; }
    public override byte CatchRate { get; set; }
    public override byte Gender { get; set; }
    public override byte HatchCycles { get; set; }
    public override byte BaseFriendship { get; set; }
    public override byte EXPGrowth { get; set; }
    public override int EscapeRate { get; set; }
    public override int BaseEXP { get; set; }
    public override int Color { get; set; }

    private byte _formCount = 1;
    public override byte FormCount { get => _formCount; set => _formCount = value; }

    /// <summary> Abilities: slot 0, slot 1, hidden. A slot with ability 0 falls back to slot 0. </summary>
    public override int AbilityCount => 3;
    public override int GetAbilityAtIndex(int abilityIndex)
    {
        if ((uint)abilityIndex >= 3)
            throw new ArgumentOutOfRangeException(nameof(abilityIndex));
        var value = Abil[abilityIndex];
        return value != 0 ? value : Abil[0];
    }

    public override int GetIndexOfAbility(int abilityID)
    {
        for (int i = 0; i < 3; i++)
        {
            if (Abil[i] == abilityID)
                return i;
        }
        return -1;
    }
}

/// <summary>
/// Personal table wrapper for Team Rocket Edition: looks entries up by PKHeX (Species, Form).
/// </summary>
public sealed class PersonalTableTRE : IPersonalTable, IPersonalTable<PersonalInfoTRE>
{
    public static readonly PersonalTableTRE Instance = new();

    public ushort MaxSpeciesID => TREData.MaxSpecies;
    public int Count => TREData.Raw.Length;

    public PersonalInfoTRE this[int index] => TREData.GetPersonal((ushort)index, 0);
    public PersonalInfoTRE this[ushort species, byte form] => TREData.GetPersonal(species, form);
    public PersonalInfoTRE GetFormEntry(ushort species, byte form) => TREData.GetPersonal(species, form);
    public int GetFormIndex(ushort species, byte form) => TREData.GetRaw(species, form);

    PersonalInfo IPersonalTable.this[int index] => this[index];
    PersonalInfo IPersonalTable.this[ushort species, byte form] => this[species, form];
    PersonalInfo IPersonalTable.GetFormEntry(ushort species, byte form) => GetFormEntry(species, form);

    public bool IsSpeciesInGame(ushort species) => TREData.GetRaw(species, 0) != 0;
    public bool IsPresentInGame(ushort species, byte form) => TREData.GetRaw(species, form) != 0;
}

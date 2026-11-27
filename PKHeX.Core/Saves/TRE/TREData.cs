using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PKHeX.Core;

/// <summary>
/// Game data for Pokémon Team Rocket Edition (pokeemerald-expansion based romhack), read from the English-patched ROM.
/// </summary>
/// <remarks>
/// The hack numbers its species in its own order: national 1-905, then regional/alternate forms, then Gen 9, then its own new Pokémon.
/// This class maps those raw IDs to PKHeX-style (Species, Form) pairs and back.
/// Species beyond the national dex (the hack's own Pokémon) are given IDs from <see cref="FirstCustomSpecies"/> upward.
/// </remarks>
public static class TREData
{
    public const ushort FirstCustomSpecies = 1026;
    public const int MaxRawSpecies = 2047; // 11-bit field
    public const int MaxRawMove = 2047;    // 11-bit field
    public const int MaxRawItem = 1023;    // 10-bit held item field

    public sealed class SpeciesEntry
    {
        public int Raw;
        public string Name = string.Empty;
        public ushort Species; // PKHeX species (national, or custom >= FirstCustomSpecies)
        public byte Form;
        public PersonalInfoTRE Personal = null!;
        public ushort[] LevelUp = [];  // hack move ID, level, hack move ID, level...
        public ushort[] Teach = [];    // TMs and tutors (hack move IDs)
        public ushort[] Egg = [];      // egg moves (hack move IDs)
        public ushort[] EvolvesInto = []; // raw species IDs
        public int PreEvolution = -1;  // raw species ID, -1 if none
    }

    public static readonly SpeciesEntry[] Raw;
    public static readonly string[] Items;
    public static readonly string[] Moves;
    public static readonly string[] Abilities;
    /// <summary>Names for custom species, indexed by (species - <see cref="FirstCustomSpecies"/>).</summary>
    public static readonly string[] CustomSpeciesNames;
    public static readonly ushort MaxSpecies;

    private static readonly Dictionary<int, int> SpeciesFormToRaw = [];
    private static readonly Dictionary<ushort, byte> FormCounts = [];

    static TREData()
    {
        using var stream = typeof(TREData).Assembly.GetManifestResourceStream("PKHeX.Core.Saves.TRE.tre_data.json")
                           ?? throw new FileNotFoundException("tre_data.json resource missing");
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        Items = ReadStrings(root.GetProperty("items"));
        Moves = ReadStrings(root.GetProperty("moves"));
        Abilities = ReadStrings(root.GetProperty("abilities"));

        var species = root.GetProperty("species");
        Raw = new SpeciesEntry[species.GetArrayLength()];
        var custom = new List<string>();
        var customByName = new Dictionary<string, ushort>();
        ushort max = 0;
        int i = 0;
        foreach (var s in species.EnumerateArray())
        {
            var e = new SpeciesEntry { Raw = i };
            e.Name = s.GetProperty("name").GetString() ?? string.Empty;
            int nat = s.GetProperty("nat").GetInt32();
            e.Personal = new PersonalInfoTRE(s);
            e.LevelUp = ReadU16(s, "lv");
            e.Teach = ReadU16(s, "tm");
            e.Egg = ReadU16(s, "eggm");
            e.EvolvesInto = ReadU16(s, "evo");

            if (i == 0 || string.IsNullOrEmpty(e.Name))
            {
                e.Species = 0; // empty / unused slot
            }
            else if (nat is >= 1 and <= 1025)
            {
                e.Species = (ushort)nat;
            }
            else
            {
                // The hack's own Pokémon. Forms of the same custom species share a name.
                if (!customByName.TryGetValue(e.Name, out var id))
                {
                    id = (ushort)(FirstCustomSpecies + custom.Count);
                    custom.Add(e.Name);
                    customByName[e.Name] = id;
                }
                e.Species = id;
            }

            if (e.Species != 0)
            {
                FormCounts.TryGetValue(e.Species, out var form);
                e.Form = form;
                FormCounts[e.Species] = (byte)(form + 1);
                SpeciesFormToRaw[(e.Species << 8) | e.Form] = i;
                if (e.Species > max)
                    max = e.Species;
            }
            Raw[i++] = e;
        }

        // Personal entries know how many forms their species has.
        foreach (var e in Raw)
        {
            if (e.Species != 0)
                e.Personal.FormCount = FormCounts[e.Species];
        }

        foreach (var e in Raw)
        {
            foreach (var target in e.EvolvesInto)
            {
                if (target < Raw.Length && target != e.Raw && Raw[target].PreEvolution < 0)
                    Raw[target].PreEvolution = e.Raw;
            }
        }

        Locations = root.TryGetProperty("locations", out var locs) ? ReadStrings(locs) : [];

        CustomSpeciesNames = [.. custom];
        MaxSpecies = max;

        // The hack has no Z-Moves, so its move IDs drift from the official ones after #621.
        // Map by English name so the rest of PKHeX (PP, types, names) sees official IDs.
        var official = Util.GetStringList("moves", "en");
        var byName = new Dictionary<string, ushort>();
        for (ushort m = 1; m < official.Length; m++)
            byName.TryAdd(NormalizeName(official[m]), m);
        MoveRawToOfficial = new ushort[Moves.Length];
        for (int m = 1; m < Moves.Length; m++)
        {
            if (!byName.TryGetValue(NormalizeName(Moves[m]), out var id))
                continue; // G-Max moves: nothing official to map to
            MoveRawToOfficial[m] = id;
            MoveOfficialToRaw.TryAdd(id, (ushort)m);
        }
    }

    /// <summary>
    /// Appends the hack's own species and abilities after the official ones, so name lookups by ID work.
    /// Other games never reach these IDs (their limits filter them out).
    /// </summary>
    public static void ExtendStrings(ref string[] species, ref string[] abilities)
    {
        if (species.Length == FirstCustomSpecies)
            species = [.. species, .. CustomSpeciesNames];
        if (abilities.Length < Abilities.Length)
            abilities = [.. abilities, .. Abilities.AsSpan(abilities.Length)];
    }

    private static ushort[]? _itemToOfficial;

    /// <summary> Hack item ID -> official item ID (by English name), for item icons. 0 if no match. </summary>
    public static int GetOfficialItem(int raw)
    {
        if (_itemToOfficial is null)
        {
            var official = Util.GetStringList("items", "en");
            var byName = new Dictionary<string, ushort>();
            for (ushort i = 1; i < official.Length; i++)
                byName.TryAdd(NormalizeName(official[i]), i);
            var map = new ushort[Items.Length];
            for (int i = 1; i < Items.Length; i++)
            {
                var name = Items[i].Replace("[PO][Ké][BL][OC][K]", "Pokéblock").Replace("[PK][MN]", "Pokémon");
                map[i] = byName.TryGetValue(NormalizeName(name), out var id) ? id : (ushort)0;
            }
            _itemToOfficial = map;
        }
        return (uint)raw < _itemToOfficial.Length ? _itemToOfficial[raw] : 0;
    }

    /// <summary> Hack move ID -> official move ID (0 if the hack move has no official counterpart). </summary>
    public static readonly ushort[] MoveRawToOfficial;
    private static readonly Dictionary<ushort, ushort> MoveOfficialToRaw = [];

    public static ushort GetOfficialMove(ushort raw) => raw < MoveRawToOfficial.Length ? MoveRawToOfficial[raw] : (ushort)0;
    public static ushort GetRawMove(ushort official) => MoveOfficialToRaw.TryGetValue(official, out var raw) ? raw : (ushort)0;
    public static bool IsMoveInGame(ushort official) => official == 0 || MoveOfficialToRaw.ContainsKey(official);

    private static string NormalizeName(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        int n = 0;
        foreach (var c in s)
        {
            if (c is ' ' or '-' or '\'' or '\u2019' or '.')
                continue;
            buf[n++] = char.ToLowerInvariant(c);
        }
        return new string(buf[..n]);
    }

    /// <summary> The hack's map section names, indexed by met location. </summary>
    public static readonly string[] Locations;

    private static IReadOnlyList<ComboItem>? _locationSource;

    /// <summary> Met location choices (every named map section), sorted by name. </summary>
    public static IReadOnlyList<ComboItem> LocationDataSource => _locationSource ??= BuildLocationSource();

    private static List<ComboItem> BuildLocationSource()
    {
        var list = new List<ComboItem>(Locations.Length);
        for (int i = 0; i < Locations.Length; i++)
        {
            var name = Locations[i];
            if (name.Length != 0)
                list.Add(new ComboItem(name, i));
        }
        list.Sort((a, b) => a.Text == b.Text ? a.Value.CompareTo(b.Value) : string.CompareOrdinal(a.Text, b.Text));
        return list;
    }

    public static string GetLocationName(int location) => (uint)location < Locations.Length && Locations[location].Length != 0 ? Locations[location] : $"#{location}";

    private static ushort[] ReadU16(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var arr))
            return [];
        var result = new ushort[arr.GetArrayLength()];
        int i = 0;
        foreach (var v in arr.EnumerateArray())
            result[i++] = (ushort)v.GetInt32();
        return result;
    }

    private static string[] ReadStrings(JsonElement arr)
    {
        var result = new string[arr.GetArrayLength()];
        int i = 0;
        foreach (var s in arr.EnumerateArray())
            result[i++] = s.GetString() ?? string.Empty;
        return result;
    }

    /// <summary> Raw hack species ID -> (Species, Form). </summary>
    public static (ushort Species, byte Form) GetSpeciesForm(int raw)
    {
        if ((uint)raw >= Raw.Length)
            return (0, 0);
        var e = Raw[raw];
        return (e.Species, e.Form);
    }

    /// <summary> (Species, Form) -> raw hack species ID. Falls back to form 0, then 0. </summary>
    public static int GetRaw(ushort species, byte form)
    {
        if (species == 0)
            return 0;
        if (SpeciesFormToRaw.TryGetValue((species << 8) | form, out var raw))
            return raw;
        if (SpeciesFormToRaw.TryGetValue(species << 8, out raw))
            return raw;
        return 0;
    }

    public static byte GetFormCount(ushort species) => FormCounts.TryGetValue(species, out var c) ? c : (byte)1;

    public static PersonalInfoTRE GetPersonal(ushort species, byte form)
    {
        var raw = GetRaw(species, form);
        return Raw[raw].Personal;
    }

    public static string GetSpeciesName(ushort species, IReadOnlyList<string> official)
    {
        if (species >= FirstCustomSpecies)
        {
            var idx = species - FirstCustomSpecies;
            return (uint)idx < CustomSpeciesNames.Length ? CustomSpeciesNames[idx] : $"#{species}";
        }
        return species < official.Count ? official[species] : $"#{species}";
    }

    public static string GetItemName(int item) => (uint)item < Items.Length ? Items[item] : $"(Item #{item:000})";
    public static string GetMoveName(int move) => (uint)move < Moves.Length ? Moves[move] : $"(Move #{move:000})";
    public static string GetAbilityName(int ability) => (uint)ability < Abilities.Length ? Abilities[ability] : $"(Ability #{ability:000})";

    /// <summary>
    /// The hack numbers its types with a NONE type at 0 and a MYSTERY type at 10. PKHeX uses Normal = 0.
    /// </summary>
    public static byte ConvertType(int t) => t switch
    {
        >= 1 and <= 9 => (byte)(t - 1),
        >= 11 and <= 19 => (byte)(t - 2),
        _ => 0,
    };
}

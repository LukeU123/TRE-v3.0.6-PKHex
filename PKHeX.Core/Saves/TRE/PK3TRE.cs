using System;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>
/// Pokémon Team Rocket Edition (pokeemerald-expansion) Pokémon.
/// </summary>
/// <remarks>
/// Same size, encryption and shuffling as a Gen 3 <see cref="PK3"/>, but the fields are packed differently:
/// 11-bit species and moves, 10-bit held item, 6-bit ball, 10-character nicknames, hidden nature and shiny modifiers,
/// three ability slots, Tera type and hyper training.
/// Blocks are stored unshuffled in this object as G(0x20) A(0x2C) E(0x38) M(0x44).
/// </remarks>
public sealed class PK3TRE : PKM, IContestStats
{
    public const int SizeStored = PokeCrypto.SIZE_3STORED; // 80
    public const int SizeParty = PokeCrypto.SIZE_3PARTY;   // 100

    /// <summary> Shiny odds threshold used by the hack (Gen 6+ default of pokeemerald-expansion). </summary>
    public const int ShinyOdds = 16;

    public override int SIZE_PARTY => SizeParty;
    public override int SIZE_STORED => SizeStored;
    public override EntityContext Context => EntityContext.Gen3;
    public override PersonalInfo PersonalInfo => TREData.GetPersonal(Species, Form);

    public PK3TRE() : base(SizeParty) { }
    public PK3TRE(Memory<byte> data) : base(DecryptParty(data)) { }

    private static Memory<byte> DecryptParty(Memory<byte> data)
    {
        PokeCrypto.DecryptIfEncrypted3(data.Span);
        if (data.Length >= SizeParty)
            return data;
        var result = new byte[SizeParty];
        data.Span.CopyTo(result);
        return result;
    }

    protected override void EncryptStored(Span<byte> stored) => PokeCrypto.Encrypt3(stored);
    protected override void EncryptParty(Span<byte> party) { }

    public override PK3TRE Clone()
    {
        PK3TRE pk = new();
        Data.CopyTo(pk.Data);
        return pk;
    }

    public override int SpriteItem => TREData.GetOfficialItem(HeldItem);

    #region Limits
    public override ushort MaxMoveID => Legal.MaxMoveID_9;
    public override ushort MaxSpeciesID => TREData.MaxSpecies;
    public override int MaxAbilityID => TREData.Abilities.Length - 1;
    public override int MaxItemID => Math.Min(TREData.MaxRawItem, TREData.Items.Length - 1);
    public override int MaxBallID => (int)Core.Ball.Strange;
    public override GameVersion MaxGameID => Legal.MaxGameID_3;
    public override int MaxIV => 31;
    public override int MaxEV => EffortValues.Max255;
    public override int MaxStringLengthTrainer => 7;
    public override int MaxStringLengthNickname => 10;
    public override int TrashCharCountTrainer => 7;
    public override int TrashCharCountNickname => 10;
    #endregion

    // Trash
    public override Span<byte> NicknameTrash => Data.Slice(0x08, 10);
    public override Span<byte> OriginalTrainerTrash => Data.Slice(0x14, 7);

    #region Header
    /// <summary>
    /// Changing the PID keeps the nature: the hack stores it as a modifier on top of the PID, so the modifier is
    /// recomputed for the new value. (Shininess is left to follow the PID, as PKHeX's reroll logic expects.)
    /// </summary>
    public override uint PID
    {
        get => ReadUInt32LittleEndian(Data);
        set
        {
            if (value == PID)
                return;
            var nature = StatAlignment;
            WriteUInt32LittleEndian(Data, value);
            StatAlignment = nature;
        }
    }
    public override uint EncryptionConstant { get => PID; set { } }
    public override uint ID32 { get => ReadUInt32LittleEndian(Data[0x04..]); set => WriteUInt32LittleEndian(Data[0x04..], value); }
    public override ushort TID16 { get => ReadUInt16LittleEndian(Data[0x04..]); set => WriteUInt16LittleEndian(Data[0x04..], value); }
    public override ushort SID16 { get => ReadUInt16LittleEndian(Data[0x06..]); set => WriteUInt16LittleEndian(Data[0x06..], value); }

    public override int Language { get => Data[0x12] & 7; set => Data[0x12] = (byte)((Data[0x12] & ~7) | (value & 7)); }
    public int HiddenNatureModifier { get => Data[0x12] >> 3; set => Data[0x12] = (byte)((Data[0x12] & 7) | ((value & 0x1F) << 3)); }

    public bool FlagIsBadEgg   { get => (Data[0x13] & 1) != 0; set => Data[0x13] = (byte)((Data[0x13] & ~1) | (value ? 1 : 0)); }
    public bool FlagHasSpecies { get => (Data[0x13] & 2) != 0; set => Data[0x13] = (byte)((Data[0x13] & ~2) | (value ? 2 : 0)); }
    public bool FlagIsEgg      { get => (Data[0x13] & 4) != 0; set => Data[0x13] = (byte)((Data[0x13] & ~4) | (value ? 4 : 0)); }

    public byte MarkingValue { get => (byte)(Data[0x1B] & 0xF); set => Data[0x1B] = (byte)((Data[0x1B] & 0xF0) | (value & 0xF)); }
    public ushort Checksum { get => ReadUInt16LittleEndian(Data[0x1C..]); set => WriteUInt16LittleEndian(Data[0x1C..], value); }
    private ushort HPLostShiny { get => ReadUInt16LittleEndian(Data[0x1E..]); set => WriteUInt16LittleEndian(Data[0x1E..], value); }
    public bool ShinyModifier { get => (HPLostShiny & (1 << 14)) != 0; set => HPLostShiny = (ushort)((HPLostShiny & ~(1 << 14)) | (value ? 1 << 14 : 0)); }

    // Nicknames: the first 10 characters only. Real saves show the hack keeps something else in the bytes
    // vanilla expansion uses for characters 11/12 (EXP's high byte and the ball word), so those are never touched.
    public override string Nickname
    {
        get => StringConverter3.GetString(NicknameTrash, Language);
        set
        {
            Span<byte> buffer = stackalloc byte[10];
            buffer.Fill(StringConverter3.TerminatorByte);
            StringConverter3.SetString(buffer, value, 10, Language, StringConverterOption.None);
            buffer.CopyTo(NicknameTrash);
        }
    }

    public override string OriginalTrainerName
    {
        get => StringConverter3.GetString(OriginalTrainerTrash, Language);
        set => StringConverter3.SetString(OriginalTrainerTrash, value, 7, Language, StringConverterOption.None);
    }
    #endregion

    #region Block G (0x20)
    private ushort G0 { get => ReadUInt16LittleEndian(Data[0x20..]); set => WriteUInt16LittleEndian(Data[0x20..], value); }
    public int SpeciesRaw { get => G0 & 0x7FF; set => G0 = (ushort)((G0 & ~0x7FF) | (value & 0x7FF)); }
    public int TeraTypeRaw { get => G0 >> 11; set => G0 = (ushort)((G0 & 0x7FF) | ((value & 0x1F) << 11)); }

    private ushort G2 { get => ReadUInt16LittleEndian(Data[0x22..]); set => WriteUInt16LittleEndian(Data[0x22..], value); }
    public override int HeldItem { get => G2 & 0x3FF; set => G2 = (ushort)((G2 & ~0x3FF) | (value & 0x3FF)); }

    private uint G4 { get => ReadUInt32LittleEndian(Data[0x24..]); set => WriteUInt32LittleEndian(Data[0x24..], value); }
    // The game reads EXP as the low 24 bits (verified in-game); the top byte belongs to something else and is preserved.
    public override uint EXP { get => G4 & 0xFFFFFF; set => G4 = (G4 & 0xFF000000u) | (value & 0xFFFFFF); }

    private byte PPUps { get => Data[0x28]; set => Data[0x28] = value; }
    public override int Move1_PPUps { get => (PPUps >> 0) & 3; set => PPUps = (byte)((PPUps & ~(3 << 0)) | ((value & 3) << 0)); }
    public override int Move2_PPUps { get => (PPUps >> 2) & 3; set => PPUps = (byte)((PPUps & ~(3 << 2)) | ((value & 3) << 2)); }
    public override int Move3_PPUps { get => (PPUps >> 4) & 3; set => PPUps = (byte)((PPUps & ~(3 << 4)) | ((value & 3) << 4)); }
    public override int Move4_PPUps { get => (PPUps >> 6) & 3; set => PPUps = (byte)((PPUps & ~(3 << 6)) | ((value & 3) << 6)); }
    public override byte OriginalTrainerFriendship { get => Data[0x29]; set => Data[0x29] = value; }

    private ushort GA { get => ReadUInt16LittleEndian(Data[0x2A..]); set => WriteUInt16LittleEndian(Data[0x2A..], value); }
    /// <summary> Ball, numbered as in the hack (0 Strange, 1 Poké, 2 Great, 3 Ultra, 4 Master...). </summary>
    public int BallRaw { get => GA & 0x3F; set => GA = (ushort)((GA & ~0x3F) | (value & 0x3F)); }
    #endregion

    #region Block A (0x2C)
    private ushort GetMoveWord(int i) => ReadUInt16LittleEndian(Data[(0x2C + (2 * i))..]);
    private void SetMoveWord(int i, ushort v) => WriteUInt16LittleEndian(Data[(0x2C + (2 * i))..], v);
    private ushort GetMoveRaw(int i) => (ushort)(GetMoveWord(i) & 0x7FF);
    private void SetMoveRaw(int i, ushort move) => SetMoveWord(i, (ushort)((GetMoveWord(i) & ~0x7FF) | (move & 0x7FF)));

    // Exposed as official move IDs; stored as the hack's own IDs.
    private ushort GetMoveOfficial(int i) => TREData.GetOfficialMove(GetMoveRaw(i));
    private void SetMoveOfficial(int i, ushort official)
    {
        if (GetMoveOfficial(i) == official)
            return; // unchanged: keeps moves with no official counterpart (G-Max) intact
        SetMoveRaw(i, TREData.GetRawMove(official));
    }

    public override ushort Move1 { get => GetMoveOfficial(0); set => SetMoveOfficial(0, value); }
    public override ushort Move2 { get => GetMoveOfficial(1); set => SetMoveOfficial(1, value); }
    public override ushort Move3 { get => GetMoveOfficial(2); set => SetMoveOfficial(2, value); }
    public override ushort Move4 { get => GetMoveOfficial(3); set => SetMoveOfficial(3, value); }

    public override int Move1_PP { get => Data[0x34] & 0x7F; set => Data[0x34] = (byte)((Data[0x34] & 0x80) | (value & 0x7F)); }
    public override int Move2_PP { get => Data[0x35] & 0x7F; set => Data[0x35] = (byte)((Data[0x35] & 0x80) | (value & 0x7F)); }
    public override int Move3_PP { get => Data[0x36] & 0x7F; set => Data[0x36] = (byte)((Data[0x36] & 0x80) | (value & 0x7F)); }
    public override int Move4_PP { get => Data[0x37] & 0x7F; set => Data[0x37] = (byte)((Data[0x37] & 0x80) | (value & 0x7F)); }
    #endregion

    #region Block E (0x38)
    public override int EV_HP  { get => Data[0x38]; set => Data[0x38] = (byte)value; }
    public override int EV_ATK { get => Data[0x39]; set => Data[0x39] = (byte)value; }
    public override int EV_DEF { get => Data[0x3A]; set => Data[0x3A] = (byte)value; }
    public override int EV_SPE { get => Data[0x3B]; set => Data[0x3B] = (byte)value; }
    public override int EV_SPA { get => Data[0x3C]; set => Data[0x3C] = (byte)value; }
    public override int EV_SPD { get => Data[0x3D]; set => Data[0x3D] = (byte)value; }
    public byte ContestCool   { get => Data[0x3E]; set => Data[0x3E] = value; }
    public byte ContestBeauty { get => Data[0x3F]; set => Data[0x3F] = value; }
    public byte ContestCute   { get => Data[0x40]; set => Data[0x40] = value; }
    public byte ContestSmart  { get => Data[0x41]; set => Data[0x41] = value; }
    public byte ContestTough  { get => Data[0x42]; set => Data[0x42] = value; }
    public byte ContestSheen  { get => Data[0x43]; set => Data[0x43] = value; }
    #endregion

    #region Block M (0x44)
    // TRE widened the met location to 16 bits, taking the byte vanilla uses for Pokérus.
    // (Verified on a real save: caught Pokémon have locations above 255 and no Pokérus byte here.)
    public override int PokerusDays { get => 0; set { } }
    public override int PokerusStrain { get => 0; set { } }
    public override ushort MetLocation { get => ReadUInt16LittleEndian(Data[0x44..]); set => WriteUInt16LittleEndian(Data[0x44..], value); }

    private ushort Origins { get => ReadUInt16LittleEndian(Data[0x46..]); set => WriteUInt16LittleEndian(Data[0x46..], value); }
    public override byte MetLevel { get => (byte)(Origins & 0x7F); set => Origins = (ushort)((Origins & ~0x7F) | (value & 0x7F)); }
    public override GameVersion Version { get => (GameVersion)((Origins >> 7) & 0xF); set => Origins = (ushort)((Origins & ~0x780) | (((byte)value & 0xF) << 7)); }
    public int DynamaxLevel { get => (Origins >> 11) & 0xF; set => Origins = (ushort)((Origins & ~0x7800) | ((value & 0xF) << 11)); }
    public override byte OriginalTrainerGender { get => (byte)(Origins >> 15); set => Origins = (ushort)((Origins & 0x7FFF) | ((value & 1) << 15)); }

    private uint IV32 { get => ReadUInt32LittleEndian(Data[0x48..]); set => WriteUInt32LittleEndian(Data[0x48..], value); }
    private int GetIVRaw(int shift) => (int)(IV32 >> shift) & 0x1F;
    private void SetIVRaw(int shift, int value) => IV32 = (IV32 & ~(0x1Fu << shift)) | ((uint)Math.Clamp(value, 0, 31) << shift);
    public override int IV_HP  { get => GetIVRaw(00); set => SetIVRaw(00, value); }
    public override int IV_ATK { get => GetIVRaw(05); set => SetIVRaw(05, value); }
    public override int IV_DEF { get => GetIVRaw(10); set => SetIVRaw(10, value); }
    public override int IV_SPE { get => GetIVRaw(15); set => SetIVRaw(15, value); }
    public override int IV_SPA { get => GetIVRaw(20); set => SetIVRaw(20, value); }
    public override int IV_SPD { get => GetIVRaw(25); set => SetIVRaw(25, value); }

    public override bool IsEgg
    {
        get => ((IV32 >> 30) & 1) == 1;
        set
        {
            IV32 = (IV32 & ~(1u << 30)) | (value ? 1u << 30 : 0);
            FlagIsEgg = value;
        }
    }

    private uint RIB0 { get => ReadUInt32LittleEndian(Data[0x4C..]); set => WriteUInt32LittleEndian(Data[0x4C..], value); }
    /// <summary> Ability slot: 0, 1, or 2 (hidden). </summary>
    public int AbilitySlot { get => (int)(RIB0 >> 29) & 3; set => RIB0 = (RIB0 & ~(3u << 29)) | ((uint)(value & 3) << 29); }
    public override bool FatefulEncounter { get => RIB0 >> 31 == 1; set => RIB0 = (RIB0 & 0x7FFFFFFF) | (value ? 1u << 31 : 0); }
    #endregion

    #region Party
    public override int Status_Condition { get => ReadInt32LittleEndian(Data[0x50..]); set => WriteInt32LittleEndian(Data[0x50..], value); }
    public override byte Stat_Level { get => Data[0x54]; set => Data[0x54] = value; }
    public sbyte HeldMailID { get => (sbyte)Data[0x55]; set => Data[0x55] = (byte)value; }
    public override int Stat_HPCurrent { get => ReadUInt16LittleEndian(Data[0x56..]); set => WriteUInt16LittleEndian(Data[0x56..], (ushort)value); }
    public override int Stat_HPMax { get => ReadUInt16LittleEndian(Data[0x58..]); set => WriteUInt16LittleEndian(Data[0x58..], (ushort)value); }
    public override int Stat_ATK { get => ReadUInt16LittleEndian(Data[0x5A..]); set => WriteUInt16LittleEndian(Data[0x5A..], (ushort)value); }
    public override int Stat_DEF { get => ReadUInt16LittleEndian(Data[0x5C..]); set => WriteUInt16LittleEndian(Data[0x5C..], (ushort)value); }
    public override int Stat_SPE { get => ReadUInt16LittleEndian(Data[0x5E..]); set => WriteUInt16LittleEndian(Data[0x5E..], (ushort)value); }
    public override int Stat_SPA { get => ReadUInt16LittleEndian(Data[0x60..]); set => WriteUInt16LittleEndian(Data[0x60..], (ushort)value); }
    public override int Stat_SPD { get => ReadUInt16LittleEndian(Data[0x62..]); set => WriteUInt16LittleEndian(Data[0x62..], (ushort)value); }
    #endregion

    #region Derived values
    public override ushort Species
    {
        get => TREData.GetSpeciesForm(SpeciesRaw).Species;
        set
        {
            var raw = TREData.GetRaw(value, Form);
            SpeciesRaw = raw;
            FlagHasSpecies = raw != 0;
        }
    }

    public override byte Form
    {
        get => TREData.GetSpeciesForm(SpeciesRaw).Form;
        set
        {
            var species = Species;
            var raw = TREData.GetRaw(species, value);
            if (raw != 0)
                SpeciesRaw = raw;
        }
    }

    public override byte Ball { get => BallFromRaw(BallRaw); set => BallRaw = BallToRaw(value); }

    public override int Ability
    {
        get => PersonalInfo.GetAbilityAtIndex(AbilitySlot);
        set
        {
            var index = PersonalInfo.GetIndexOfAbility(value);
            if (index >= 0)
                AbilitySlot = index;
        }
    }

    public override int AbilityNumber { get => 1 << AbilitySlot; set => AbilitySlot = value switch { 4 => 2, 2 => 1, _ => 0 }; }
    public override void RefreshAbility(int n) => AbilitySlot = Math.Clamp(n, 0, 2);

    // The nature the game uses (PID nature adjusted by the hidden nature modifier). Setting it never changes the PID.
    public override Nature Nature { get => StatAlignment; set => StatAlignment = value; }

    /// <summary> Stat nature (the hack's "hidden nature", e.g. after using a Mint). </summary>
    public override Nature StatAlignment
    {
        get
        {
            var n = (int)(PID % 25) ^ HiddenNatureModifier;
            return n < 25 ? (Nature)n : (Nature)(PID % 25);
        }
        set => HiddenNatureModifier = (int)(PID % 25) ^ (int)value;
    }

    public override byte Gender
    {
        get
        {
            var ratio = PersonalInfo.Gender;
            return ratio switch
            {
                PersonalInfo.RatioMagicGenderless => 2,
                PersonalInfo.RatioMagicFemale => 1,
                PersonalInfo.RatioMagicMale => 0,
                _ => (PID & 0xFF) < ratio ? (byte)1 : (byte)0,
            };
        }
        set { }
    }

    private int ShinyValue => (int)((ID32 >> 16) ^ (ID32 & 0xFFFF) ^ (PID >> 16) ^ (PID & 0xFFFF));
    public override bool IsShiny => (ShinyValue < ShinyOdds) ^ ShinyModifier;

    public override void SetShiny()
    {
        if (!IsShiny)
            ShinyModifier = !ShinyModifier;
    }

    public void SetNotShiny()
    {
        if (IsShiny)
            ShinyModifier = !ShinyModifier;
    }

    public override uint PSV => ((PID >> 16) ^ (PID & 0xFFFF)) >> 4;
    public override uint TSV => (uint)(TID16 ^ SID16) >> 4;
    public override int Characteristic => -1;
    public override bool IsNicknamed { get => Nickname != GetDefaultName(); set { } }
    public override byte CurrentFriendship { get => OriginalTrainerFriendship; set => OriginalTrainerFriendship = value; }
    public override byte CurrentHandler { get => 0; set { } }
    public override ushort EggLocation { get => 0; set { } }
    public override bool Valid { get => true; set { } }

    private string GetDefaultName()
    {
        var species = Species;
        if (species >= TREData.FirstCustomSpecies)
            return TREData.GetSpeciesName(species, []).ToUpperInvariant();
        return SpeciesName.GetSpeciesNameGeneration(species, (int)LanguageID.English, 3);
    }

    public override int GetMovePP(ushort move, int ppUpCount)
    {
        if (move == 0)
            return 0;
        var basePP = move <= Legal.MaxMoveID_9 ? MoveInfo.GetPP(EntityContext.Gen9, move) : (byte)5;
        return basePP * (5 + ppUpCount) / 5;
    }
    #endregion

    #region Ball conversion
    // Hack (pokeemerald-expansion) ball order.
    private static readonly Ball[] RawToBall =
    [
        Core.Ball.Strange, Core.Ball.Poke, Core.Ball.Great, Core.Ball.Ultra, Core.Ball.Master, Core.Ball.Premier,
        Core.Ball.Heal, Core.Ball.Net, Core.Ball.Nest, Core.Ball.Dive, Core.Ball.Dusk, Core.Ball.Timer,
        Core.Ball.Quick, Core.Ball.Repeat, Core.Ball.Luxury, Core.Ball.Level, Core.Ball.Lure, Core.Ball.Moon,
        Core.Ball.Friend, Core.Ball.Love, Core.Ball.Fast, Core.Ball.Heavy, Core.Ball.Dream, Core.Ball.Safari,
        Core.Ball.Sport, Core.Ball.None /* Park Ball: no PKHeX equivalent */, Core.Ball.Beast, Core.Ball.Cherish,
    ];

    private static byte BallFromRaw(int raw) => (uint)raw < RawToBall.Length ? (byte)RawToBall[raw] : (byte)Core.Ball.Poke;

    private static int BallToRaw(byte ball)
    {
        if (ball == (byte)Core.Ball.None)
            return 25; // Park Ball
        var index = Array.IndexOf(RawToBall, (Ball)ball);
        return index < 0 ? 1 : index;
    }
    #endregion

    #region Checksum
    private ushort CalculateChecksum() => Checksums.Add16(Data[0x20..SizeStored]);
    public override void RefreshChecksum()
    {
        FlagIsBadEgg = false;
        Checksum = CalculateChecksum();
    }
    public override bool ChecksumValid => CalculateChecksum() == Checksum;
    #endregion

    #region Strings
    public override string GetString(ReadOnlySpan<byte> data) => StringConverter3.GetString(data, Language);
    public override int LoadString(ReadOnlySpan<byte> data, Span<char> destBuffer) => StringConverter3.LoadString(data, destBuffer, Language);
    public override int SetString(Span<byte> destBuffer, ReadOnlySpan<char> value, int maxLength, StringConverterOption option)
        => StringConverter3.SetString(destBuffer, value, maxLength, Language, option);
    public override int GetStringTerminatorIndex(ReadOnlySpan<byte> data) => TrashBytes8.GetTerminatorIndex(data);
    public override int GetStringLength(ReadOnlySpan<byte> data) => TrashBytes8.GetStringLength(data);
    public override int GetBytesPerChar() => 1;
    #endregion
}

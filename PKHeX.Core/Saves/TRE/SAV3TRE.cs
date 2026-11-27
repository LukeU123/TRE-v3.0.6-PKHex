using System;
using System.Collections.Generic;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>
/// Pokémon Team Rocket Edition save file (pokeemerald-expansion based romhack).
/// </summary>
/// <remarks>
/// Layout differs from Emerald: a single save of 28 sectors (no backup slot), sector IDs 0-27,
/// each sector holding 0xFF4 bytes of data (no 0xF80 limit), 24 PC boxes of 30.
/// Sector 0 = trainer block (SaveBlock2), 1-4 = SaveBlock1 (party etc.), 5-19 = PC storage.
/// </remarks>
public sealed class SAV3TRE : SaveFile
{
    public const int SIZE_SECTOR = 0x1000;
    public const int SIZE_SECTOR_DATA = 0xFF4;
    public const int SECTOR_COUNT = 28;
    public const uint SECTOR_SIGNATURE = 0x08012025;
    private const int StorageFirst = 5;
    private const int StorageLast = 19;
    private const int LargeFirst = 1;
    private const int LargeLast = 4;

    private const int COUNT_BOX = 24;
    private const int COUNT_SLOTS = 30;
    private const int OFS_BOXES = 4;
    private const int OFS_BOXNAMES = OFS_BOXES + (COUNT_BOX * COUNT_SLOTS * PokeCrypto.SIZE_3STORED);
    private const int OFS_WALLPAPERS = OFS_BOXNAMES + (COUNT_BOX * 9);

    private const int OFS_PARTYCOUNT = 0x234;
    private const int OFS_PARTY = 0x238;

    private readonly byte[] SmallData = new byte[SIZE_SECTOR_DATA];
    private readonly byte[] LargeData = new byte[(LargeLast - LargeFirst + 1) * SIZE_SECTOR_DATA];
    private readonly byte[] StorageData = new byte[(StorageLast - StorageFirst + 1) * SIZE_SECTOR_DATA];
    private readonly int[] SectorOffsets = new int[SECTOR_COUNT]; // sector ID -> file offset

    public Span<byte> Small => SmallData;
    public Span<byte> Large => LargeData;
    public Span<byte> Storage => StorageData;

    public SAV3TRE(Memory<byte> data) : base(data)
    {
        MapSectors(Data, SectorOffsets);
        ReadSectors();
    }

    private SAV3TRE(SAV3TRE other) : base(other.Data.ToArray())
    {
        MapSectors(Data, SectorOffsets);
        ReadSectors();
    }

    /// <summary>
    /// Checks whether the input looks like a Team Rocket Edition save: all 28 sector IDs 0-27 present with valid signatures.
    /// </summary>
    public static bool IsTRE(ReadOnlySpan<byte> data)
    {
        if (data.Length < SECTOR_COUNT * SIZE_SECTOR)
            return false;
        Span<int> map = stackalloc int[SECTOR_COUNT];
        return MapSectors(data, map);
    }

    private static bool MapSectors(ReadOnlySpan<byte> data, Span<int> map)
    {
        uint seen = 0;
        for (int i = 0; i < SECTOR_COUNT; i++)
        {
            int ofs = i * SIZE_SECTOR;
            var id = ReadUInt16LittleEndian(data[(ofs + 0xFF4)..]);
            var sig = ReadUInt32LittleEndian(data[(ofs + 0xFF8)..]);
            if (sig != SECTOR_SIGNATURE || id >= SECTOR_COUNT)
                return false;
            seen |= 1u << id;
            map[id] = ofs;
        }
        return seen == (1u << SECTOR_COUNT) - 1;
    }

    private Span<byte> GetChunk(int id) => id switch
    {
        0 => SmallData,
        >= LargeFirst and <= LargeLast => LargeData.AsSpan((id - LargeFirst) * SIZE_SECTOR_DATA, SIZE_SECTOR_DATA),
        >= StorageFirst and <= StorageLast => StorageData.AsSpan((id - StorageFirst) * SIZE_SECTOR_DATA, SIZE_SECTOR_DATA),
        _ => [],
    };

    private void ReadSectors()
    {
        for (int id = 0; id <= StorageLast; id++)
            Data.Slice(SectorOffsets[id], SIZE_SECTOR_DATA).CopyTo(GetChunk(id));
    }

    private void WriteSectors()
    {
        for (int id = 0; id <= StorageLast; id++)
            GetChunk(id).CopyTo(Data.Slice(SectorOffsets[id], SIZE_SECTOR_DATA));
    }

    protected override Memory<byte> GetFinalData()
    {
        WriteSectors();
        return base.GetFinalData();
    }

    protected override void SetChecksums()
    {
        WriteSectors();
        for (int i = 0; i < SECTOR_COUNT; i++)
        {
            var sector = Data.Slice(i * SIZE_SECTOR, SIZE_SECTOR);
            WriteUInt16LittleEndian(sector[0xFF6..], Checksums.CheckSum32(sector[..SIZE_SECTOR_DATA]));
        }
    }

    private bool IsSectorValid(int i)
    {
        var sector = Data.Slice(i * SIZE_SECTOR, SIZE_SECTOR);
        return Checksums.CheckSum32(sector[..SIZE_SECTOR_DATA]) == ReadUInt16LittleEndian(sector[0xFF6..]);
    }

    public override bool ChecksumsValid
    {
        get
        {
            for (int i = 0; i < SECTOR_COUNT; i++)
            {
                if (!IsSectorValid(i))
                    return false;
            }
            return true;
        }
    }

    public override string ChecksumInfo
    {
        get
        {
            var list = new List<string>();
            for (int i = 0; i < SECTOR_COUNT; i++)
            {
                if (!IsSectorValid(i))
                    list.Add($"Sector {i} @ {i * SIZE_SECTOR:X5} invalid.");
            }
            return list.Count != 0 ? string.Join(Environment.NewLine, list) : "Checksums are valid.";
        }
    }

    protected override SaveFile CloneInternal()
    {
        WriteSectors();
        return new SAV3TRE(this);
    }

    protected internal override string ShortSummary => $"{OT} (Team Rocket Edition) - {PlayTimeString}";
    public override string Extension => ".sav";
    public override GameVersion Version { get => GameVersion.E; set { } }
    public override byte Generation => 3;
    public override EntityContext Context => EntityContext.Gen3;
    public override IPersonalTable Personal => PersonalTableTRE.Instance;

    public override int MaxStringLengthTrainer => 7;
    public override int MaxStringLengthNickname => 10;
    public override ushort MaxMoveID => Legal.MaxMoveID_9;
    public override ushort MaxSpeciesID => TREData.MaxSpecies;
    public override int MaxAbilityID => TREData.Abilities.Length - 1;
    public override int MaxItemID => Math.Min(TREData.MaxRawItem, TREData.Items.Length - 1);
    public override int MaxBallID => (int)Ball.Strange;
    public override GameVersion MaxGameID => Legal.MaxGameID_3;
    public override int MaxEV => EffortValues.Max255;
    public override int MaxMoney => 999999;

    private static readonly ushort[] AllHeldItems = BuildHeldItems();
    private static ushort[] BuildHeldItems()
    {
        var count = Math.Min(TREData.MaxRawItem, TREData.Items.Length - 1);
        var result = new List<ushort>(count);
        for (ushort i = 1; i <= count; i++)
        {
            if (TREData.Items[i].Length != 0 && TREData.Items[i] != "????????")
                result.Add(i);
        }
        return [.. result];
    }
    public override ReadOnlySpan<ushort> HeldItems => AllHeldItems;

    #region Entities
    public override Type PKMType => typeof(PK3TRE);
    public override PK3TRE BlankPKM => new();
    public override int SIZE_STORED => PokeCrypto.SIZE_3STORED;
    public override int SIZE_PARTY => PokeCrypto.SIZE_3PARTY;
    protected override PK3TRE GetPKM(Memory<byte> data) => new(data);
    protected override void DecryptPKM(Span<byte> data) => PokeCrypto.Decrypt3(data);
    public override bool IsPKMPresent(ReadOnlySpan<byte> data) => EntityDetection.IsPresentGBA(data);

    protected override Span<byte> BoxBuffer => StorageData;
    protected override Span<byte> PartyBuffer => LargeData.AsSpan(OFS_PARTY);

    public override int BoxCount => COUNT_BOX;
    public override int GetBoxOffset(int box) => OFS_BOXES + (box * COUNT_SLOTS * SIZE_STORED);

    public override bool HasParty => true;
    public override int PartyCount
    {
        get => LargeData[OFS_PARTYCOUNT];
        protected set => LargeData[OFS_PARTYCOUNT] = (byte)value;
    }
    public override int GetPartyOffset(int slot) => SIZE_PARTY * slot;

    protected override void SetPartyValues(PKM pk, bool isParty)
    {
        if (pk is PK3TRE p && !isParty)
            p.HeldMailID = 0;
        else if (pk is PK3TRE q)
            q.HeldMailID = -1;
        base.SetPartyValues(pk, isParty);
    }
    #endregion

    #region Trainer
    public override string OT
    {
        get => GetString(Small[..7]);
        set => SetString(Small[..7], value, 7, StringConverterOption.ClearFF);
    }
    public override byte Gender { get => Small[0x08]; set => Small[0x08] = value; }
    public override uint ID32 { get => ReadUInt32LittleEndian(Small[0x0A..]); set => WriteUInt32LittleEndian(Small[0x0A..], value); }
    public override ushort TID16 { get => ReadUInt16LittleEndian(Small[0x0A..]); set => WriteUInt16LittleEndian(Small[0x0A..], value); }
    public override ushort SID16 { get => ReadUInt16LittleEndian(Small[0x0C..]); set => WriteUInt16LittleEndian(Small[0x0C..], value); }
    public override int PlayedHours { get => ReadUInt16LittleEndian(Small[0x0E..]); set => WriteUInt16LittleEndian(Small[0x0E..], (ushort)value); }
    public override int PlayedMinutes { get => Small[0x10]; set => Small[0x10] = (byte)value; }
    public override int PlayedSeconds { get => Small[0x11]; set => Small[0x11] = (byte)value; }
    public override int Language { get => (int)LanguageID.English; set { } }
    #endregion

    #region Box names
    public string GetBoxName(int box) => GetString(Storage.Slice(OFS_BOXNAMES + (box * 9), 9));
    public void SetBoxName(int box, ReadOnlySpan<char> value)
        => SetString(Storage.Slice(OFS_BOXNAMES + (box * 9), 8), value, 8, StringConverterOption.ClearZero);
    public override int CurrentBox { get => Storage[0]; set => Storage[0] = (byte)value; }
    #endregion

    #region Strings
    public override string GetString(ReadOnlySpan<byte> data) => StringConverter3.GetString(data, (int)LanguageID.English);
    public override int LoadString(ReadOnlySpan<byte> data, Span<char> text) => StringConverter3.LoadString(data, text, (int)LanguageID.English);
    public override int SetString(Span<byte> destBuffer, ReadOnlySpan<char> value, int maxLength, StringConverterOption option)
        => StringConverter3.SetString(destBuffer, value, maxLength, (int)LanguageID.English, option);
    #endregion
}

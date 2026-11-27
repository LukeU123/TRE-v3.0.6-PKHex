using System;

namespace PKHeX.Core;

/// <summary>
/// Move learnability for Team Rocket Edition Pokémon, from the hack's own learnsets
/// (level-up, TM/tutor and egg moves of the species and all its pre-evolutions).
/// </summary>
public static class TRELearn
{
    /// <summary>
    /// Marks every official move ID the Pokémon (or a pre-evolution) can learn.
    /// </summary>
    public static void GetLearnable(PKM pk, Span<bool> result)
    {
        foreach (var e in GetChain(pk))
        {
            var lv = e.LevelUp;
            for (int i = 0; i + 1 < lv.Length; i += 2)
                Mark(result, lv[i]);
            foreach (var m in e.Teach)
                Mark(result, m);
            foreach (var m in e.Egg)
                Mark(result, m);
        }
        result[0] = false;

        static void Mark(Span<bool> result, ushort raw)
        {
            var move = TREData.GetOfficialMove(raw);
            if (move != 0 && move < result.Length)
                result[move] = true;
        }
    }

    /// <summary>
    /// Checks each current move against the hack's learnsets.
    /// </summary>
    public static void Verify(PKM pk, Span<MoveResult> result)
    {
        Span<ushort> moves = stackalloc ushort[4];
        pk.GetMoves(moves);
        var chain = GetChain(pk);
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            if (move == 0)
            {
                result[i] = MoveResult.Empty;
                continue;
            }
            if (moves[..i].Contains(move))
            {
                result[i] = MoveResult.Duplicate;
                continue;
            }
            result[i] = Find(chain, move);
        }
    }

    private static MoveResult Find(ReadOnlySpan<TREData.SpeciesEntry> chain, ushort move)
    {
        for (byte stage = 0; stage < chain.Length; stage++)
        {
            var e = chain[stage];
            var lv = e.LevelUp;
            for (int i = 0; i + 1 < lv.Length; i += 2)
            {
                if (TREData.GetOfficialMove(lv[i]) == move)
                    return new MoveResult(new MoveLearnInfo(LearnMethod.LevelUp, LearnEnvironment.None, (byte)Math.Min(lv[i + 1], byte.MaxValue)));
            }
            foreach (var m in e.Teach)
            {
                if (TREData.GetOfficialMove(m) == move)
                    return new MoveResult(new MoveLearnInfo(LearnMethod.TMHM, LearnEnvironment.None));
            }
            foreach (var m in e.Egg)
            {
                if (TREData.GetOfficialMove(m) == move)
                    return new MoveResult(new MoveLearnInfo(LearnMethod.EggMove, LearnEnvironment.None));
            }
        }
        return MoveResult.Unobtainable();
    }

    /// <summary> The Pokémon's own species entry followed by its pre-evolutions. </summary>
    private static TREData.SpeciesEntry[] GetChain(PKM pk)
    {
        var raw = TREData.GetRaw(pk.Species, pk.Form);
        if ((uint)raw >= TREData.Raw.Length || raw == 0)
            return [];
        Span<int> buffer = stackalloc int[8];
        int count = 0;
        while (raw > 0 && count < buffer.Length)
        {
            if (buffer[..count].Contains(raw))
                break; // guard against loops in the data
            buffer[count++] = raw;
            raw = TREData.Raw[raw].PreEvolution;
        }
        var result = new TREData.SpeciesEntry[count];
        for (int i = 0; i < count; i++)
            result[i] = TREData.Raw[buffer[i]];
        return result;
    }
}

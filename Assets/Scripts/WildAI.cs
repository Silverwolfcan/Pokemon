public static class WildAI
{
    public static int ChooseMoveIndex(PokemonInstance wild)
    {
        if (wild == null || wild.Moves == null) return 0;
        for (int i = 0; i < wild.Moves.Count; i++)
        {
            var m = wild.Moves[i];
            if (m != null && m.data != null && m.currentPP > 0) return i;
        }
        return 0;
    }
}

// Server/GameSystems/Skills/PatternReach.cs
using System.Collections.Generic;

public static class PatternReach
{
    static readonly int[] Up = { 1 }, Down = { -1 }, Both = { 1, -1 };

    // Steps that move an offset one cell further out along its axis. On the 0 line "outward" is both ways
    // (Mathf.Sign(0) == 1 would only ever extend toward +).
    public static IReadOnlyList<int> OutwardSteps(int offset) => offset > 0 ? Up : offset < 0 ? Down : Both;
}

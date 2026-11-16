using System.Collections.Generic;

namespace AbyssDefense.Sim
{
    /// 한 심연의 지역 100개가 이어진 모양. 10지역이 한 구간이고 모든 구간이 같은 그물이다.
    ///
    ///        [앞 구간의 군주]         (첫 구간은 위 줄 1·2·3 이 처음부터 열려 있다)
    ///         /     |     \
    ///      (1)----(2)----(3)
    ///       | \    |    / |
    ///      (4)----(5)----(6)
    ///       | /    |    \ |
    ///      (7)----(8)----(9)
    ///         \     |     /
    ///           [10 군주]
    ///
    /// 점령한 지역과 이어진 지역만 고를 수 있다. 군주를 점령해야 다음 구간이 열리므로 군주는 건너뛸 수 없고,
    /// 나머지는 건너뛸 수 있다 (구간마다 가장 짧은 길은 4판).
    public static class RegionGraph
    {
        public const int Segment = 10;

        // 구간 안의 자리(1~10)마다 이어진 자리
        static readonly int[][] local =
        {
            null,
            new[] { 2, 4, 5 }, new[] { 1, 3, 5 }, new[] { 2, 5, 6 },
            new[] { 1, 5, 7 }, new[] { 1, 2, 3, 4, 6, 7, 8, 9 }, new[] { 3, 5, 9 },
            new[] { 4, 5, 8, 10 }, new[] { 5, 7, 9, 10 }, new[] { 5, 6, 8, 10 },
            new[] { 7, 8, 9 },
        };

        /// 구간 안의 자리 (1~9 는 3×3 그물, 10 은 군주)
        public static int Slot(int region) => (region - 1) % Segment + 1;

        /// 아무것도 점령하지 않아도 고를 수 있는 지역
        public static bool IsEntry(int region) => region >= 1 && region <= 3;

        public static List<int> Neighbors(int region)
        {
            var list = new List<int>();
            int b = (region - 1) / Segment * Segment, p = region - b;
            foreach (int q in local[p]) list.Add(b + q);
            if (p <= 3 && b > 0) list.Add(b);                                   // 위 줄 ← 앞 구간의 군주
            if (p == Segment && region < Content.RegionCount)                   // 군주 → 다음 구간의 위 줄
                for (int q = 1; q <= 3; q++) list.Add(region + q);
            return list;
        }

        /// done[지역] 이 점령 여부일 때 그 지역을 고를 수 있는가
        public static bool Reachable(bool[] done, int region)
        {
            if (region < 1 || region > Content.RegionCount) return false;
            if (IsEntry(region) || (done != null && region < done.Length && done[region])) return true;
            if (done == null) return false;
            foreach (int n in Neighbors(region)) if (n < done.Length && done[n]) return true;
            return false;
        }
    }
}

using manHours.Screens.Noim;

namespace manHours;

// 근무시간 프리셋 목록(주간 근무표 · 근무시간 입력 공용).
// 저장 형식은 "HH:MM-HH:MM", 화면 표시는 WorkerScreen.FmtRange 로 축약한다.
static class TimePresets
{
    static readonly string[] Cols = ["라벨", "시간"];

    public static List<string> Load()
    {
        var list = new List<string>();
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("시간프리셋", Cols);
            var (cols, rows) = db.SelectStrings("시간프리셋");
            int ti = Array.IndexOf(cols, "시간");
            foreach (var r in rows)
            {
                string tm = ti >= 0 && ti < r.Length ? r[ti] : "";
                if (!string.IsNullOrEmpty(tm) && !list.Contains(tm)) list.Add(tm);
            }
        }
        catch { }
        if (list.Count == 0)   // 최초 실행: 기본 프리셋 씨앗
        {
            foreach (var (_, t) in AppConfig.WorkTimePresets)
                list.Add(WorkerScreen.NormRange(t));
            Save(list);
        }
        return list;
    }

    public static void Save(List<string> times)
    {
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("시간프리셋", Cols);
            db.Execute("DELETE FROM \"시간프리셋\"");
            foreach (var t in times)
                db.InsertRow("시간프리셋", Cols, [WorkerScreen.FmtRange(t), t]);
        }
        catch { }
    }
}

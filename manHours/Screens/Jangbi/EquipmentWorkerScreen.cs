using manHours.Forms;
using manHours.Screens.Noim;

namespace manHours.Screens.Jangbi;

public class EquipmentWorkerScreen : UserControl
{
    public event Action? GoPrev;
    public event Action? GoNext;

    // ── 레이아웃 상수 ─────────────────────────────────────
    const int N_INFO  = 7;
    const int N_DATES = 16;
    const int N_SUMS  = 3;
    const int D_DATE_COL = 7;
    const int D_SUM_COL  = 23;

    static readonly int[] InfoWidths = [36, 34, 130, 110, 120, 110, 72];
    static readonly string[] InfoHdrs =
        ["☑\n전체", "순번", "사업장명\n사업자번호", "건설기계명\n근무구분", "장비운전자\n주민등록번호", "연락처\n건설기계번호", "보수\n단가"];
    static readonly string[] SumHdrs =
        ["작업일수\n보수총액", "고용보험률\n산재보험률", "고용주\n특고자"];

    // ── DB 인덱스 (EquipPayCols) ──────────────────────────
    // INFO: 선택(0) 순번(1) 사업장명(2) 사업자등록번호(3) 근무구분(4) 건설기계명(5)
    //        공급가(6) 거주지역(7) 장비운전자(8) 주민등록번호(9) 연락처(10) 건설기계번호(11)
    //        세금계산서총액(12) 보수단가(13)
    // DATE: 1일(14) ~ 31일(44)
    // SUM:  작업일수(45) 보수총액(46) 고용보험률(47) 산재보험률(48) 고용주(49) 특고자(50)
    const int I_SEL    = 0;
    const int I_SEQ    = 1;
    const int I_SITE   = 2;
    const int I_BIZNO  = 3;
    const int I_JIKJ   = 4;
    const int I_MACH   = 5;
    const int I_WORKER = 8;
    const int I_JUMIN  = 9;
    const int I_PHONE  = 10;
    const int I_MNO    = 11;
    const int I_DANAKA = 13;
    const int I_ILSU   = 45;
    const int I_TOTAL  = 46;
    const int I_GRATEL = 47;
    const int I_SRATEL = 48;
    const int I_EMPLOY = 49;
    const int I_WORKER2 = 50;

    // ── 상태 ──────────────────────────────────────────────
    int    _year, _month;
    string _project = "";
    int    _lastDay = 31;
    HashSet<int> _holidays = [];
    List<string[]> _equipWorkers = [];

    DataGridView _grid    = null!;
    Label        _lblStatus = null!;

    public EquipmentWorkerScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.Controls.Add(BuildToolbar(),   0, 0);
        root.Controls.Add(BuildGrid(),      0, 1);
        root.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(root);
    }

    Panel BuildToolbar()
    {
        var p    = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgPanel };
        var sep  = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ThemeManager.Border };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(8, 6, 8, 6),
        };
        flow.Controls.Add(MakeBtn("↺ 새로고침", 96, () => Load(_year, _month, _project)));
        var btnSave = MakeBtn("저장", 70, SaveData);
        btnSave.BackColor = Color.FromArgb(30, 100, 60);
        btnSave.ForeColor = Color.White;
        flow.Controls.Add(btnSave);
        p.Controls.Add(sep);
        p.Controls.Add(flow);
        return p;
    }

    DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = ThemeManager.BgCell,
            GridColor = ThemeManager.GridLine, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 52,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false, ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, ScrollBars = ScrollBars.Both,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            EnableHeadersVisualStyles = false,
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.BgHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.HeaderText;
        _grid.ColumnHeadersDefaultCellStyle.Font = ThemeManager.F(8.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.BackColor = ThemeManager.BgCell;
        _grid.DefaultCellStyle.ForeColor = ThemeManager.TextMain;
        _grid.DefaultCellStyle.Font = ThemeManager.F(9f);
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        _grid.DefaultCellStyle.SelectionForeColor = ThemeManager.SelectFg;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.BgAltCell;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        SetupColumns();
        _grid.CellPainting    += OnCellPainting;
        _grid.CellClick       += OnCellClick;
        _grid.CellEndEdit     += OnCellEndEdit;
        _grid.MouseDown       += OnGrdMouseDown;
        return _grid;
    }

    Panel BuildBottomBar()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgBottom };
        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            BackColor = Color.Transparent, Padding = new Padding(3, 3, 3, 3),
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var btnPrev = MakeBtn("← 이전 단계", 104, () => GoPrev?.Invoke());
        btnPrev.Dock = DockStyle.Fill;

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill, ForeColor = ThemeManager.TextSub,
            Font = ThemeManager.F(9f), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true,
        };

        var btnNext = MakeBtn("다음 단계 →", 104, () => { SaveData(); GoNext?.Invoke(); });
        btnNext.Dock = DockStyle.Fill;
        btnNext.BackColor = Color.FromArgb(30, 80, 200);
        btnNext.ForeColor = Color.White;

        tbl.Controls.Add(btnPrev,    0, 0);
        tbl.Controls.Add(_lblStatus, 1, 0);
        tbl.Controls.Add(btnNext,    2, 0);
        p.Controls.Add(tbl);
        return p;
    }

    void SetupColumns()
    {
        _grid.Columns.Clear();
        // INFO 컬럼
        for (int i = 0; i < N_INFO; i++)
        {
            DataGridViewColumn col;
            if (i == 0)
                col = new DataGridViewCheckBoxColumn { Width = InfoWidths[i], ReadOnly = false };
            else
            {
                col = new DataGridViewTextBoxColumn
                {
                    Width = InfoWidths[i],
                    ReadOnly = i != 6, // 보수단가 편집 가능
                };
                if (i == 2 || i == 3 || i == 4 || i == 5)
                    ((DataGridViewTextBoxColumn)col).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
            col.HeaderText = InfoHdrs[i];
            col.HeaderCell = new MultiLineHeaderCell { Value = InfoHdrs[i] };
            col.SortMode   = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(col);
        }
        // DATE 컬럼
        for (int s = 0; s < N_DATES; s++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = "", Width = 30, ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell = new MultiLineHeaderCell { Value = "" };
            _grid.Columns.Add(col);
        }
        // SUM 컬럼
        for (int i = 0; i < N_SUMS; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Width = i == 0 ? 64 : i == 1 ? 70 : 72,
                ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.HeaderCell = new MultiLineHeaderCell { Value = SumHdrs[i] };
            _grid.Columns.Add(col);
        }
    }

    // ── 데이터 로드 ───────────────────────────────────────
    public new void Load(int year, int month, string project)
    {
        _year = year; _month = month; _project = project;
        _lastDay  = DateTime.DaysInMonth(year, month);
        _holidays = AppConfig.GetHolidayDays(year, month);
        LoadWorkers();
        RefreshGrid();
        UpdateStatus();
    }

    void LoadWorkers()
    {
        _equipWorkers.Clear();
        var dbPath = AppConfig.EquipMonthlyDbPath(_year, _month, _project);
        if (!File.Exists(dbPath)) return;
        using var db = new Database(dbPath);

        // 장비지급대장이 있으면 그걸 로드
        if (db.TableExists("장비지급대장"))
        {
            var (payCols, payRows) = db.SelectStrings("장비지급대장");
            foreach (var row in payRows)
            {
                var d = new string[AppConfig.EquipPayCols.Length];
                for (int i = 0; i < AppConfig.EquipPayCols.Length; i++)
                {
                    int ci = Array.IndexOf(payCols, AppConfig.EquipPayCols[i]);
                    d[i] = ci >= 0 && ci < row.Length ? (row[ci] ?? "") : "";
                }
                if (string.IsNullOrEmpty(d[I_SEL])) d[I_SEL] = "1";
                // 건설기계명/장비운전자 등 빈 필드는 전체장비목록에서 보완
                if (string.IsNullOrEmpty(d[I_MACH]) || string.IsNullOrEmpty(d[I_WORKER]))
                    SupplementFromBaseDb(d);
                _equipWorkers.Add(d);
            }
            return;
        }

        // 없으면 장비목록으로 초기화
        if (!db.TableExists("장비목록")) return;
        var (cols, rows) = db.SelectStrings("장비목록");
        int seq = 1;
        foreach (var row in rows)
        {
            var d = new string[AppConfig.EquipPayCols.Length];
            d[I_SEL] = "1";
            d[I_SEQ] = (seq++).ToString();
            for (int ci = 0; ci < AppConfig.EquipmentCols.Length && ci < AppConfig.EQ_INFO_CNT; ci++)
            {
                int si = Array.IndexOf(cols, AppConfig.EquipmentCols[ci]);
                // 매핑: EquipmentCols → EquipPayCols (INFO 부분 일부 대응)
                // EquipPayCols INFO: 선택 순번 사업장명 사업자등록번호 근무구분 건설기계명 공급가 거주지역 장비운전자 주민등록번호 연락처 건설기계번호 세금계산서총액 보수단가
                // EquipmentCols:     자재장비업체명 사업자번호 대표자명 연락처 주민등록번호 자재장비구분 자재장비코드 건설기계구분 건설기계코드 임차구매품목 청구금액 은행명 은행코드 계좌번호 타인명의계좌사유 지급유형
                string val = si >= 0 && si < row.Length ? (row[si] ?? "") : "";
                if (AppConfig.EquipmentCols[ci] == "자재장비업체명") d[I_SITE]   = val;
                else if (AppConfig.EquipmentCols[ci] == "사업자번호")  d[I_BIZNO]  = val;
                else if (AppConfig.EquipmentCols[ci] == "대표자명")   d[I_WORKER] = val;
                else if (AppConfig.EquipmentCols[ci] == "연락처")     d[I_PHONE]  = val;
                else if (AppConfig.EquipmentCols[ci] == "주민등록번호") d[I_JUMIN]  = val;
                else if (AppConfig.EquipmentCols[ci] == "건설기계구분") { d[I_MACH] = val; d[I_JIKJ] = val; }
                else if (AppConfig.EquipmentCols[ci] == "임차구매품목") d[I_MNO]    = val;
                else if (AppConfig.EquipmentCols[ci] == "청구금액")    d[I_DANAKA] = val;
            }
            // 기본 보험률
            d[I_GRATEL] = "0.9";
            d[I_SRATEL] = "0.3";
            // 장비목록에서 누락된 필드 보완
            if (string.IsNullOrEmpty(d[I_MACH]) || string.IsNullOrEmpty(d[I_WORKER]))
                SupplementFromBaseDb(d);
            _equipWorkers.Add(d);
        }
    }

    void RefreshGrid()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        for (int wi = 0; wi < _equipWorkers.Count; wi++)
        {
            var d  = _equipWorkers[wi];
            var r0 = new DataGridViewRow(); r0.CreateCells(_grid); r0.Height = 24;
            FillRow(r0, d, 0);
            _grid.Rows.Add(r0);
            var r1 = new DataGridViewRow(); r1.CreateCells(_grid); r1.Height = 22;
            FillRow(r1, d, 1);
            _grid.Rows.Add(r1);
        }
        _grid.ResumeLayout();
    }

    void FillRow(DataGridViewRow row, string[] d, int subRow)
    {
        // INFO
        row.Cells[0].Value = subRow == 0 && d[I_SEL] == "1";  // 선택 (merged)
        row.Cells[1].Value = subRow == 0 ? d[I_SEQ] : "";      // 순번 (merged)
        row.Cells[2].Value = subRow == 0 ? d[I_SITE] : d[I_BIZNO];
        row.Cells[3].Value = subRow == 0 ? d[I_MACH] : d[I_JIKJ];
        row.Cells[4].Value = subRow == 0 ? d[I_WORKER] : d[I_JUMIN];
        row.Cells[5].Value = subRow == 0 ? d[I_PHONE] : d[I_MNO];
        row.Cells[6].Value = subRow == 0 ? FmtComma(d[I_DANAKA]) : "";  // 보수단가 (merged)

        // DATE
        for (int s = 0; s < N_DATES; s++)
        {
            int day = subRow == 0 ? (s < 15 ? s + 1 : 0) : (s < 15 ? s + 16 : _lastDay);
            row.Cells[D_DATE_COL + s].Value =
                (day > 0 && day <= _lastDay) ? d[AppConfig.EQ_DATE_DB + day - 1] : "";
        }

        // SUM: pairs (row0, row1) per sum col
        for (int i = 0; i < N_SUMS; i++)
        {
            int dbIdx = AppConfig.EQ_SUM_DB + i * 2 + subRow;
            string v = dbIdx < d.Length ? d[dbIdx] : "";
            row.Cells[D_SUM_COL + i].Value = i == 1 ? v : FmtComma(v);
        }
    }

    void OnCellClick(object? s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
        int wi = e.RowIndex / 2;
        if (wi >= _equipWorkers.Count) return;
        _equipWorkers[wi][I_SEL] = _equipWorkers[wi][I_SEL] == "1" ? "" : "1";
        _grid.Rows[wi * 2].Cells[0].Value = _equipWorkers[wi][I_SEL] == "1";
        _grid.InvalidateRow(wi * 2);
        if (wi * 2 + 1 < _grid.Rows.Count) _grid.InvalidateRow(wi * 2 + 1);
    }

    void OnCellEndEdit(object? s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        int wi = e.RowIndex / 2;
        if (wi >= _equipWorkers.Count) return;
        int subRow = e.RowIndex % 2;
        int col    = e.ColumnIndex;
        var d      = _equipWorkers[wi];
        string raw = _grid.Rows[e.RowIndex].Cells[col].Value?.ToString() ?? "";

        // 보수단가 편집
        if (col == 6 && subRow == 0)
        {
            d[I_DANAKA] = raw.Replace(",", "").Trim();
            RecalcWorker(wi);
            RefreshWorkerRows(wi);
            return;
        }

        // 날짜 셀 편집
        if (col >= D_DATE_COL && col < D_DATE_COL + N_DATES)
        {
            int slot = col - D_DATE_COL;
            int day  = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
            if (day > 0 && day <= _lastDay)
            {
                d[AppConfig.EQ_DATE_DB + day - 1] = raw;
                RecalcWorker(wi);
                RefreshWorkerRows(wi);
            }
        }
    }

    void RecalcWorker(int wi)
    {
        var d = _equipWorkers[wi];
        double danaka = ParseD(d[I_DANAKA]);

        // 작업일수
        double ilsu = 0;
        for (int day = 1; day <= _lastDay; day++)
            ilsu += ParseD(d[AppConfig.EQ_DATE_DB + day - 1]);
        d[I_ILSU] = ilsu.ToString("G");

        // 보수총액
        double total = ilsu * danaka;
        d[I_TOTAL] = total.ToString("0");

        // 고용보험률, 산재보험률 (기존값 유지)
        double goyong = ParseD(d[I_GRATEL]);
        double sanjae = ParseD(d[I_SRATEL]);

        // 고용주 = 보수총액 × (고용보험률/2 + 산재보험률) / 100
        double employer = Math.Round(total * (goyong / 2 + sanjae) / 100);
        d[I_EMPLOY] = employer.ToString("0");

        // 특고자 = 보수총액 × 고용보험률/2 / 100
        double worker = Math.Round(total * (goyong / 2) / 100);
        d[I_WORKER2] = worker.ToString("0");
    }

    void RefreshWorkerRows(int wi)
    {
        if (wi * 2 >= _grid.Rows.Count) return;
        var d = _equipWorkers[wi];
        FillRow(_grid.Rows[wi * 2],     d, 0);
        FillRow(_grid.Rows[wi * 2 + 1], d, 1);
        _grid.InvalidateRow(wi * 2);
        _grid.InvalidateRow(wi * 2 + 1);
    }

    // ── 우클릭 정보수정 ───────────────────────────────────
    void OnGrdMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        int wi = hit.RowIndex / 2;
        if (wi >= _equipWorkers.Count) return;
        _grid.ClearSelection();
        _grid.Rows[wi * 2].Selected = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("정보수정").Click += (_, _) => EditFromGrid(wi);
        menu.Show(_grid, e.Location);
    }

    void EditFromGrid(int wi)
    {
        string name = _equipWorkers[wi][I_SITE];
        var vals = LoadEquipFromDb(name);
        if (vals == null)
        {
            MessageBox.Show("장비 정보를 찾을 수 없습니다.", "알림");
            return;
        }
        var dlg = new EquipmentDialog(vals);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        SaveEquipmentUpdate(dlg.Values, vals[0]);

        // 메모리의 _equipWorkers 갱신 (변경된 필드 반영)
        var d = _equipWorkers[wi];
        d[I_SITE]   = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "자재장비업체명")];
        d[I_BIZNO]  = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "사업자번호")];
        d[I_WORKER] = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "대표자명")];
        d[I_PHONE]  = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "연락처")];
        d[I_JUMIN]  = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "주민등록번호")];
        string mach = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "건설기계구분")];
        d[I_MACH]   = mach;
        d[I_JIKJ]   = mach;
        d[I_MNO]    = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "임차구매품목")];
        d[I_DANAKA] = dlg.Values[Array.IndexOf(AppConfig.EquipmentCols, "청구금액")];

        RecalcWorker(wi);
        RefreshWorkerRows(wi);
    }

    void SupplementFromBaseDb(string[] d)
    {
        string name = d[I_SITE];
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            using var baseDb = new Database(AppConfig.BaseDbPath);
            if (!baseDb.TableExists("전체장비목록")) return;
            var (cols, rows) = baseDb.SelectStrings("전체장비목록",
                $"\"자재장비업체명\"='{name.Replace("'", "''")}' LIMIT 1");
            if (rows.Length == 0) return;
            var r = rows[0];
            string GetCol(string col) {
                int i = Array.IndexOf(cols, col);
                return i >= 0 && i < r.Length ? r[i] ?? "" : "";
            }
            if (string.IsNullOrEmpty(d[I_WORKER])) d[I_WORKER] = GetCol("대표자명");
            if (string.IsNullOrEmpty(d[I_JUMIN]))  d[I_JUMIN]  = GetCol("주민등록번호");
            if (string.IsNullOrEmpty(d[I_PHONE]))  d[I_PHONE]  = GetCol("연락처");
            if (string.IsNullOrEmpty(d[I_BIZNO]))  d[I_BIZNO]  = GetCol("사업자번호");
            string mach = GetCol("건설기계구분");
            if (string.IsNullOrEmpty(d[I_MACH]))   d[I_MACH]   = mach;
            if (string.IsNullOrEmpty(d[I_JIKJ]))   d[I_JIKJ]   = mach;
            if (string.IsNullOrEmpty(d[I_MNO]))    d[I_MNO]    = GetCol("임차구매품목");
            if (string.IsNullOrEmpty(d[I_DANAKA])) d[I_DANAKA] = GetCol("청구금액");
        }
        catch { }
    }

    string[]? LoadEquipFromDb(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            if (!db.TableExists("전체장비목록")) return null;
            var (cols, rows) = db.SelectStrings("전체장비목록",
                $"\"자재장비업체명\"='{name.Replace("'", "''")}' LIMIT 1");
            if (rows.Length == 0) return null;
            var vals = new string[AppConfig.EquipmentCols.Length];
            for (int ci = 0; ci < AppConfig.EquipmentCols.Length; ci++)
            {
                int idx = Array.IndexOf(cols, AppConfig.EquipmentCols[ci]);
                vals[ci] = idx >= 0 && idx < rows[0].Length ? rows[0][idx] ?? "" : "";
            }
            return vals;
        }
        catch { return null; }
    }

    void SaveEquipmentUpdate(string[] newVals, string oldName)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체장비목록", AppConfig.EquipmentCols);
        var sets = string.Join(", ", AppConfig.EquipmentCols.Select((c, i) => $"\"{c}\"=@p{i}"));
        db.Execute($"UPDATE \"전체장비목록\" SET {sets} WHERE \"자재장비업체명\"=@p{AppConfig.EquipmentCols.Length}",
            newVals.Cast<object?>().Append(oldName).ToArray());
    }

    // ── 셀 병합 페인팅 ────────────────────────────────────
    static bool IsMergedCol(int c) => c is 0 or 1 or 6;

    void OnCellPainting(object? s, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is null || e.CellStyle is null) return;

        if (e.RowIndex >= 0 && IsMergedCol(e.ColumnIndex))
        {
            int wi = e.RowIndex / 2;
            int sr = e.RowIndex % 2;
            if (wi < _equipWorkers.Count)
            {
                if (sr == 0 && wi * 2 + 1 < _grid.Rows.Count) { PaintMergedCell(e, wi); return; }
                if (sr == 1) { e.Handled = true; return; }
            }
        }

        if (e.RowIndex == -1)
        {
            if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
                PaintDateHeader(e);
            return;
        }
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (e.ColumnIndex < D_DATE_COL || e.ColumnIndex >= D_DATE_COL + N_DATES) return;

        int subRow = e.RowIndex % 2;
        int slot   = e.ColumnIndex - D_DATE_COL;
        int day    = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
        if (day <= 0 || day > _lastDay) return;

        string kind = AppConfig.GetDayKind(_year, _month, day, _holidays, []);
        if (kind == "")
        {
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
            DrawDayWatermark(e.Graphics!, e.CellBounds, day);
            e.Handled = true;
            return;
        }
        var bg = kind == "sun" ? Color.FromArgb(255, 220, 235) : Color.FromArgb(187, 222, 251);
        using var bb = new SolidBrush(bg);
        e.Graphics.FillRectangle(bb, e.CellBounds);
        using var pen = new Pen(Color.FromArgb(80, 85, 105));
        e.Graphics.DrawRectangle(pen, e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
        string txt = e.FormattedValue?.ToString() ?? "";
        if (!string.IsNullOrEmpty(txt))
        {
            using var tb = new SolidBrush(Color.Black);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(txt, e.CellStyle.Font ?? Font, tb, e.CellBounds, sf);
        }
        DrawDayWatermark(e.Graphics!, e.CellBounds, day);
        e.Handled = true;
    }

    void PaintMergedCell(DataGridViewCellPaintingEventArgs e, int wi)
    {
        int h1 = _grid.Rows[e.RowIndex + 1].Height;
        var combined = new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width, e.CellBounds.Height + h1);
        Color bg = wi % 2 == 0 ? ThemeManager.BgCell : ThemeManager.BgAltCell;

        var savedClip = e.Graphics.Clip.Clone();
        try
        {
            e.Graphics.SetClip(combined, System.Drawing.Drawing2D.CombineMode.Replace);
            using var br = new SolidBrush(bg);
            e.Graphics.FillRectangle(br, combined);

            if (e.ColumnIndex == 0)
            {
                bool isChecked = e.Value is bool b && b;
                var st = isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var sz = CheckBoxRenderer.GetGlyphSize(e.Graphics, st);
                int cx = combined.X + (combined.Width - sz.Width) / 2;
                int cy = combined.Y + (combined.Height - sz.Height) / 2;
                CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(cx, cy), st);
            }
            else
            {
                string text = e.FormattedValue?.ToString() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    using var tb = new SolidBrush(e.CellStyle!.ForeColor);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(text, e.CellStyle.Font ?? Font, tb, combined, sf);
                }
            }
            using var gp = new Pen(_grid.GridColor);
            e.Graphics.DrawRectangle(gp, combined.X, combined.Y, combined.Width - 1, combined.Height - 1);
        }
        finally { e.Graphics.Clip = savedClip; }
        e.Handled = true;
    }

    void PaintDateHeader(DataGridViewCellPaintingEventArgs e)
    {
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        var g = e.Graphics;
        if (g == null || e.CellStyle == null) { e.Handled = true; return; }
        int slot  = e.ColumnIndex - D_DATE_COL;
        int day1  = slot + 1;
        int day2  = slot + 16;
        int halfH = e.CellBounds.Height / 2;

        void PaintHalf(int day, int y, int h, bool valid)
        {
            if (!valid) return;
            var rect = new Rectangle(e.CellBounds.X + 1, y, e.CellBounds.Width - 2, h);
            string kind = AppConfig.GetDayKind(_year, _month, day, _holidays, []);
            Color tc = e.CellStyle.ForeColor;
            if (kind == "sun") { using var bg = new SolidBrush(Color.FromArgb(255, 220, 235)); g.FillRectangle(bg, rect); tc = Color.FromArgb(180, 0, 60); }
            else if (kind == "sat") { using var bg = new SolidBrush(Color.FromArgb(187, 222, 251)); g.FillRectangle(bg, rect); tc = Color.FromArgb(20, 80, 180); }
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var br = new SolidBrush(tc);
            g.DrawString(day.ToString(), e.CellStyle.Font ?? Font, br, rect, sf);
        }
        PaintHalf(day1, e.CellBounds.Y,         halfH,                       day1 <= 15);
        PaintHalf(day2, e.CellBounds.Y + halfH, e.CellBounds.Height - halfH, day2 <= _lastDay);
        e.Handled = true;
    }

    void DrawDayWatermark(Graphics g, Rectangle rect, int day)
    {
        using var f  = new Font("맑은 고딕", 7f);
        using var wp = new SolidBrush(ThemeManager.IsDark ? Color.FromArgb(90, 180, 195, 210) : Color.FromArgb(90, 120, 130, 160));
        using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        g.DrawString(day.ToString(), f, wp, rect, sf);
    }

    // ── DB 저장/로드 ──────────────────────────────────────
    public void SaveData()
    {
        // 순번 재정렬
        for (int i = 0; i < _equipWorkers.Count; i++)
            _equipWorkers[i][I_SEQ] = (i + 1).ToString();

        var dbPath = AppConfig.EquipMonthlyDbPath(_year, _month, _project);
        using var db = new Database(dbPath);
        db.EnsureTable("장비지급대장", AppConfig.EquipPayCols);
        db.Execute("DELETE FROM \"장비지급대장\"");
        foreach (var d in _equipWorkers)
            db.InsertRow("장비지급대장", AppConfig.EquipPayCols, d);
        UpdateStatus();
    }

    void UpdateStatus()
    {
        double tot = _equipWorkers.Sum(d => ParseD(d.Length > I_TOTAL ? d[I_TOTAL] : ""));
        _lblStatus.Text = $"  {_year}년 {_month:D2}월 | {_project} | {_equipWorkers.Count}건 | 보수총액: {tot:N0}원";
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    static double ParseD(string? s) =>
        !string.IsNullOrEmpty(s) && double.TryParse(s.Replace(",", ""), out var v) ? v : 0;

    static string FmtComma(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Replace(",", "").Trim();
        return long.TryParse(t, out var n) ? n.ToString("N0") : s;
    }

    static Button MakeBtn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text = text, Width = w, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(9f), Cursor = Cursors.Hand, Margin = new Padding(2, 0, 2, 0),
        };
        b.FlatAppearance.BorderColor = ThemeManager.IsDark ? ThemeManager.Border : Color.FromArgb(130, 134, 158);
        b.Click += (_, _) => onClick();
        return b;
    }
}

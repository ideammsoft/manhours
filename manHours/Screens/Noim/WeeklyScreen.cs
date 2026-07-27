namespace manHours.Screens.Noim;

// 주간 근무표: 근로자(현재근무) × 요일(월~일) 표.
// 요일 칸을 클릭하고 시간 프리셋을 누르거나 직접 입력해 주간 패턴을 만든 뒤,
// '이번 달 적용'으로 그 달의 근무시간(지급대장)에 자동으로 채운다.
public class WeeklyScreen : UserControl
{
    // ── 그리드 컬럼 ───────────────────────────────────────
    const int C_SEQ = 0, C_NAME = 1, C_PHONE = 2, C_STAT = 3, C_MON = 4;   // 월=4 ... 일=10
    static readonly string[] DayNames = ["월", "화", "수", "목", "금", "토", "일"];

    // ── PayCols 인덱스 (이름으로 조회 — 컬럼 순서 바뀌어도 안전) ──
    static readonly int P_AUTO  = Array.IndexOf(AppConfig.PayCols, "금액자동");
    static readonly int P_BREAK = Array.IndexOf(AppConfig.PayCols, "휴게자동");
    static readonly int P_SEQ   = Array.IndexOf(AppConfig.PayCols, "순번");
    static readonly int P_NAME  = Array.IndexOf(AppConfig.PayCols, "이름");
    static readonly int P_JUMIN = Array.IndexOf(AppConfig.PayCols, "생년월일6자리");
    static readonly int P_START = Array.IndexOf(AppConfig.PayCols, "근무시작일");
    static readonly int P_PHONE = Array.IndexOf(AppConfig.PayCols, "전화번호");
    static readonly int P_ADDR  = Array.IndexOf(AppConfig.PayCols, "거주지역");
    static readonly int P_WAGE  = Array.IndexOf(AppConfig.PayCols, "시급");
    static int P_DAY(int day)   => Array.IndexOf(AppConfig.PayCols, $"{day}일");

    sealed class Row
    {
        public string Name = "", Jumin = "", Phone = "", Addr = "", Wage = "", Status = "현재근무";
        public string[] Times = new string[7];   // 월~일
    }

    int _year = DateTime.Now.Year, _month = DateTime.Now.Month;
    string _project = "";
    readonly List<Row> _rows = [];

    DataGridView _grid = null!;
    Label _lblStatus = null!;
    CheckBox _chkInactive = null!;
    Button _bApply = null!;
    ComboBox _cmbWeek = null!;
    ComboBox _cmbTimes = null!;     // 시간 목록(편집 가능): 추가/삭제 + 셀 콤보 원본
    readonly List<(string Label, List<int> Days)> _weeks = [];
    bool _syncing;                  // 그리드 재구성 중 값변경 이벤트 무시

    // 시간 프리셋(사용자 편집·저장) — '근무시간 입력' 화면과 공용
    List<string> _presets = [];
    ContextMenuStrip _cellMenu = null!;

    public WeeklyScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    public void SetContext(int year, int month, string project)
    {
        _year = year; _month = month; _project = project;
        _bApply.Text = $"▶ {_month}월 전체";
        BuildWeeks();
        Reload();
    }

    // 그 달을 덮는 주(월~일) 목록. 각 주는 그 달에 속한 날짜만 담는다.
    void BuildWeeks()
    {
        _weeks.Clear();
        _cmbWeek.Items.Clear();
        int lastDay = DateTime.DaysInMonth(_year, _month);
        var first = new DateTime(_year, _month, 1);
        var monEnd = new DateTime(_year, _month, lastDay);
        int backToMon = ((int)first.DayOfWeek + 6) % 7;          // 월=0
        for (var ws = first.AddDays(-backToMon); ws <= monEnd; ws = ws.AddDays(7))
        {
            var we = ws.AddDays(6);
            var days = new List<int>();
            for (var dt = ws; dt <= we; dt = dt.AddDays(1))
                if (dt.Year == _year && dt.Month == _month) days.Add(dt.Day);
            if (days.Count == 0) continue;
            string label = $"{ws.Month}/{ws.Day}~{we.Month}/{we.Day}";
            _weeks.Add((label, days));
            _cmbWeek.Items.Add(label);
        }
        // 기본 선택: 오늘이 포함된 주(표시 월에 오늘이 있을 때), 없으면 첫 주
        int sel = 0;
        var today = DateTime.Today;
        if (today.Year == _year && today.Month == _month)
        {
            int idx = _weeks.FindIndex(w => w.Days.Contains(today.Day));
            if (idx >= 0) sel = idx;
        }
        if (_cmbWeek.Items.Count > 0) _cmbWeek.SelectedIndex = sel;
    }

    // ── UI ────────────────────────────────────────────────
    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildGrid(), 0, 1);

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = ThemeManager.F(9f), ForeColor = ThemeManager.TextSub, BackColor = ThemeManager.BgBottom,
        };
        root.Controls.Add(_lblStatus, 0, 2);
        Controls.Add(root);
    }

    Panel BuildToolbar()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgPanel };

        // 1줄: 동작 버튼 + 옵션
        var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(8, 6, 8, 0), BackColor = Color.Transparent };
        row1.Controls.Add(Btn("↺ 새로고침", 96, Reload));
        var bSave = Btn("주간표 저장", 100, Save);
        bSave.BackColor = Color.FromArgb(30, 100, 60); bSave.ForeColor = Color.White;
        row1.Controls.Add(bSave);
        // 주 단위 적용
        _cmbWeek = new ComboBox
        {
            Width = 128, DropDownStyle = ComboBoxStyle.DropDownList,
            Font = ThemeManager.F(9f), Margin = new Padding(16, 4, 4, 0),
        };
        row1.Controls.Add(_cmbWeek);
        var bWeek = Btn("▶ 선택 주 적용", 108, ApplyToWeek);
        bWeek.BackColor = Color.FromArgb(30, 90, 160); bWeek.ForeColor = Color.White;
        row1.Controls.Add(bWeek);

        // 월 전체 적용
        _bApply = Btn($"▶ {_month}월 전체", 100, ApplyToMonth);
        _bApply.BackColor = Color.FromArgb(60, 70, 90); _bApply.ForeColor = Color.White;
        row1.Controls.Add(_bApply);

        _chkInactive = new CheckBox
        {
            Text = "휴직/퇴사 포함", AutoSize = true, Margin = new Padding(16, 8, 0, 0),
            Font = ThemeManager.F(9f), ForeColor = ThemeManager.TextSub, BackColor = Color.Transparent,
        };
        _chkInactive.CheckedChanged += (_, _) => Reload();
        row1.Controls.Add(_chkInactive);

        // 2줄: 시간 목록 관리 — 콤보박스에 시간을 추가/삭제 (셀 콤보의 원본)
        var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(8, 4, 8, 4), BackColor = Color.Transparent };
        row2.Controls.Add(new Label { Text = "시간 목록 →", AutoSize = true, Margin = new Padding(0, 8, 6, 0), Font = ThemeManager.F(9f), ForeColor = ThemeManager.TextSub, BackColor = Color.Transparent });

        _cmbTimes = new ComboBox
        {
            Width = 140, DropDownStyle = ComboBoxStyle.DropDown,   // 편집 가능 → 직접 입력
            Font = ThemeManager.F(9f), Margin = new Padding(0, 5, 6, 0),
        };
        _cmbTimes.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddTime(); } };
        row2.Controls.Add(_cmbTimes);
        row2.Controls.Add(Btn("＋ 시간추가", 92, AddTime));
        row2.Controls.Add(Btn("－ 시간삭제", 92, DeleteTime));

        LoadPresets();

        var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ThemeManager.Border };
        p.Controls.Add(row2);
        p.Controls.Add(row1);
        p.Controls.Add(sep);
        return p;
    }

    DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = ThemeManager.BgCell, GridColor = ThemeManager.GridLine,
            BorderStyle = BorderStyle.None, ColumnHeadersHeight = 30,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            EnableHeadersVisualStyles = false, RowTemplate = { Height = 28 },
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.BgHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.HeaderText;
        _grid.ColumnHeadersDefaultCellStyle.Font = ThemeManager.F(9f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.BackColor = ThemeManager.BgCell;
        _grid.DefaultCellStyle.ForeColor = ThemeManager.TextMain;
        _grid.DefaultCellStyle.Font = ThemeManager.F(9f);
        _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        _grid.DefaultCellStyle.SelectionForeColor = ThemeManager.SelectFg;

        void Col(string h, int w, bool ro)
            => _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = h, Width = w, ReadOnly = ro, SortMode = DataGridViewColumnSortMode.NotSortable });
        Col("순번", 40, true);
        Col("이름", 90, true);
        Col("전화번호", 110, true);   // 동명이인 구분
        Col("상태", 66, true);
        // 요일 칸: 시간 목록 콤보박스 (포커스되면 드롭다운으로 선택)
        foreach (var d in DayNames)
        {
            var c = new DataGridViewComboBoxColumn
            {
                HeaderText = d, Width = 74,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                DisplayStyleForCurrentCellOnly = true,
                AutoComplete = false,
            };
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(c);
        }

        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.DataError += (_, e) => { e.ThrowException = false; };                 // 콤보 값 불일치 무시
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>                             // 콤보 선택 즉시 커밋
        { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellClick += (_, e) =>                                                // 요일 셀 클릭 → 드롭다운 자동
        {
            if (_syncing || e.RowIndex < 0 || e.ColumnIndex < C_MON || e.RowIndex >= _rows.Count) return;
            BeginInvoke(() => { try { if (_grid.EditingControl is ComboBox cb) cb.DroppedDown = true; } catch { } });
        };

        // 셀 시간 삭제: Del 키 또는 우클릭 → 삭제
        _cellMenu = new ContextMenuStrip();
        _cellMenu.Items.Add("삭제", null, (_, _) => ClearSelectedCells());
        _grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) { ClearSelectedCells(); e.Handled = true; } };
        _grid.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0 || hit.ColumnIndex < C_MON) return;
            if (!_grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex].Selected)
            {
                _grid.ClearSelection();
                _grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex].Selected = true;
            }
            _cellMenu.Show(_grid, e.Location);
        };
        return _grid;
    }

    // 선택한 요일 칸(들)의 시간을 지운다
    void ClearSelectedCells()
    {
        foreach (DataGridViewCell cell in _grid.SelectedCells)
        {
            int d = cell.ColumnIndex - C_MON;
            if (d is < 0 or > 6 || cell.RowIndex >= _rows.Count) continue;
            _rows[cell.RowIndex].Times[d] = "";
            cell.Value = "";
        }
    }

    static Button Btn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text = text, Width = w, Height = 28, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(9f), Cursor = Cursors.Hand, Margin = new Padding(0, 4, 6, 0),
        };
        b.FlatAppearance.BorderColor = ThemeManager.Border;
        b.Click += (_, _) => onClick();
        return b;
    }

    // ── 데이터 로드/저장 ───────────────────────────────────
    void LoadRows()
    {
        _rows.Clear();

        // 주간근무(사업명별) 시간 로드 — 동명이인 구분을 위해 이름+전화번호로 키를 잡는다.
        var times = new Dictionary<string, string[]>();
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("주간근무", AppConfig.WeeklyCols);
            var (cols, rows) = db.SelectStrings("주간근무",
                $"\"사업명\"='{_project.Replace("'", "''")}'");
            int ni = Array.IndexOf(cols, "이름"), pi = Array.IndexOf(cols, "전화번호");
            foreach (var r in rows)
            {
                if (ni < 0 || ni >= r.Length) continue;
                var arr = new string[7];
                for (int d = 0; d < 7; d++)
                {
                    int ci = Array.IndexOf(cols, DayNames[d]);
                    arr[d] = ci >= 0 && ci < r.Length ? r[ci] : "";
                }
                string phone = pi >= 0 && pi < r.Length ? r[pi] : "";
                times[AppConfig.WorkerKey(r[ni], phone)] = arr;
            }
        }
        catch { }

        // 전체근로자에서 로스터 구성 (이름+전화번호로 정확히 매칭 — 동명이인 분리)
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("전체근로자", AppConfig.WorkerCols);
            var (cols, rows) = db.SelectStrings("전체근로자");
            string V(string[] r, string c) { int i = Array.IndexOf(cols, c); return i >= 0 && i < r.Length ? r[i] : ""; }
            foreach (var r in rows)
            {
                string status = V(r, "상태"); if (string.IsNullOrWhiteSpace(status)) status = "현재근무";
                if (!_chkInactive.Checked && status != "현재근무") continue;
                string name = V(r, "이름");
                if (string.IsNullOrWhiteSpace(name)) continue;
                string phone = V(r, "전화번호");
                _rows.Add(new Row
                {
                    Name = name, Jumin = V(r, "생년월일6자리"), Phone = phone,
                    Addr = V(r, "거주지역"), Wage = V(r, "시급"), Status = status,
                    Times = times.TryGetValue(AppConfig.WorkerKey(name, phone), out var t) ? t : new string[7],
                });
            }
        }
        catch { }

        _lblStatus.Text = $"  {_project}  |  근로자 {_rows.Count}명  |  요일 칸을 눌러 시간을 선택하세요 · 지우기: 셀 선택 후 Del 또는 우클릭";
    }

    void Reload() { LoadRows(); RefreshTimeOptions(); RefreshGrid(); }

    // 프리셋 + 현재 사용 시간으로 셀 콤보/관리 콤보의 목록을 갱신
    void RefreshTimeOptions()
    {
        var displays = new List<string>();
        void Add(string norm)
        {
            if (string.IsNullOrEmpty(norm)) return;
            var d = WorkerScreen.FmtRange(norm);
            if (!displays.Contains(d)) displays.Add(d);
        }
        foreach (var t in _presets) Add(t);
        foreach (var r in _rows) foreach (var t in r.Times) Add(t);

        for (int c = C_MON; c < C_MON + 7; c++)
        {
            var col = (DataGridViewComboBoxColumn)_grid.Columns[c];
            col.Items.Clear();
            col.Items.Add("");                      // 빈칸(지우기용)
            foreach (var d in displays) col.Items.Add(d);
        }
        string keep = _cmbTimes.Text;
        _cmbTimes.Items.Clear();
        foreach (var d in displays) _cmbTimes.Items.Add(d);
        _cmbTimes.Text = keep;
    }

    void RefreshGrid()
    {
        _syncing = true;
        _grid.Rows.Clear();
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            var cells = new object[4 + 7];
            cells[C_SEQ] = i + 1;
            cells[C_NAME] = r.Name;
            cells[C_PHONE] = r.Phone;
            cells[C_STAT] = r.Status;
            for (int d = 0; d < 7; d++) cells[C_MON + d] = WorkerScreen.FmtRange(r.Times[d]);
            int ri = _grid.Rows.Add(cells);
            // 주말 헤더 색 힌트: 토(파랑)/일(분홍) 칸
            _grid.Rows[ri].Cells[C_MON + 5].Style.ForeColor = Color.FromArgb(60, 110, 200);
            _grid.Rows[ri].Cells[C_MON + 6].Style.ForeColor = Color.FromArgb(200, 80, 90);
            if (r.Status != "현재근무")
                _grid.Rows[ri].Cells[C_STAT].Style.ForeColor = Color.FromArgb(180, 90, 90);
        }
        _syncing = false;
    }

    // 콤보에서 시간을 고르면 해당 요일 시간 반영
    void OnCellValueChanged(object? s, DataGridViewCellEventArgs e)
    {
        if (_syncing || e.RowIndex < 0 || e.ColumnIndex < C_MON) return;
        int d = e.ColumnIndex - C_MON;
        if (d is < 0 or > 6 || e.RowIndex >= _rows.Count) return;
        string disp = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
        _rows[e.RowIndex].Times[d] = WorkerScreen.NormRange(disp);
    }

    // ── 시간 프리셋 관리 ───────────────────────────────────
    void LoadPresets() => _presets = TimePresets.Load();

    // 콤보에 입력/선택된 시간을 목록에 추가 (셀 콤보에도 반영·저장)
    void AddTime()
    {
        string norm = WorkerScreen.NormRange(_cmbTimes.Text);
        if (string.IsNullOrEmpty(norm))
        {
            MessageBox.Show("시간을 '시작-끝' 형식으로 입력하세요. (예: 10-15, 9:30-14)", "시간추가", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!_presets.Contains(norm))
        {
            _presets.Add(norm);
            TimePresets.Save(_presets);
        }
        RefreshTimeOptions();
        _cmbTimes.Text = WorkerScreen.FmtRange(norm);
    }

    // 콤보에서 고른 시간을 목록에서 삭제
    void DeleteTime()
    {
        string norm = WorkerScreen.NormRange(_cmbTimes.Text);
        if (string.IsNullOrEmpty(norm))
        {
            MessageBox.Show("삭제할 시간을 콤보에서 고르세요.", "시간삭제", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!_presets.Remove(norm)) { MessageBox.Show("목록에 없는 시간입니다.", "시간삭제", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        TimePresets.Save(_presets);
        RefreshTimeOptions();
        _cmbTimes.Text = "";
    }

    void Save()
    {
        if (SaveRows(silent: false))
            MessageBox.Show("주간 근무표를 저장했습니다.", "저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>화면의 주간 패턴을 저장한다. 목록에 없는 근로자(휴직/퇴사 등)의 행은 건드리지 않는다.</summary>
    bool SaveRows(bool silent)
    {
        _grid.EndEdit();
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("주간근무", AppConfig.WeeklyCols);
            // 전체 삭제하면 화면에서 걸러진 휴직/퇴사자의 패턴까지 지워진다. 보이는 사람만 교체.
            foreach (var r in _rows)
                db.Execute(
                    "DELETE FROM \"주간근무\" WHERE \"사업명\"=@p0 AND \"이름\"=@p1 AND \"전화번호\"=@p2",
                    _project, r.Name, r.Phone);
            foreach (var r in _rows)
            {
                if (r.Times.All(string.IsNullOrEmpty)) continue;   // 전부 비면 저장 안 함
                db.InsertRow("주간근무", AppConfig.WeeklyCols,
                    [_project, r.Name, r.Phone, .. r.Times]);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (!silent) MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    // 월 전체 적용
    void ApplyToMonth()
    {
        int lastDay = DateTime.DaysInMonth(_year, _month);
        var days = Enumerable.Range(1, lastDay).ToList();
        ApplyPattern(days, $"{_year}년 {_month}월 전체");
    }

    // 선택한 주(월~일)만 적용 — 주 단위로 출퇴근을 확인하는 경우
    void ApplyToWeek()
    {
        int idx = _cmbWeek.SelectedIndex;
        if (idx < 0 || idx >= _weeks.Count)
        {
            MessageBox.Show("적용할 주를 선택하세요.", "주 적용", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var (label, days) = _weeks[idx];
        ApplyPattern(days, $"{_month}월 {label} 주");
    }

    // 주간 패턴 → 그 달(년/월/매장)의 지급대장에서 targetDays 날짜들을 채운다.
    void ApplyPattern(List<int> targetDays, string desc)
    {
        _grid.EndEdit();
        // 현재근무 전원이 대상. 요일 시간이 없는 사람은 '빈 패턴'을 적용해 그 날짜를 비운다.
        var current  = _rows.Where(r => r.Status == "현재근무").ToList();
        var scheduled = current.Where(r => r.Times.Any(t => !string.IsNullOrEmpty(t))).ToList();
        if (scheduled.Count == 0)
        {
            MessageBox.Show("적용할 근무(현재근무 + 요일 시간)가 없습니다.", "적용", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(
                $"{_project}  {desc} 근무시간에\n주간 근무표를 적용합니다.\n(해당 날짜의 기존 입력은 덮어씁니다)\n\n계속할까요?",
                "적용", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        if (!SaveRows(silent: false)) return;   // 적용 전에 주간표를 자동 저장

        int lastDay = DateTime.DaysInMonth(_year, _month);
        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);
        try
        {
            using var db = new Database(dbPath);
            db.EnsureTable("근로자목록", AppConfig.WorkerCols);
            db.EnsureTable("지급대장", AppConfig.PayCols);

            // 근로자목록 키 집합 (없으면 마스터에서 채워 넣는다) — 이름+전화번호
            var (wlCols, wlRows) = db.SelectStrings("근로자목록");
            int wlNi = Array.IndexOf(wlCols, "이름"), wlPi = Array.IndexOf(wlCols, "전화번호");
            string WlV(string[] r, int i) => i >= 0 && i < r.Length ? r[i] : "";
            var wlKeys = wlRows.Select(r => AppConfig.WorkerKey(WlV(r, wlNi), WlV(r, wlPi))).ToHashSet();

            // 기존 지급대장 → 이름+전화번호별
            var (payCols, payRows) = db.SelectStrings("지급대장");
            int pNi = Array.IndexOf(payCols, "이름"), pPi = Array.IndexOf(payCols, "전화번호");
            var payDict = new Dictionary<string, string[]>();
            foreach (var r in payRows)
            {
                var d = new string[AppConfig.PayCols.Length];
                for (int i = 0; i < AppConfig.PayCols.Length; i++)
                {
                    int ci = Array.IndexOf(payCols, AppConfig.PayCols[i]);
                    d[i] = ci >= 0 && ci < r.Length ? r[ci] : "";
                }
                if (pNi >= 0 && pNi < r.Length)
                    payDict[AppConfig.WorkerKey(r[pNi], WlV(r, pPi))] = d;
            }

            int seq = payDict.Count;
            foreach (var r in current)
            {
                string key = AppConfig.WorkerKey(r.Name, r.Phone);
                bool hasPattern = r.Times.Any(t => !string.IsNullOrEmpty(t));

                // 주간표에 시간이 없는 사람은 이미 지급대장에 있을 때만 손댄다.
                // (요일 시간이 비었다고 새 인원을 지급대장에 끌어들이지 않는다)
                if (!hasPattern && !payDict.ContainsKey(key)) continue;

                // 근로자목록에 없으면 추가 — 시간이 있는 사람만
                if (hasPattern && !wlKeys.Contains(key))
                {
                    var w = new string[AppConfig.WorkerCols.Length];
                    void W(string c, string v) { int i = Array.IndexOf(AppConfig.WorkerCols, c); if (i >= 0) w[i] = v; }
                    W("이름", r.Name); W("생년월일6자리", r.Jumin); W("거주지역", r.Addr);
                    W("전화번호", r.Phone); W("시급", r.Wage); W("상태", "현재근무");
                    db.InsertRow("근로자목록", AppConfig.WorkerCols, w);
                    wlKeys.Add(key);
                }

                // 지급대장 행 가져오거나 새로
                if (!payDict.TryGetValue(key, out var d))
                {
                    d = new string[AppConfig.PayCols.Length];
                    for (int i = 0; i < d.Length; i++) d[i] = "";
                    if (P_SEQ >= 0) d[P_SEQ] = (++seq).ToString();
                    payDict[key] = d;
                }
                if (P_NAME  >= 0) d[P_NAME]  = r.Name;
                if (P_JUMIN >= 0) d[P_JUMIN] = r.Jumin;
                if (P_PHONE >= 0) d[P_PHONE] = r.Phone;
                if (P_ADDR  >= 0) d[P_ADDR]  = r.Addr;
                // 마스터에 시급이 없으면 지급대장에 입력된 시급을 덮어쓰지 않는다
                if (P_WAGE  >= 0 && !string.IsNullOrWhiteSpace(r.Wage)) d[P_WAGE] = r.Wage;
                if (P_AUTO  >= 0) d[P_AUTO]  = "1";
                if (P_BREAK >= 0) d[P_BREAK] = "1";

                // 대상 날짜만 요일 시간으로 채우기 (그 외 날짜는 건드리지 않음).
                // 요일 시간이 비어 있으면 그 날짜도 빈 값이 되어 근무가 지워진다.
                foreach (int day in targetDays)
                {
                    int wd = ((int)new DateTime(_year, _month, day).DayOfWeek + 6) % 7;  // 월=0
                    int ci = P_DAY(day);
                    if (ci < 0) continue;
                    d[ci] = r.Times[wd];
                }
                // 근무시작일 = 채워진 전체 날짜 기준 첫 근무일 재계산
                if (P_START >= 0)
                {
                    int fd = 0;
                    for (int day = 1; day <= lastDay; day++)
                    {
                        int ci = P_DAY(day);
                        if (ci >= 0 && !string.IsNullOrEmpty(d[ci])) { fd = day; break; }
                    }
                    d[P_START] = fd > 0 ? $"{fd}일" : "";
                }
            }

            // 적용 결과 그 달에 근무시간이 하나도 남지 않은 사람은 이번 달 인원에서 뺀다.
            // (다른 주에 직접 입력한 근무가 남아 있으면 그대로 유지된다)
            foreach (var r in current)
            {
                string key = AppConfig.WorkerKey(r.Name, r.Phone);
                if (!payDict.TryGetValue(key, out var d)) continue;
                bool anyHours = false;
                for (int day = 1; day <= lastDay && !anyHours; day++)
                {
                    int ci = P_DAY(day);
                    if (ci >= 0 && !string.IsNullOrEmpty(d[ci])) anyHours = true;
                }
                if (anyHours) continue;
                payDict.Remove(key);
                db.Execute("DELETE FROM \"근로자목록\" WHERE \"이름\"=@p0 AND \"전화번호\"=@p1",
                    r.Name, r.Phone);
            }

            // 지급대장 재작성
            db.Execute("DELETE FROM \"지급대장\"");
            foreach (var d in payDict.Values)
                db.InsertRow("지급대장", AppConfig.PayCols, d);

            MessageBox.Show(
                $"{desc} 근무시간에 적용했습니다. ({scheduled.Count}명)\n'근무시간 입력' 화면에서 확인하세요.",
                "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

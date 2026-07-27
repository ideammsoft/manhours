using manHours.Forms;

namespace manHours.Screens.Noim;

public class FileScreen : UserControl
{
    // ── 이벤트 ────────────────────────────────────────────
    public event Action<int, int, string>? GoNext;

    // ── 상태 ──────────────────────────────────────────────
    int    _year, _month;
    string _project = "";

    // 표시 컬럼: 자격증 제외 (10 = 총 일급여)
    static readonly int[] VisibleColIdx = [0, 1, 2, 3, 4, 5, 6, 7, 10]; // WorkerCols 인덱스
    static readonly int W_NAME  = Array.IndexOf(AppConfig.WorkerCols, "이름");
    static readonly int W_PHONE = Array.IndexOf(AppConfig.WorkerCols, "전화번호");
    // _grdSel/_grdAll 의 셀 인덱스 (0번은 체크박스라 +1)
    static readonly int G_NAME  = Array.IndexOf(VisibleColIdx, W_NAME)  + 1;
    static readonly int G_PHONE = Array.IndexOf(VisibleColIdx, W_PHONE) + 1;
    static readonly string[] VisibleColNames =
        VisibleColIdx.Select(i => AppConfig.WorkerCols[i]).ToArray();

    // ── 컨트롤 ────────────────────────────────────────────
    DataGridView _grdAll = null!, _grdSel = null!;
    CheckBox     _chkAllLeft = null!, _chkAllRight = null!;
    Label        _lblCount   = null!;
    TextBox      _txtSearch  = null!;
    Label        _lblDb      = null!;

    public FileScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    // ── UI ────────────────────────────────────────────────
    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 4,
            BackColor   = Color.Transparent,
            Padding     = new Padding(16, 12, 16, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // 제목 행
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // DB 경로
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 메인 스플릿
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // 하단 버튼

        // 제목 행
        var topRow = new FlowLayoutPanel
        {
            Dock         = DockStyle.Fill,
            WrapContents = false,
            BackColor    = Color.Transparent,
        };
        var title = new Label
        {
            Text      = "인원선택",
            Font      = ThemeManager.F(15f, FontStyle.Bold),
            ForeColor = ThemeManager.TextMain,
            AutoSize  = true,
            Margin    = new Padding(0, 2, 0, 0),
        };
        topRow.Controls.Add(title);
        root.Controls.Add(topRow, 0, 0);

        // DB 경로
        _lblDb = new Label
        {
            Text      = "",
            ForeColor = ThemeManager.TextSub,
            Font      = ThemeManager.F(8f),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(_lblDb, 0, 1);

        // 스플릿
        var split = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor     = ThemeManager.Border,
        };
        split.Panel1.BackColor = ThemeManager.BgMain;
        split.Panel2.BackColor = ThemeManager.BgMain;
        BuildLeft(split.Panel1);
        BuildRight(split.Panel2);
        root.Controls.Add(split, 0, 2);

        // 하단 버튼
        var btnRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor     = Color.Transparent,
            WrapContents  = false,
            Padding       = new Padding(0, 6, 0, 0),
        };
        var btnNext = MakeBtn("저장 & 다음 단계  →", 180, 30);
        btnNext.BackColor = Color.FromArgb(30, 100, 200);
        btnNext.ForeColor = Color.White;
        btnNext.Font = ThemeManager.F(10f, FontStyle.Bold);
        btnNext.Click += (_, _) => OnNext();
        btnRow.Controls.Add(btnNext);
        _lblCount = new Label
        {
            Text      = "선택된 인원: 0명",
            ForeColor = ThemeManager.TextSub,
            Font      = ThemeManager.F(10f),
            AutoSize  = true,
            Margin    = new Padding(0, 8, 16, 0),
        };
        btnRow.Controls.Add(_lblCount);
        root.Controls.Add(btnRow, 0, 3);

        Controls.Add(root);
    }

    void BuildLeft(SplitterPanel panel)
    {
        var lay = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 4,
            BackColor   = Color.Transparent,
            Padding     = new Padding(0, 0, 4, 0),
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 헤더
        var hdr = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hdr.Controls.Add(MakeSecLabel("전체 근로자 목록"));
        var btnRefresh = MakeBtn("↻ 새로고침", 100, 26);
        btnRefresh.Click += (_, _) => LoadData();
        hdr.Controls.Add(btnRefresh);
        lay.Controls.Add(hdr, 0, 0);

        // 전체선택
        var chkRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        _chkAllLeft = new CheckBox
        {
            Text      = "전체선택",
            Font      = ThemeManager.F(9.5f),
            ForeColor = ThemeManager.TextSub,
            AutoSize  = true,
            Margin    = new Padding(2, 4, 0, 0),
        };
        _chkAllLeft.CheckedChanged += (_, _) => ToggleAllLeft(_chkAllLeft.Checked);
        chkRow.Controls.Add(_chkAllLeft);
        lay.Controls.Add(chkRow, 0, 1);

        // 검색 + 추가
        var searchRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        searchRow.Controls.Add(new Label
        {
            Text = "성명으로 추가:", AutoSize = true,
            ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(9.5f),
            Margin = new Padding(0, 6, 4, 0),
        });
        _txtSearch = new TextBox
        {
            Width = 160, Height = 26,
            BackColor = ThemeManager.BgInput,
            ForeColor = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font = ThemeManager.F(9.5f),
            Margin = new Padding(0, 2, 4, 0),
        };
        _txtSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) AddSmart(); };
        searchRow.Controls.Add(_txtSearch);
        var btnAdd = MakeBtn("추가", 60, 26);
        btnAdd.Click += (_, _) => AddSmart();
        searchRow.Controls.Add(btnAdd);
        lay.Controls.Add(searchRow, 0, 2);

        // 그리드
        _grdAll = MakeGrid();
        SetupColumns(_grdAll, isLeft: true);
        _grdAll.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AddWorkerFromLeft(e.RowIndex); };
        _grdAll.MouseDown += OnGrdAllMouseDown;
        lay.Controls.Add(_grdAll, 0, 3);
        panel.Controls.Add(lay);
    }

    void BuildRight(SplitterPanel panel)
    {
        var lay = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 4,
            BackColor   = Color.Transparent,
            Padding     = new Padding(4, 0, 0, 0),
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hdr = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hdr.Controls.Add(MakeSecLabel("이번달 매장 근무 인원"));
        lay.Controls.Add(hdr, 0, 0);

        var chkRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        _chkAllRight = new CheckBox
        {
            Text      = "전체선택",
            Font      = ThemeManager.F(9.5f),
            ForeColor = ThemeManager.TextSub,
            AutoSize  = true,
            Margin    = new Padding(2, 4, 0, 0),
        };
        _chkAllRight.CheckedChanged += (_, _) => ToggleAllRight(_chkAllRight.Checked);
        chkRow.Controls.Add(_chkAllRight);
        lay.Controls.Add(chkRow, 0, 1);

        var hintRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hintRow.Controls.Add(new Label
        {
            Text = "더블클릭 또는 체크 후 제외 버튼", AutoSize = true,
            ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(9f),
            Margin = new Padding(0, 0, 0, 0),
        });
        var btnRemove = MakeBtn("제외", 60, 26);
        btnRemove.Click += (_, _) => RemoveSelected();
        hintRow.Controls.Add(btnRemove);
        lay.Controls.Add(hintRow, 0, 2);

        _grdSel = MakeGrid();
        SetupColumns(_grdSel, isLeft: false);
        _grdSel.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ConfirmRemove(e.RowIndex); };
        _grdSel.MouseDown += OnGrdSelMouseDown;
        lay.Controls.Add(_grdSel, 0, 3);
        panel.Controls.Add(lay);
    }

    // ── 그리드 세팅 ───────────────────────────────────────
    DataGridView MakeGrid()
    {
        var g = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            BackgroundColor       = ThemeManager.BgCell,
            GridColor             = ThemeManager.GridLine,
            BorderStyle           = BorderStyle.None,
            ColumnHeadersHeight   = 26,
            RowHeadersVisible     = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            ReadOnly              = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
            ScrollBars            = ScrollBars.Both,
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.BgHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.HeaderText;
        g.ColumnHeadersDefaultCellStyle.Font = ThemeManager.F(9f, FontStyle.Bold);
        g.DefaultCellStyle.BackColor   = ThemeManager.BgCell;
        g.DefaultCellStyle.ForeColor   = ThemeManager.TextMain;
        g.DefaultCellStyle.Font        = ThemeManager.F(9.5f);
        g.DefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        g.DefaultCellStyle.SelectionForeColor = ThemeManager.SelectFg;
        g.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.BgAltCell;
        return g;
    }

    void SetupColumns(DataGridView g, bool isLeft)
    {
        g.Columns.Clear();
        // 체크박스 컬럼
        var chk = new DataGridViewCheckBoxColumn
        {
            HeaderText = "☑", Width = 28, Resizable = DataGridViewTriState.False,
            ReadOnly = false,
        };
        g.Columns.Add(chk);

        // 데이터 컬럼 (visible only)
        int[] widths = [90, 110, 200, 80, 110, 80, 100, 70, 90]; // 이름,생년월일6자리,거주지역,은행,계좌번호,예금주,전화번호,근무구분,총 일급여
        for (int i = 0; i < VisibleColNames.Length; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = VisibleColNames[i],
                Width      = i < widths.Length ? widths[i] : 80,
                ReadOnly   = true,
                SortMode   = DataGridViewColumnSortMode.Automatic,
            };
            g.Columns.Add(col);
        }

        if (!isLeft) g.Columns[0].Width = 28;
    }

    // ── 데이터 로드 ───────────────────────────────────────
    public void SetContext(int year, int month, string project)
    {
        _year = year; _month = month; _project = project;
        _lblDb.Text = $"DB: {AppConfig.MonthlyDbPath(year, month, project)}";
        LoadData();
    }

    void LoadData()
    {
        LoadAllWorkers();
        LoadSelWorkers();
        UpdateCount();
    }

    void LoadAllWorkers()
    {
        _grdAll.Rows.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체근로자", AppConfig.WorkerCols);
        var (cols, rows) = db.SelectStrings("전체근로자");
        // 성명 가나다순 정렬 (한글 정렬)
        int nameCol = Array.IndexOf(cols, "이름");
        var ordered = nameCol >= 0
            ? rows.OrderBy(r => nameCol < r.Length ? r[nameCol] : "", StringComparer.Create(new System.Globalization.CultureInfo("ko-KR"), false))
            : rows.AsEnumerable();
        foreach (var row in ordered)
        {
            var cells = new object[VisibleColIdx.Length + 1];
            cells[0] = false;
            for (int i = 0; i < VisibleColIdx.Length; i++)
            {
                int idx = Array.IndexOf(cols, AppConfig.WorkerCols[VisibleColIdx[i]]);
                cells[i + 1] = idx >= 0 && idx < row.Length ? row[idx] : "";
            }
            _grdAll.Rows.Add(cells);
        }
    }

    void LoadSelWorkers()
    {
        _grdSel.Rows.Clear();
        if (string.IsNullOrEmpty(_project)) return;
        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);
        if (!File.Exists(dbPath)) { AskPreviousMonth(); return; }
        using var db = new Database(dbPath);
        if (!db.TableExists("근로자목록")) { AskPreviousMonth(); return; }
        var (cols, rows) = db.SelectStrings("근로자목록");
        foreach (var row in rows) AddToSel(cols, row, false);
        if (_grdSel.Rows.Count == 0) AskPreviousMonth();
        UpdateCount();
    }

    void AddToSel(string[] cols, string[] row, bool isNew)
    {
        // 중복 체크 (이름+전화번호) — 동명이인은 서로 다른 사람으로 본다
        int nameIdx  = Array.IndexOf(cols, "이름");
        int phoneIdx = Array.IndexOf(cols, "전화번호");
        string name  = nameIdx  >= 0 && nameIdx  < row.Length ? row[nameIdx]  : "";
        string phone = phoneIdx >= 0 && phoneIdx < row.Length ? row[phoneIdx] : "";
        if (IsAlreadySelected(name, phone)) return;

        var cells = new object[VisibleColIdx.Length + 1];
        cells[0] = false;
        for (int i = 0; i < VisibleColIdx.Length; i++)
        {
            int idx = Array.IndexOf(cols, AppConfig.WorkerCols[VisibleColIdx[i]]);
            cells[i + 1] = idx >= 0 && idx < row.Length ? row[idx] : "";
        }
        int rIdx = _grdSel.Rows.Add(cells);
        if (isNew)
            _grdSel.Rows[rIdx].DefaultCellStyle.BackColor = ThemeManager.IsDark ? Color.FromArgb(20, 60, 30) : Color.FromArgb(195, 230, 200);
    }

    bool IsAlreadySelected(string name, string phone)
    {
        string key = AppConfig.WorkerKey(name, phone);
        for (int r = 0; r < _grdSel.Rows.Count; r++)
        {
            var row = _grdSel.Rows[r];
            if (AppConfig.WorkerKey(row.Cells[G_NAME].Value?.ToString(),
                                    row.Cells[G_PHONE].Value?.ToString()) == key) return true;
        }
        return false;
    }

    void AskPreviousMonth()
    {
        var prev = FindPreviousMonth();
        if (prev == null) return;
        var (py, pm, prevCols, prevRows) = prev.Value;
        var ans = MessageBox.Show($"{py}년 {pm:D2}월 인원 데이터를 불러올까요?",
            "이전 인원 불러오기", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ans != DialogResult.Yes) return;
        foreach (var row in prevRows) AddToSel(prevCols, row, true);
        UpdateCount();
    }

    (int year, int month, string[] cols, string[][] rows)? FindPreviousMonth()
    {
        int cy = _year, cm = _month;
        for (int i = 0; i < 24; i++)
        {
            cm--; if (cm == 0) { cm = 12; cy--; }
            var path = AppConfig.MonthlyDbPath(cy, cm, _project);
            if (!File.Exists(path)) continue;
            try
            {
                using var db = new Database(path);
                if (!db.TableExists("근로자목록")) continue;
                var (cols, rows) = db.SelectStrings("근로자목록");
                if (rows.Length > 0) return (cy, cm, cols, rows);
            }
            catch { }
        }
        return null;
    }

    // ── 인원 추가/제거 ────────────────────────────────────
    void AddWorkerFromLeft(int rowIdx)
    {
        var cells = new object[VisibleColIdx.Length + 1];
        cells[0] = false;
        for (int i = 0; i < VisibleColIdx.Length; i++)
            cells[i + 1] = _grdAll.Rows[rowIdx].Cells[i + 1].Value ?? "";

        if (IsAlreadySelected(cells[G_NAME]?.ToString() ?? "", cells[G_PHONE]?.ToString() ?? "")) return;

        int nr = _grdSel.Rows.Add(cells);
        _grdSel.Rows[nr].DefaultCellStyle.BackColor = ThemeManager.IsDark ? Color.FromArgb(20, 60, 30) : Color.FromArgb(195, 230, 200);
        UpdateCount();
    }

    void AddCheckedWorkers()
    {
        int added = 0;
        for (int r = 0; r < _grdAll.Rows.Count; r++)
        {
            if (_grdAll.Rows[r].Cells[0].Value is true)
            {
                AddWorkerFromLeft(r);
                _grdAll.Rows[r].Cells[0].Value = false;
                added++;
            }
        }
        if (added == 0) MessageBox.Show("추가할 항목을 체크하세요.", "알림");
        _chkAllLeft.Checked = false;
        UpdateCount();
    }

    // 체크된 항목이 있으면 그것들을, 없으면 성명 입력으로 추가한다.
    void AddSmart()
    {
        for (int r = 0; r < _grdAll.Rows.Count; r++)
            if (_grdAll.Rows[r].Cells[0].Value is true) { AddCheckedWorkers(); return; }
        AddByName();
    }

    void AddByName()
    {
        string name = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        for (int r = 0; r < _grdAll.Rows.Count; r++)
        {
            if (_grdAll.Rows[r].Cells[1].Value?.ToString() == name)
            {
                AddWorkerFromLeft(r);
                _txtSearch.Clear();
                UpdateCount();
                return;
            }
        }
        var ans = MessageBox.Show($"'{name}'이(가) 목록에 없습니다. 새로 추가하시겠습니까?",
            "없음", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ans != DialogResult.Yes) return;
        var dlg = new WorkerDialog(new string[] { name }
            .Concat(Enumerable.Repeat("", AppConfig.WorkerCols.Length - 1)).ToArray(), forceNew: true);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveNewWorker(dlg.Values);
            LoadAllWorkers();
            for (int r = 0; r < _grdAll.Rows.Count; r++)
                if (_grdAll.Rows[r].Cells[1].Value?.ToString() == dlg.Values[0])
                    AddWorkerFromLeft(r);
            UpdateCount();
        }
        _txtSearch.Clear();
    }

    void ConfirmRemove(int rowIdx)
    {
        string name = _grdSel.Rows[rowIdx].Cells[1].Value?.ToString() ?? "";
        var ans = MessageBox.Show($"\"{name}\"을(를) 현재 리스트에서 제외 하시겠습니까?",
            "제외 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (ans == DialogResult.Yes)
        {
            _grdSel.Rows.RemoveAt(rowIdx);
            UpdateCount();
        }
    }

    void RemoveSelected()
    {
        var checkedRows = new List<int>();
        for (int r = 0; r < _grdSel.Rows.Count; r++)
            if (_grdSel.Rows[r].Cells[0].Value is true) checkedRows.Add(r);
        if (checkedRows.Count > 0)
        {
            for (int i = checkedRows.Count - 1; i >= 0; i--) _grdSel.Rows.RemoveAt(checkedRows[i]);
            _chkAllRight.Checked = false;
        }
        else
        {
            var selected = _grdSel.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Index).OrderByDescending(i => i).ToList();
            foreach (var r in selected) _grdSel.Rows.RemoveAt(r);
        }
        UpdateCount();
    }

    void ToggleAllLeft(bool check)
    {
        foreach (DataGridViewRow r in _grdAll.Rows)
            r.Cells[0].Value = check;
    }

    void ToggleAllRight(bool check)
    {
        foreach (DataGridViewRow r in _grdSel.Rows)
            r.Cells[0].Value = check;
    }

    // ── 우클릭 컨텍스트 메뉴 ─────────────────────────────
    void OnGrdAllMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grdAll.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        _grdAll.ClearSelection();
        _grdAll.Rows[hit.RowIndex].Selected = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("정보수정").Click += (_, _) => EditWorkerFromLeft(hit.RowIndex);
        menu.Show(_grdAll, e.Location);
    }

    void OnGrdSelMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grdSel.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        _grdSel.ClearSelection();
        _grdSel.Rows[hit.RowIndex].Selected = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("정보수정").Click += (_, _) => EditWorkerFromSel(hit.RowIndex);
        menu.Show(_grdSel, e.Location);
    }

    void EditWorkerFromLeft(int rowIdx)
    {
        var vals = GetWorkerValues(_grdAll, rowIdx);
        var dlg = new WorkerDialog(vals);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveWorkerUpdate(dlg.Values, vals[W_NAME], vals[W_PHONE]);
            LoadData();
        }
    }

    void EditWorkerFromSel(int rowIdx)
    {
        var vals = GetWorkerValues(_grdSel, rowIdx);
        var dlg = new WorkerDialog(vals);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveWorkerUpdate(dlg.Values, vals[W_NAME], vals[W_PHONE]);
            var newVals = dlg.Values;
            for (int i = 0; i < VisibleColIdx.Length; i++)
            {
                int ci = VisibleColIdx[i];
                if (ci < newVals.Length)
                    _grdSel.Rows[rowIdx].Cells[i + 1].Value = newVals[ci];
            }
            LoadAllWorkers();
        }
    }

    // 그리드는 일부 컬럼만 보여준다. 화면에 없는 컬럼(시급·자격증·비고·상태)은
    // 전체근로자 원본에서 가져와야 편집·저장 때 지워지지 않는다.
    string[] GetWorkerValues(DataGridView g, int rowIdx)
    {
        var vals = new string[AppConfig.WorkerCols.Length];
        for (int i = 0; i < VisibleColIdx.Length; i++)
            vals[VisibleColIdx[i]] = g.Rows[rowIdx].Cells[i + 1].Value?.ToString() ?? "";
        MergeHiddenCols(vals);
        return vals;
    }

    // 전체근로자에서 같은 이름+전화번호 레코드를 찾아 화면에 없는 컬럼만 채운다.
    void MergeHiddenCols(string[] vals)
    {
        int ni = Array.IndexOf(AppConfig.WorkerCols, "이름");
        int pi = Array.IndexOf(AppConfig.WorkerCols, "전화번호");
        string key = AppConfig.WorkerKey(vals[ni], vals[pi]);
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("전체근로자", AppConfig.WorkerCols);
            var (cols, rows) = db.SelectStrings("전체근로자");
            int dni = Array.IndexOf(cols, "이름"), dpi = Array.IndexOf(cols, "전화번호");
            string V(string[] r, int i) => i >= 0 && i < r.Length ? r[i] : "";
            var src = rows.FirstOrDefault(r => AppConfig.WorkerKey(V(r, dni), V(r, dpi)) == key);
            if (src == null) return;
            for (int i = 0; i < AppConfig.WorkerCols.Length; i++)
            {
                if (VisibleColIdx.Contains(i)) continue;      // 화면 값이 우선
                int ci = Array.IndexOf(cols, AppConfig.WorkerCols[i]);
                vals[i] = ci >= 0 && ci < src.Length ? src[ci] : "";
            }
        }
        catch { }
    }

    // ── DB 저장 ───────────────────────────────────────────
    void SaveNewWorker(string[] vals)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체근로자", AppConfig.WorkerCols);
        db.InsertRow("전체근로자", AppConfig.WorkerCols, vals);
    }

    // 동명이인이 있으므로 이름만으로 UPDATE 하면 남의 정보를 덮어쓴다. 전화번호까지 함께 본다.
    void SaveWorkerUpdate(string[] newVals, string oldName, string oldPhone)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체근로자", AppConfig.WorkerCols);
        var sets = string.Join(", ", AppConfig.WorkerCols.Select((c, i) => $"\"{c}\"=@p{i}"));
        int n = AppConfig.WorkerCols.Length;
        db.Execute($"UPDATE \"전체근로자\" SET {sets} WHERE \"이름\"=@p{n} AND \"전화번호\"=@p{n + 1}",
            newVals.Cast<object?>().Append(oldName).Append(oldPhone).ToArray());
    }

    /// <summary>다른 화면으로 이동할 때 인원 선택 결과(제외 포함)를 저장한다.</summary>
    public void SaveRoster()
    {
        if (string.IsNullOrEmpty(_project)) return;
        // 목록이 비어 있으면 저장하지 않는다 — 화면을 열기만 해도 그 달 인원이 통째로 지워질 수 있다.
        if (_grdSel.Rows.Count == 0) return;
        try { SaveToDb(); } catch { }
    }

    void SaveToDb()
    {
        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);
        using var db = new Database(dbPath);
        db.EnsureTable("근로자목록", AppConfig.WorkerCols);
        db.Execute("DELETE FROM \"근로자목록\"");
        var keep = new HashSet<string>();
        for (int r = 0; r < _grdSel.Rows.Count; r++)
        {
            var vals = GetWorkerValues(_grdSel, r);
            db.InsertRow("근로자목록", AppConfig.WorkerCols, vals);
            keep.Add(AppConfig.WorkerKey(vals[W_NAME], vals[W_PHONE]));
        }
        PurgePayroll(db, keep);
    }

    // 인원선택에서 제외한 근로자는 그 달 지급대장에서도 빼준다.
    static void PurgePayroll(Database db, HashSet<string> keep)
    {
        if (!db.TableExists("지급대장")) return;
        var (cols, rows) = db.SelectStrings("지급대장");
        int ni = Array.IndexOf(cols, "이름"), pi = Array.IndexOf(cols, "전화번호");
        if (ni < 0) return;
        string V(string[] r, int i) => i >= 0 && i < r.Length ? r[i] : "";
        foreach (var row in rows)
        {
            string name = V(row, ni), phone = V(row, pi);
            if (keep.Contains(AppConfig.WorkerKey(name, phone))) continue;
            db.Execute("DELETE FROM \"지급대장\" WHERE \"이름\"=@p0 AND \"전화번호\"=@p1", name, phone);
        }
    }

    void OnNext()
    {
        if (_grdSel.Rows.Count == 0)
        {
            MessageBox.Show("투입 인원을 1명 이상 선택하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { SaveToDb(); }
        catch (Exception ex)
        {
            MessageBox.Show($"DB 저장 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        GoNext?.Invoke(_year, _month, _project);
    }

    void UpdateCount() => _lblCount.Text = $"선택된 인원: {_grdSel.Rows.Count}명";

    // ── 헬퍼 ─────────────────────────────────────────────
    static Button MakeBtn(string text, int w, int h) => new()
    {
        Text      = text,
        Width     = w, Height = h,
        FlatStyle = FlatStyle.Flat,
        BackColor = ThemeManager.BtnSide,
        ForeColor = ThemeManager.BtnText,
        Font      = ThemeManager.F(9f),
        Cursor    = Cursors.Hand,
        Margin    = new Padding(4, 0, 0, 0),
    };

    static Label MakeSecLabel(string text) => new()
    {
        Text      = text,
        AutoSize  = true,
        Font      = ThemeManager.F(10f, FontStyle.Bold),
        ForeColor = ThemeManager.Accent,
        Margin    = new Padding(0, 4, 8, 0),
    };
}

using manHours.Forms;

namespace manHours.Screens.Jangbi;

public class EquipmentFileScreen : UserControl
{
    public event Action<int, int, string>? GoNext;

    int    _year, _month;
    string _project = "";

    // 표시 컬럼 인덱스 (EquipmentCols 기준)
    static readonly int[] VisibleColIdx = [0, 5, 6, 7, 9, 10, 15];
    // 자재장비업체명, 자재장비구분, 자재장비코드, 건설기계구분, 임차구매품목, 청구금액, 지급유형
    static readonly int[] ColWidths     = [150, 70, 80, 100, 120, 80, 80];
    static readonly string[] VisibleColNames =
        VisibleColIdx.Select(i => AppConfig.EquipmentCols[i]).ToArray();

    DataGridView _grdAll = null!, _grdSel = null!;
    CheckBox     _chkAllLeft = null!, _chkAllRight = null!;
    Label        _lblCount   = null!;
    TextBox      _txtSearch  = null!;
    Label        _lblDb      = null!;

    public EquipmentFileScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Color.Transparent, Padding = new Padding(16, 12, 16, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var topRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        topRow.Controls.Add(new Label
        {
            Text = "장비선택", Font = ThemeManager.F(15f, FontStyle.Bold),
            ForeColor = ThemeManager.TextMain, AutoSize = true, Margin = new Padding(0, 2, 0, 0),
        });
        root.Controls.Add(topRow, 0, 0);

        _lblDb = new Label
        {
            Text = "", ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(8f),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(_lblDb, 0, 1);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterWidth = 6, BackColor = ThemeManager.Border,
        };
        split.Panel1.BackColor = ThemeManager.BgMain;
        split.Panel2.BackColor = ThemeManager.BgMain;
        BuildLeft(split.Panel1);
        BuildRight(split.Panel2);
        root.Controls.Add(split, 0, 2);

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0, 6, 0, 0),
        };
        var btnNext = MakeBtn("저장 & 다음 단계  →", 180, 30);
        btnNext.BackColor = Color.FromArgb(30, 100, 200);
        btnNext.ForeColor = Color.White;
        btnNext.Font = ThemeManager.F(10f, FontStyle.Bold);
        btnNext.Click += (_, _) => OnNext();
        btnRow.Controls.Add(btnNext);
        _lblCount = new Label
        {
            Text = "선택된 장비: 0건", ForeColor = ThemeManager.TextSub,
            Font = ThemeManager.F(10f), AutoSize = true, Margin = new Padding(0, 8, 16, 0),
        };
        btnRow.Controls.Add(_lblCount);
        root.Controls.Add(btnRow, 0, 3);

        Controls.Add(root);
    }

    void BuildLeft(SplitterPanel panel)
    {
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Color.Transparent, Padding = new Padding(0, 0, 4, 0),
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hdr = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hdr.Controls.Add(MakeSecLabel("전체 장비 목록"));
        var btnRefresh = MakeBtn("↻ 새로고침", 100, 26);
        btnRefresh.Click += (_, _) => LoadData();
        hdr.Controls.Add(btnRefresh);
        lay.Controls.Add(hdr, 0, 0);

        var chkRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        _chkAllLeft = new CheckBox
        {
            Text = "전체선택", Font = ThemeManager.F(9.5f), ForeColor = ThemeManager.TextSub,
            AutoSize = true, Margin = new Padding(2, 4, 0, 0),
        };
        _chkAllLeft.CheckedChanged += (_, _) => ToggleAllLeft(_chkAllLeft.Checked);
        chkRow.Controls.Add(_chkAllLeft);
        lay.Controls.Add(chkRow, 0, 1);

        var searchRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        searchRow.Controls.Add(new Label
        {
            Text = "업체명으로 추가:", AutoSize = true, ForeColor = ThemeManager.TextSub,
            Font = ThemeManager.F(9.5f), Margin = new Padding(0, 6, 4, 0),
        });
        _txtSearch = new TextBox
        {
            Width = 140, Height = 26, BackColor = ThemeManager.BgInput, ForeColor = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle, Font = ThemeManager.F(9.5f), Margin = new Padding(0, 2, 4, 0),
        };
        _txtSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) AddByName(); };
        searchRow.Controls.Add(_txtSearch);
        var btnAdd = MakeBtn("추가", 60, 26);
        btnAdd.Click += (_, _) => AddCheckedEquipment();
        searchRow.Controls.Add(btnAdd);
        lay.Controls.Add(searchRow, 0, 2);

        _grdAll = MakeGrid();
        SetupColumns(_grdAll);
        _grdAll.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AddFromLeft(e.RowIndex); };
        _grdAll.MouseDown += OnGrdAllMouseDown;
        lay.Controls.Add(_grdAll, 0, 3);
        panel.Controls.Add(lay);
    }

    void BuildRight(SplitterPanel panel)
    {
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Color.Transparent, Padding = new Padding(4, 0, 0, 0),
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hdr = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hdr.Controls.Add(MakeSecLabel("이번 현장 투입 장비"));
        lay.Controls.Add(hdr, 0, 0);

        var chkRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        _chkAllRight = new CheckBox
        {
            Text = "전체선택", Font = ThemeManager.F(9.5f), ForeColor = ThemeManager.TextSub,
            AutoSize = true, Margin = new Padding(2, 4, 0, 0),
        };
        _chkAllRight.CheckedChanged += (_, _) => ToggleAllRight(_chkAllRight.Checked);
        chkRow.Controls.Add(_chkAllRight);
        lay.Controls.Add(chkRow, 0, 1);

        var hintRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 7, 0, 0) };
        hintRow.Controls.Add(new Label
        {
            Text = "더블클릭 또는 체크 후 제외 버튼", AutoSize = true,
            ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(9f), Margin = new Padding(0, 0, 0, 0),
        });
        var btnRemove = MakeBtn("제외", 60, 26);
        btnRemove.Click += (_, _) => RemoveSelected();
        hintRow.Controls.Add(btnRemove);
        lay.Controls.Add(hintRow, 0, 2);

        _grdSel = MakeGrid();
        SetupColumns(_grdSel);
        _grdSel.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ConfirmRemove(e.RowIndex); };
        _grdSel.MouseDown += OnGrdSelMouseDown;
        lay.Controls.Add(_grdSel, 0, 3);
        panel.Controls.Add(lay);
    }

    DataGridView MakeGrid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, BackgroundColor = ThemeManager.BgCell,
            GridColor = ThemeManager.GridLine, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 26, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            ReadOnly = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            ScrollBars = ScrollBars.Both,
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.BgHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.HeaderText;
        g.ColumnHeadersDefaultCellStyle.Font = ThemeManager.F(9f, FontStyle.Bold);
        g.DefaultCellStyle.BackColor = ThemeManager.BgCell;
        g.DefaultCellStyle.ForeColor = ThemeManager.TextMain;
        g.DefaultCellStyle.Font = ThemeManager.F(9.5f);
        g.DefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        g.DefaultCellStyle.SelectionForeColor = ThemeManager.SelectFg;
        g.AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.BgAltCell;
        return g;
    }

    void SetupColumns(DataGridView g)
    {
        g.Columns.Clear();
        var chk = new DataGridViewCheckBoxColumn
        {
            HeaderText = "☑", Width = 28, Resizable = DataGridViewTriState.False, ReadOnly = false,
        };
        g.Columns.Add(chk);
        for (int i = 0; i < VisibleColNames.Length; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = VisibleColNames[i], Width = ColWidths[i], ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
            };
            g.Columns.Add(col);
        }
    }

    public void SetContext(int year, int month, string project)
    {
        _year = year; _month = month; _project = project;
        _lblDb.Text = $"DB: {AppConfig.EquipMonthlyDbPath(year, month, project)}";
        LoadData();
    }

    void LoadData()
    {
        LoadAllEquipment();
        LoadSelEquipment();
        UpdateCount();
    }

    void LoadAllEquipment()
    {
        _grdAll.Rows.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체장비목록", AppConfig.EquipmentCols);
        var (cols, rows) = db.SelectStrings("전체장비목록");
        foreach (var row in rows)
        {
            var cells = new object[VisibleColIdx.Length + 1];
            cells[0] = false;
            for (int i = 0; i < VisibleColIdx.Length; i++)
            {
                int idx = Array.IndexOf(cols, AppConfig.EquipmentCols[VisibleColIdx[i]]);
                cells[i + 1] = idx >= 0 && idx < row.Length ? row[idx] : "";
            }
            _grdAll.Rows.Add(cells);
        }
    }

    void LoadSelEquipment()
    {
        _grdSel.Rows.Clear();
        if (string.IsNullOrEmpty(_project)) return;
        var dbPath = AppConfig.EquipMonthlyDbPath(_year, _month, _project);
        if (!File.Exists(dbPath)) return;
        using var db = new Database(dbPath);
        if (!db.TableExists("장비목록")) return;
        var (cols, rows) = db.SelectStrings("장비목록");
        foreach (var row in rows) AddToSel(cols, row, false);
        UpdateCount();
    }

    void AddToSel(string[] cols, string[] row, bool isNew)
    {
        int nameIdx = Array.IndexOf(cols, "자재장비업체명");
        int codeIdx = Array.IndexOf(cols, "자재장비코드");
        string name = nameIdx >= 0 && nameIdx < row.Length ? row[nameIdx] : "";
        string code = codeIdx >= 0 && codeIdx < row.Length ? row[codeIdx] : "";
        for (int r = 0; r < _grdSel.Rows.Count; r++)
        {
            string selName = _grdSel.Rows[r].Cells[1].Value?.ToString() ?? "";
            string selCode = _grdSel.Rows[r].Cells[3].Value?.ToString() ?? "";
            if (selName == name && selCode == code) return;
        }

        var cells = new object[VisibleColIdx.Length + 1];
        cells[0] = false;
        for (int i = 0; i < VisibleColIdx.Length; i++)
        {
            int idx = Array.IndexOf(cols, AppConfig.EquipmentCols[VisibleColIdx[i]]);
            cells[i + 1] = idx >= 0 && idx < row.Length ? row[idx] : "";
        }
        int rIdx = _grdSel.Rows.Add(cells);
        if (isNew)
            _grdSel.Rows[rIdx].DefaultCellStyle.BackColor =
                ThemeManager.IsDark ? Color.FromArgb(20, 60, 30) : Color.FromArgb(195, 230, 200);
    }

    void AddFromLeft(int rowIdx)
    {
        var cells = new object[VisibleColIdx.Length + 1];
        cells[0] = false;
        for (int i = 0; i < VisibleColIdx.Length; i++)
            cells[i + 1] = _grdAll.Rows[rowIdx].Cells[i + 1].Value ?? "";

        string name = cells[1]?.ToString() ?? "";
        string code = cells[3]?.ToString() ?? "";
        for (int r = 0; r < _grdSel.Rows.Count; r++)
        {
            if (_grdSel.Rows[r].Cells[1].Value?.ToString() == name &&
                _grdSel.Rows[r].Cells[3].Value?.ToString() == code) return;
        }
        int nr = _grdSel.Rows.Add(cells);
        _grdSel.Rows[nr].DefaultCellStyle.BackColor =
            ThemeManager.IsDark ? Color.FromArgb(20, 60, 30) : Color.FromArgb(195, 230, 200);
        UpdateCount();
    }

    void AddCheckedEquipment()
    {
        int added = 0;
        for (int r = 0; r < _grdAll.Rows.Count; r++)
        {
            if (_grdAll.Rows[r].Cells[0].Value is true)
            {
                AddFromLeft(r);
                _grdAll.Rows[r].Cells[0].Value = false;
                added++;
            }
        }
        if (added == 0) MessageBox.Show("추가할 항목을 체크하세요.", "알림");
        _chkAllLeft.Checked = false;
        UpdateCount();
    }

    void AddByName()
    {
        string name = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        bool found = false;
        for (int r = 0; r < _grdAll.Rows.Count; r++)
        {
            if (_grdAll.Rows[r].Cells[1].Value?.ToString()?.Contains(name, StringComparison.OrdinalIgnoreCase) == true)
            {
                AddFromLeft(r);
                found = true;
            }
        }
        if (found) { _txtSearch.Clear(); UpdateCount(); }
        else
        {
            var ans = MessageBox.Show($"'{name}'이(가) 목록에 없습니다. 새로 추가하시겠습니까?",
                "없음", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;
            var dlg = new EquipmentDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                SaveNewEquipment(dlg.Values);
                LoadAllEquipment();
            }
            _txtSearch.Clear();
        }
    }

    void ConfirmRemove(int rowIdx)
    {
        string name = _grdSel.Rows[rowIdx].Cells[1].Value?.ToString() ?? "";
        var ans = MessageBox.Show($"\"{name}\"을(를) 현재 리스트에서 제외 하시겠습니까?",
            "제외 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (ans == DialogResult.Yes) { _grdSel.Rows.RemoveAt(rowIdx); UpdateCount(); }
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

    void ToggleAllLeft(bool check)  { foreach (DataGridViewRow r in _grdAll.Rows) r.Cells[0].Value = check; }
    void ToggleAllRight(bool check) { foreach (DataGridViewRow r in _grdSel.Rows) r.Cells[0].Value = check; }

    void OnGrdAllMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grdAll.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        _grdAll.ClearSelection(); _grdAll.Rows[hit.RowIndex].Selected = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("정보수정").Click += (_, _) => EditFromLeft(hit.RowIndex);
        menu.Show(_grdAll, e.Location);
    }

    void OnGrdSelMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grdSel.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        _grdSel.ClearSelection(); _grdSel.Rows[hit.RowIndex].Selected = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("정보수정").Click += (_, _) => EditFromSel(hit.RowIndex);
        menu.Show(_grdSel, e.Location);
    }

    void EditFromLeft(int rowIdx)
    {
        string name = _grdAll.Rows[rowIdx].Cells[1].Value?.ToString() ?? "";
        var vals = LoadEquipFromDb(name) ?? GetFullValues(_grdAll, rowIdx);
        var dlg = new EquipmentDialog(vals);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveEquipmentUpdate(dlg.Values, vals[0]);
            LoadData();
        }
    }

    void EditFromSel(int rowIdx)
    {
        string name = _grdSel.Rows[rowIdx].Cells[1].Value?.ToString() ?? "";
        var vals = LoadEquipFromDb(name) ?? GetFullValues(_grdSel, rowIdx);
        var dlg = new EquipmentDialog(vals);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SaveEquipmentUpdate(dlg.Values, vals[0]);
            for (int i = 0; i < VisibleColIdx.Length; i++)
            {
                int ci = VisibleColIdx[i];
                if (ci < dlg.Values.Length)
                    _grdSel.Rows[rowIdx].Cells[i + 1].Value = dlg.Values[ci];
            }
            LoadAllEquipment();
        }
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

    string[] GetFullValues(DataGridView g, int rowIdx)
    {
        var vals = new string[AppConfig.EquipmentCols.Length];
        for (int i = 0; i < VisibleColIdx.Length; i++)
            vals[VisibleColIdx[i]] = g.Rows[rowIdx].Cells[i + 1].Value?.ToString() ?? "";
        return vals;
    }

    void SaveNewEquipment(string[] vals)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체장비목록", AppConfig.EquipmentCols);
        db.InsertRow("전체장비목록", AppConfig.EquipmentCols, vals);
    }

    void SaveEquipmentUpdate(string[] newVals, string oldName)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체장비목록", AppConfig.EquipmentCols);
        var sets = string.Join(", ", AppConfig.EquipmentCols.Select((c, i) => $"\"{c}\"=@p{i}"));
        db.Execute($"UPDATE \"전체장비목록\" SET {sets} WHERE \"자재장비업체명\"=@p{AppConfig.EquipmentCols.Length}",
            newVals.Cast<object?>().Append(oldName).ToArray());
    }

    void SaveToDb()
    {
        var dbPath = AppConfig.EquipMonthlyDbPath(_year, _month, _project);
        using var db = new Database(dbPath);
        db.EnsureTable("장비목록", AppConfig.EquipmentCols);
        db.Execute("DELETE FROM \"장비목록\"");
        for (int r = 0; r < _grdSel.Rows.Count; r++)
        {
            var vals = new string[AppConfig.EquipmentCols.Length];
            for (int i = 0; i < VisibleColIdx.Length; i++)
                vals[VisibleColIdx[i]] = _grdSel.Rows[r].Cells[i + 1].Value?.ToString() ?? "";

            // 전체장비목록에서 전체 데이터 불러와 보완
            string name = vals[0];
            using var baseDb = new Database(AppConfig.BaseDbPath);
            var (cols, rows) = baseDb.SelectStrings("전체장비목록",
                $"\"자재장비업체명\"='{name.Replace("'", "''")}' LIMIT 1");
            if (rows.Length > 0)
                for (int ci = 0; ci < AppConfig.EquipmentCols.Length; ci++)
                {
                    int idx = Array.IndexOf(cols, AppConfig.EquipmentCols[ci]);
                    if (idx >= 0 && string.IsNullOrEmpty(vals[ci]))
                        vals[ci] = rows[0][idx] ?? "";
                }

            db.InsertRow("장비목록", AppConfig.EquipmentCols, vals);
        }
    }

    void OnNext()
    {
        if (_grdSel.Rows.Count == 0)
        {
            MessageBox.Show("투입 장비를 1건 이상 선택하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    void UpdateCount() => _lblCount.Text = $"선택된 장비: {_grdSel.Rows.Count}건";

    static Button MakeBtn(string text, int w, int h) => new()
    {
        Text = text, Width = w, Height = h, FlatStyle = FlatStyle.Flat,
        BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
        Font = ThemeManager.F(9f), Cursor = Cursors.Hand, Margin = new Padding(4, 0, 0, 0),
    };

    static Label MakeSecLabel(string text) => new()
    {
        Text = text, AutoSize = true, Font = ThemeManager.F(10f, FontStyle.Bold),
        ForeColor = ThemeManager.Accent, Margin = new Padding(0, 4, 8, 0),
    };
}

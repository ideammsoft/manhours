namespace manHours.Forms;

public class SettingsDialog : Form
{
    readonly int    _year;
    readonly int    _month;
    readonly string _project;

    TabControl _tabs = null!;

    // Tab 0 – 전체근로자목록
    DataGridView _grdWorkers = null!;

    // Tab 1 – 일반설정
    TextBox _txtExcelDir = null!, _txtBaseDir = null!;
    Label   _lblFontSz   = null!;
    Button  _btnThemeDlg = null!;

    // Tab 2 – 사업명관리
    ListBox  _lstProjects = null!;

    // Tab 3 – 코드관리 (은행 / 근무구분 / 자격증)
    DataGridView _grdBank = null!, _grdWorkType = null!, _grdCert = null!;

    // Tab 4 – 법정기준(연도별)
    DataGridView _grdLegal = null!;
    TextBox  _txtNewProj  = null!;
    Label    _lblBusiName = null!;
    readonly Dictionary<string, TextBox> _busiInputs = new();
    Button   _btnCopyBusi = null!, _btnSaveBusi = null!;

    // ── 사업별 폼 그룹 (label, dbKey) ──────────────────────
    static readonly (string Label, string Key)[][] BusiGroups =
    [
        [("상호","상호"),("대표자","대표자"),("거주지역","거주지역"),
         ("전화번호","전화번호"),("현장대리인","현장대리인"),
         ("상시근로자수(5인 이상이면 가산수당 적용)","상시근로자수")],
        [("소득세(%)","소득세"),("주민세(%)","주민세"),("국민연금(%)","국민연금"),
         ("건강보험(%)","건강보험"),("고용보험(%)","고용보험"),("장기요양(건강보험료 대비 %)","장기요양")],
        [("사업장관리번호","사업장관리번호"),("명칭","명칭"),("사업장등록번호","사업장등록번호"),
         ("소재지","소재지"),("유선전화번호","유선전화번호"),("FAX번호","FAX번호"),
         ("매장유형","매장유형"),("근무장소","근무장소"),("임금지급일","임금지급일")],
    ];
    static readonly Dictionary<string, string> BusiDefaults = new()
    {
        ["소득세"] = "2.7", ["주민세"] = "10.0", ["국민연금"] = "4.5",
        // 장기요양은 '건강보험료 대비 %' 로 계산된다(care = health × 률/100).
        // 기존 기본값 0.4591(보수 대비 환산율)은 잘못된 값이라 12.95 로 바로잡음.
        ["건강보험"] = "3.545", ["고용보험"] = "0.9", ["장기요양"] = "12.95",
    };

    public SettingsDialog(int year, int month, string project)
    {
        _year = year; _month = month; _project = project;
        Text            = "설정";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterParent;
        Size            = new Size(1040, 650);
        MinimumSize     = new Size(720, 500);
        MaximizeBox     = false;
        BackColor       = ThemeManager.BgMain;
        Build();
        LoadAll();
        WireDirty(this);   // 로드 후에 걸어야 초기 로딩이 변경으로 잡히지 않음
    }

    // ── 변경 감지 + 닫을 때 저장 확인 ──────────────────────
    bool _dirty;

    void WireDirty(Control root)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox tb:      tb.TextChanged += (_, _) => _dirty = true; break;
                case ComboBox cmb:    cmb.SelectedIndexChanged += (_, _) => _dirty = true; break;
                case CheckBox cb:     cb.CheckedChanged += (_, _) => _dirty = true; break;
                case DataGridView g:
                    g.CellValueChanged            += (_, _) => _dirty = true;
                    g.CurrentCellDirtyStateChanged += (_, _) => _dirty = true;
                    g.RowsRemoved                 += (_, _) => _dirty = true;
                    break;
            }
            WireDirty(c);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_dirty && DialogResult != DialogResult.OK)
        {
            var r = MessageBox.Show("변경한 내용을 저장하시겠습니까?", "설정",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) { e.Cancel = true; return; }
            if (r == DialogResult.Yes)    { DoSave(); DialogResult = DialogResult.OK; }
        }
        base.OnFormClosing(e);
    }

    // ── UI 구성 ───────────────────────────────────────────
    void Build()
    {
        _tabs = new TabControl
        {
            Dock    = DockStyle.Fill,
            Font    = new Font("맑은 고딕", 10f),
            Padding = new Point(12, 5),
        };
        ThemeManager.ApplyTabControl(_tabs);

        _tabs.TabPages.Add(BuildGeneralTab());
        _tabs.TabPages.Add(BuildProjectTab());
        _tabs.TabPages.Add(BuildWorkerTab());
        _tabs.TabPages.Add(BuildCodeTab());
        _tabs.TabPages.Add(BuildLegalTab());

        var bottom = new Panel { Height = 44, Dock = DockStyle.Bottom, BackColor = ThemeManager.BgBottom };
        var btnSave = new Button
        {
            Text = "설정 저장", Width = 100, Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 100, 60),
            ForeColor = Color.White,
            Font = new Font("맑은 고딕", 10f, FontStyle.Bold),
        };
        btnSave.Click += (_, _) => SaveAll();
        btnSave.Left = bottom.Width - 114; btnSave.Top = 7;
        bottom.Resize += (_, _) => btnSave.Left = bottom.Width - 114;
        bottom.Controls.Add(btnSave);

        Controls.Add(_tabs);
        Controls.Add(bottom);
    }

    // ── Tab 0: 전체근로자목록 ─────────────────────────────
    TabPage BuildWorkerTab()
    {
        var tp = new TabPage("전체 근로자 목록")
            { BackColor = ThemeManager.BgMain, Padding = new Padding(8) };
        var root = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = ThemeManager.BgMain };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        btnRow.Controls.Add(MakeBtn("추가",    60, AddWorker));
        btnRow.Controls.Add(MakeBtn("수정",    60, EditWorker));
        btnRow.Controls.Add(MakeBtn("삭제",    60, DeleteWorker));
        btnRow.Controls.Add(MakeBtn("새로고침", 80, LoadWorkers));
        root.Controls.Add(btnRow, 0, 0);

        _grdWorkers = new DataGridView
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 26, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };
        ThemeManager.ApplyGrid(_grdWorkers);
        _grdWorkers.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9f, FontStyle.Bold);
        _grdWorkers.DefaultCellStyle.Font              = new Font("맑은 고딕", 9.5f);
        _grdWorkers.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditWorker(); };

        // 생년월일은 글자폭만큼, 거주지역은 1/2로 줄여 뒤쪽(자격증·시급·총 일급여·비고)까지 보이게
        int[] widths = [65, 100, 80, 70, 100, 65, 95, 80, 80, 60, 70, 70];
        for (int i = 0; i < AppConfig.WorkerCols.Length; i++)
            _grdWorkers.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = AppConfig.WorkerCols[i],
                Width = i < widths.Length ? widths[i] : 80,
            });

        root.Controls.Add(_grdWorkers, 0, 1);
        tp.Controls.Add(root);
        return tp;
    }

    // ── Tab 1: 일반설정 ───────────────────────────────────
    TabPage BuildGeneralTab()
    {
        var tp = new TabPage("일반 설정")
            { BackColor = ThemeManager.BgMain, Padding = new Padding(20, 16, 20, 8) };

        var tbl = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            ColumnCount = 2,
            RowCount    = 8,
            AutoSize    = true,
            BackColor   = ThemeManager.BgMain,
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: 버전 / 업데이트
        var verFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Height = 32 };
        var lblVer  = new Label
        {
            Text = $"v{AppConfig.Version}", AutoSize = true,
            Font = new Font("맑은 고딕", 12f, FontStyle.Bold),
            ForeColor = ThemeManager.Accent,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 12, 0),
        };
        var btnChkUpd = MakeBtn("업데이트 확인", 130, () => _ = CheckUpdateAsync());
        verFlow.Controls.Add(lblVer);
        verFlow.Controls.Add(btnChkUpd);
        tbl.Controls.Add(MakeLabel("프로그램 버전"), 0, 0);
        tbl.Controls.Add(verFlow, 1, 0);

        // Row 1: 구분선
        var sep1 = MakeSep();
        tbl.Controls.Add(sep1, 0, 1);
        tbl.SetColumnSpan(sep1, 2);

        // Row 2: 엑셀 저장 폴더명
        _txtExcelDir = MakeTxtBox("엑셀다운로드");
        tbl.Controls.Add(MakeLabel("엑셀 저장 폴더명"), 0, 2);
        tbl.Controls.Add(_txtExcelDir, 1, 2);

        // Row 3: 기본 실행 폴더
        var baseDirFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Height = 30 };
        _txtBaseDir = MakeTxtBox(AppConfig.BaseDir);
        _txtBaseDir.Dock = DockStyle.None;
        _txtBaseDir.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        var btnBrowse = MakeBtn("...", 36, () =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _txtBaseDir.Text };
            if (dlg.ShowDialog() == DialogResult.OK) _txtBaseDir.Text = dlg.SelectedPath;
        });
        baseDirFlow.Controls.Add(_txtBaseDir);
        baseDirFlow.Controls.Add(btnBrowse);
        baseDirFlow.Resize += (_, _) =>
            _txtBaseDir.Width = Math.Max(100, baseDirFlow.ClientSize.Width - btnBrowse.Width - 8);
        tbl.Controls.Add(MakeLabel("기본 실행 폴더"), 0, 3);
        tbl.Controls.Add(baseDirFlow, 1, 3);

        // Row 5: 구분선
        var sep2 = MakeSep();
        tbl.Controls.Add(sep2, 0, 5);
        tbl.SetColumnSpan(sep2, 2);

        // Row 6: 배경 색상 (테마 토글)
        _btnThemeDlg = new Button
        {
            Text      = ThemeManager.IsDark ? "🌙 어두운 테마 (현재)" : "☀ 밝은 테마 (현재)",
            Dock      = DockStyle.Fill,
            Height    = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 100, 200),
            ForeColor = Color.White,
            Font      = new Font("맑은 고딕", 10f),
            Margin    = new Padding(0, 2, 0, 2),
        };
        _btnThemeDlg.Click += (_, _) =>
        {
            AppSettings.Instance.Theme = ThemeManager.IsDark ? "light" : "dark";
            AppSettings.Instance.Save();
            DialogResult = DialogResult.Cancel;
            MainForm.ReApplyTheme();
        };
        tbl.Controls.Add(MakeLabel("배경 색상"), 0, 6);
        tbl.Controls.Add(_btnThemeDlg, 1, 6);

        // Row 7: 글자 크기
        var fontFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Height = 30 };
        var btnFontUp   = MakeBtn("가+  키우기", 100, () => ChangeFontSize(+1f));
        var btnFontDown = MakeBtn("가−  줄이기", 100, () => ChangeFontSize(-1f));
        _lblFontSz = new Label
        {
            Text      = $"현재 {AppSettings.Instance.FontSize:F0}px",
            AutoSize  = true,
            ForeColor = ThemeManager.TextSub,
            BackColor = Color.Transparent,
            Font      = new Font("맑은 고딕", 9.5f),
            Margin    = new Padding(8, 6, 0, 0),
        };
        fontFlow.Controls.Add(btnFontUp);
        fontFlow.Controls.Add(btnFontDown);
        fontFlow.Controls.Add(_lblFontSz);
        tbl.Controls.Add(MakeLabel("글자 크기"), 0, 7);
        tbl.Controls.Add(fontFlow, 1, 7);

        tp.Controls.Add(tbl);
        return tp;
    }

    void ChangeFontSize(float delta)
    {
        AppSettings.Instance.FontSize = Math.Clamp(AppSettings.Instance.FontSize + delta, 8f, 18f);
        AppSettings.Instance.Save();
        DialogResult = DialogResult.Cancel;
        MainForm.ReApplyTheme();
    }

    // ── Tab 2: 사업명관리 ─────────────────────────────────
    TabPage BuildProjectTab()
    {
        var tp = new TabPage("매장 관리")
            { BackColor = ThemeManager.BgMain, Padding = new Padding(8) };
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterWidth = 6, BackColor = ThemeManager.Border,
        };
        split.Panel1.BackColor = ThemeManager.BgMain;
        split.Panel2.BackColor = ThemeManager.BgMain;
        split.HandleCreated += (_, _) =>
        {
            try { split.SplitterDistance = Math.Max(25, split.Width / 2); } catch { }
        };

        // ── 좌: 매장 목록 ──
        var leftRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            BackColor = ThemeManager.BgMain,
        };
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        leftRoot.Controls.Add(new Label
        {
            Text = "매장 목록 (최대 50개)",
            ForeColor = ThemeManager.Accent,
            BackColor = Color.Transparent,
            Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _lstProjects = new ListBox
        {
            Dock      = DockStyle.Fill,
            BackColor = ThemeManager.BgCell,
            ForeColor = ThemeManager.TextMain,
            Font      = new Font("맑은 고딕", 10.5f),
            BorderStyle = BorderStyle.FixedSingle,
        };
        _lstProjects.SelectedIndexChanged += (_, _) => OnProjectSelChanged(_lstProjects.SelectedIndex);
        leftRoot.Controls.Add(_lstProjects, 0, 1);

        var addRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 7, 0, 0),
        };
        _txtNewProj = new TextBox
        {
            Width = 180, Height = 26,
            BackColor = ThemeManager.BgInput,
            ForeColor = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("맑은 고딕", 9.5f),
            PlaceholderText = "새 사업명 입력...",
            Margin = new Padding(0, 0, 4, 0),
        };
        _txtNewProj.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) AddProject(); };
        var btnPAdd = MakeBtn("추가", 48, AddProject);
        btnPAdd.BackColor = Color.FromArgb(30, 100, 60);
        btnPAdd.ForeColor = Color.White;
        var btnPDel = MakeBtn("삭제", 48, DeleteProject);
        btnPDel.BackColor = Color.FromArgb(150, 40, 40);
        btnPDel.ForeColor = Color.White;
        addRow.Controls.Add(_txtNewProj);
        addRow.Controls.Add(btnPAdd);
        addRow.Controls.Add(btnPDel);
        leftRoot.Controls.Add(addRow, 0, 2);
        split.Panel1.Controls.Add(leftRoot);

        // ── 우: 사업별 상세정보 ──
        var rightRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            BackColor = ThemeManager.BgMain,
            Padding = new Padding(8, 0, 0, 0),
        };
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _lblBusiName = new Label
        {
            Text      = "← 사업명을 선택하세요",
            ForeColor = ThemeManager.Accent,
            BackColor = ThemeManager.BgMain,
            Font      = new Font("맑은 고딕", 11f, FontStyle.Bold),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        rightRoot.Controls.Add(_lblBusiName, 0, 0);

        // 스크롤 폼
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = ThemeManager.BgMain,
        };
        var formTbl = new TableLayoutPanel
        {
            ColumnCount = 2, AutoSize = true,
            BackColor = ThemeManager.BgMain,
            Padding = new Padding(0, 4, 4, 4),
        };
        formTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        formTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scrollPanel.Resize += (_, _) =>
            formTbl.Width = Math.Max(300, scrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);

        int formRow = 0;
        for (int g = 0; g < BusiGroups.Length; g++)
        {
            if (g > 0)
            {
                var sep = new Panel { Height = 1, BackColor = ThemeManager.Border, Margin = new Padding(0, 4, 0, 4) };
                formTbl.Controls.Add(sep, 0, formRow);
                formTbl.SetColumnSpan(sep, 2);
                formRow++;
            }
            foreach (var (lbl, key) in BusiGroups[g])
            {
                var le = MakeTxtBox();
                le.Enabled = false;
                if (key == "임금지급일") le.PlaceholderText = "매월 25 일";   // 예시(실제 입력값 아님)
                _busiInputs[key] = le;
                formTbl.Controls.Add(MakeLabel(lbl), 0, formRow);
                formTbl.Controls.Add(le, 1, formRow);
                formRow++;
            }
        }
        formTbl.RowCount = formRow;

        scrollPanel.Controls.Add(formTbl);
        rightRoot.Controls.Add(scrollPanel, 0, 1);

        var btnBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 7, 0, 0),
        };
        _btnSaveBusi = MakeBtn("사업별 정보 저장", 130, () => SaveBusiSettings());
        _btnSaveBusi.BackColor = Color.FromArgb(30, 100, 200);
        _btnSaveBusi.ForeColor = Color.White;
        _btnSaveBusi.Enabled   = false;
        _btnCopyBusi = MakeBtn("다른 사업에서 복사", 140, CopyBusiSettings);
        _btnCopyBusi.Enabled   = false;
        btnBar.Controls.Add(_btnSaveBusi);
        btnBar.Controls.Add(_btnCopyBusi);
        rightRoot.Controls.Add(btnBar, 0, 2);

        split.Panel2.Controls.Add(rightRoot);
        tp.Controls.Add(split);
        return tp;
    }

    // ── 데이터 로드 ───────────────────────────────────────
    void LoadAll()
    {
        LoadWorkers();
        LoadGeneralSettings();
        LoadProjects();
        LoadCodes();
    }

    void LoadWorkers()
    {
        _grdWorkers.Rows.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체근로자", AppConfig.WorkerCols);
        var (cols, rows) = db.SelectStrings("전체근로자");
        foreach (var row in rows)
        {
            var cells = new object[AppConfig.WorkerCols.Length];
            for (int i = 0; i < AppConfig.WorkerCols.Length; i++)
            {
                int ci = Array.IndexOf(cols, AppConfig.WorkerCols[i]);
                cells[i] = ci >= 0 && ci < row.Length ? row[ci] : "";
            }
            _grdWorkers.Rows.Add(cells);
        }
    }

    void LoadGeneralSettings()
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("설정", ["키", "값"]);
        _txtExcelDir.Text = db.GetSetting("엑셀다운로드폴더") ?? "엑셀다운로드";
        _txtBaseDir.Text  = AppConfig.BaseDir;
    }

    void LoadProjects()
    {
        _lstProjects.Items.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("사업명", ["이름"]);
        var (_, rows) = db.SelectStrings("사업명");
        foreach (var row in rows)
            if (row.Length > 0 && !string.IsNullOrEmpty(row[0]))
                _lstProjects.Items.Add(row[0]);

        // 현재 매장유형이 목록에 있으면 선택
        int selIdx = _lstProjects.FindString(_project);
        if (selIdx >= 0) _lstProjects.SelectedIndex = selIdx;
    }

    void OnProjectSelChanged(int row)
    {
        if (row < 0)
        {
            _lblBusiName.Text = "← 사업명을 선택하세요";
            foreach (var le in _busiInputs.Values) { le.Enabled = false; le.Clear(); }
            _btnSaveBusi.Enabled = false;
            _btnCopyBusi.Enabled = false;
            return;
        }
        string projName = _lstProjects.Items[row].ToString()!;
        _lblBusiName.Text = $"사업명: {projName}";
        LoadBusiSettings(projName);
        foreach (var le in _busiInputs.Values) le.Enabled = true;
        _btnSaveBusi.Enabled = true;
        _btnCopyBusi.Enabled = true;
    }

    void LoadBusiSettings(string projName)
    {
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("사업설정", AppConfig.BusiSettingCols);
        var (cols, rows) = db.SelectStrings("사업설정",
            $"\"사업명\"='{projName.Replace("'", "''")}'");

        foreach (var (_, key) in BusiGroups.SelectMany(g => g))
        {
            string val = "";
            if (rows.Length > 0)
            {
                int ci = Array.IndexOf(cols, key);
                val = ci >= 0 ? rows[0][ci] ?? "" : "";
            }
            if (string.IsNullOrEmpty(val) && BusiDefaults.TryGetValue(key, out var def))
                val = def;
            _busiInputs[key].Text = val;
        }
    }

    void SaveBusiSettings(bool showMsg = true)
    {
        int row = _lstProjects.SelectedIndex;
        if (row < 0) return;
        string projName = _lstProjects.Items[row].ToString()!;

        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("사업설정", AppConfig.BusiSettingCols);
        var (_, existing) = db.SelectStrings("사업설정",
            $"\"사업명\"='{projName.Replace("'","''")}'");

        var vals = AppConfig.BusiSettingCols.Skip(1)
            .Select(c => (object?)(_busiInputs.TryGetValue(c, out var t) ? t.Text.Trim() : ""))
            .ToArray();

        if (existing.Length > 0)
        {
            var sets = string.Join(", ", AppConfig.BusiSettingCols.Skip(1).Select((c, i) => $"\"{c}\"=@p{i}"));
            db.Execute($"UPDATE \"사업설정\" SET {sets} WHERE \"사업명\"=@p{AppConfig.BusiSettingCols.Length - 1}",
                [.. vals, projName]);
        }
        else
        {
            var colStr = string.Join(", ", AppConfig.BusiSettingCols.Select(c => $"\"{c}\""));
            var ph     = string.Join(", ", AppConfig.BusiSettingCols.Select((_, i) => $"@p{i}"));
            db.Execute($"INSERT INTO \"사업설정\" ({colStr}) VALUES ({ph})",
                [(object?)projName, .. vals]);
        }
        if (showMsg)
            MessageBox.Show($"\"{projName}\" 정보가 저장되었습니다.", "저장",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void CopyBusiSettings()
    {
        int curRow = _lstProjects.SelectedIndex;
        if (curRow < 0) return;
        string curName = _lstProjects.Items[curRow].ToString()!;
        var sources = Enumerable.Range(0, _lstProjects.Items.Count)
            .Where(i => i != curRow)
            .Select(i => _lstProjects.Items[i].ToString()!)
            .ToArray();
        if (sources.Length == 0) { MessageBox.Show("복사할 다른 사업이 없습니다.", "알림"); return; }

        string? picked = sources.Length == 1
            ? sources[0]
            : ShowPicker("복사할 원본 사업을 선택하세요:", sources);
        if (picked == null) return;

        LoadBusiSettings(picked);
        MessageBox.Show($"\"{picked}\" → \"{curName}\" 복사되었습니다.\n저장 버튼으로 저장하세요.", "복사 완료",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    string? ShowPicker(string prompt, string[] items)
    {
        using var dlg = new Form
        {
            Text = "사업 선택", Width = 320, Height = 165,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(30, 30, 46),
        };
        dlg.Controls.Add(new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true,
            ForeColor = Color.FromArgb(205, 214, 244), BackColor = Color.Transparent,
            Font = new Font("맑은 고딕", 9.5f) });
        var cmb = new ComboBox { Location = new Point(12, 38), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 65), ForeColor = Color.FromArgb(205, 214, 244),
            Font = new Font("맑은 고딕", 10f) };
        foreach (var s in items) cmb.Items.Add(s);
        cmb.SelectedIndex = 0;
        var btnOk     = new Button { Text = "확인", Location = new Point(108, 82), Width = 85, Height = 28,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 100, 200), ForeColor = Color.White,
            DialogResult = DialogResult.OK, Font = new Font("맑은 고딕", 9.5f) };
        var btnCancel = new Button { Text = "취소", Location = new Point(198, 82), Width = 85, Height = 28,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 80), ForeColor = Color.FromArgb(205, 214, 244),
            DialogResult = DialogResult.Cancel, Font = new Font("맑은 고딕", 9.5f) };
        dlg.Controls.AddRange([cmb, btnOk, btnCancel]);
        dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
        return dlg.ShowDialog() == DialogResult.OK ? cmb.SelectedItem?.ToString() : null;
    }

    // ── Tab 4: 코드관리 ───────────────────────────────────
    TabPage BuildCodeTab()
    {
        var tp = new TabPage("코드 관리")
            { BackColor = ThemeManager.BgMain, Padding = new Padding(8) };

        var inner = new TabControl
        {
            Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 9.5f), Padding = new Point(10, 4),
        };
        ThemeManager.ApplyTabControl(inner);
        inner.TabPages.Add(BuildCodeSubTab("은행 코드", ref _grdBank,
            AppConfig.CodeBankCols, "코드_은행"));
        inner.TabPages.Add(BuildCodeSubTab("근무구분", ref _grdWorkType,
            AppConfig.CodeWorkTypeCols, "코드_근무구분"));
        inner.TabPages.Add(BuildCodeSubTab("자격증", ref _grdCert,
            AppConfig.CodeCertCols, "코드_자격증"));

        tp.Controls.Add(inner);
        return tp;
    }

    // ── Tab 4: 법정기준(연도별) ───────────────────────────
    TabPage BuildLegalTab()
    {
        var tp = new TabPage("법정기준(연도별)")
            { BackColor = ThemeManager.BgMain, Padding = new Padding(8) };
        var root = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = ThemeManager.BgMain };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        btnRow.Controls.Add(MakeBtn("연도 추가", 80, AddLegalYear));
        btnRow.Controls.Add(MakeBtn("삭제", 60, () =>
        {
            if (_grdLegal.SelectedRows.Count > 0) _grdLegal.Rows.Remove(_grdLegal.SelectedRows[0]);
        }));
        btnRow.Controls.Add(MakeBtn("저장", 60, () =>
            SaveCodeTable("법정기준", AppConfig.LegalStdCols, _grdLegal)));
        root.Controls.Add(btnRow, 0, 0);

        var note = new Label
        {
            Text = "※ 법정 수치는 매년 바뀝니다. 연도별로 행을 만들어 관리하세요. 해당 연도 행이 없으면 그 이전 중 가장 최근 연도 값을 사용합니다.\n"
                 + "※ 최저임금은 직접 입력하세요.   장기요양률은 '건강보험료 대비 %' 입니다 (보수 대비 아님).   요율은 사업설정에 값이 있으면 그쪽이 우선합니다.",
            Dock = DockStyle.Fill,
            ForeColor = ThemeManager.TextSub,
            Font = new Font("맑은 고딕", 8.5f),
            BackColor = Color.Transparent,
        };
        root.Controls.Add(note, 0, 1);

        _grdLegal = new DataGridView
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 42,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            ReadOnly = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };
        ThemeManager.ApplyGrid(_grdLegal);
        _grdLegal.ColumnHeadersDefaultCellStyle.Font     = new Font("맑은 고딕", 8.5f, FontStyle.Bold);
        _grdLegal.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grdLegal.DefaultCellStyle.Font                  = new Font("맑은 고딕", 9f);

        int[] w = [55, 75, 65, 70, 70, 75, 80, 60, 70, 70, 70, 70, 70, 60];
        for (int i = 0; i < AppConfig.LegalStdCols.Length; i++)
            _grdLegal.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = AppConfig.LegalStdCols[i],
                Name       = AppConfig.LegalStdCols[i],
                Width      = i < w.Length ? w[i] : 70,
            });

        root.Controls.Add(_grdLegal, 0, 2);
        tp.Controls.Add(root);
        return tp;
    }

    // 새 연도 행 추가 (기본값으로 채움, 최저임금은 비워 둠)
    void AddLegalYear()
    {
        var d = new AppConfig.LegalStandard();
        int y = DateTime.Now.Year;
        foreach (DataGridViewRow r in _grdLegal.Rows)
            if (!r.IsNewRow && int.TryParse(r.Cells[0].Value?.ToString(), out var ey) && ey >= y)
                y = ey + 1;
        _grdLegal.Rows.Add(
            y.ToString(), "",
            d.JuhyuHours, d.InsMinHours, d.InsMinDays, d.ExtraPayMinStaff,
            d.DailyDeduct, d.TaxRate, d.MinTaxCut,
            d.Kukmin, d.Health, d.Care, d.Employ, d.LocalTax);
    }

    TabPage BuildCodeSubTab(string title, ref DataGridView grid, string[] cols, string tableName)
    {
        var tp = new TabPage(title) { BackColor = ThemeManager.BgMain, Padding = new Padding(6) };
        var root = new TableLayoutPanel
            { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = ThemeManager.BgMain };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var g = new DataGridView
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 24, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            ReadOnly = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        ThemeManager.ApplyGrid(g);
        g.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 9f, FontStyle.Bold);
        g.DefaultCellStyle.Font = new Font("맑은 고딕", 9.5f);
        foreach (var c in cols)
            g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = c, Name = c });
        grid = g;

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent };
        string tn = tableName;
        string[] cs = cols;
        DataGridView gRef = g;
        btnRow.Controls.Add(MakeBtn("행 추가", 70, () =>
        {
            int r = gRef.Rows.Add(cs.Select(_ => (object)"").ToArray());
            gRef.CurrentCell = gRef.Rows[r].Cells[0];
            gRef.BeginEdit(true);
        }));
        btnRow.Controls.Add(MakeBtn("삭제", 60, () =>
        {
            if (gRef.SelectedRows.Count == 0) return;
            gRef.Rows.Remove(gRef.SelectedRows[0]);
        }));
        btnRow.Controls.Add(MakeBtn("저장", 60, () => SaveCodeTable(tn, cs, gRef)));
        root.Controls.Add(btnRow, 0, 0);
        root.Controls.Add(g, 0, 1);
        tp.Controls.Add(root);
        return tp;
    }

    void LoadCodes()
    {
        LoadCodeGrid(_grdBank,     AppConfig.CodeBankCols,     "코드_은행");
        LoadCodeGrid(_grdWorkType, AppConfig.CodeWorkTypeCols, "코드_근무구분");
        LoadCodeGrid(_grdCert,     AppConfig.CodeCertCols,     "코드_자격증");
        LoadCodeGrid(_grdLegal,    AppConfig.LegalStdCols,     "법정기준");
    }

    void LoadCodeGrid(DataGridView g, string[] cols, string tableName)
    {
        g.Rows.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable(tableName, cols);
        var (dbCols, rows) = db.SelectStrings(tableName);
        foreach (var row in rows)
        {
            var cells = cols.Select(c =>
            {
                int ci = Array.IndexOf(dbCols, c);
                return (object)(ci >= 0 && ci < row.Length ? row[ci] : "");
            }).ToArray();
            g.Rows.Add(cells);
        }
    }

    void SaveCodeTable(string tableName, string[] cols, DataGridView g)
    {
        SaveGridSilent(tableName, cols, g);
        MessageBox.Show($"[{tableName}] 저장되었습니다.", "저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void SaveGridSilent(string tableName, string[] cols, DataGridView g)
    {
        g.EndEdit();   // 편집 중이던 셀 값 확정 (안 하면 방금 입력한 값이 누락됨)
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable(tableName, cols);
        db.Execute($"DELETE FROM \"{tableName}\"");
        foreach (DataGridViewRow row in g.Rows)
        {
            if (row.IsNewRow) continue;
            var vals = cols.Select((_, i) => row.Cells[i].Value?.ToString() ?? "").ToArray();
            if (vals.All(string.IsNullOrEmpty)) continue;
            db.InsertRow(tableName, cols, vals);
        }
    }

    // ── 전체 저장 ─────────────────────────────────────────
    void SaveAll()
    {
        DoSave();
        MessageBox.Show("저장되었습니다.", "저장", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    // 모든 탭의 내용을 한 번에 저장 (하단 '설정 저장' 버튼이 탭별 저장을 대신함)
    void DoSave()
    {
        using (var db = new Database(AppConfig.BaseDbPath))
        {
            db.EnsureTable("설정", ["키", "값"]);
            db.SetSetting("엑셀다운로드폴더", _txtExcelDir.Text.Trim().Length > 0 ? _txtExcelDir.Text.Trim() : "엑셀다운로드");

            if (_lstProjects.SelectedIndex >= 0) SaveBusiSettings(showMsg: false);

            db.Execute("DELETE FROM \"사업명\"");
            for (int i = 0; i < _lstProjects.Items.Count; i++)
                db.Execute("INSERT INTO \"사업명\" (\"이름\") VALUES (@p0)", _lstProjects.Items[i].ToString());
        }

        // 법정기준 · 코드표도 함께 저장 (탭별 '저장' 버튼을 누르지 않아도 반영)
        SaveGridSilent("법정기준",     AppConfig.LegalStdCols,     _grdLegal);
        SaveGridSilent("코드_은행",     AppConfig.CodeBankCols,     _grdBank);
        SaveGridSilent("코드_근무구분", AppConfig.CodeWorkTypeCols, _grdWorkType);
        SaveGridSilent("코드_자격증",   AppConfig.CodeCertCols,     _grdCert);

        var newDir = _txtBaseDir.Text.Trim();
        if (!string.IsNullOrEmpty(newDir) && newDir != AppConfig.BaseDir)
        {
            AppConfig.SaveBaseDir(newDir);
            MessageBox.Show("기본 폴더가 변경되었습니다. 프로그램을 재시작해 주세요.", "알림");
        }
        _dirty = false;
    }

    // ── 근로자 조작 ───────────────────────────────────────
    void AddWorker()
    {
        var dlg = new WorkerDialog();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var vals = dlg.Values;
        if (string.IsNullOrEmpty(vals[0])) return;
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("전체근로자", AppConfig.WorkerCols);
        db.InsertRow("전체근로자", AppConfig.WorkerCols, vals);
        LoadWorkers();
    }

    void EditWorker()
    {
        if (_grdWorkers.SelectedRows.Count == 0) return;
        var row  = _grdWorkers.SelectedRows[0];
        var vals = Enumerable.Range(0, AppConfig.WorkerCols.Length)
            .Select(i => row.Cells[i].Value?.ToString() ?? "").ToArray();
        int pi = Array.IndexOf(AppConfig.WorkerCols, "전화번호");
        string oldName  = vals[0];
        string oldPhone = pi >= 0 && pi < vals.Length ? vals[pi] : "";
        var dlg = new WorkerDialog(vals);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var nv   = dlg.Values;
        using var db = new Database(AppConfig.BaseDbPath);
        int n = AppConfig.WorkerCols.Length;
        var sets = string.Join(", ", AppConfig.WorkerCols.Select((c, i) => $"\"{c}\"=@p{i}"));
        // 동명이인 보호: 예전 이름+전화번호로 정확히 한 명만 수정
        db.Execute($"UPDATE \"전체근로자\" SET {sets} WHERE \"이름\"=@p{n} AND IFNULL(\"전화번호\",'')=@p{n + 1}",
            nv.Cast<object?>().Append(oldName).Append(oldPhone).ToArray());
        LoadWorkers();
    }

    void DeleteWorker()
    {
        if (_grdWorkers.SelectedRows.Count == 0) return;
        int pi = Array.IndexOf(AppConfig.WorkerCols, "전화번호");
        var sel = _grdWorkers.SelectedRows[0];
        string name  = sel.Cells[0].Value?.ToString() ?? "";
        string phone = pi >= 0 ? sel.Cells[pi].Value?.ToString() ?? "" : "";
        var ans = MessageBox.Show($"\"{name}\"을(를) 전체근로자 목록에서 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (ans != DialogResult.Yes) return;
        using var db = new Database(AppConfig.BaseDbPath);
        // 동명이인 보호: 이름+전화번호로 정확히 한 명만 삭제
        db.Execute("DELETE FROM \"전체근로자\" WHERE \"이름\"=@p0 AND IFNULL(\"전화번호\",'')=@p1", name, phone);
        LoadWorkers();
    }

    // ── 사업명 조작 ───────────────────────────────────────
    void AddProject()
    {
        string name = _txtNewProj.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (_lstProjects.Items.Count >= 50) { MessageBox.Show("사업명은 최대 50개까지 등록할 수 있습니다.", "알림"); return; }
        for (int i = 0; i < _lstProjects.Items.Count; i++)
            if (_lstProjects.Items[i].ToString() == name) { MessageBox.Show("이미 존재하는 사업명입니다.", "알림"); return; }
        _lstProjects.Items.Add(name);
        _lstProjects.SelectedIndex = _lstProjects.Items.Count - 1;
        _txtNewProj.Clear();
    }

    void DeleteProject()
    {
        int row = _lstProjects.SelectedIndex;
        if (row < 0) { MessageBox.Show("삭제할 사업명을 선택하세요.", "알림"); return; }
        string name = _lstProjects.Items[row].ToString()!;
        var ans = MessageBox.Show($"\"{name}\" 사업명을 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (ans != DialogResult.Yes) return;
        _lstProjects.Items.RemoveAt(row);
    }

    // ── 업데이트 확인 ─────────────────────────────────────
    async Task CheckUpdateAsync()
    {
        var ver = AppConfig.Version;
        try
        {
            var info = await Updater.CheckAsync(ver);
            if (info == null)
            {
                MessageBox.Show("현재 최신 버전입니다.", "업데이트 확인",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Invoke(() => new UpdateDialog(ver, info).ShowDialog(this));
        }
        catch
        {
            MessageBox.Show("업데이트 서버에 연결할 수 없습니다.", "업데이트 확인",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    static Button MakeBtn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text = text, Width = w, Height = 28, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(9f), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 4, 0),
        };
        b.FlatAppearance.BorderColor = ThemeManager.Border;
        b.Click += (_, _) => onClick();
        return b;
    }

    static Label MakeLabel(string text) => new()
    {
        Text      = text, AutoSize = false, Height = 28, Dock = DockStyle.Fill,
        Font      = ThemeManager.F(9.5f),
        ForeColor = ThemeManager.BtnText,
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleRight,
        Margin    = new Padding(0, 2, 8, 2),
    };

    static TextBox MakeTxtBox(string val = "") => new()
    {
        Text        = val, Height = 26, Dock = DockStyle.Fill,
        BackColor   = ThemeManager.BgInput, ForeColor = ThemeManager.TextMain,
        BorderStyle = BorderStyle.FixedSingle, Font = ThemeManager.F(9.5f),
        Margin      = new Padding(0, 2, 0, 2),
    };

    static Panel MakeSep() =>
        new() { Height = 1, BackColor = ThemeManager.Border, Margin = new Padding(0, 8, 0, 8), Dock = DockStyle.Fill };
}

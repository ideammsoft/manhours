namespace manHours.Forms;

public class WorkerDialog : Form
{
    // ── 입력 필드 ──────────────────────────────────────────
    readonly TextBox[] _fields = new TextBox[AppConfig.WorkerCols.Length];
    readonly ComboBox  _cmbBank  = new();  // 은행명 콤보
    readonly ComboBox  _cmbWork  = new();  // 근무구분 콤보 (코드관리)
    readonly ComboBox  _cmbCert  = new();  // 자격증 콤보 (코드관리, 직접입력 가능)
    readonly ComboBox  _cmbStat  = new();  // 상태 콤보 (현재근무/휴직/퇴사)

    // ── 사진 (주민/자격증/통장) ────────────────────────────
    static readonly string[] PhotoTypes  = ["주민", "자격증", "통장"];
    static readonly string[] PhotoTitles =
        ["주민이미지(주민번호 뒷자리 가림)", "자격증 이미지", "통장 이미지"];
    readonly Image?[]     _photos    = new Image?[3];
    readonly PictureBox[] _photoBoxes = new PictureBox[3];
    readonly Button[]     _delBtns    = new Button[3];
    string? _savedName, _savedPhone;

    static readonly int IName   = Array.IndexOf(AppConfig.WorkerCols, "이름");
    static readonly int IBank   = Array.IndexOf(AppConfig.WorkerCols, "은행");
    static readonly int IPayee  = Array.IndexOf(AppConfig.WorkerCols, "예금주");
    static readonly int IWork   = Array.IndexOf(AppConfig.WorkerCols, "근무구분");
    static readonly int ICert   = Array.IndexOf(AppConfig.WorkerCols, "자격증");
    static readonly int IStat   = Array.IndexOf(AppConfig.WorkerCols, "상태");

    public string[] Values
    {
        get
        {
            var v = _fields.Select(f => f.Text.Trim()).ToArray();
            if (IBank  >= 0) v[IBank]  = _cmbBank.Text.Trim();
            if (IWork  >= 0) v[IWork]  = _cmbWork.Text.Trim();
            if (ICert  >= 0) v[ICert]  = _cmbCert.Text.Trim();
            if (IStat  >= 0) v[IStat]  = _cmbStat.Text.Trim();
            return v;
        }
    }

    /// <summary>코드관리 표에서 이름 목록을 읽는다. 비어 있으면 fallback 사용.</summary>
    static string[] LoadCodeNames(string table, string[] cols, string nameCol, string[] fallback)
    {
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable(table, cols);
            var (c, rows) = db.SelectStrings(table);
            int i = Array.IndexOf(c, nameCol);
            if (i < 0) return fallback;
            var list = rows.Select(r => i < r.Length ? r[i] : "")
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .Distinct().ToArray();
            return list.Length > 0 ? list : fallback;
        }
        catch { return fallback; }
    }

    public WorkerDialog(string[]? existingValues = null, bool forceNew = false)
    {
        Text            = (existingValues == null || forceNew) ? "근로자 정보 추가" : "근로자 정보 수정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        Size            = new Size(720, 700);   // 시급/비고/상태 추가로 항목 늘어남
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = ThemeManager.BgMain;

        Build(existingValues);

        if (existingValues != null && !forceNew)
        {
            _savedName  = GetField("이름");
            _savedPhone = GetField("전화번호");
            LoadPhotos();
        }
    }

    // ── UI ────────────────────────────────────────────────
    void Build(string[]? vals)
    {
        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
            BackColor   = Color.Transparent,
            Padding     = new Padding(16, 12, 16, 6),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));

        // ── 필드 영역 ──
        var tbl = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = AppConfig.WorkerCols.Length + 1,   // +1: 남는 공간 흡수용
            BackColor   = Color.Transparent,
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 각 입력행은 고정 높이. 마지막 여분행이 남는 공간을 먹어야
        // 마지막 항목(총 일급여/비고)의 라벨-입력칸 높이가 어긋나지 않는다.
        for (int r = 0; r < AppConfig.WorkerCols.Length; r++)
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        for (int i = 0; i < AppConfig.WorkerCols.Length; i++)
        {
            var lbl = new Label
            {
                Text      = AppConfig.WorkerCols[i],
                ForeColor = ThemeManager.TextSub,
                BackColor = Color.Transparent,
                Font      = ThemeManager.F(9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 2, 6, 2),
            };
            // 필수 항목 표시
            if (AppConfig.WorkerCols[i] is "이름" or "전화번호")
                lbl.ForeColor = Color.FromArgb(200, 80, 80);
            tbl.Controls.Add(lbl, 0, i);

            var txt = new TextBox
            {
                Text        = vals != null && i < vals.Length ? vals[i] : "",
                BackColor   = ThemeManager.BgInput,
                ForeColor   = ThemeManager.TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = ThemeManager.F(9.5f),
                Dock        = DockStyle.Fill,
                Margin      = new Padding(0, 2, 0, 2),
            };
            _fields[i] = txt;

            if (i == IBank)
            {
                SetupBankCombo(vals != null && i < vals.Length ? vals[i] : "");
                tbl.Controls.Add(_cmbBank, 1, i);
            }
            else if (i == IWork)
            {
                SetupWorkCombo(vals != null && i < vals.Length ? vals[i] : "");
                tbl.Controls.Add(_cmbWork, 1, i);
            }
            else if (i == ICert)
            {
                SetupCertCombo(vals != null && i < vals.Length ? vals[i] : "");
                tbl.Controls.Add(_cmbCert, 1, i);
            }
            else if (i == IStat)
            {
                SetupStatCombo(vals != null && i < vals.Length ? vals[i] : "");
                tbl.Controls.Add(_cmbStat, 1, i);
            }
            else
            {
                tbl.Controls.Add(txt, 1, i);
            }
        }

        // 이름 → 예금주 자동 입력: 예금주가 비어있거나 이전 이름과 같으면(=자동입력 상태) 계속 동기화
        if (IName >= 0 && IPayee >= 0)
        {
            string prevName = vals != null && IName < vals.Length ? vals[IName].Trim() : "";
            _fields[IName].TextChanged += (_, _) =>
            {
                string newName = _fields[IName].Text.Trim();
                if (string.IsNullOrEmpty(_fields[IPayee].Text) || _fields[IPayee].Text.Trim() == prevName)
                    _fields[IPayee].Text = newName;
                prevName = newName;
            };
        }

        root.Controls.Add(tbl, 0, 0);

        // ── 사진 영역 ──
        var photoFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 6, 0, 0),
        };

        for (int i = 0; i < PhotoTypes.Length; i++)
        {
            int idx = i;
            const int PW = 204, PH = 140, TH = 20, BW = 20, BH = 20;

            var container = new Panel
            {
                Width     = PW,
                Height    = PH,
                BackColor = ThemeManager.IsDark ? Color.FromArgb(22, 22, 38) : Color.FromArgb(230, 230, 240),
                Margin    = new Padding(0, 0, 10, 0),
            };

            var lblTitle = new Label
            {
                Text      = PhotoTitles[i],
                Left      = 0, Top = 0, Width = PW, Height = TH,
                ForeColor = Color.White,                       // 진한 바탕 + 흰 글씨로 대비 확보
                BackColor = Color.FromArgb(70, 100, 170),
                Font      = ThemeManager.F(8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
            };

            var pb = new PictureBox
            {
                Left   = 0, Top    = TH,
                Width  = PW, Height = PH - TH,
                Cursor = Cursors.Hand,
            };
            pb.Click     += (_, _) => SelectPhoto(idx);
            pb.AllowDrop  = true;
            pb.DragEnter += (_, e) => e.Effect = e.Data!.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            pb.DragDrop  += (_, e) =>
            {
                if (e.Data!.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    SetPhoto(idx, files[0]);
            };
            var noImgColor = ThemeManager.IsDark ? Color.FromArgb(22, 22, 38) : Color.FromArgb(230, 230, 240);
            pb.Paint += (s, e) =>
            {
                var p = (PictureBox)s!;
                e.Graphics.Clear(noImgColor);
                var img = _photos[idx];
                if (img == null)
                {
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    using var br = new SolidBrush(ThemeManager.TextSub);
                    e.Graphics.DrawString("이미지 없음\n(파일 끌어오기)", ThemeManager.F(8.5f), br, p.ClientRectangle, sf);
                    return;
                }
                float scale = Math.Min((float)p.Width / img.Width, (float)p.Height / img.Height);
                int w = (int)(img.Width * scale), h = (int)(img.Height * scale);
                e.Graphics.DrawImage(img, (p.Width - w) / 2, (p.Height - h) / 2, w, h);
            };
            _photoBoxes[i] = pb;

            var btnDel = new Button
            {
                Text      = "✕",
                Left      = PW - BW, Top = TH,
                Width     = BW, Height = BH,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                Font      = new Font("맑은 고딕", 7f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Visible   = false,
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (_, _) => DeletePhoto(idx);
            _delBtns[i]   = btnDel;

            // 파일/붙여넣기 버튼
            var btnFile  = MakeSmallBtn("파일",   60, () => SelectPhoto(idx));
            var btnPaste = MakeSmallBtn("붙여넣기", 70, () => PastePhoto(idx));
            btnFile.Left  = 0;  btnFile.Top  = PH - 22;
            btnPaste.Left = 64; btnPaste.Top = PH - 22;

            container.Controls.Add(pb);
            container.Controls.Add(lblTitle);
            container.Controls.Add(btnDel);
            container.Controls.Add(btnFile);
            container.Controls.Add(btnPaste);
            btnDel.BringToFront();

            photoFlow.Controls.Add(container);
        }
        root.Controls.Add(photoFlow, 0, 1);

        // ── 하단 버튼 ──
        var bottom = new Panel
        {
            Height    = 44,
            Dock      = DockStyle.Bottom,
            BackColor = ThemeManager.BgBottom,
        };
        var btnOk = new Button
        {
            Text      = "저장",
            Width     = 90, Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 100, 200),
            ForeColor = Color.White,
            Font      = ThemeManager.F(10f, FontStyle.Bold),
        };
        btnOk.Click += (_, _) => { SavePhotos(); DialogResult = DialogResult.OK; };
        var btnCancel = new Button
        {
            Text      = "취소",
            Width     = 80, Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(10f),
        };
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        btnOk.Left = bottom.Width - 190; btnCancel.Left = bottom.Width - 96;
        btnOk.Top = btnCancel.Top = 7;
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);
        bottom.Resize += (_, _) =>
        {
            btnOk.Left     = bottom.Width - 190;
            btnCancel.Left = bottom.Width - 96;
        };

        Controls.Add(root);
        Controls.Add(bottom);
    }

    // ── 은행 콤보박스 설정 ────────────────────────────────
    void SetupBankCombo(string currentVal)
    {
        _cmbBank.Dock        = DockStyle.Fill;
        _cmbBank.BackColor   = ThemeManager.BgInput;
        _cmbBank.ForeColor   = ThemeManager.TextMain;
        _cmbBank.Font        = ThemeManager.F(9.5f);
        _cmbBank.DropDownStyle    = ComboBoxStyle.DropDown;
        _cmbBank.Margin      = new Padding(0, 2, 0, 2);
        _cmbBank.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
        _cmbBank.AutoCompleteSource = AutoCompleteSource.ListItems;

        _cmbBank.Items.Add("");
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("코드_은행", AppConfig.CodeBankCols);
            var (_, rows) = db.SelectStrings("코드_은행");
            foreach (var row in rows.OrderBy(r => r.Length > 1 ? r[1] : "",
                                              StringComparer.CurrentCulture))
                if (row.Length > 1 && !string.IsNullOrEmpty(row[1]))
                    _cmbBank.Items.Add(row[1]);
        }
        catch { }
        _cmbBank.Text = currentVal;

        // 일부 입력 후 Enter → 부분 일치 첫 항목 선택
        _cmbBank.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Return && e.KeyCode != Keys.Enter) return;
            string typed = _cmbBank.Text.Trim();
            if (string.IsNullOrEmpty(typed)) return;
            foreach (var item in _cmbBank.Items)
            {
                string s = item?.ToString() ?? "";
                if (s.Contains(typed, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbBank.Text = s;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                }
            }
        };
    }

    // 근무구분: 코드관리(코드_근무구분)에서 목록을 읽어온다
    void SetupWorkCombo(string currentVal)
    {
        _cmbWork.Dock          = DockStyle.Fill;
        _cmbWork.BackColor     = ThemeManager.BgInput;
        _cmbWork.ForeColor     = ThemeManager.TextMain;
        _cmbWork.Font          = ThemeManager.F(9.5f);
        _cmbWork.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbWork.Margin        = new Padding(0, 2, 0, 2);

        _cmbWork.Items.Add("");
        foreach (var t in LoadCodeNames("코드_근무구분", AppConfig.CodeWorkTypeCols,
                                        "근무구분", AppConfig.WorkTypes))
            _cmbWork.Items.Add(t);

        int idx = _cmbWork.Items.IndexOf(currentVal);
        _cmbWork.SelectedIndex = idx >= 0 ? idx : 0;
    }

    // 상태: 현재근무 / 휴직 / 퇴사
    void SetupStatCombo(string currentVal)
    {
        _cmbStat.Dock          = DockStyle.Fill;
        _cmbStat.BackColor     = ThemeManager.BgInput;
        _cmbStat.ForeColor     = ThemeManager.TextMain;
        _cmbStat.Font          = ThemeManager.F(9.5f);
        _cmbStat.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbStat.Margin        = new Padding(0, 2, 0, 2);

        foreach (var t in AppConfig.WorkStatuses) _cmbStat.Items.Add(t);
        int idx = _cmbStat.Items.IndexOf(currentVal);
        _cmbStat.SelectedIndex = idx >= 0 ? idx : 0;   // 기본 현재근무
    }

    // 자격증: 코드관리(코드_자격증) 목록 + 직접 입력도 허용
    void SetupCertCombo(string currentVal)
    {
        _cmbCert.Dock          = DockStyle.Fill;
        _cmbCert.BackColor     = ThemeManager.BgInput;
        _cmbCert.ForeColor     = ThemeManager.TextMain;
        _cmbCert.Font          = ThemeManager.F(9.5f);
        _cmbCert.DropDownStyle = ComboBoxStyle.DropDown;   // 목록에 없는 자격증도 입력 가능
        _cmbCert.Margin        = new Padding(0, 2, 0, 2);
        _cmbCert.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
        _cmbCert.AutoCompleteSource = AutoCompleteSource.ListItems;

        _cmbCert.Items.Add("");
        foreach (var t in LoadCodeNames("코드_자격증", AppConfig.CodeCertCols, "자격증", []))
            _cmbCert.Items.Add(t);

        _cmbCert.Text = currentVal;
    }

    // ── 사진 헬퍼 ─────────────────────────────────────────
    string? PhotoFolder()
    {
        var name  = GetField("이름").Trim();
        var phone = GetField("전화번호").Trim();
        if (name.Length == 0) return null;
        var folder = string.IsNullOrEmpty(phone) ? name : $"{name}_{phone}";
        return Path.Combine(AppConfig.ImageDir, folder);
    }

    static Image LoadImageFree(string path)
    {
        using var tmp = Image.FromFile(path);
        return new Bitmap(tmp);
    }

    void LoadPhotos()
    {
        var folder = PhotoFolder();
        if (folder == null) return;
        for (int i = 0; i < PhotoTypes.Length; i++)
        {
            var path = Path.Combine(folder, $"{PhotoTypes[i]}.jpg");
            if (!File.Exists(path)) continue;
            try
            {
                _photos[i] = LoadImageFree(path);
                _delBtns[i].Visible = true;
                _photoBoxes[i].Invalidate();
            }
            catch { }
        }
    }

    void SelectPhoto(int idx)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = $"{PhotoTypes[idx]} 사진 선택",
            Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp|모든 파일|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK) SetPhoto(idx, dlg.FileName);
    }

    void PastePhoto(int idx)
    {
        if (!Clipboard.ContainsImage()) return;
        var img = Clipboard.GetImage();
        if (img == null) return;
        _photos[idx]?.Dispose();
        _photos[idx] = new Bitmap(img);
        _delBtns[idx].Visible = true;
        _photoBoxes[idx].Invalidate();
    }

    void SetPhoto(int idx, string srcPath)
    {
        try
        {
            _photos[idx]?.Dispose();
            _photos[idx] = LoadImageFree(srcPath);
            _delBtns[idx].Visible = true;
            _photoBoxes[idx].Invalidate();
        }
        catch { MessageBox.Show("이미지를 불러올 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void DeletePhoto(int idx)
    {
        _photos[idx]?.Dispose();
        _photos[idx] = null;
        _delBtns[idx].Visible = false;
        _photoBoxes[idx].Invalidate();
        var folder = PhotoFolder();
        if (folder == null) return;
        var path = Path.Combine(folder, $"{PhotoTypes[idx]}.jpg");
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    void SavePhotos()
    {
        var folder = PhotoFolder();
        if (folder == null) return;
        Directory.CreateDirectory(folder);
        for (int i = 0; i < PhotoTypes.Length; i++)
        {
            if (_photos[i] == null) continue;
            try { _photos[i]!.Save(Path.Combine(folder, $"{PhotoTypes[i]}.jpg"),
                System.Drawing.Imaging.ImageFormat.Jpeg); }
            catch { }
        }
        if (_savedName != null)
        {
            var oldFolder = Path.Combine(AppConfig.ImageDir,
                string.IsNullOrEmpty(_savedPhone) ? _savedName : $"{_savedName}_{_savedPhone}");
            if (Directory.Exists(oldFolder) && !oldFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                try { Directory.Move(oldFolder, folder); } catch { }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        foreach (var img in _photos) img?.Dispose();
    }

    string GetField(string col)
    {
        int i = Array.IndexOf(AppConfig.WorkerCols, col);
        return i >= 0 && i < _fields.Length ? _fields[i].Text.Trim() : "";
    }

    static Button MakeSmallBtn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text      = text,
            Width     = w, Height = 20,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(8f),
            Cursor    = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => onClick();
        return b;
    }
}

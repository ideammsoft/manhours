using manHours.Screens;
using manHours.Screens.Noim;
using manHours.Screens.Jangbi;

namespace manHours.Forms;

public class MainForm : Form
{
    // ── 사이드바 컨트롤 ──────────────────────────────────
    Panel   _sidebar    = null!;
    Panel   _content    = null!;
    Image?  _bgImage;
    Button  _btnTheme   = null!;

    // ── 상단 헤더 ────────────────────────────────────────
    ComboBox _cbProject = null!;
    ComboBox _cbYear    = null!;
    ComboBox _cbMonth   = null!;
    TextBox  _txtCompany = null!;

    // ── 화면 ─────────────────────────────────────────────
    HomeScreen?   _homeScreen;
    FileScreen?   _fileScreen;
    WorkerScreen? _workerScreen;
    PrintScreen?  _printScreen;
    WeeklyScreen? _weeklyScreen;
    Button?       _btnWeekly;
    EquipmentFileScreen?   _equipFileScreen;
    EquipmentWorkerScreen? _equipWorkerScreen;
    EquipmentPrintScreen?  _equipPrintScreen;
    Control?      _current;

    // ── 사이드바 메뉴 버튼 ───────────────────────────────
    Panel?  _noimSubPanel;
    Button? _btnNoim;
    bool    _noimExpanded;
    Button? _btnInwonAdd;
    Button? _btnInwonEdit;
    Button? _btnNaeyeok;

    Panel?  _jangbiSubPanel;
    Button? _btnJangbi;
    bool    _jangbiExpanded;
    Button? _btnJangbiAdd;
    Button? _btnJangbiEdit;
    Button? _btnJangbiPrint;

    Button? _activeMenuBtn;

    public MainForm()
    {
        Text          = $"{AppConfig.AppName} v{AppConfig.Version}";
        MinimumSize   = new Size(1300, 850);
        Size          = new Size(1440, 920);
        StartPosition = FormStartPosition.CenterScreen;

        var ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(ico)) try { Icon = new Icon(ico); } catch { }

        _LoadBgImage();
        BuildUI();
        LoadProjectCombo();
        ShowStartScreen();
        Load += (_, _) => { _ = CheckForUpdateAsync(silent: true); };
    }

    // ── 배경 이미지 ──────────────────────────────────────
    void _LoadBgImage()
    {
        var p = @"C:\Users\kslee\OneDrive\사진\엠엠소프트마크등\mm_mark.png";
        if (File.Exists(p)) try { _bgImage = Image.FromFile(p); } catch { }
    }

    // ── UI 구성 ──────────────────────────────────────────
    void BuildUI()
    {
        BackColor = ThemeManager.BgMain;

        // 사이드바
        _sidebar = new Panel
        {
            Width     = 170,
            Dock      = DockStyle.Left,
            BackColor = ThemeManager.BgSidebar,
        };
        BuildSidebar();

        // 콘텐츠 영역
        _content = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = ThemeManager.BgMain,
        };
        _content.Paint += ContentPaint;

        // 상단 헤더
        var header = BuildHeader();

        Controls.Add(_content);
        Controls.Add(header);
        Controls.Add(_sidebar);
    }

    // ── 상단 헤더 ────────────────────────────────────────
    Panel BuildHeader()
    {
        var hdr = new Panel
        {
            Height    = 44,
            Dock      = DockStyle.Top,
            BackColor = ThemeManager.BgBottom,
            Padding   = new Padding(8, 6, 8, 6),
        };

        var flow = new FlowLayoutPanel
        {
            Dock         = DockStyle.Fill,
            WrapContents = false,
            BackColor    = Color.Transparent,
        };

        flow.Controls.Add(MakeHdrLabel("매장유형:"));
        _cbProject = MakeHdrCombo(200);
        _cbProject.SelectedIndexChanged += (_, _) => OnProjectChanged();
        flow.Controls.Add(_cbProject);

        flow.Controls.Add(MakeHdrLabel("  년도:"));
        _cbYear = MakeHdrCombo(75);
        for (int y = 2020; y <= 2031; y++) _cbYear.Items.Add($"{y}년");
        _cbYear.SelectedIndex = Math.Max(0, DateTime.Now.Year - 2020);
        _cbYear.SelectedIndexChanged += (_, _) => OnYearMonthChanged();
        flow.Controls.Add(_cbYear);

        _cbMonth = MakeHdrCombo(65);
        for (int m = 1; m <= 12; m++) _cbMonth.Items.Add($"{m:D2}월");
        _cbMonth.SelectedIndex = DateTime.Now.Month - 1;
        _cbMonth.SelectedIndexChanged += (_, _) => OnYearMonthChanged();
        flow.Controls.Add(_cbMonth);

        flow.Controls.Add(MakeHdrLabel("  상호:"));
        _txtCompany = new TextBox
        {
            Width       = 160,
            Height      = 28,
            BackColor   = ThemeManager.BgInput,
            ForeColor   = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = ThemeManager.F(9.5f),
            Margin      = new Padding(0, 2, 0, 0),
        };
        _txtCompany.Leave += (_, _) => SaveCompany();
        flow.Controls.Add(_txtCompany);

        // 우측: 설정 버튼
        var btnHdrSettings = new Button
        {
            Text      = "⚙ 설정",
            Dock      = DockStyle.Right,
            Width     = 90,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(10f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        btnHdrSettings.FlatAppearance.BorderColor = ThemeManager.Border;
        btnHdrSettings.Click += (_, _) => OpenSettings();
        hdr.Controls.Add(btnHdrSettings);
        hdr.Controls.Add(flow);
        return hdr;
    }

    // ── 사이드바 구성 ────────────────────────────────────
    void BuildSidebar()
    {
        const int W = 150;

        // 상단 로고
        var topFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            Padding       = new Padding(10, 16, 10, 4),
        };

        // 로고 이미지
        try
        {
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(icoPath))
            {
                using var ico = new Icon(icoPath, 48, 48);
                topFlow.Controls.Add(new PictureBox
                {
                    Image    = ico.ToBitmap(),
                    Width    = W, Height = 52,
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Margin   = new Padding(0),
                    BackColor= Color.Transparent,
                });
            }
        }
        catch { }

        topFlow.Controls.Add(MakeSbLabel(AppConfig.AppName, 14f, FontStyle.Bold, Color.FromArgb(137, 180, 250)));

        var lnk = new Label
        {
            Text      = AppConfig.WebUrl.Replace("http://", ""),
            Width     = W, Height = 18,
            Font      = new Font("맑은 고딕", 8f, FontStyle.Underline),
            ForeColor = Color.FromArgb(70, 130, 180),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
        };
        lnk.Click += (_, _) =>
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(AppConfig.WebUrl) { UseShellExecute = true });
        topFlow.Controls.Add(lnk);

        topFlow.Controls.Add(new Panel { Width = W, Height = 1, BackColor = ThemeManager.Border, Margin = new Padding(0) });
        topFlow.Controls.Add(new Panel { Width = W, Height = 8, BackColor = Color.Transparent, Margin = new Padding(0) });

        // ── 메뉴 아이템 ──────────────────────────────────
        // 맨아워즈 헤더 버튼
        _btnNoim = MakeSbMenuBtn("▶ 맨아워즈", true);
        _btnNoim.Click += (_, _) => ToggleNoimMenu();
        topFlow.Controls.Add(_btnNoim);

        // 맨아워즈 서브 메뉴 패널 (접기/펼치기)
        _noimSubPanel = new Panel
        {
            Width     = W,
            AutoSize  = true,
            BackColor = Color.Transparent,
            Margin    = new Padding(0),
            Visible   = false,
        };
        var subFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            Padding       = new Padding(16, 0, 0, 0),
            BackColor     = Color.Transparent,
        };
        _btnInwonAdd  = MakeSbSubBtn("인원추가");
        _btnInwonEdit = MakeSbSubBtn("근무시간 입력");
        _btnNaeyeok   = MakeSbSubBtn("급여내역확인");
        _btnWeekly    = MakeSbSubBtn("주간 근무표");
        _btnWeekly.Font = ThemeManager.F(9f, FontStyle.Bold);   // 급여내역확인과 같은 크기 + 진하게
        _btnInwonAdd.Click  += (_, _) => { SetActiveMenu(_btnInwonAdd!);  ShowScreen("파일"); };
        _btnInwonEdit.Click += (_, _) => { SetActiveMenu(_btnInwonEdit!); ShowScreen("지급대장"); };
        _btnNaeyeok.Click   += (_, _) => { SetActiveMenu(_btnNaeyeok!);   ShowScreen("프린트"); };
        _btnWeekly.Click    += (_, _) => { SetActiveMenu(_btnWeekly!);    ShowScreen("주간"); };
        subFlow.Controls.Add(_btnInwonAdd);
        subFlow.Controls.Add(_btnInwonEdit);
        subFlow.Controls.Add(_btnNaeyeok);
        subFlow.Controls.Add(_btnWeekly);
        _noimSubPanel.Controls.Add(subFlow);
        topFlow.Controls.Add(_noimSubPanel);

        // (장비관리 · 자재관리 메뉴는 맨아워즈에서 제외 — 맨아워즈 전용)

        // 하단
        var bottomFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            Padding       = new Padding(10, 6, 10, 10),
            BackColor     = Color.Transparent,
        };

        bottomFlow.Controls.Add(new Panel { Width = W, Height = 1,
            BackColor = ThemeManager.Border, Margin = new Padding(0, 0, 0, 6) });

        // 테마 토글
        _btnTheme = new Button
        {
            Text      = ThemeManager.IsDark ? "☀ 밝은 테마" : "🌙 어두운 테마",
            Width     = W, Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(8.5f),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 0, 2),
        };
        _btnTheme.FlatAppearance.BorderColor = ThemeManager.Border;
        _btnTheme.Click += (_, _) => ChangeTheme();
        bottomFlow.Controls.Add(_btnTheme);

        // 글자크기 (TableLayout으로 수직 중앙 정렬)
        var fontRow = new TableLayoutPanel
        {
            Width = W, Height = 26, ColumnCount = 4, RowCount = 1,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4), Padding = new Padding(0),
        };
        fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 2));
        fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
        fontRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26));
        fontRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var lblFont = new Label
        {
            Text = "글자크기:", AutoSize = false, Dock = DockStyle.Fill,
            Font = ThemeManager.F(8.5f), ForeColor = ThemeManager.TextSub,
            BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
        };
        var btnFontM = new Button { Text = "−", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(9f), Margin = new Padding(0), Cursor = Cursors.Hand };
        var btnFontP = new Button { Text = "+", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(9f), Margin = new Padding(0), Cursor = Cursors.Hand };
        btnFontM.FlatAppearance.BorderColor = ThemeManager.Border;
        btnFontP.FlatAppearance.BorderColor = ThemeManager.Border;
        btnFontM.Click += (_, _) => ChangeFontSize(-0.5f);
        btnFontP.Click += (_, _) => ChangeFontSize(+0.5f);
        fontRow.Controls.Add(lblFont,   0, 0);
        fontRow.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 1, 0);
        fontRow.Controls.Add(btnFontM, 2, 0);
        fontRow.Controls.Add(btnFontP, 3, 0);
        bottomFlow.Controls.Add(fontRow);

        // 광고 (manDjb 와 동일: 소프트웨어 주문제작)
        var adPanel = new Panel
        {
            Width = W, Height = 56,
            BackColor = Color.FromArgb(192, 57, 43),
            Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 8),
        };
        var adT = new Label { Text = "🛠️ 프로그램 주문제작",
            Font = new Font("맑은 고딕", 8.5f, FontStyle.Bold), ForeColor = Color.White,
            Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.BottomCenter, BackColor = Color.Transparent };
        var adS = new Label { Text = "주문제작 사이트 보기",
            Font = new Font("맑은 고딕", 7.5f), ForeColor = Color.FromArgb(255, 220, 220),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, BackColor = Color.Transparent };
        adPanel.Controls.Add(adS); adPanel.Controls.Add(adT);
        EventHandler adClick = (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("https://www.mmsoft.co.kr/ad/order.html") { UseShellExecute = true });
        adPanel.Click += adClick; adT.Click += adClick; adS.Click += adClick;
        bottomFlow.Controls.Add(adPanel);

        var btnUpdate = new Button
        {
            Text      = "업데이트 확인",
            Width     = W, Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = ThemeManager.BtnText,
            Font      = new Font("맑은 고딕", 8f),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 0, 2),
        };
        btnUpdate.FlatAppearance.BorderColor = ThemeManager.Border;
        btnUpdate.Click += (_, _) => { _ = CheckForUpdateAsync(silent: false); };
        bottomFlow.Controls.Add(btnUpdate);

        var ver = new Label { Text = $"v{AppConfig.Version}", Width = W, Height = 18,
            TextAlign = ContentAlignment.MiddleCenter, Font = new Font("맑은 고딕", 8f),
            ForeColor = ThemeManager.TextSub, BackColor = Color.Transparent, Margin = new Padding(0) };
        bottomFlow.Controls.Add(ver);

        _sidebar.Controls.Add(bottomFlow);
        _sidebar.Controls.Add(topFlow);
    }

    // ── 맨아워즈 서브메뉴 토글 ───────────────────────────
    void ToggleNoimMenu()
    {
        _noimExpanded = !_noimExpanded;
        _noimSubPanel!.Visible = _noimExpanded;
        _btnNoim!.Text = (_noimExpanded ? "▼ " : "▶ ") + "맨아워즈";
        if (_noimExpanded && _current == null)
            ShowHome();
    }

    // 시작 시: 메뉴를 펼치고 첫 화면(인원추가)을 바로 보여준다
    void ShowStartScreen()
    {
        ToggleNoimMenu();
        SetActiveMenu(_btnInwonAdd!);
        ShowScreen("파일");
    }

    // ── 장비관리 서브메뉴 토글 ───────────────────────────
    void ToggleJangbiMenu()
    {
        _jangbiExpanded = !_jangbiExpanded;
        _jangbiSubPanel!.Visible = _jangbiExpanded;
        _btnJangbi!.Text = (_jangbiExpanded ? "▼ " : "▶ ") + "장비관리";
        if (_jangbiExpanded && _current == null)
            ShowHome();
    }

    void SetActiveMenu(Button btn)
    {
        if (_activeMenuBtn != null)
        {
            _activeMenuBtn.BackColor = Color.Transparent;
            _activeMenuBtn.ForeColor = ThemeManager.TextSub;
        }
        _activeMenuBtn = btn;
        btn.BackColor = ThemeManager.BtnSide;
        btn.ForeColor = ThemeManager.IsDark ? ThemeManager.Accent : Color.FromArgb(20, 70, 180);
    }

    // ── 화면 전환 ────────────────────────────────────────
    void ShowHome()
    {
        _homeScreen ??= new HomeScreen();
        Show(_homeScreen);
    }

    void ShowScreen(string name)
    {
        // 근무시간 입력/장비변경/인원선택에서 다른 화면으로 이동할 때 자동 저장
        if (_current is WorkerScreen ws)           ws.SaveData();
        if (_current is EquipmentWorkerScreen ews) ews.SaveData();
        if (_current is FileScreen fs)             fs.SaveRoster();

        int year  = 2020 + _cbYear.SelectedIndex;
        int month = _cbMonth.SelectedIndex + 1;
        var proj  = _cbProject.Text;

        // 무료 이용기간이 끝나면 입력 화면(근무시간 입력·주간 근무표)만 잠근다.
        if (AppConfig.TrialExpired && name is "지급대장" or "주간")
        {
            MessageBox.Show(
                "무료 이용 기간이 만료되어 근무시간 입력과 주간 근무표는 사용할 수 없습니다.\n" +
                "연장 후 이용해 주세요. (그 외 기능은 그대로 사용하실 수 있습니다)",
                "무료 이용 기간 만료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        switch (name)
        {
            case "파일":
                _fileScreen ??= new FileScreen();
                _fileScreen.SetContext(year, month, proj);
                _fileScreen.GoNext -= OnFileGoNext;
                _fileScreen.GoNext += OnFileGoNext;
                Show(_fileScreen);
                break;
            case "지급대장":
                _workerScreen ??= new WorkerScreen();
                _workerScreen.Load(year, month, proj);
                _workerScreen.GoPrev -= OnWorkerGoPrev;
                _workerScreen.GoNext -= OnWorkerGoNext;
                _workerScreen.GoPrev += OnWorkerGoPrev;
                _workerScreen.GoNext += OnWorkerGoNext;
                Show(_workerScreen);
                break;
            case "프린트":
                _printScreen ??= new PrintScreen();
                _printScreen.Load(year, month, proj);
                _printScreen.GoPrev -= OnPrintGoPrev;
                _printScreen.GoPrev += OnPrintGoPrev;
                Show(_printScreen);
                break;
            case "주간":
                _weeklyScreen ??= new WeeklyScreen();
                _weeklyScreen.SetContext(year, month, proj);
                Show(_weeklyScreen);
                break;
            case "장비파일":
                _equipFileScreen ??= new EquipmentFileScreen();
                _equipFileScreen.SetContext(year, month, proj);
                _equipFileScreen.GoNext -= OnJangbiFileGoNext;
                _equipFileScreen.GoNext += OnJangbiFileGoNext;
                Show(_equipFileScreen);
                break;
            case "장비변경":
                _equipWorkerScreen ??= new EquipmentWorkerScreen();
                _equipWorkerScreen.Load(year, month, proj);
                _equipWorkerScreen.GoPrev -= OnJangbiWorkerGoPrev;
                _equipWorkerScreen.GoNext -= OnJangbiWorkerGoNext;
                _equipWorkerScreen.GoPrev += OnJangbiWorkerGoPrev;
                _equipWorkerScreen.GoNext += OnJangbiWorkerGoNext;
                Show(_equipWorkerScreen);
                break;
            case "장비프린트":
                _equipPrintScreen ??= new EquipmentPrintScreen();
                _equipPrintScreen.Load(year, month, proj);
                _equipPrintScreen.GoPrev -= OnJangbiPrintGoPrev;
                _equipPrintScreen.GoPrev += OnJangbiPrintGoPrev;
                Show(_equipPrintScreen);
                break;
        }
    }

    void Show(Control screen)
    {
        screen.Dock    = DockStyle.Fill;
        screen.Visible = false;
        if (!_content.Controls.Contains(screen))
            _content.Controls.Add(screen);
        _current?.Hide();
        screen.Show();
        _current = screen;
    }

    void OnFileGoNext(int year, int month, string proj)
    {
        _cbYear.SelectedIndex  = Math.Max(0, year - 2020);
        _cbMonth.SelectedIndex = month - 1;
        SetProjectCombo(proj);
        SetActiveMenu(_btnInwonEdit!);
        ShowScreen("지급대장");
    }

    void OnWorkerGoPrev() { SetActiveMenu(_btnInwonAdd!); ShowScreen("파일"); }
    void OnWorkerGoNext() { SetActiveMenu(_btnNaeyeok!);  ShowScreen("프린트"); }
    void OnPrintGoPrev()  { SetActiveMenu(_btnInwonEdit!); ShowScreen("지급대장"); }

    void OnJangbiFileGoNext(int year, int month, string proj)
    {
        _cbYear.SelectedIndex  = Math.Max(0, year - 2020);
        _cbMonth.SelectedIndex = month - 1;
        SetProjectCombo(proj);
        SetActiveMenu(_btnJangbiEdit!);
        ShowScreen("장비변경");
    }

    void OnJangbiWorkerGoPrev() { SetActiveMenu(_btnJangbiAdd!);   ShowScreen("장비파일"); }
    void OnJangbiWorkerGoNext() { SetActiveMenu(_btnJangbiPrint!); ShowScreen("장비프린트"); }
    void OnJangbiPrintGoPrev()  { SetActiveMenu(_btnJangbiEdit!);  ShowScreen("장비변경"); }

    // ── 매장유형(프로젝트) 콤보 ────────────────────────────
    void LoadProjectCombo()
    {
        var cur = _cbProject.Text;
        _cbProject.Items.Clear();
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("사업명", ["이름"]);
        var (_, rows) = db.Select("사업명");
        var names = rows.Select(r => r[0]?.ToString() ?? "").Where(n => n.Length > 0).ToList();
        if (names.Count == 0) names = ["기본 매장1", "기본 매장2"];
        foreach (var n in names) _cbProject.Items.Add(n);
        var idx = _cbProject.Items.IndexOf(cur);
        _cbProject.SelectedIndex = idx >= 0 ? idx : 0;
        LoadCompany();
    }

    void SetProjectCombo(string proj)
    {
        var idx = _cbProject.Items.IndexOf(proj);
        if (idx >= 0) _cbProject.SelectedIndex = idx;
    }

    void OnProjectChanged() { LoadCompany(); OnYearMonthChanged(); }

    // 년/월이 바뀌면 현재 보고 있는 화면을 그 달 기준으로 다시 로드한다.
    // (인원선택은 이때 '이전 달 데이터 불러오기'를 즉시 물어본다)
    void OnYearMonthChanged()
    {
        switch (_current)
        {
            case FileScreen:    ShowScreen("파일");    break;
            case WorkerScreen:  ShowScreen("지급대장"); break;
            case PrintScreen:   ShowScreen("프린트");   break;
            case WeeklyScreen:  ShowScreen("주간");    break;
        }
    }

    void LoadCompany()
    {
        var proj = _cbProject.Text;
        string name = "";
        if (!string.IsNullOrEmpty(proj))
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("사업설정", AppConfig.BusiSettingCols);
            var (cols, rows) = db.Select("사업설정", $"\"사업명\"='{proj.Replace("'", "''")}'");
            if (rows.Length > 0)
            {
                int ci = Array.IndexOf(cols, "상호");
                name = ci >= 0 ? rows[0][ci]?.ToString()?.Trim() ?? "" : "";
            }
        }
        if (string.IsNullOrEmpty(name))
        {
            using var db2 = new Database(AppConfig.BaseDbPath);
            name = db2.GetSetting("상호") ?? "";
        }
        _txtCompany.Text = string.IsNullOrEmpty(name) ? "엠엠샘플회사" : name;
    }

    void SaveCompany()
    {
        var name = _txtCompany.Text.Trim();
        if (string.IsNullOrEmpty(name)) { _txtCompany.Text = "엠엠샘플회사"; name = "엠엠샘플회사"; }
        var proj = _cbProject.Text;
        using var db = new Database(AppConfig.BaseDbPath);
        db.EnsureTable("사업설정", AppConfig.BusiSettingCols);
        if (!string.IsNullOrEmpty(proj))
        {
            var (_, rows) = db.Select("사업설정", $"\"사업명\"='{proj.Replace("'","''")}'");
            if (rows.Length > 0)
                db.Execute("UPDATE \"사업설정\" SET \"상호\"=@p0 WHERE \"사업명\"=@p1", name, proj);
            else
                db.Execute("INSERT INTO \"사업설정\" (\"사업명\",\"상호\") VALUES (@p0,@p1)", proj, name);
        }
        db.EnsureTable("설정", ["키", "값"]);
        db.SetSetting("상호", name);
    }

    // ── 배경 이미지 ──────────────────────────────────────
    void ContentPaint(object? s, PaintEventArgs e)
    {
        if (_bgImage == null) return;
        var g     = e.Graphics;
        var sz    = _content.ClientSize;
        var iw    = _bgImage.Width;
        var ih    = _bgImage.Height;
        float scale = Math.Min((float)sz.Width / iw, (float)sz.Height / ih) * 0.55f;
        int   dw    = (int)(iw * scale);
        int   dh    = (int)(ih * scale);
        int   dx    = (sz.Width  - dw) / 2;
        int   dy    = (sz.Height - dh) / 2;

        // 투명 PNG를 배경색 위에 미리 합성하여 체커보드 패턴 방지
        using var bmp = new Bitmap(dw, dh);
        using (var g2 = Graphics.FromImage(bmp))
        {
            g2.Clear(ThemeManager.BgMain);
            g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g2.DrawImage(_bgImage, 0, 0, dw, dh);
        }

        using var attr = new System.Drawing.Imaging.ImageAttributes();
        float[][] mat = {
            new float[] {1,0,0,0,0},
            new float[] {0,1,0,0,0},
            new float[] {0,0,1,0,0},
            new float[] {0,0,0,0.07f,0},
            new float[] {0,0,0,0,1},
        };
        attr.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(mat));
        g.DrawImage(bmp,
            new Rectangle(dx, dy, dw, dh),
            0, 0, dw, dh, GraphicsUnit.Pixel, attr);
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    static Label MakeSbLabel(string text, float size, FontStyle fs, Color fg) => new()
    {
        Text = text, Width = 150, AutoSize = false,
        Font = new Font("맑은 고딕", size, fs),
        ForeColor = fg, TextAlign = ContentAlignment.MiddleCenter,
        BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4),
    };

    static Button MakeSbMenuBtn(string text, bool enabled)
    {
        var b = new Button
        {
            Text      = text,
            Width     = 150, Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(10f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor    = enabled ? Cursors.Hand : Cursors.Default,
            Enabled   = enabled,
            Margin    = new Padding(0, 1, 0, 1),
            Padding   = new Padding(10, 0, 0, 0),
        };
        b.FlatAppearance.BorderSize          = 0;
        b.FlatAppearance.MouseOverBackColor  = ThemeManager.BtnSide;
        b.FlatAppearance.MouseDownBackColor  = ThemeManager.BtnSide;
        return b;
    }

    static Button MakeSbSubBtn(string text)
    {
        var b = new Button
        {
            Text      = text,
            Width     = 130, Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = ThemeManager.TextSub,
            Font      = ThemeManager.F(9f),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 1, 0, 1),
            Padding   = new Padding(8, 0, 0, 0),
        };
        b.FlatAppearance.BorderSize         = 0;
        b.FlatAppearance.MouseOverBackColor = ThemeManager.BtnSide;
        b.FlatAppearance.MouseDownBackColor = ThemeManager.BtnSide;
        return b;
    }

    static Label MakeHdrLabel(string text) => new()
    {
        Text      = text,
        ForeColor = ThemeManager.TextSub,
        AutoSize  = true,
        Font      = ThemeManager.F(9.5f),
        Margin    = new Padding(0, 6, 0, 0),
    };

    static ComboBox MakeHdrCombo(int width) => new()
    {
        Width         = width,
        Height        = 28,
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor     = ThemeManager.BgInput,
        ForeColor     = ThemeManager.TextMain,
        FlatStyle     = FlatStyle.Flat,
        Font          = ThemeManager.F(9.5f),
        Margin        = new Padding(0, 2, 4, 0),
    };

    // ── 설정 ─────────────────────────────────────────────
    void OpenSettings()
    {
        int year  = 2020 + _cbYear.SelectedIndex;
        int month = _cbMonth.SelectedIndex + 1;
        var proj  = _cbProject.Text;
        var dlg   = new SettingsDialog(year, month, proj);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            LoadProjectCombo();
            if (_current == _fileScreen)
                _fileScreen?.SetContext(year, month, _cbProject.Text);
        }
    }

    // ── 테마 / 글씨크기 ──────────────────────────────────
    void ChangeTheme()
    {
        AppSettings.Instance.Theme = ThemeManager.IsDark ? "light" : "dark";
        AppSettings.Instance.Save();
        ReApplyTheme();
    }

    void ChangeFontSize(float delta)
    {
        AppSettings.Instance.FontSize = Math.Clamp(AppSettings.Instance.FontSize + delta, 8f, 14f);
        AppSettings.Instance.Save();
        ReApplyTheme();
    }

    public static void ReApplyTheme()
    {
        var main = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (main == null) return;
        main.BeginInvoke(main.DoReApplyTheme);
    }

    void DoReApplyTheme()
    {
        var savedProject = _cbProject?.Text ?? "";
        var savedYear    = _cbYear?.SelectedIndex  ?? Math.Max(0, DateTime.Now.Year - 2020);
        var savedMonth   = _cbMonth?.SelectedIndex ?? DateTime.Now.Month - 1;
        var savedScreen  = _current == _fileScreen        ? "파일"      :
                           _current == _workerScreen      ? "지급대장"  :
                           _current == _printScreen       ? "프린트"    :
                           _current == _equipFileScreen   ? "장비파일"  :
                           _current == _equipWorkerScreen ? "장비변경"  :
                           _current == _equipPrintScreen  ? "장비프린트": "";

        SuspendLayout();

        var oldControls = Controls.Cast<Control>().ToList();
        Controls.Clear();
        foreach (var c in oldControls) c.Dispose();

        _homeScreen?.Dispose();        _homeScreen        = null;
        _fileScreen?.Dispose();        _fileScreen        = null;
        _workerScreen?.Dispose();      _workerScreen      = null;
        _printScreen?.Dispose();       _printScreen       = null;
        _weeklyScreen?.Dispose();      _weeklyScreen      = null;
        _equipFileScreen?.Dispose();   _equipFileScreen   = null;
        _equipWorkerScreen?.Dispose(); _equipWorkerScreen = null;
        _equipPrintScreen?.Dispose();  _equipPrintScreen  = null;
        _current       = null;
        _noimExpanded  = false;
        _jangbiExpanded = false;
        _activeMenuBtn = null;
        _btnNoim = _btnInwonAdd = _btnInwonEdit = _btnNaeyeok = null;
        _noimSubPanel  = null;
        _btnJangbi = _btnJangbiAdd = _btnJangbiEdit = _btnJangbiPrint = null;
        _jangbiSubPanel = null;

        BackColor = ThemeManager.BgMain;
        BuildUI();

        _cbYear.SelectedIndex  = savedYear;
        _cbMonth.SelectedIndex = savedMonth;
        LoadProjectCombo();
        var idx = _cbProject.Items.IndexOf(savedProject);
        if (idx >= 0) _cbProject.SelectedIndex = idx;

        if (!string.IsNullOrEmpty(savedScreen))
        {
            bool isJangbi = savedScreen is "장비파일" or "장비변경" or "장비프린트";
            if (isJangbi) ToggleJangbiMenu();
            else ToggleNoimMenu();
            var menuBtn = savedScreen switch
            {
                "파일"      => _btnInwonAdd,
                "지급대장"  => _btnInwonEdit,
                "프린트"    => _btnNaeyeok,
                "장비파일"  => _btnJangbiAdd,
                "장비변경"  => _btnJangbiEdit,
                _            => _btnJangbiPrint,
            };
            SetActiveMenu(menuBtn!);
            ShowScreen(savedScreen);
        }
        else
            ShowHome();
        ResumeLayout(true);
        Refresh();
    }

    public static void RestartApp()
    {
        var old = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
        if (old == null) return;
        var bounds = old.Bounds;
        old.BeginInvoke(() =>
        {
            foreach (Form f in Application.OpenForms.Cast<Form>().Where(f => f != old).ToList())
                f.Close();
            AppConfig.PendingNextForm = new MainForm { StartPosition = FormStartPosition.Manual, Bounds = bounds };
            old.Close();
        });
    }

    // ── 자동 업데이트 ────────────────────────────────────
    async Task CheckForUpdateAsync(bool silent)
    {
        var currentVer = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                typeof(MainForm).Assembly)
            ?.InformationalVersion ?? AppConfig.Version;
        try
        {
            var info = await Updater.CheckAsync(currentVer);
            if (info == null)
            {
                if (!silent)
                    MessageBox.Show("현재 최신 버전입니다.", "업데이트 확인",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Invoke(() => new UpdateDialog(currentVer, info).ShowDialog(this));
        }
        catch
        {
            if (!silent)
                MessageBox.Show("업데이트 서버에 연결할 수 없습니다.", "업데이트 확인",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

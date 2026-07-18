using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace manHours.Forms;

public class SmsDialog : Form
{
    const string ApiUrl = "https://www.mmsoft.co.kr";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    readonly List<(string name, string phone)> _workers;
    readonly List<Dictionary<string, string>> _payroll;
    readonly string _project, _companyName;
    readonly int _year, _month;

    string _apiKey = "";
    string _tplCode = "UH_4451";
    string _channelSenderKey = "";
    long _balance = 0;
    readonly Dictionary<int, string> _payrollMsgs = new();
    readonly Dictionary<int, string> _payrollEmTitles = new();
    record TplInfo(string Name, string Content, string EmTitle);
    readonly Dictionary<string, TplInfo> _tplCache = new();

    Label   _lblBal1 = null!, _lblBal2 = null!, _lblChars = null!, _lblCurTpl = null!;
    TextBox _txtKey  = null!, _txtMsg  = null!, _txtTplPreview = null!;
    DataGridView _grdWorkers = null!, _grdTpl = null!;
    Button  _btnSend = null!, _btnKakao = null!;
    Panel[] _pages   = null!;
    Button[] _navBtns = null!;

    public SmsDialog(
        List<(string name, string phone)> workers,
        List<Dictionary<string, string>> payroll,
        string project, int year, int month, string companyName)
    {
        _workers     = workers;
        _payroll     = payroll;
        _project     = project;
        _year        = year;
        _month       = month;
        _companyName = companyName;

        Text          = "문자 발송";
        MinimumSize   = new Size(680, 580);
        Size          = new Size(730, 630);
        StartPosition = FormStartPosition.CenterParent;
        BackColor     = ThemeManager.BgMain;
        ForeColor     = ThemeManager.TextMain;

        LoadSettings();
        Build();

        Shown += async (_, _) =>
        {
            await FetchBalanceAsync();
            if (_payroll.Count > 0) ApplyPayrollMessages(silent: true);
        };
    }

    // ── 설정 ──────────────────────────────────────────────
    void LoadSettings()
    {
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("설정", ["키", "값"]);
            _apiKey  = db.GetSetting("문자_API_KEY")   ?? "";
            _tplCode = db.GetSetting("카카오_TPL_CODE") is { Length: > 0 } t ? t : "UH_4451";
        }
        catch { }
    }

    void SaveSetting(string key, string value)
    {
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("설정", ["키", "값"]);
            db.SetSetting(key, value);
        }
        catch { }
    }

    // ── UI ───────────────────────────────────────────────
    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Padding = new Padding(12, 10, 12, 10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        // mid: nav + stack
        var mid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Color.Transparent,
        };
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            BackColor = Color.Transparent, WrapContents = false,
            Padding = new Padding(0, 0, 6, 0),
        };
        _navBtns = new Button[3];
        string[] navLabels = ["발송 화면", "충전 잔액", "알림톡\n템플릿"];
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            _navBtns[i] = new Button
            {
                Text = navLabels[i], Width = 86, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = ThemeManager.BgPanel,
                ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(9f),
                Margin = new Padding(0, 0, 0, 3), Cursor = Cursors.Hand,
            };
            _navBtns[i].FlatAppearance.BorderColor = ThemeManager.Border;
            _navBtns[i].Click += (_, _) => SwitchTab(idx);
            nav.Controls.Add(_navBtns[i]);
        }
        mid.Controls.Add(nav, 0, 0);

        _pages = [BuildPageSend(), BuildPageBalance(), BuildPageTemplate()];
        var stack = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        foreach (var pg in _pages) { pg.Dock = DockStyle.Fill; stack.Controls.Add(pg); }
        mid.Controls.Add(stack, 1, 0);
        root.Controls.Add(mid, 0, 0);

        // bottom buttons
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 6, 0, 0),
        };
        int nPhone = _workers.Count(w => !string.IsNullOrWhiteSpace(w.phone));
        _btnSend = MakeDlgBtn($"문자발송  ({nPhone}명)", 150,
            Color.FromArgb(30, 80, 200), Color.White);
        _btnSend.Click += async (_, _) => await SendSmsAsync();
        _btnKakao = MakeDlgBtn($"카카오톡발송  ({nPhone}명)", 170,
            Color.FromArgb(245, 195, 0), Color.FromArgb(30, 30, 46));
        _btnKakao.Click += async (_, _) => await SendKakaoAsync();
        var btnCancel = MakeDlgBtn("취소", 80, ThemeManager.BtnSide, ThemeManager.BtnText);
        btnCancel.Click += (_, _) => Close();

        btnRow.Controls.Add(_btnSend);
        btnRow.Controls.Add(_btnKakao);
        btnRow.Controls.Add(btnCancel);
        root.Controls.Add(btnRow, 0, 1);

        Controls.Add(root);
        SwitchTab(0);
    }

    Panel BuildPageSend()
    {
        var p   = new Panel { BackColor = Color.Transparent };
        int tblH = Math.Min(Math.Max(_workers.Count, 3) * 24 + 28, 140);
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            BackColor = Color.Transparent,
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, tblH));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _lblBal1 = MakeLabel("잔액 조회중...", ThemeManager.Accent, 9.5f, FontStyle.Bold);
        _lblBal1.TextAlign = ContentAlignment.MiddleLeft;
        lay.Controls.Add(_lblBal1, 0, 0);

        lay.Controls.Add(MakeLabel($"발송 대상: {_workers.Count}명", ThemeManager.TextMain, 9.5f, FontStyle.Bold), 0, 1);

        _grdWorkers = MakeSimpleGrid(["이름", "전화번호"], [90]);
        foreach (var (name, phone) in _workers)
        {
            bool noPhone = string.IsNullOrWhiteSpace(phone);
            int r = _grdWorkers.Rows.Add(name, noPhone ? "(없음)" : phone);
            if (noPhone) _grdWorkers.Rows[r].DefaultCellStyle.ForeColor = Color.FromArgb(210, 80, 80);
        }
        lay.Controls.Add(_grdWorkers, 0, 2);

        lay.Controls.Add(MakeLabel("메시지:", ThemeManager.TextSub, 9f, FontStyle.Regular), 0, 3);

        _txtMsg = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
            BackColor = ThemeManager.BgInput, ForeColor = ThemeManager.TextMain,
            Font = ThemeManager.F(9.5f), BorderStyle = BorderStyle.FixedSingle,
        };
        _txtMsg.TextChanged += OnMsgChanged;
        lay.Controls.Add(_txtMsg, 0, 4);

        var qRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 4),
        };
        var btnDirect = MakeSmBtn("메시지직접입력");
        btnDirect.Click += (_, _) => { _txtMsg.Clear(); _payrollMsgs.Clear(); };
        qRow.Controls.Add(btnDirect);
        if (_payroll.Count > 0)
        {
            var btnP = MakeSmBtn("노무비명세표");
            btnP.Click += (_, _) => ApplyPayrollMessages(silent: false);
            qRow.Controls.Add(btnP);
        }
        _lblChars = MakeLabel("0자  [SMS(단문) - 17원/건]   [카카오톡 - 11원/건]",
            ThemeManager.TextSub, 8.5f, FontStyle.Regular);
        _lblChars.Margin = new Padding(8, 5, 0, 0);
        qRow.Controls.Add(_lblChars);
        lay.Controls.Add(qRow, 0, 5);

        p.Controls.Add(lay);
        return p;
    }

    Panel BuildPageBalance()
    {
        var p   = new Panel { BackColor = Color.Transparent };
        var lay = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0),
        };

        var keyRow = new FlowLayoutPanel
        {
            Width = 560, Height = 32, WrapContents = false, BackColor = Color.Transparent,
        };
        keyRow.Controls.Add(MakeLabel("API 키:", ThemeManager.TextSub, 9f, FontStyle.Regular));
        _txtKey = new TextBox
        {
            Width = 200, Height = 26, UseSystemPasswordChar = true,
            BackColor = ThemeManager.BgInput, ForeColor = ThemeManager.TextMain,
            Font = ThemeManager.F(9.5f), Text = _apiKey,
            Margin = new Padding(4, 2, 4, 0),
        };
        keyRow.Controls.Add(_txtKey);
        var btnPaste = MakeSmBtn("붙여넣기");
        btnPaste.Click += (_, _) => _txtKey.Text = Clipboard.GetText().Trim();
        keyRow.Controls.Add(btnPaste);
        var btnSave = MakeSmBtn("저장 후 잔액조회");
        btnSave.Click += async (_, _) =>
        {
            _apiKey = _txtKey.Text.Trim();
            SaveSetting("문자_API_KEY", _apiKey);
            await FetchBalanceAsync();
        };
        keyRow.Controls.Add(btnSave);
        lay.Controls.Add(keyRow);

        _lblBal2 = MakeLabel("잔액: 조회중...", ThemeManager.Accent, 14f, FontStyle.Bold);
        _lblBal2.Width = 400; _lblBal2.Height = 32;
        lay.Controls.Add(_lblBal2);

        foreach (var (text, url) in new[]
        {
            ("회원가입하기",   "https://www.mmsoft.co.kr"),
            ("요금충전하기",   "https://www.mmsoft.co.kr/payment?api=1"),
            ("API키 발급하기", "https://www.mmsoft.co.kr/sms-service"),
        })
        {
            var btn = new Button
            {
                Text = text, Width = 220, Height = 34, FlatStyle = FlatStyle.Flat,
                BackColor = ThemeManager.BgPanel, ForeColor = ThemeManager.Accent,
                Font = ThemeManager.F(10f), TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0), Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 0, 0),
            };
            btn.FlatAppearance.BorderColor = ThemeManager.Border;
            string u = url;
            btn.Click += (_, _) => System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(u) { UseShellExecute = true });
            lay.Controls.Add(btn);
        }

        p.Controls.Add(lay);
        return p;
    }

    Panel BuildPageTemplate()
    {
        var p   = new Panel { BackColor = Color.Transparent };
        var lay = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            BackColor = Color.Transparent,
        };
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        lay.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        lay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hdr = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 4),
        };
        hdr.Controls.Add(MakeLabel("카카오 알림톡 템플릿", ThemeManager.TextMain, 9.5f, FontStyle.Bold));
        var btnRef = MakeSmBtn("↻ 새로고침");
        btnRef.Click += async (_, _) => await FetchTemplatesAsync(silent: false);
        hdr.Controls.Add(btnRef);
        lay.Controls.Add(hdr, 0, 0);

        _grdTpl = MakeSimpleGrid(["코드", "템플릿명"], [90]);
        _grdTpl.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) SelectTemplate(e.RowIndex); };
        _grdTpl.CellClick       += (_, e) => { if (e.RowIndex >= 0) PreviewTemplate(e.RowIndex); };
        lay.Controls.Add(_grdTpl, 0, 1);

        _lblCurTpl = MakeLabel($"현재 선택: {_tplCode}", ThemeManager.TextSub, 9f, FontStyle.Regular);
        _lblCurTpl.TextAlign = ContentAlignment.MiddleLeft;
        lay.Controls.Add(_lblCurTpl, 0, 2);

        lay.Controls.Add(MakeLabel("템플릿 내용 미리보기:", ThemeManager.TextSub, 9f, FontStyle.Regular), 0, 3);

        _txtTplPreview = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
            ReadOnly = true, BackColor = ThemeManager.BgCell, ForeColor = ThemeManager.TextMain,
            Font = ThemeManager.F(9f), BorderStyle = BorderStyle.FixedSingle,
        };
        lay.Controls.Add(_txtTplPreview, 0, 4);

        p.Controls.Add(lay);
        return p;
    }

    void SwitchTab(int idx)
    {
        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visible = i == idx;
            _navBtns[i].BackColor = i == idx ? ThemeManager.BtnSide : ThemeManager.BgPanel;
            _navBtns[i].ForeColor = i == idx ? ThemeManager.Accent  : ThemeManager.TextSub;
        }
    }

    // ── 잔액 조회 ─────────────────────────────────────────
    async Task FetchBalanceAsync()
    {
        string key = _txtKey?.Text.Trim() is { Length: > 0 } k ? k : _apiKey;
        if (string.IsNullOrEmpty(key)) { SetBalance(null, "API 키 없음"); return; }
        SetBalanceText("잔액 조회중...");
        try
        {
            var url  = $"{ApiUrl}/api/noim/sms/balance-by-key?apiKey={Uri.EscapeDataString(key)}";
            var json = await Http.GetStringAsync(url);
            var doc  = JsonDocument.Parse(json).RootElement;
            if (!IsHandleCreated || IsDisposed) return;
            Invoke(() => SetBalance(doc));
        }
        catch (Exception ex) { if (!IsDisposed) Invoke(() => SetBalance(null, ex.Message)); }
    }

    void SetBalance(JsonElement? el, string? errMsg = null)
    {
        if (el is null || !(el.Value.TryGetProperty("valid", out var v) && v.GetBoolean()))
        {
            _balance = 0;
            _btnSend.Enabled = _btnKakao.Enabled = false;
            string msg = el?.TryGetProperty("message", out var m) == true ? m.GetString() ?? "" : errMsg ?? "조회 실패";
            SetBalanceText($"잔액: 0원  ({msg})");
            return;
        }
        if (el.Value.TryGetProperty("isApproved", out var ap) && !ap.GetBoolean())
        {
            _balance = 0;
            _btnSend.Enabled = _btnKakao.Enabled = false;
            SetBalanceText("⛔ 미승인 계정 — 관리자에게 승인 요청");
            return;
        }
        _balance = el.Value.TryGetProperty("balance", out var b) ? b.GetInt64() : 0;
        _btnSend.Enabled = _btnKakao.Enabled = true;
        SetBalanceText($"잔여 잔액: {_balance:N0}원");
        _ = FetchChannelAsync();
    }

    void SetBalanceText(string text)
    {
        if (_lblBal1 != null) _lblBal1.Text = text;
        if (_lblBal2 != null) _lblBal2.Text = text;
    }

    async Task FetchChannelAsync()
    {
        string key = _txtKey?.Text.Trim() is { Length: > 0 } k ? k : _apiKey;
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            var url  = $"{ApiUrl}/api/noim/kakao/channel-by-api-key?apiKey={Uri.EscapeDataString(key)}";
            var json = await Http.GetStringAsync(url);
            var doc  = JsonDocument.Parse(json).RootElement;
            bool has = doc.TryGetProperty("hasSenderKey", out var h) && h.GetBoolean();
            _channelSenderKey = has && doc.TryGetProperty("senderKey", out var sk)
                ? sk.GetString() ?? "" : "";
            await FetchTemplatesAsync(sender_key: _channelSenderKey);
        }
        catch { await FetchTemplatesAsync(); }
    }

    // ── 템플릿 조회 ───────────────────────────────────────
    async Task FetchTemplatesAsync(bool silent = true, string sender_key = "")
    {
        string key = _txtKey?.Text.Trim() is { Length: > 0 } k ? k : _apiKey;
        if (string.IsNullOrEmpty(key))
        {
            if (!silent && !IsDisposed) Invoke(() => MessageBox.Show("API 키를 먼저 입력·저장하세요.", "알림"));
            return;
        }
        try
        {
            var qs   = string.IsNullOrEmpty(sender_key)
                ? $"apiKey={Uri.EscapeDataString(key)}"
                : $"apiKey={Uri.EscapeDataString(key)}&senderKey={Uri.EscapeDataString(sender_key)}";
            var json = await Http.GetStringAsync($"{ApiUrl}/api/noim/kakao/templates?{qs}");
            var doc  = JsonDocument.Parse(json);
            if (!IsHandleCreated || IsDisposed) return;
            Invoke(() => PopulateTemplates(doc.RootElement, silent, sender_key));
        }
        catch (Exception ex)
        {
            if (!silent && !IsDisposed)
                Invoke(() => MessageBox.Show($"템플릿 조회 오류:\n{ex.Message}", "오류"));
        }
    }

    static readonly HashSet<string> TplSkip = ["REQ", "RJT", "REJ", "UPD", "DEL", "STOP"];

    void PopulateTemplates(JsonElement root, bool silent, string senderKey)
    {
        JsonElement[]? raw = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToArray()
            : new[] { "data", "list", "templates", "result", "items" }
                .Select(k => root.TryGetProperty(k, out var v) ? v : default)
                .FirstOrDefault(v => v.ValueKind == JsonValueKind.Array)
                .EnumerateArray().ToArray();

        var templates = (raw ?? []).Where(t =>
        {
            foreach (var sf in new[] { "inspStatus", "templtStatus", "status", "templateStatus" })
                if (t.TryGetProperty(sf, out var sv) && TplSkip.Contains(sv.GetString()?.ToUpper() ?? ""))
                    return false;
            string name = GetStr(t, "templtName", "name", "templateName");
            return name.StartsWith("근로내역서");
        }).ToArray();

        if (templates.Length == 0)
        {
            if (!string.IsNullOrEmpty(senderKey))
            {
                _channelSenderKey = "";
                _ = FetchTemplatesAsync(silent: true, sender_key: "");
                return;
            }
            if (!silent) MessageBox.Show("승인된 템플릿이 없습니다.", "알림");
            _lblCurTpl.Text = $"현재 선택: {_tplCode}  (조회 결과 없음)";
            return;
        }

        _tplCache.Clear();
        _grdTpl.Rows.Clear();
        foreach (var t in templates)
        {
            string code    = GetStr(t, "templtCode", "code", "templateCode");
            string name    = GetStr(t, "templtName", "name", "templateName");
            string content = GetStr(t, "templtContent", "content", "templateContent", "body");
            string emTitle = GetStr(t, "emTitle", "emphasisTitle", "em_title", "emphasisText", "emtitle");
            _tplCache[code] = new TplInfo(name, content, emTitle);
            int r = _grdTpl.Rows.Add(code, name);
            if (code == _tplCode)
                _grdTpl.Rows[r].DefaultCellStyle.ForeColor = Color.FromArgb(100, 200, 130);
        }

        _lblCurTpl.Text = $"현재 선택: {_tplCode}";

        if (!string.IsNullOrEmpty(_channelSenderKey) && templates.Length > 0)
        {
            string first = GetStr(templates[0], "templtCode", "code", "templateCode");
            if (!string.IsNullOrEmpty(first) && first != _tplCode)
                SetTemplate(first, silent: true);
        }

        if (_payroll.Count > 0) ApplyPayrollMessages(silent: true);
    }

    void SelectTemplate(int rowIdx)
    {
        string code = _grdTpl.Rows[rowIdx].Cells[0].Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(code)) return;
        SetTemplate(code, silent: false);
    }

    void PreviewTemplate(int rowIdx)
    {
        string code = _grdTpl.Rows[rowIdx].Cells[0].Value?.ToString() ?? "";
        if (_tplCache.TryGetValue(code, out var info))
        {
            string preview = string.IsNullOrEmpty(info.EmTitle)
                ? info.Content
                : $"[강조 타이틀]\n{info.EmTitle}\n\n[본문]\n{info.Content}";
            _txtTplPreview.Text = preview.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
    }

    void SetTemplate(string code, bool silent)
    {
        _tplCode = code;
        SaveSetting("카카오_TPL_CODE", code);
        string name = _tplCache.TryGetValue(code, out var info) ? info.Name : code;
        _lblCurTpl.Text = $"현재 선택: {code}";
        for (int r = 0; r < _grdTpl.Rows.Count; r++)
        {
            bool cur = _grdTpl.Rows[r].Cells[0].Value?.ToString() == code;
            _grdTpl.Rows[r].DefaultCellStyle.ForeColor = cur
                ? Color.FromArgb(100, 200, 130) : ThemeManager.TextMain;
        }
        if (_payroll.Count > 0) ApplyPayrollMessages(silent: true);
        if (!silent)
            MessageBox.Show($"[{name}]이(가) 선택되었습니다.", "템플릿 선택");
    }

    // ── 노무비명세표 메시지 생성 ──────────────────────────
    void ApplyPayrollMessages(bool silent)
    {
        string workYm = $"{_year}년 {_month:D2}월";

        string tplContent = _tplCache.TryGetValue(_tplCode, out var ti) ? ti.Content : "";
        string tplEmTitle = ti?.EmTitle ?? "";

        _payrollMsgs.Clear();
        _payrollEmTitles.Clear();

        for (int i = 0; i < _workers.Count && i < _payroll.Count; i++)
        {
            string name = _workers[i].name;
            var p = _payroll[i];

            var rep = new Dictionary<string, string>
            {
                ["#{siteName}"]             = _project,
                ["#{workYearMonth}"]        = workYm,
                ["#{userName}"]             = name,
                ["#{company}"]              = _companyName,
                ["#{workDays}"]             = FmtDays(p.GetV("일수")),
                ["#{weeklyRestAllowance}"]  = FmtNum(p.GetV("주휴수당")),
                ["#{totalLaborCost}"]       = FmtNum(p.GetV("임금총액")),
                ["#{totalDeductions}"]      = FmtNum(p.GetV("공제합계")),
                ["#{incomeTax}"]            = FmtNum(p.GetV("소득세")),
                ["#{localIncomeTax}"]       = FmtNum(p.GetV("주민세")),
                ["#{nationalPension}"]      = FmtNum(p.GetV("국민연금")),
                ["#{healthInsurance}"]      = FmtNum(p.GetV("건강보험")),
                ["#{longTermCareInsurance}"]= FmtNum(p.GetV("요양보험")),
                ["#{employmentInsurance}"]  = FmtNum(p.GetV("고용보험")),
                ["#{netPay}"]               = FmtNum(p.GetV("차감지급액")),
            };

            string msg;
            if (!string.IsNullOrEmpty(tplContent))
            {
                msg = rep.Aggregate(tplContent, (s, kv) => s.Replace(kv.Key, kv.Value));
            }
            else
            {
                string co = string.IsNullOrEmpty(_companyName) ? "" : _companyName + "\n";
                msg = $"{co}{_project}\n{workYm} 노임명세표\n\n"
                    + $"성 명: {name}\n근무일수: {FmtDays(p.GetV("일수"))}일\n"
                    + $"주휴수당: {FmtNum(p.GetV("주휴수당"))}원\n"
                    + $"노무비총액: {FmtNum(p.GetV("임금총액"))}원\n\n"
                    + $"[공제 내역]\n"
                    + $"공제액계: {FmtNum(p.GetV("공제합계"))}원\n"
                    + $"소득세: {FmtNum(p.GetV("소득세"))}원\n"
                    + $"지방소득세: {FmtNum(p.GetV("주민세"))}원\n"
                    + $"국민연금: {FmtNum(p.GetV("국민연금"))}원\n"
                    + $"건강보험: {FmtNum(p.GetV("건강보험"))}원\n"
                    + $"장기요양보험: {FmtNum(p.GetV("요양보험"))}원\n"
                    + $"고용보험: {FmtNum(p.GetV("고용보험"))}원\n\n"
                    + $"실지급액: {FmtNum(p.GetV("차감지급액"))}원";
            }
            _payrollMsgs[i] = msg.Replace("\r\n", "\n").Replace("\r", "\n");

            string emTitle = string.IsNullOrEmpty(tplEmTitle)
                ? "노임명세표"
                : rep.Aggregate(tplEmTitle, (s, kv) => s.Replace(kv.Key, kv.Value));
            _payrollEmTitles[i] = emTitle;
        }

        if (_txtMsg != null && _payrollMsgs.TryGetValue(0, out var preview))
            _txtMsg.Text = preview.Replace("\r\n", "\n").Replace("\n", "\r\n");

        if (!silent && _payrollMsgs.Count > 0)
            MessageBox.Show($"{_payrollMsgs.Count}명의 개별 명세 메시지가 준비되었습니다.\n발송 버튼을 누르면 각자에게 전송됩니다.", "노무비명세표");
    }

    // ── 문자 발송 ─────────────────────────────────────────
    async Task SendSmsAsync()
    {
        string msg = _txtMsg.Text.Trim();
        if (string.IsNullOrEmpty(msg)) { MessageBox.Show("메시지를 입력해 주세요.", "오류"); return; }
        string key = _txtKey?.Text.Trim() is { Length: > 0 } k ? k : _apiKey;
        if (string.IsNullOrEmpty(key)) { MessageBox.Show("API 키를 입력해 주세요.", "오류"); return; }

        var valid = _workers.Where(w => !string.IsNullOrWhiteSpace(w.phone)).ToList();
        if (!CheckPhoneWarning(valid)) return;
        if (!CheckBalance(valid.Count, msg)) return;

        _btnSend.Enabled = false; _btnSend.Text = "문자발송중...";

        var workers = _payrollMsgs.Count > 0
            ? valid.Select((w, i) =>
            {
                int idx = _workers.IndexOf(w);
                return new { name = w.name, phone = w.phone, message = _payrollMsgs.GetValueOrDefault(idx, msg) };
            }).ToArray()
            : valid.Select(w => new { name = w.name, phone = w.phone, message = msg }).ToArray();

        var payload = new { apiKey = key, message = msg, workers };
        await PostAndHandleAsync($"{ApiUrl}/api/noim/sms/send", payload,
            ok =>
            {
                int oks = ok.TryGetProperty("successCnt", out var s) ? s.GetInt32() : 0;
                int fail = ok.TryGetProperty("failCnt",   out var f) ? f.GetInt32() : 0;
                long ded  = ok.TryGetProperty("deducted",  out var d) ? d.GetInt64() : 0;
                long rem  = ok.TryGetProperty("remainBalance", out var r) ? r.GetInt64() : 0;
                MessageBox.Show($"발송 완료!\n\n성공: {oks}건  실패: {fail}건\n차감: {ded:N0}원\n잔여: {rem:N0}원", "발송 완료");
                Close();
            },
            err => { _btnSend.Enabled = true; _btnSend.Text = $"문자발송  ({valid.Count}명)"; });
    }

    // ── 카카오 발송 ───────────────────────────────────────
    async Task SendKakaoAsync()
    {
        if (_payrollMsgs.Count == 0 && _payroll.Count > 0)
            ApplyPayrollMessages(silent: true);

        string msg = _txtMsg.Text.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
        if (string.IsNullOrEmpty(msg)) { MessageBox.Show("메시지를 입력해 주세요.", "오류"); return; }
        string key = _txtKey?.Text.Trim() is { Length: > 0 } k ? k : _apiKey;
        if (string.IsNullOrEmpty(key)) { MessageBox.Show("API 키를 입력해 주세요.", "오류"); return; }

        if (_balance <= 0) { await SendKakaoTestAsync(msg, key); return; }

        var valid = _workers.Where(w => !string.IsNullOrWhiteSpace(w.phone)).ToList();
        if (!CheckPhoneWarning(valid)) return;
        if (valid.Count == 0) return;

        long kakaoTotal = 11L * valid.Count;
        if (kakaoTotal > _balance)
        {
            MessageBox.Show($"충전 잔액이 부족합니다.\n예상 비용: {kakaoTotal:N0}원, 잔여: {_balance:N0}원", "잔액 부족");
            return;
        }

        if (string.IsNullOrEmpty(_tplCode))
        {
            MessageBox.Show("카카오 템플릿을 선택하세요.\n(알림톡 템플릿 탭에서 선택)", "오류");
            SwitchTab(2); return;
        }

        string emDefault = _payrollEmTitles.TryGetValue(0, out var et) ? et : "노임명세표";
        string CleanPhone(string p) => new string(p.Where(char.IsDigit).ToArray());

        var workers = valid.Select((w, i) =>
        {
            int idx = _workers.IndexOf(w);
            string wMsg  = _payrollMsgs.GetValueOrDefault(idx, msg).Replace("\r\n", "\n");
            string wEm   = _payrollEmTitles.GetValueOrDefault(idx, emDefault);
            return new { name = w.name, phone = CleanPhone(w.phone), message = wMsg, emTitle = wEm, emtitle_1 = wEm };
        }).ToArray();

        var payload = new
        {
            apiKey    = key,
            senderKey = _channelSenderKey,
            tplCode   = _tplCode,
            message   = msg,
            emTitle   = emDefault, emtitle_1 = emDefault,
            workers,
        };

        _btnKakao.Enabled = false; _btnKakao.Text = "카카오톡 발송중...";
        await PostAndHandleAsync($"{ApiUrl}/api/noim/kakao/send", payload,
            ok =>
            {
                int oks  = ok.TryGetProperty("successCnt", out var s) ? s.GetInt32() : 0;
                int fail = ok.TryGetProperty("failCnt",    out var f) ? f.GetInt32() : 0;
                long ded = ok.TryGetProperty("deducted",   out var d) ? d.GetInt64() : 0;
                long rem = ok.TryGetProperty("remainBalance", out var r) ? r.GetInt64() : 0;
                string body = $"발송 완료!\n\n성공: {oks}건  실패: {fail}건\n차감: {ded:N0}원\n잔여: {rem:N0}원";
                if (fail > 0) MessageBox.Show(body, "카카오톡 발송 결과", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else { MessageBox.Show(body, "카카오톡 발송 완료"); Close(); }
            },
            err => { _btnKakao.Enabled = true; _btnKakao.Text = $"카카오톡발송  ({valid.Count}명)"; });
    }

    async Task SendKakaoTestAsync(string msg, string key)
    {
        var ans = MessageBox.Show(
            "아직 충전이 되지 않았습니다.\n\n테스트로 귀하의 휴대폰으로 받아보시겠습니까?\n(mmsoft 채널로 5회까지 무료 발송)",
            "테스트 발송 안내", MessageBoxButtons.YesNo);
        if (ans != DialogResult.Yes) return;

        string phone = Microsoft.VisualBasic.Interaction.InputBox(
            "받으실 휴대폰 번호를 입력하세요:\n(예: 010-1234-5678)", "테스트 발송", "");
        if (string.IsNullOrWhiteSpace(phone)) return;

        string emDefault = _payrollEmTitles.TryGetValue(0, out var et) ? et : "노임명세표";
        string topMsg = _payrollMsgs.TryGetValue(0, out var pm) ? pm : msg;
        string topName = _workers.Count > 0 ? _workers[0].name : "테스트";
        string cleanPhone = new string(phone.Where(char.IsDigit).ToArray());

        var payload = new
        {
            tplCode = _tplCode, message = topMsg,
            emTitle = emDefault, emtitle_1 = emDefault, isTest = true,
            workers = new[] { new { name = topName, phone = cleanPhone, message = topMsg, emTitle = emDefault, emtitle_1 = emDefault } },
        };
        _btnKakao.Enabled = false; _btnKakao.Text = "테스트 발송중...";
        await PostAndHandleAsync($"{ApiUrl}/api/noim/kakao/send", payload,
            ok => MessageBox.Show("테스트 발송이 완료되었습니다.", "완료"),
            err => { });
        _btnKakao.Enabled = true; _btnKakao.Text = $"카카오톡발송  ({_workers.Count(w => !string.IsNullOrWhiteSpace(w.phone))}명)";
    }

    async Task PostAndHandleAsync<T>(string url, T payload, Action<JsonElement> onSuccess, Action<string> onFail)
    {
        try
        {
            var body = JsonSerializer.Serialize(payload);
            var resp = await Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json).RootElement;
            if (!IsHandleCreated || IsDisposed) return;
            Invoke(() =>
            {
                bool ok = doc.TryGetProperty("success", out var s) && s.GetBoolean();
                if (ok) onSuccess(doc);
                else
                {
                    string err = doc.TryGetProperty("message", out var m) ? m.GetString() ?? "실패" : "발송에 실패했습니다.";
                    MessageBox.Show(err, "발송 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    onFail(err);
                }
            });
        }
        catch (Exception ex)
        {
            if (!IsDisposed) Invoke(() => { MessageBox.Show(ex.Message, "오류"); onFail(ex.Message); });
        }
    }

    bool CheckPhoneWarning(List<(string name, string phone)> valid)
    {
        var noPhone = _workers.Where(w => string.IsNullOrWhiteSpace(w.phone)).ToList();
        if (valid.Count == 0) { MessageBox.Show("전화번호가 있는 근로자가 없습니다.", "오류"); return false; }
        if (noPhone.Count > 0)
        {
            string names = string.Join(", ", noPhone.Select(w => w.name));
            var ans = MessageBox.Show(
                $"전화번호 미등록 근로자는 제외됩니다:\n{names}\n\n{valid.Count}명에게 발송하시겠습니까?",
                "전화번호 없음", MessageBoxButtons.YesNo);
            if (ans != DialogResult.Yes) return false;
        }
        return true;
    }

    bool CheckBalance(int n, string msg)
    {
        if (_balance <= 0 || n <= 0) return true;
        string mtype = msg.Length <= 90 ? "SMS(단문)" : "LMS(장문)";
        long cost = (msg.Length <= 90 ? 17L : 39L) * n;
        if (cost > _balance)
        {
            MessageBox.Show(
                $"충전 잔액이 부족합니다.\n{mtype} {n}건 예상비용: {cost:N0}원, 잔여: {_balance:N0}원",
                "잔액 부족");
            return false;
        }
        return true;
    }

    // ── 메시지 글자수 표시 ────────────────────────────────
    void OnMsgChanged(object? s, EventArgs e)
    {
        int n = _txtMsg.Text.Length;
        string mtype = n <= 90 ? "SMS(단문)" : n <= 2000 ? "LMS(장문)" : "MMS(멀티)";
        int cost = n <= 90 ? 17 : n <= 2000 ? 39 : 99;
        _lblChars.Text = $"{n}자  [{mtype} - {cost}원/건]   [카카오톡 - 11원/건]";
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    DataGridView MakeSimpleGrid(string[] headers, int[] firstWidths)
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, RowHeadersVisible = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = ThemeManager.BgCell, GridColor = ThemeManager.GridLine,
            BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        };
        for (int i = 0; i < headers.Length; i++)
        {
            var col = new DataGridViewTextBoxColumn { HeaderText = headers[i] };
            if (i < firstWidths.Length) col.Width = firstWidths[i];
            else col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            g.Columns.Add(col);
        }
        if (headers.Length > 1 && firstWidths.Length < headers.Length)
            g.Columns[^1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        g.ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.BgHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.HeaderText;
        g.DefaultCellStyle.BackColor = ThemeManager.BgCell;
        g.DefaultCellStyle.ForeColor = ThemeManager.TextMain;
        return g;
    }

    static Label MakeLabel(string text, Color fg, float size, FontStyle fs) => new()
    {
        Text = text, AutoSize = true, ForeColor = fg, BackColor = Color.Transparent,
        Font = ThemeManager.F(size, fs), Margin = new Padding(0, 4, 8, 0),
    };

    static Button MakeDlgBtn(string text, int w, Color bg, Color fg) => new()
    {
        Text = text, Width = w, Height = 32, FlatStyle = FlatStyle.Flat,
        BackColor = bg, ForeColor = fg, Font = ThemeManager.F(9.5f, FontStyle.Bold),
        Cursor = Cursors.Hand, Margin = new Padding(4, 0, 0, 0),
    };

    static Button MakeSmBtn(string text) => new()
    {
        Text = text, AutoSize = true, Height = 24, FlatStyle = FlatStyle.Flat,
        BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
        Font = ThemeManager.F(9f), Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 6, 0), Padding = new Padding(6, 0, 6, 0),
    };

    static string GetStr(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }

    static string FmtNum(string v)
    {
        if (long.TryParse(v.Replace(",", "").Trim(), out var n)) return $"{n:N0}";
        if (double.TryParse(v.Replace(",", "").Trim(), out var d)) return $"{(long)d:N0}";
        return v;
    }

    static string FmtDays(string v)
    {
        if (double.TryParse(v.Replace(",", "").Trim(), out var d))
            return d == Math.Floor(d) ? ((long)d).ToString() : d.ToString("0.0");
        return v;
    }
}

// Extension helper
file static class DictExt
{
    public static string GetV(this Dictionary<string, string> d, string key)
        => d.TryGetValue(key, out var v) ? v : "";
}

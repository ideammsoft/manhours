using manHours.Forms;

namespace manHours.Screens.Noim;

public class WorkerScreen : UserControl
{
    // ── 이벤트 ────────────────────────────────────────────
    public event Action? GoPrev;
    public event Action? GoNext;

    // ── 레이아웃 상수 ─────────────────────────────────────
    const int N_INFO     = 7;
    const int N_DATES    = 16;
    const int N_SUMS     = 11;
    const int N_DISP     = N_INFO + N_DATES + N_SUMS;   // 34
    const int D_DATE_COL = 7;    // 그리드 컬럼 인덱스
    const int D_SUM_COL  = 23;
    const int D_DATE_DB  = 9;    // DB 컬럼 인덱스 (PayCols)
    const int D_SUM_DB   = 40;

    static readonly int[] InfoWidths  = [40, 37, 37, 35, 90, 120, 50];
    static readonly int   DateWidth   = 58;   // 출퇴근 시각("18-22") 표시용
    static readonly int[] SumWidths   = [56, 56, 56, 62, 72, 66, 72, 66, 66, 62, 72];
    static readonly string[] InfoHdrs = ["☐\n전체", "금액\n자동", "휴게\n자동", "순번", "성명\n전화번호", "생년월일6자리\n거주지역", "근무\n시작일"];
    static readonly string[] SumHdrs  = ["근무시간\n근무일수", "주휴시간\n휴일시간", "연장시간\n야간시간", "시급\n최저미달", "기본급\n주휴수당", "연장수당\n야간수당", "휴일수당\n임금총액", "국민연금\n건강보험", "고용보험\n요양보험", "소득세\n주민세", "공제합계\n차감지급액"];

    // ── DB 인덱스 (PayCols) ───────────────────────────────
    const int I_SELECT    = 0;
    const int I_AUTO      = 1;
    const int I_BREAK     = 2;    // 휴게자동 (0/1)
    const int I_SEQ       = 3;
    const int I_NAME      = 4;
    const int I_JUMIN     = 5;
    const int I_START     = 6;
    const int I_PHONE     = 7;
    const int I_ADDR      = 8;
    // I_DAY1 = D_DATE_DB = 9 ... I_DAY31 = 39  (값: "18:00-22:00")
    const int I_HOURS     = 40;   // 총근무시간
    const int I_DAYS      = 41;   // 근무일수
    const int I_JUHU_H    = 42;   // 주휴시간
    const int I_HOL_H     = 43;   // 휴일시간
    const int I_OT_H      = 44;   // 연장시간
    const int I_NIGHT_H   = 45;   // 야간시간
    const int I_WAGE      = 46;   // 시급
    const int I_MINWARN   = 47;   // 최저임금미달
    const int I_BASIC     = 48;   // 기본급
    const int I_JUHU_PAY  = 49;   // 주휴수당
    const int I_OT_PAY    = 50;   // 연장수당
    const int I_NIGHT_PAY = 51;   // 야간수당
    const int I_HOL_PAY   = 52;   // 휴일수당
    const int I_TOTAL     = 53;   // 임금총액
    const int I_KUKMIN    = 54;
    const int I_HEALTH    = 55;
    const int I_EMPLOY    = 56;
    const int I_CARE      = 57;
    const int I_TAX       = 58;
    const int I_MINTAX    = 59;
    const int I_DEDUCT    = 60;
    const int I_NET       = 61;

    // ── 상태 ──────────────────────────────────────────────
    int    _year, _month;
    string _project = "";
    int    _lastDay = 31;
    HashSet<int> _holidays = [];
    HashSet<int> _customHolidays = [];
    List<string[]> _workers = [];   // 각 원소: PayCols.Length 개 문자열

    // ── 요율 (연도별 법정기준이 기본값 → 사업설정에 값이 있으면 그쪽이 우선) ──
    double _rKukmin = 4.5, _rHealth = 3.545, _rEmploy = 0.9, _rCare = 12.95;
    double _rTax = 0, _rMinTax = 10;
    // 연도별 법정기준 (하드코딩 금지 — 매년 바뀜)
    double _dailyDeduct  = 150_000;  // 일용근로소득 일 공제액
    double _minTaxCut    = 1_000;    // 소액부징수 기준
    double _minWage      = 0;        // 최저임금(시급). 0 = 미입력 → 검증 안 함
    double _juhuHours    = 15;       // 주휴 발생 기준(주 근로시간)
    double _insMinHours  = 60;       // 4대보험 가입기준(월 시간)
    double _insMinDays   = 8;        // 4대보험 가입기준(월 일수)
    int    _extraMinStaff = 5;       // 가산수당 의무 최소 인원
    double _break4Min    = 30;       // 4시간 이상 근무 시 휴게(분)
    double _break8Min    = 60;       // 8시간 이상 근무 시 휴게(분)
    int    _staffCount   = 0;        // 사업장 상시근로자수 (사업설정)

    // ── 컨트롤 ────────────────────────────────────────────
    DataGridView _grid = null!;
    Label        _lblStatus = null!;
    TextBox      _txtWarn  = null!;
    Panel        _pnlWarn  = null!;
    ComboBox     _cmbTimes = null!;          // 시간 목록(주간 근무표와 공용)

    // 시간 프리셋 + 날짜 셀 콤보에 들어 있는 표시값
    List<string> _presets = [];
    readonly HashSet<string> _timeOptions = [];

    public WorkerScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    // ── UI ────────────────────────────────────────────────
    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // 경보 패널
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        root.Controls.Add(BuildToolbar(),   0, 0);
        root.Controls.Add(BuildWarnPanel(), 0, 1);
        root.Controls.Add(BuildGrid(),      0, 2);
        root.Controls.Add(BuildBottomBar(), 0, 3);
        Controls.Add(root);
    }

    Panel BuildToolbar()
    {
        var p    = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgPanel };
        var sep  = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ThemeManager.Border };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(8, 6, 8, 6) };
        flow.Controls.Add(MakeToolBtn("↺ 새로고침", 96, () => Load(_year, _month, _project)));
        flow.Controls.Add(MakeToolBtn("신규 추가",   80, AddWorker));
        var btnSave = MakeToolBtn("저장", 70, SaveData);
        btnSave.BackColor = Color.FromArgb(30, 100, 60);
        btnSave.ForeColor = Color.White;
        flow.Controls.Add(btnSave);

        // 시간 목록 관리 — 주간 근무표와 같은 프리셋을 쓴다
        flow.Controls.Add(new Label
        {
            Text = "시간 목록 →", AutoSize = true, Margin = new Padding(18, 8, 6, 0),
            Font = ThemeManager.F(9f), ForeColor = ThemeManager.TextSub, BackColor = Color.Transparent,
        });
        _cmbTimes = new ComboBox
        {
            Width = 140, DropDownStyle = ComboBoxStyle.DropDown,   // 편집 가능 → 직접 입력
            Font = ThemeManager.F(9f), Margin = new Padding(0, 4, 6, 0),
        };
        _cmbTimes.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddTime(); } };
        flow.Controls.Add(_cmbTimes);
        flow.Controls.Add(MakeToolBtn("＋ 시간추가", 92, AddTime));
        flow.Controls.Add(MakeToolBtn("－ 시간삭제", 92, DeleteTime));

        // Bottom 먼저 추가 → Fill이 나머지 공간 차지
        p.Controls.Add(sep);
        p.Controls.Add(flow);
        return p;
    }

    DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock                    = DockStyle.Fill,
            BackgroundColor         = ThemeManager.BgCell,
            GridColor               = ThemeManager.GridLine,
            BorderStyle             = BorderStyle.None,
            ColumnHeadersHeight     = 52,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible       = false,
            SelectionMode           = DataGridViewSelectionMode.CellSelect,
            MultiSelect             = true,
            AllowUserToAddRows      = false,
            AllowUserToDeleteRows   = false,
            AllowUserToResizeRows   = false,
            ScrollBars              = ScrollBars.Both,
            AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.None,
            EnableHeadersVisualStyles = false,
        };

        // 스타일
        _grid.ColumnHeadersDefaultCellStyle.BackColor  = ThemeManager.BgHeader;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor  = ThemeManager.HeaderText;
        _grid.ColumnHeadersDefaultCellStyle.Font       = ThemeManager.F(8.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.BackColor               = ThemeManager.BgCell;
        _grid.DefaultCellStyle.ForeColor               = ThemeManager.TextMain;
        _grid.DefaultCellStyle.Font                    = ThemeManager.F(9f);
        _grid.DefaultCellStyle.Alignment               = DataGridViewContentAlignment.MiddleCenter;
        _grid.DefaultCellStyle.SelectionBackColor      = ThemeManager.SelectBg;
        _grid.DefaultCellStyle.SelectionForeColor      = ThemeManager.SelectFg;
        _grid.AlternatingRowsDefaultCellStyle.BackColor          = ThemeManager.BgAltCell;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = ThemeManager.SelectBg;
        _grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = ThemeManager.SelectFg;

        SetupColumns();
        _grid.CellPainting         += OnCellPainting;
        _grid.CellClick            += OnCellClick;
        _grid.CellEndEdit          += OnCellEndEdit;
        _grid.MouseDown            += OnGridMouseDown;
        _grid.KeyDown              += OnGridKeyDown;
        _grid.ClipboardCopyMode     = DataGridViewClipboardCopyMode.Disable;
        _grid.ColumnHeaderMouseClick += OnHeaderClick;
        _grid.DataError += (_, e) => { e.ThrowException = false; };   // 콤보 값 불일치 무시
        _grid.CurrentCellDirtyStateChanged += (_, _) =>               // 콤보 선택 즉시 커밋
        { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += OnCellValueChanged;
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

        var btnPrev = MakeToolBtn("← 이전 단계", 104, () => GoPrev?.Invoke());
        btnPrev.Dock = DockStyle.Fill;

        _lblStatus = new Label
        {
            Dock          = DockStyle.Fill,
            ForeColor     = ThemeManager.TextSub,
            Font          = ThemeManager.F(9f),
            TextAlign     = ContentAlignment.MiddleCenter,
            AutoEllipsis  = true,
        };

        var btnNext = MakeToolBtn("다음 단계 →", 104, () => { SaveData(); GoNext?.Invoke(); });
        btnNext.Dock      = DockStyle.Fill;
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

        // INFO 컬럼 (0-6)
        for (int i = 0; i < N_INFO; i++)
        {
            DataGridViewColumn col;
            if (i < 3) // 체크박스
            {
                col = new DataGridViewCheckBoxColumn
                {
                    Width    = InfoWidths[i],
                    ReadOnly = false,
                };
            }
            else
            {
                col = new DataGridViewTextBoxColumn
                {
                    Width    = InfoWidths[i],
                    ReadOnly = i != 6, // 근무시작일은 편집 가능
                };
                if (i == 4 || i == 5)
                    ((DataGridViewTextBoxColumn)col).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            }
            col.HeaderText  = InfoHdrs[i];
            col.HeaderCell  = new MultiLineHeaderCell { Value = InfoHdrs[i] };
            col.SortMode    = DataGridViewColumnSortMode.NotSortable;
            col.Resizable   = DataGridViewTriState.True;
            _grid.Columns.Add(col);
        }

        // DATE 컬럼 (7-22): 시간 목록에서 골라 넣는다 (직접 타이핑 대신)
        for (int s = 0; s < N_DATES; s++)
        {
            var col = new DataGridViewComboBoxColumn
            {
                HeaderText   = "",
                Width        = DateWidth,
                ReadOnly     = false,
                SortMode     = DataGridViewColumnSortMode.NotSortable,
                FlatStyle    = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,  // 셀은 직접 그린다
                AutoComplete = false,
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell = new MultiLineHeaderCell { Value = "" };
            _grid.Columns.Add(col);
        }

        // SUM 컬럼 (23-33)
        for (int i = 0; i < N_SUMS; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = SumHdrs[i],
                Width      = SumWidths[i],
                ReadOnly   = false,
                SortMode   = DataGridViewColumnSortMode.NotSortable,
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.HeaderCell = new MultiLineHeaderCell { Value = SumHdrs[i] };
            _grid.Columns.Add(col);
        }
    }

    // ── 데이터 로드 ───────────────────────────────────────
    public new void Load(int year, int month, string project)
    {
        _year    = year;
        _month   = month;
        _project = project;
        _lastDay = DateTime.DaysInMonth(year, month);
        _holidays = AppConfig.GetHolidayDays(year, month);
        _customHolidays.Clear();
        LoadSettings();
        LoadWorkers();
        RefreshTimeOptions();
        _grid.Columns[0].HeaderCell.Value = "☐\n전체";   // 전체선택은 기억하지 않는다
        // 로드 시 현재 법정기준(최저임금·요율 등)으로 자동계산 재실행
        //  → 나중에 최저임금을 바꿔도 '최저미달' 등 파생값이 반영된다.
        for (int wi = 0; wi < _workers.Count; wi++)
            if (_workers[wi][I_AUTO] == "1") AutoCalc(wi);
        RefreshGrid();
        UpdateStatus();
    }

    void LoadSettings()
    {
        // 1) 해당 연도의 법정기준을 기본값으로 깔고
        var std = AppConfig.GetLegalStandard(_year);
        _rKukmin     = std.Kukmin;
        _rHealth     = std.Health;
        _rEmploy     = std.Employ;
        _rCare       = std.Care;
        _rTax         = std.TaxRate;
        _rMinTax      = std.LocalTax;
        _dailyDeduct  = std.DailyDeduct;
        _minTaxCut    = std.MinTaxCut;
        _minWage      = std.MinWage;
        _juhuHours    = std.JuhyuHours;
        _insMinHours  = std.InsMinHours;
        _insMinDays   = std.InsMinDays;
        _extraMinStaff = std.ExtraPayMinStaff;
        _break4Min    = std.Break4Min;
        _break8Min    = std.Break8Min;
        _staffCount   = 0;

        // 2) 사업설정에 값이 있으면 그 사업장 값으로 덮어쓴다(예외 사업장 허용)
        if (string.IsNullOrEmpty(_project)) return;
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            db.EnsureTable("사업설정", AppConfig.BusiSettingCols);
            var (cols, rows) = db.SelectStrings("사업설정",
                $"\"사업명\"='{_project.Replace("'", "''")}'");
            if (rows.Length == 0) return;
            var row = rows[0];
            double Get(string col)
            {
                int i = Array.IndexOf(cols, col);
                return i >= 0 && double.TryParse(row[i], out var v) ? v : 0;
            }
            var r = Get("국민연금"); if (r > 0) _rKukmin = r;
            r = Get("건강보험"); if (r > 0) _rHealth = r;
            r = Get("고용보험"); if (r > 0) _rEmploy = r;
            r = Get("장기요양"); if (r > 0) _rCare   = r;
            r = Get("소득세");   if (r > 0) _rTax    = r;
            r = Get("주민세");   if (r > 0) _rMinTax = r;
            r = Get("상시근로자수"); if (r > 0) _staffCount = (int)r;
        }
        catch { }
    }

    // 전체근로자(시급 원본)를 이름+전화번호로 조회. 한 번만 읽어서 캐시한다.
    Dictionary<string, string>? _masterWages;

    string MasterWage(string name, string phone)
    {
        if (_masterWages == null)
        {
            _masterWages = [];
            try
            {
                using var db = new Database(AppConfig.BaseDbPath);
                db.EnsureTable("전체근로자", AppConfig.WorkerCols);
                var (cols, rows) = db.SelectStrings("전체근로자");
                int ni = Array.IndexOf(cols, "이름"), pi = Array.IndexOf(cols, "전화번호");
                int wi = Array.IndexOf(cols, "시급");
                string V(string[] r, int i) => i >= 0 && i < r.Length ? r[i] : "";
                foreach (var r in rows)
                    _masterWages[AppConfig.WorkerKey(V(r, ni), V(r, pi))] = V(r, wi);
            }
            catch { }
        }
        return _masterWages.GetValueOrDefault(AppConfig.WorkerKey(name, phone), "");
    }

    void LoadWorkers()
    {
        _workers.Clear();
        _masterWages = null;
        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);

        // 근로자목록 로드 (FileScreen 이 저장한 목록)
        string[][]  workerList = [];
        string[]    wlCols     = [];
        if (File.Exists(dbPath))
        {
            using var db = new Database(dbPath);
            if (db.TableExists("근로자목록"))
            {
                (wlCols, workerList) = db.SelectStrings("근로자목록");
            }
        }

        // 지급대장 로드 (기존 임금 데이터) — 동명이인 구분을 위해 이름+전화번호로 키를 잡는다
        Dictionary<string, string[]> payDict = [];
        if (File.Exists(dbPath))
        {
            using var db = new Database(dbPath);
            db.EnsureTable("지급대장", AppConfig.PayCols);
            var (payCols, payRows) = db.SelectStrings("지급대장");
            foreach (var row in payRows)
            {
                var d = new string[AppConfig.PayCols.Length];
                for (int i = 0; i < AppConfig.PayCols.Length; i++)
                {
                    int ci = Array.IndexOf(payCols, AppConfig.PayCols[i]);
                    d[i] = ci >= 0 && ci < row.Length ? row[ci] : "";
                }
                if (!string.IsNullOrEmpty(d[I_NAME]))
                    payDict[AppConfig.WorkerKey(d[I_NAME], d[I_PHONE])] = d;
            }
        }

        // 근로자목록의 순서로 _workers 구성
        int seq = 1;
        int wlNameIdx  = Array.IndexOf(wlCols, "이름");
        int wlPhoneIdx = Array.IndexOf(wlCols, "전화번호");
        int wlWageIdx  = Array.IndexOf(wlCols, "시급");
        foreach (var wRow in workerList)
        {
            string WV(int i) => i >= 0 && i < wRow.Length ? wRow[i] : "";
            string name = WV(wlNameIdx);
            string[] data;
            if (payDict.TryGetValue(AppConfig.WorkerKey(name, WV(wlPhoneIdx)), out var existing))
            {
                data = existing;
                if (string.IsNullOrEmpty(data[I_AUTO])) data[I_AUTO] = "1";
            }
            else
            {
                // 새 근로자 → 기본값 설정
                data = new string[AppConfig.PayCols.Length];
                CopyFromWorkerList(data, wlCols, wRow);
                data[I_START] = "1일";
                data[I_AUTO]  = "1";   // 금액자동 기본 ON
                data[I_BREAK] = "1";   // 휴게 자동공제 기본 ON
            }
            // 시급이 비어 있으면 임금이 전부 0 으로 계산된다.
            // 근로자목록 → 전체근로자(원본) 순으로 채워 넣는다.
            if (string.IsNullOrWhiteSpace(data[I_WAGE])) data[I_WAGE] = WV(wlWageIdx);
            if (string.IsNullOrWhiteSpace(data[I_WAGE]))
                data[I_WAGE] = MasterWage(name, WV(wlPhoneIdx));
            data[I_SELECT] = "";       // '전체 선택'은 기억하지 않고 매번 초기화
            data[I_SEQ] = seq++.ToString();
            _workers.Add(data);
        }

        // 근로자목록이 없으면 지급대장에서 직접 로드
        if (_workers.Count == 0 && payDict.Count > 0)
        {
            foreach (var kv in payDict) { kv.Value[I_SELECT] = ""; _workers.Add(kv.Value); }
        }
    }

    // ── 시간 목록 (주간 근무표와 공용 프리셋) ─────────────
    // 프리셋 + 현재 입력된 시간으로 날짜 셀 콤보 / 관리 콤보의 목록을 만든다.
    void RefreshTimeOptions()
    {
        _presets = TimePresets.Load();

        var displays = new List<string> { "" };            // 빈칸(지우기용)
        void Add(string? norm)
        {
            if (string.IsNullOrEmpty(norm)) return;
            var disp = FmtRange(norm);
            if (!displays.Contains(disp)) displays.Add(disp);
        }
        foreach (var t in _presets) Add(t);
        foreach (var d in _workers)
            for (int day = 1; day <= _lastDay; day++) Add(d[D_DATE_DB + day - 1]);

        _timeOptions.Clear();
        for (int c = D_DATE_COL; c < D_DATE_COL + N_DATES; c++)
        {
            var col = (DataGridViewComboBoxColumn)_grid.Columns[c];
            col.Items.Clear();
            foreach (var disp in displays) col.Items.Add(disp);
        }
        foreach (var disp in displays) _timeOptions.Add(disp);

        string keep = _cmbTimes.Text;
        _cmbTimes.Items.Clear();
        foreach (var disp in displays.Skip(1)) _cmbTimes.Items.Add(disp);
        _cmbTimes.Text = keep;
    }

    // 목록에 없는 시간(붙여넣기 등)이 셀에 들어가도 콤보가 거부하지 않도록 추가해 둔다.
    void EnsureOption(string display)
    {
        if (string.IsNullOrEmpty(display) || !_timeOptions.Add(display)) return;
        for (int c = D_DATE_COL; c < D_DATE_COL + N_DATES; c++)
            ((DataGridViewComboBoxColumn)_grid.Columns[c]).Items.Add(display);
        _cmbTimes.Items.Add(display);
    }

    // 콤보에 입력/선택된 시간을 목록에 추가 (저장은 주간 근무표와 공유)
    void AddTime()
    {
        string norm = NormRange(_cmbTimes.Text);
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
        _cmbTimes.Text = FmtRange(norm);
    }

    void DeleteTime()
    {
        string norm = NormRange(_cmbTimes.Text);
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

    void CopyFromWorkerList(string[] data, string[] wlCols, string[] wRow)
    {
        string[] mapping = ["이름", "생년월일6자리", "전화번호", "거주지역"];
        int[]    targets  = [I_NAME, I_JUMIN, I_PHONE, I_ADDR];
        for (int mi = 0; mi < mapping.Length; mi++)
        {
            int si = Array.IndexOf(wlCols, mapping[mi]);
            if (si >= 0 && si < wRow.Length) data[targets[mi]] = wRow[si];
        }
    }

    // ── 그리드 채우기 ─────────────────────────────────────
    void RefreshGrid()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();

        for (int wi = 0; wi < _workers.Count; wi++)
        {
            AddWorkerRows(wi);
        }
        _grid.ResumeLayout();
    }

    void AddWorkerRows(int wi)
    {
        var d = _workers[wi];
        // Row 0 (top)
        var r0 = new DataGridViewRow();
        r0.CreateCells(_grid);
        r0.Height = 24;
        FillRow(r0, d, 0, wi);
        _grid.Rows.Add(r0);

        // Row 1 (bottom)
        var r1 = new DataGridViewRow();
        r1.CreateCells(_grid);
        r1.Height = 22;
        FillRow(r1, d, 1, wi);
        _grid.Rows.Add(r1);
    }

    bool _syncing;   // 그리드에 값을 넣는 중 — CellValueChanged 무시

    void FillRow(DataGridViewRow row, string[] d, int subRow, int wi)
    {
        _syncing = true;
        try { FillRowCore(row, d, subRow); }
        finally { _syncing = false; }
    }

    void FillRowCore(DataGridViewRow row, string[] d, int subRow)
    {
        // INFO cols
        row.Cells[0].Value = subRow == 0 && d[I_SELECT] == "1";   // ☑
        row.Cells[1].Value = subRow == 0 && d[I_AUTO] == "1"; // 금액자동
        row.Cells[2].Value = subRow == 0 && d[I_BREAK] != "0";   // 휴게자동(기본 ON)
        row.Cells[3].Value = subRow == 0 ? d[I_SEQ] : "";
        row.Cells[4].Value = subRow == 0 ? d[I_NAME] : d[I_PHONE];
        row.Cells[5].Value = subRow == 0 ? d[I_JUMIN] : d[I_ADDR];
        row.Cells[6].Value = subRow == 0 ? d[I_START] : "";

        // DATE cols: slot s → day (s+1) for row 0, day (s+16) for row 1
        for (int s = 0; s < N_DATES; s++)
        {
            int day = subRow == 0
                ? (s < 15 ? s + 1 : 0)
                : (s < 15 ? s + 16 : _lastDay);
            string val = "";
            if (day > 0 && day <= _lastDay)
            {
                int dbIdx = D_DATE_DB + day - 1;
                val = d[dbIdx];
            }
            var disp = FmtRange(val);                          // "18:00-22:00" → "18-22"
            EnsureOption(disp);
            row.Cells[D_DATE_COL + s].Value = disp;
        }

        // SUM cols: pair i → DB[D_SUM_DB + i*2 + subRow]
        for (int i = 0; i < N_SUMS; i++)
        {
            int dbIdx = D_SUM_DB + i * 2 + subRow;
            string val = dbIdx < d.Length ? FmtComma(d[dbIdx]) : "";
            row.Cells[D_SUM_COL + i].Value = val;
        }
    }

    // ── 셀 페인팅 ─────────────────────────────────────────
    // 파이썬의 setSpan(r1,col,2,1)과 동일하게 병합된 것처럼 보이는 컬럼: 0,1,2,3,6
    static bool IsMergedCol(int c) => c is 0 or 1 or 2 or 3 or 6;

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

            // 체크박스 컬럼 (0,1,2): 가운데 정렬 체크박스 그리기
            if (e.ColumnIndex <= 2)
            {
                bool isChecked = e.Value is bool b && b;
                var st = isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var sz = CheckBoxRenderer.GetGlyphSize(e.Graphics, st);
                int cx = combined.X + (combined.Width  - sz.Width)  / 2;
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

    void OnCellPainting(object? s, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is null || e.CellStyle is null) return;

        // ── 셀병합: sub-row 0 → 2행 합친 사각형에 가운데 정렬 렌더링 ──────
        if (e.RowIndex >= 0 && IsMergedCol(e.ColumnIndex))
        {
            int wIdx = e.RowIndex / 2;
            int sr   = e.RowIndex % 2;
            if (wIdx < _workers.Count)
            {
                if (sr == 0 && wIdx * 2 + 1 < _grid.Rows.Count) { PaintMergedCell(e, wIdx); return; }
                if (sr == 1) { e.Handled = true; return; }
            }
        }

        // 헤더 행
        if (e.RowIndex == -1)
        {
            // 날짜 컬럼 헤더: 상단(day1)/하단(day2) 분할 + 공휴일/토요일 색상
            if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
            {
                PaintDateHeader(e);
                return;
            }
            // 그 외 헤더는 MultiLineHeaderCell 이 처리하도록 위임
            return;
        }
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        int wi     = e.RowIndex / 2;
        int subRow = e.RowIndex % 2;
        if (wi >= _workers.Count) return;

        var worker = _workers[wi];
        bool isManual = worker[I_AUTO] != "1";

        // 날짜 컬럼 배경색 결정
        Color? bgColor = null;
        if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
        {
            int slot = e.ColumnIndex - D_DATE_COL;
            int day  = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
            if (day > 0 && day <= _lastDay)
            {
                string kind = AppConfig.GetDayKind(_year, _month, day, _holidays, _customHolidays);
                if      (kind == "sun") bgColor = Color.FromArgb(255, 220, 235);  // 연핑크
                else if (kind == "sat") bgColor = Color.FromArgb(187, 222, 251);  // 연파랑
            }
        }
        // 수동 행 (금액자동 해제) → 황색 (checkbox 컬럼 0-2 제외)
        if (isManual && bgColor == null && e.ColumnIndex >= 3)
            bgColor = Color.FromArgb(255, 240, 160);

        if (bgColor != null)
        {
            using var brush = new SolidBrush(bgColor.Value);
            e.Graphics.FillRectangle(brush, e.CellBounds);
            // 테두리
            using var pen = new Pen(Color.FromArgb(80, 85, 105));
            e.Graphics.DrawRectangle(pen, e.CellBounds.X, e.CellBounds.Y,
                e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            // 텍스트
            string text = e.FormattedValue?.ToString() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                using var tb = new SolidBrush(Color.Black);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(text, e.CellStyle.Font ?? Font, tb, e.CellBounds, sf);
            }
            // 날짜 워터마크 (우상단 작은 숫자)
            if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
                DrawDayWatermark(e.Graphics, e.CellBounds, e.ColumnIndex, subRow);
            e.Handled = true;
        }
        else if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
        {
            // 워터마크만 추가
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
            DrawDayWatermark(e.Graphics, e.CellBounds, e.ColumnIndex, subRow);
            e.Handled = true;
        }
    }

    void DrawDayWatermark(Graphics g, Rectangle rect, int col, int subRow)
    {
        int slot = col - D_DATE_COL;
        int day  = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
        if (day <= 0 || day > _lastDay) return;
        using var f   = new Font("맑은 고딕", 7f);
        using var pen = new SolidBrush(ThemeManager.IsDark ? Color.FromArgb(90, 180, 195, 210) : Color.FromArgb(90, 120, 130, 160));
        using var sf  = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        g.DrawString(day.ToString(), f, pen, rect, sf);
    }

    void OnHeaderClick(object? s, DataGridViewCellMouseEventArgs e)
    {
        // '전체' 헤더(0열) → 전원 선택/해제 토글
        if (e.ColumnIndex == 0)
        {
            if (_workers.Count == 0) return;
            bool allSel = _workers.All(d => d[I_SELECT] == "1");
            string v = allSel ? "" : "1";
            for (int wi = 0; wi < _workers.Count; wi++)
            {
                _workers[wi][I_SELECT] = v;
                RefreshWorkerRows(wi);
            }
            _grid.Columns[0].HeaderCell.Value = v == "1" ? "☑\n전체" : "☐\n전체";
            _grid.Invalidate(new Rectangle(0, 0, _grid.Width, _grid.ColumnHeadersHeight));
            return;
        }
        if (e.ColumnIndex < D_DATE_COL || e.ColumnIndex >= D_DATE_COL + N_DATES) return;
        int slot  = e.ColumnIndex - D_DATE_COL;
        int halfH = _grid.ColumnHeadersHeight / 2;
        int day   = e.Y < halfH ? (slot + 1) : (slot + 16);
        if (day < 1 || day > _lastDay) return;
        if (_holidays.Contains(day)) return;                       // 법정 공휴일은 변경 불가
        if ((int)new DateTime(_year, _month, day).DayOfWeek == 0) return;  // 이미 일요일
        if (_customHolidays.Contains(day)) _customHolidays.Remove(day);
        else _customHolidays.Add(day);
        _grid.InvalidateColumn(e.ColumnIndex);
        _grid.Invalidate(new Rectangle(0, 0, _grid.Width, _grid.ColumnHeadersHeight));
        for (int wi = 0; wi < _workers.Count; wi++)
            if (_workers[wi][I_AUTO] == "1") AutoCalc(wi);
        RefreshGrid();
    }

    void PaintDateHeader(DataGridViewCellPaintingEventArgs e)
    {
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        var g = e.Graphics;
        if (g == null || e.CellStyle == null) { e.Handled = true; return; }

        int slot  = e.ColumnIndex - D_DATE_COL;
        int day1  = slot + 1;
        int day2  = slot + 16;
        bool v1   = day1 <= 15;
        bool v2   = day2 <= _lastDay;
        int  halfH = e.CellBounds.Height / 2;

        void PaintHalf(int day, int y, int h, bool valid)
        {
            if (!valid) return;
            var rect = new Rectangle(e.CellBounds.X + 1, y, e.CellBounds.Width - 2, h);
            string kind = AppConfig.GetDayKind(_year, _month, day, _holidays, _customHolidays);
            Color textColor = e.CellStyle.ForeColor;  // 기본: 테마 헤더 색 (밝음)
            if (kind == "sun")
            {
                using var bg = new SolidBrush(Color.FromArgb(255, 220, 235));
                g.FillRectangle(bg, rect);
                textColor = Color.FromArgb(180, 0, 60);
            }
            else if (kind == "sat")
            {
                using var bg = new SolidBrush(Color.FromArgb(187, 222, 251));
                g.FillRectangle(bg, rect);
                textColor = Color.FromArgb(20, 80, 180);
            }
            using var sf    = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var brush = new SolidBrush(textColor);
            g.DrawString(day.ToString(), e.CellStyle.Font ?? Font, brush, rect, sf);
        }

        PaintHalf(day1, e.CellBounds.Y,         halfH,                       v1);
        PaintHalf(day2, e.CellBounds.Y + halfH, e.CellBounds.Height - halfH, v2);
        e.Handled = true;
    }

    // ── 셀 클릭 ───────────────────────────────────────────
    void OnCellClick(object? s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        int wi     = e.RowIndex / 2;
        int subRow = e.RowIndex % 2;
        if (wi >= _workers.Count) return;
        var d = _workers[wi];

        // 체크박스 컬럼 클릭
        if (e.ColumnIndex == 0) { d[I_SELECT] = d[I_SELECT] == "1" ? "" : "1"; RefreshWorkerRows(wi); return; }
        if (e.ColumnIndex == 1 && subRow == 0) { d[I_AUTO] = d[I_AUTO] == "1" ? "" : "1"; if (d[I_AUTO] == "1") AutoCalc(wi); RefreshWorkerRows(wi); return; }
        if (e.ColumnIndex == 2 && subRow == 0) { d[I_BREAK] = d[I_BREAK] == "0" ? "1" : "0"; if (d[I_AUTO] == "1") AutoCalc(wi); RefreshWorkerRows(wi); return; }

        // 날짜 칸 클릭 → 시간 목록 드롭다운 자동 열기
        if (CellToDay(e.RowIndex, e.ColumnIndex) > 0)
            BeginInvoke(() =>
            {
                try
                {
                    _grid.BeginEdit(true);
                    if (_grid.EditingControl is ComboBox cb) cb.DroppedDown = true;
                }
                catch { }
            });
    }

    // 날짜 칸에서 시간을 고르면(콤보 커밋) 다른 칸으로 옮기지 않아도 바로 다시 계산한다.
    void OnCellValueChanged(object? s, DataGridViewCellEventArgs e)
    {
        if (_syncing || e.RowIndex < 0) return;
        int day = CellToDay(e.RowIndex, e.ColumnIndex);
        if (day <= 0) return;
        int wi = e.RowIndex / 2;
        if (wi >= _workers.Count) return;

        var d = _workers[wi];
        d[D_DATE_DB + day - 1] = NormRange(_grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString());
        // 편집이 아직 진행 중이므로 그리드 갱신은 뒤로 미룬다
        BeginInvoke(() =>
        {
            if (d[I_AUTO] == "1") AutoCalc(wi);
            RefreshWorkerRows(wi);
            UpdateWarnings();
        });
    }

    void OnCellEndEdit(object? s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        int wi     = e.RowIndex / 2;
        int subRow = e.RowIndex % 2;
        if (wi >= _workers.Count) return;
        var d   = _workers[wi];
        var val = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Trim() ?? "";

        // 근무시작일 편집
        if (e.ColumnIndex == 6 && subRow == 0) { d[I_START] = val; return; }

        // 날짜 셀 편집: 출퇴근 시각 입력("18-22", "18:30-22", "1800-2200") → "HH:MM-HH:MM" 로 정규화
        if (e.ColumnIndex >= D_DATE_COL && e.ColumnIndex < D_DATE_COL + N_DATES)
        {
            int slot = e.ColumnIndex - D_DATE_COL;
            int day  = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
            if (day <= 0 || day > _lastDay) return;

            d[D_DATE_DB + day - 1] = NormRange(val);   // 형식이 틀리면 빈칸
            if (d[I_AUTO] == "1") AutoCalc(wi);
            RefreshWorkerRows(wi);
            UpdateWarnings();
            return;
        }

        // 합계 컬럼 편집 (금액자동 OFF 상태에서 직접 입력)
        if (e.ColumnIndex >= D_SUM_COL && e.ColumnIndex < D_SUM_COL + N_SUMS && d[I_AUTO] != "1")
        {
            int pairIdx = e.ColumnIndex - D_SUM_COL;
            int dbIdx   = D_SUM_DB + pairIdx * 2 + subRow;
            if (dbIdx < d.Length) d[dbIdx] = val.Replace(",", "");
        }
    }

    // ── 우클릭 컨텍스트 ──────────────────────────────────
    string _clipTime = "";   // 우클릭 시간 복사용 클립보드

    // (rowIndex, colIndex) → 그 셀이 가리키는 날짜(day). 날짜 칸이 아니면 0.
    int CellToDay(int rowIndex, int colIndex)
    {
        if (colIndex < D_DATE_COL || colIndex >= D_DATE_COL + N_DATES) return 0;
        int subRow = rowIndex % 2;
        int slot   = colIndex - D_DATE_COL;
        int day    = subRow == 0 ? (slot < 15 ? slot + 1 : 0) : (slot < 15 ? slot + 16 : _lastDay);
        return (day > 0 && day <= _lastDay) ? day : 0;
    }

    // Ctrl+C : 현재 날짜 칸의 시간 복사 / Ctrl+V : 선택한 날짜 칸에 붙여넣기
    void OnGridKeyDown(object? s, KeyEventArgs e)
    {
        if (!e.Control) return;
        var cell = _grid.CurrentCell;
        if (cell == null) return;
        int day = CellToDay(cell.RowIndex, cell.ColumnIndex);
        if (day <= 0) return;                       // 날짜 칸이 아니면 무시
        int wi = cell.RowIndex / 2;
        if (wi >= _workers.Count) return;

        if (e.KeyCode == Keys.C)
        {
            _clipTime = _workers[wi][D_DATE_DB + day - 1];
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.V)
        {
            if (!string.IsNullOrEmpty(_clipTime))
                PasteTime(wi, cell.RowIndex, cell.ColumnIndex);
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    void OnGridMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = _grid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0) return;
        int wi = hit.RowIndex / 2;
        if (wi >= _workers.Count) return;

        var menu = new ContextMenuStrip();

        // 날짜 칸을 우클릭했으면 시간 복사/붙여넣기 메뉴
        int day = CellToDay(hit.RowIndex, hit.ColumnIndex);
        if (day > 0)
        {
            string cur = _workers[wi][D_DATE_DB + day - 1];
            var copy = menu.Items.Add($"시간 복사  ({FmtRange(cur)})");
            copy.Enabled = ParseRange(cur).st >= 0;
            copy.Click += (_, _) => _clipTime = cur;

            var paste = menu.Items.Add(string.IsNullOrEmpty(_clipTime)
                ? "붙여넣기 (복사된 시간 없음)"
                : $"붙여넣기  ({FmtRange(_clipTime)})");
            paste.Enabled = !string.IsNullOrEmpty(_clipTime);
            paste.Click += (_, _) => PasteTime(wi, hit.RowIndex, hit.ColumnIndex);

            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add("정보수정").Click     += (_, _) => EditWorker(wi);
        menu.Items.Add("행 삭제").Click      += (_, _) => DeleteWorker(wi);
        menu.Items.Add("계산 재실행").Click  += (_, _) => { AutoCalc(wi); RefreshWorkerRows(wi); };
        menu.Show(_grid, e.Location);
    }

    // 복사한 시간을 붙여넣는다. 여러 날짜 칸이 선택돼 있으면 전부, 아니면 우클릭한 칸에.
    void PasteTime(int clickedWi, int clickedRow, int clickedCol)
    {
        if (string.IsNullOrEmpty(_clipTime)) return;

        // 선택된 날짜 셀 모으기
        var targets = new List<(int wi, int day)>();
        foreach (DataGridViewCell cell in _grid.SelectedCells)
        {
            int wi  = cell.RowIndex / 2;
            int day = CellToDay(cell.RowIndex, cell.ColumnIndex);
            if (wi < _workers.Count && day > 0) targets.Add((wi, day));
        }
        // 선택이 없거나 날짜 칸이 아니면 우클릭한 칸 하나만
        if (targets.Count == 0)
        {
            int day = CellToDay(clickedRow, clickedCol);
            if (clickedWi < _workers.Count && day > 0) targets.Add((clickedWi, day));
        }

        var affected = new HashSet<int>();
        foreach (var (wi, day) in targets)
        {
            _workers[wi][D_DATE_DB + day - 1] = _clipTime;
            affected.Add(wi);
        }
        foreach (var wi in affected)
        {
            if (_workers[wi][I_AUTO] == "1") AutoCalc(wi);
            RefreshWorkerRows(wi);
        }
        UpdateWarnings();
    }

    // ── 인원 조작 ─────────────────────────────────────────
    void AddWorker()
    {
        var dlg = new WorkerDialog();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var vals = dlg.Values;
        if (string.IsNullOrEmpty(vals[W_NAME])) return;
        var d = new string[AppConfig.PayCols.Length];
        d[I_NAME]   = vals[W_NAME];
        d[I_JUMIN]  = vals[W_JUMIN];
        d[I_ADDR]   = vals[W_ADDR];
        d[I_PHONE]  = vals[W_PHONE];
        d[I_WAGE]   = vals[W_WAGE];
        d[I_START]  = "1일";
        d[I_AUTO]   = "1";   // 금액자동 기본 ON
        d[I_BREAK]  = "1";   // 휴게 자동공제 기본 ON
        d[I_SEQ]    = (_workers.Count + 1).ToString();
        _workers.Add(d);
        AddWorkerRows(_workers.Count - 1);
        UpdateStatus();
    }

    // WorkerCols 인덱스 (컬럼 추가/순서 변경에 안전하도록 이름으로 조회)
    static readonly int W_NAME  = Array.IndexOf(AppConfig.WorkerCols, "이름");
    static readonly int W_JUMIN = Array.IndexOf(AppConfig.WorkerCols, "생년월일6자리");
    static readonly int W_ADDR  = Array.IndexOf(AppConfig.WorkerCols, "거주지역");
    static readonly int W_PHONE = Array.IndexOf(AppConfig.WorkerCols, "전화번호");
    static readonly int W_WAGE  = Array.IndexOf(AppConfig.WorkerCols, "시급");

    void EditWorker(int wi)
    {
        var d    = _workers[wi];
        var vals = new string[AppConfig.WorkerCols.Length];
        vals[W_NAME]  = d[I_NAME];  vals[W_JUMIN] = d[I_JUMIN]; vals[W_ADDR] = d[I_ADDR];
        vals[W_PHONE] = d[I_PHONE]; vals[W_WAGE]  = d[I_WAGE];
        var dlg = new WorkerDialog(vals);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var nv = dlg.Values;
        d[I_NAME]  = nv[W_NAME];  d[I_JUMIN] = nv[W_JUMIN]; d[I_ADDR] = nv[W_ADDR];
        d[I_PHONE] = nv[W_PHONE]; d[I_WAGE]  = nv[W_WAGE];
        RefreshWorkerRows(wi);
    }

    void DeleteWorker(int wi)
    {
        string name = _workers[wi][I_NAME];
        var ans = MessageBox.Show($"\"{name}\"을(를) 지급대장에서 제외하시겠습니까?",
            "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (ans != DialogResult.Yes) return;
        _workers.RemoveAt(wi);
        RefreshGrid();
        UpdateStatus();
    }

    // ── 실시간 경보 ───────────────────────────────────────
    Panel BuildWarnPanel()
    {
        _pnlWarn = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgPanel, Padding = new Padding(8, 4, 8, 4) };
        _txtWarn = new TextBox
        {
            Dock        = DockStyle.Fill,
            Multiline   = true,
            ReadOnly    = true,
            ScrollBars  = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Font        = ThemeManager.F(9f),
            BackColor   = ThemeManager.BgPanel,
            ForeColor   = ThemeManager.TextSub,
            Text        = "",
        };
        _pnlWarn.Controls.Add(_txtWarn);
        var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ThemeManager.Border };
        _pnlWarn.Controls.Add(sep);
        return _pnlWarn;
    }

    /// <summary>근무시간이 바뀔 때마다 법정 리스크를 즉시 알린다.</summary>
    void UpdateWarnings()
    {
        if (_txtWarn == null) return;

        var lines = new List<string>();
        foreach (var d in _workers)
        {
            string name = d[I_NAME];
            if (string.IsNullOrWhiteSpace(name)) continue;

            var a = Analyze(d);
            double.TryParse(d[I_WAGE], out var wage);

            // 1) 주휴수당 발생 / 임박 (주 15시간 기준)
            foreach (var w in a.Weeks.Values.OrderBy(v => v.First))
            {
                if (w.H >= _juhuHours)
                {
                    double jh  = Math.Min(w.H / 40.0 * 8.0, 8.0);
                    double pay = wage * jh;
                    lines.Add($"⚠ {name} : {_month}/{w.First}~{_month}/{w.Last} 주 {w.H:0.##}시간 → 주휴수당 발생 ({jh:0.##}시간, {pay:N0}원)");
                }
                else if (w.H >= _juhuHours * 0.8)
                {
                    lines.Add($"ⓘ {name} : {_month}/{w.First}~{_month}/{w.Last} 주 {w.H:0.##}시간 — {_juhuHours - w.H:0.##}시간 더 하면 주휴수당 발생");
                }
            }

            // 2) 4대보험 가입 의무 (월 60시간 이상 또는 월 8일 이상)
            if (a.TotalH >= _insMinHours || a.Days >= _insMinDays)
            {
                var why = a.TotalH >= _insMinHours
                    ? $"월 {a.TotalH:0.##}시간(≥{_insMinHours:0.##})"
                    : $"월 {a.Days}일(≥{_insMinDays:0.##})";
                lines.Add($"⚠ {name} : 4대보험 가입대상 — {why}");
            }

            // 3) 시급 미입력 — 금액자동이 켜져 있어도 임금이 전부 0 으로 계산된다
            if (wage <= 0 && a.TotalH > 0)
                lines.Add($"⚠ {name} : 시급이 없어 임금이 0원으로 계산됩니다 — 우클릭 '정보수정'에서 시급을 입력하세요");

            // 4) 최저임금 미달
            if (_minWage > 0 && wage > 0 && wage < _minWage)
                lines.Add($"⚠ {name} : 시급 {wage:N0}원 < 최저임금 {_minWage:N0}원 — 미달");

            // 5) 5인 미만이라 가산수당이 안 붙는 연장/야간/휴일 근로
            if (_staffCount < _extraMinStaff && (a.OtH > 0 || a.NightH > 0 || a.HolH > 0))
            {
                var parts = new List<string>();
                if (a.OtH    > 0) parts.Add($"연장 {a.OtH:0.##}h");
                if (a.NightH > 0) parts.Add($"야간 {a.NightH:0.##}h");
                if (a.HolH   > 0) parts.Add($"휴일 {a.HolH:0.##}h");
                lines.Add($"ⓘ {name} : {string.Join(", ", parts)} — 상시근로자 {_extraMinStaff}인 미만이라 가산수당 미적용");
            }
        }

        if (_minWage <= 0)
            lines.Add("ⓘ 최저임금이 설정되지 않아 미달 검증을 건너뜁니다. (상단 설정 → 법정기준(연도별) 에서 입력 해주세요)");

        bool danger = lines.Any(l => l.StartsWith("⚠"));
        _txtWarn.Text      = lines.Count > 0 ? string.Join("\r\n", lines) : "✓ 이상 없음";
        _txtWarn.ForeColor = lines.Count == 0 ? Color.FromArgb(60, 150, 90)
                           : danger            ? Color.FromArgb(210, 80, 70)
                                               : ThemeManager.TextSub;
    }

    // ── 시각 파싱 유틸 ────────────────────────────────────
    // "18:00-22:00", "18-22", "1800-2200" 모두 허용. 자정 넘김(22-02) 지원.
    // 반환: (시작시각, 종료시각) 단위=시간(double). 실패 시 (-1,-1).
    internal static (double st, double en) ParseRange(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (-1, -1);
        var t = raw.Replace(" ", "").Replace("~", "-").Replace("–", "-");
        int dash = t.IndexOf('-');
        if (dash <= 0 || dash == t.Length - 1) return (-1, -1);
        double a = ParseClock(t[..dash]);
        double b = ParseClock(t[(dash + 1)..]);
        if (a < 0 || b < 0) return (-1, -1);
        if (b <= a) b += 24;                 // 자정 넘김
        return (a, b);
    }

    internal static double ParseClock(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        int h, m = 0;
        int colon = s.IndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(s[..colon], out h)) return -1;
            if (!int.TryParse(s[(colon + 1)..], out m)) return -1;
        }
        else if (s.Length >= 3)              // "1830" 형태
        {
            if (!int.TryParse(s[..^2], out h)) return -1;
            if (!int.TryParse(s[^2..], out m)) return -1;
        }
        else if (!int.TryParse(s, out h)) return -1;
        if (h < 0 || h > 30 || m < 0 || m > 59) return -1;
        return h + m / 60.0;
    }

    /// <summary>입력값을 저장 형식("HH:MM-HH:MM")으로 정규화. 형식이 틀리면 빈 문자열.</summary>
    internal static string NormRange(string? raw)
    {
        var (a, b) = ParseRange(raw);
        return a < 0 ? "" : $"{Hhmm(a)}-{Hhmm(b % 24)}";
    }

    /// <summary>시간(double) → "HH:MM" 저장 형식</summary>
    static string Hhmm(double v)
    {
        int h = (int)v, m = (int)Math.Round((v - h) * 60);
        if (m == 60) { h++; m = 0; }
        return $"{h % 24:D2}:{m:D2}";
    }

    /// <summary>화면 표시용 축약: "18:00-22:00" → "18-22", "18:30-22:00" → "18:30-22"</summary>
    internal static string FmtRange(string? raw)
    {
        var (a, b) = ParseRange(raw);
        if (a < 0) return raw ?? "";
        return $"{Clock(a)}-{Clock(b % 24)}";

        static string Clock(double v)
        {
            int h = (int)v, m = (int)Math.Round((v - h) * 60);
            if (m == 60) { h++; m = 0; }
            return m == 0 ? h.ToString() : $"{h}:{m:D2}";
        }
    }

    /// <summary>22:00~익일 06:00 구간과 겹치는 시간(시간 단위).</summary>
    static double NightHours(double st, double en)
    {
        double night = 0;
        // 00~06 구간
        night += Math.Max(0, Math.Min(en, 6) - Math.Max(st, 0));
        // 22~30(=익일 06시) 구간, 그리고 그 다음날치까지
        for (int shift = 0; shift <= 24; shift += 24)
        {
            double ns = 22 + shift, ne = 30 + shift;
            night += Math.Max(0, Math.Min(en, ne) - Math.Max(st, ns));
        }
        return night;
    }

    /// <summary>법정 휴게(분): 8시간 이상 → break8, 4시간 이상 → break4.</summary>
    double BreakMinutes(double grossHours) =>
        grossHours >= 8 ? _break8Min : grossHours >= 4 ? _break4Min : 0;

    // ── 근무 집계 (계산과 경보가 같은 근거를 쓰도록 분리) ──────
    sealed class Analysis
    {
        public double   TotalH, OtH, NightH, HolH, JuhuH;
        public int      Days;
        public double[] DayH   = new double[32];
        public HashSet<int> Worked = [];
        // ISO주 → (근로시간, 그 주의 첫날, 마지막날)  ※ 이 달 안에서만
        public Dictionary<int, (double H, int First, int Last)> Weeks = [];
    }

    Analysis Analyze(string[] d)
    {
        var a = new Analysis();
        bool autoBreak = d[I_BREAK] != "0";   // 기본 ON

        for (int day = 1; day <= _lastDay; day++)
        {
            var (st, en) = ParseRange(d[D_DATE_DB + day - 1]);
            if (st < 0) continue;
            double gross = en - st;
            if (gross <= 0) continue;

            double work = gross - (autoBreak ? BreakMinutes(gross) / 60.0 : 0);
            if (work <= 0) continue;

            a.DayH[day] = work;
            a.TotalH   += work;
            a.Days++;
            a.Worked.Add(day);

            // 야간(22~06): 휴게 뺀 실근로를 넘지 않도록 상한
            a.NightH += Math.Min(NightHours(st, en), work);

            // 연장: 1일 8시간 초과분
            if (work > 8) a.OtH += work - 8;

            // 휴일: 일요일 + 법정/지정 공휴일 (토요일은 무급휴무일이라 가산 없음)
            var dt = new DateTime(_year, _month, day);
            if (dt.DayOfWeek == DayOfWeek.Sunday || _holidays.Contains(day) || _customHolidays.Contains(day))
                a.HolH += work;

            int isoW = System.Globalization.ISOWeek.GetWeekOfYear(dt);
            var w = a.Weeks.GetValueOrDefault(isoW, (0, day, day));
            a.Weeks[isoW] = (w.H + work, Math.Min(w.First, day), Math.Max(w.Last, day));
        }

        // 주휴시간: 주 근로시간이 기준(15h) 이상인 주만 발생
        // 단시간근로자 주휴 = (1주 소정근로시간 / 40) × 8, 최대 8시간
        foreach (var w in a.Weeks.Values)
            if (w.H >= _juhuHours)
                a.JuhuH += Math.Min(w.H / 40.0 * 8.0, 8.0);

        return a;
    }

    // ── 자동 계산 (시급 × 근무시간) ────────────────────────
    void AutoCalc(int wi)
    {
        var d = _workers[wi];
        if (!double.TryParse(d[I_WAGE], out var wage)) wage = 0;

        var a = Analyze(d);
        double totalH = a.TotalH, otH = a.OtH, nightH = a.NightH, holH = a.HolH, juhuH = a.JuhuH;
        int    days   = a.Days;
        var    dayH   = a.DayH;
        var    worked = a.Worked;

        // 연장·야간·휴일 가산(50%)은 상시근로자 5인 이상 사업장만 법정 의무
        bool addPay = _staffCount >= _extraMinStaff;

        double basic    = wage * totalH;
        double juhuPay  = wage * juhuH;
        double otPay    = addPay ? wage * otH    * 0.5 : 0;
        double nightPay = addPay ? wage * nightH * 0.5 : 0;
        double holPay   = addPay ? wage * holH   * 0.5 : 0;
        double total    = basic + juhuPay + otPay + nightPay + holPay;

        // 4) 소득세(일용근로소득): 일별로 (그날 급여 − 일공제액) × 세율, 소액부징수 미만 → 0
        double tax = 0;
        for (int day = 1; day <= _lastDay; day++)
        {
            if (dayH[day] <= 0) continue;
            tax += Math.Max(0.0, wage * dayH[day] - _dailyDeduct) * (_rTax / 100.0);
        }
        tax = (int)tax;
        if (tax < _minTaxCut) tax = 0;
        double mintax = (int)(tax * _rMinTax / 100);
        tax    = Math.Round(tax    / 10.0, MidpointRounding.AwayFromZero) * 10;
        mintax = Math.Round(mintax / 10.0, MidpointRounding.AwayFromZero) * 10;

        // 5) 4대보험: 월 60시간 이상 또는 월 8일 이상 (나이 면제는 유지)
        bool   levy   = ShouldLevy4di(totalH, days);
        double kukmin = 0, health = 0, employ = 0, care = 0;
        if (levy)
        {
            int age = CalcAgeFromJumin(d[I_JUMIN]);
            if (age < 60)
            {
                kukmin = (int)(total * _rKukmin / 100);
                health = (int)(total * _rHealth / 100);
                care   = (int)(health * _rCare  / 100);
            }
            if (age < 65)
                employ = (int)(total * _rEmploy / 100);
        }

        double deduct = tax + mintax + kukmin + health + employ + care;
        double net    = total - deduct;

        if (worked.Count > 0) d[I_START] = $"{worked.Min()}일";

        d[I_HOURS]     = totalH > 0 ? totalH.ToString("0.##") : "";
        d[I_DAYS]      = days   > 0 ? days.ToString()         : "";
        d[I_JUHU_H]    = juhuH  > 0 ? juhuH.ToString("0.##")  : "";
        d[I_HOL_H]     = holH   > 0 ? holH.ToString("0.##")   : "";
        d[I_OT_H]      = otH    > 0 ? otH.ToString("0.##")    : "";
        d[I_NIGHT_H]   = nightH > 0 ? nightH.ToString("0.##") : "";
        // 최저임금 미달 경고 (최저임금이 입력된 연도에만 검증)
        d[I_MINWARN]   = (_minWage > 0 && wage > 0 && wage < _minWage) ? "미달" : "";
        d[I_BASIC]     = ((int)basic).ToString();
        d[I_JUHU_PAY]  = ((int)juhuPay).ToString();
        d[I_OT_PAY]    = ((int)otPay).ToString();
        d[I_NIGHT_PAY] = ((int)nightPay).ToString();
        d[I_HOL_PAY]   = ((int)holPay).ToString();
        d[I_TOTAL]     = ((int)total).ToString();
        d[I_KUKMIN]    = kukmin.ToString("0");
        d[I_HEALTH]    = health.ToString("0");
        d[I_EMPLOY]    = employ.ToString("0");
        d[I_CARE]      = care.ToString("0");
        d[I_TAX]       = tax.ToString("0");
        d[I_MINTAX]    = mintax.ToString("0");
        d[I_DEDUCT]    = deduct.ToString("0");
        d[I_NET]       = net.ToString("0");
    }

    // 4대보험 부과 대상: 월 60시간 이상 또는 월 8일 이상 (기준은 연도별 법정기준)
    bool ShouldLevy4di(double totalHours, int days) =>
        totalHours >= _insMinHours || days >= _insMinDays;

    int CalcAgeFromJumin(string jumin)
    {
        try
        {
            var s = jumin.Replace("-", "").Replace(" ", "");
            if (s.Length < 7) return -1;
            int yy = int.Parse(s[..2]);
            int mm = int.Parse(s[2..4]);
            int dd = int.Parse(s[4..6]);
            int g  = int.Parse(s[6..7]);
            int birthYear = g is 1 or 2 ? 1900 + yy : 2000 + yy;
            var refDate   = new DateTime(_year, _month, _lastDay);
            int age       = refDate.Year - birthYear;
            if (refDate.Month < mm || (refDate.Month == mm && refDate.Day < dd)) age--;
            return Math.Max(0, age);
        }
        catch { return -1; }
    }

    // ── 그리드 행 갱신 (단일 근로자) ─────────────────────
    void RefreshWorkerRows(int wi)
    {
        int r0 = wi * 2;
        int r1 = r0 + 1;
        if (r0 >= _grid.Rows.Count) return;
        var d = _workers[wi];
        FillRow(_grid.Rows[r0], d, 0, wi);
        if (r1 < _grid.Rows.Count) FillRow(_grid.Rows[r1], d, 1, wi);
        _grid.InvalidateRow(r0);
        _grid.InvalidateRow(r1);
        UpdateStatus();
    }

    // ── DB 저장 ───────────────────────────────────────────
    public void SaveData()
    {
        // 편집 중인 셀이 있으면 먼저 커밋
        if (_grid.IsCurrentCellInEditMode)
            _grid.EndEdit();

        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);
        try
        {
            CollectGridEdits();
            using var db = new Database(dbPath);
            db.EnsureTable("지급대장", AppConfig.PayCols);
            db.Execute("DELETE FROM \"지급대장\"");
            foreach (var d in _workers)
                db.InsertRow("지급대장", AppConfig.PayCols, d);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void CollectGridEdits()
    {
        for (int r = 0; r < _grid.Rows.Count; r++)
        {
            int wi     = r / 2;
            int subRow = r % 2;
            if (wi >= _workers.Count) continue;
            var d = _workers[wi];
            var row = _grid.Rows[r];

            // 근무시작일
            if (subRow == 0) d[I_START] = row.Cells[6].Value?.ToString()?.Trim() ?? d[I_START];

            // 날짜 셀
            for (int s = 0; s < N_DATES; s++)
            {
                int day = subRow == 0 ? (s < 15 ? s + 1 : 0) : (s < 15 ? s + 16 : _lastDay);
                if (day > 0 && day <= _lastDay)
                {
                    int dbIdx = D_DATE_DB + day - 1;
                    var v = row.Cells[D_DATE_COL + s].Value?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(v)) d[dbIdx] = NormRange(v);
                }
            }

            // 합계 (금액자동 OFF만)
            if (d[I_AUTO] != "1")
            {
                for (int i = 0; i < N_SUMS; i++)
                {
                    int dbIdx = D_SUM_DB + i * 2 + subRow;
                    if (dbIdx < d.Length)
                    {
                        var v = row.Cells[D_SUM_COL + i].Value?.ToString()?.Replace(",", "").Trim() ?? "";
                        if (!string.IsNullOrEmpty(v)) d[dbIdx] = v;
                    }
                }
            }
        }
    }

    // ── 상태 표시 ─────────────────────────────────────────
    void UpdateStatus()
    {
        double totPay = _workers.Sum(d => ParseD(d[I_TOTAL]));
        double totNet = _workers.Sum(d => ParseD(d[I_NET]));
        _lblStatus.Text = $"  총 {_workers.Count}명 | 임금총액: {totPay:N0}원 | 차감지급: {totNet:N0}원";
        UpdateWarnings();
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

    static Button MakeToolBtn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text      = text,
            Width     = w, Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide,
            ForeColor = ThemeManager.BtnText,
            Font      = ThemeManager.F(9f),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(2, 0, 2, 0),
        };
        b.FlatAppearance.BorderColor = ThemeManager.IsDark ? ThemeManager.Border : Color.FromArgb(130, 134, 158);
        b.Click += (_, _) => onClick();
        return b;
    }
}

// ── 멀티라인 헤더 셀 ─────────────────────────────────────
internal class MultiLineHeaderCell : DataGridViewColumnHeaderCell
{
    protected override void Paint(Graphics g, Rectangle clipBounds, Rectangle cellBounds,
        int rowIndex, DataGridViewElementStates elementState, object? value,
        object? formattedValue, string? errorText, DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(g, clipBounds, cellBounds, rowIndex, elementState, null, null, errorText,
            cellStyle, advancedBorderStyle,
            paintParts & ~DataGridViewPaintParts.ContentForeground &
                         ~DataGridViewPaintParts.ContentBackground);
        string text = value?.ToString() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        using var sf = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        using var brush = new SolidBrush(cellStyle.ForeColor);
        var r = cellBounds;
        r.Inflate(-2, -2);
        g.DrawString(text, cellStyle.Font ?? SystemFonts.DefaultFont, brush, r, sf);
    }
}

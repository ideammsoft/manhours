using ClosedXML.Excel;
using System.Runtime.InteropServices;

namespace manHours.Screens.Noim;

public class PrintScreen : UserControl
{
    // ── 이벤트 ────────────────────────────────────────────
    public event Action? GoPrev;

    // ── 레이아웃 상수 (WorkerScreen 동일) ─────────────────
    const int N_INFO     = 7;
    const int N_DATES    = 16;
    const int N_SUMS     = 11;
    const int D_DATE_COL = 7;
    const int D_SUM_COL  = 23;
    const int D_DATE_DB  = 9;
    const int D_SUM_DB   = 40;

    // ── DB 인덱스 (지급대장) ──────────────────────────────
    const int I_SELECT    = 0;
    const int I_AUTO      = 1;
    const int I_BREAK     = 2;    // 휴게자동
    const int I_SEQ       = 3;
    const int I_NAME      = 4;
    const int I_JUMIN     = 5;
    const int I_START     = 6;
    const int I_PHONE     = 7;
    const int I_ADDR      = 8;
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

    static readonly int[] InfoWidths = [36, 0, 34, 35, 90, 120, 50];
    static readonly int[] SumWidths  = [56, 56, 56, 62, 72, 66, 72, 66, 66, 62, 72];
    static readonly string[] InfoHdrs = ["☑\n전체", "(자동)", "휴게\n자동", "순번", "성명\n전화번호", "생년월일6자리\n거주지역", "근무\n시작일"];
    static readonly string[] SumHdrs  = ["근무시간\n근무일수", "주휴시간\n휴일시간", "연장시간\n야간시간", "시급\n최저미달", "기본급\n주휴수당", "연장수당\n야간수당", "휴일수당\n임금총액", "국민연금\n건강보험", "고용보험\n요양보험", "소득세\n주민세", "공제합계\n차감지급액"];

    // ── 상태 ──────────────────────────────────────────────
    int    _year, _month;
    string _project = "";
    int    _lastDay = 31;
    HashSet<int> _holidays = [];
    List<string[]> _workers = [];

    // 보조 데이터 (전체근로자, 사업설정)
    Dictionary<string, string[]> _workerFull = new();
    string[] _fullCols = [];
    Dictionary<string, string> _busiSet = new();

    DataGridView _grid    = null!;
    Label        _lblInfo = null!;
    Button       _btnMyungse = null!;
    Button       _btnSms  = null!;

    public PrintScreen()
    {
        BackColor = ThemeManager.BgMain;
        Build();
    }

    // ── UI 빌드 ───────────────────────────────────────────
    void Build()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            BackColor = Color.Transparent,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.Controls.Add(BuildToolbar(),   0, 0);
        root.Controls.Add(BuildGrid(),      0, 1);
        root.Controls.Add(BuildBottomBar(), 0, 2);
        Controls.Add(root);
    }

    Panel BuildToolbar()
    {
        var p   = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgPanel };
        var sep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ThemeManager.Border };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(8, 6, 8, 6) };
        flow.Controls.Add(MakeBtn("↺ 새로고침", 96, () => Load(_year, _month, _project)));
        var btnXl = MakeBtn("Excel 저장", 90, ExportExcel);
        btnXl.BackColor = Color.FromArgb(20, 100, 50);
        btnXl.ForeColor = Color.White;
        flow.Controls.Add(btnXl);
        p.Controls.Add(sep);
        p.Controls.Add(flow);
        return p;
    }

    Panel BuildBottomBar()
    {
        var p   = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.BgBottom };
        var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ThemeManager.Border };

        _lblInfo = new Label
        {
            Dock = DockStyle.Bottom, Height = 22,
            ForeColor = ThemeManager.TextSub, Font = ThemeManager.F(9f),
            TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true,
        };

        var btnArea = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.Transparent,
            Padding = new Padding(4, 4, 4, 4),
        };

        var btnPrev = MakeBtn("← 이전 단계", 108, () => GoPrev?.Invoke());
        btnPrev.Dock      = DockStyle.Left;
        btnPrev.BackColor = ThemeManager.IsDark ? ThemeManager.BtnSide : Color.FromArgb(140, 146, 175);
        btnPrev.ForeColor = ThemeManager.IsDark ? ThemeManager.BtnText : Color.White;

        var printFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Right,
            WrapContents  = false,
            AutoSize      = true,
            BackColor     = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(0, 0, 0, 4),
        };
        printFlow.Controls.Add(MakePrintBtn("근로계약서",  100, PrintGyeyak));
        _btnMyungse = MakePrintBtn($"{_month}월 임금명세서", 120, PrintMyungseo);
        printFlow.Controls.Add(_btnMyungse);
        printFlow.Controls.Add(MakePrintBtn("교부확인서",  100, PrintGyobu));
        printFlow.Controls.Add(MakePrintBtn("신분증사본",  100, PrintSinbun));
        _btnSms = MakePrintBtn("📱 문자 발송", 120, OpenSmsDialog);
        _btnSms.BackColor = Color.FromArgb(30, 50, 100);
        _btnSms.ForeColor = Color.FromArgb(137, 180, 250);
        printFlow.Controls.Add(_btnSms);

        btnArea.Controls.Add(printFlow);
        btnArea.Controls.Add(btnPrev);

        p.Controls.Add(btnArea);
        p.Controls.Add(_lblInfo);
        p.Controls.Add(sep);
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
            ReadOnly                = false,
            SelectionMode           = DataGridViewSelectionMode.CellSelect,
            AllowUserToAddRows      = false,
            AllowUserToDeleteRows   = false,
            AllowUserToResizeRows   = false,
            ScrollBars              = ScrollBars.Both,
            AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.None,
            EnableHeadersVisualStyles = false,
        };
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
        _grid.CellPainting += OnCellPainting;
        _grid.CellClick    += OnCellClick;
        _grid.ColumnHeaderMouseClick += OnHeaderClick;
        SetupColumns();
        return _grid;
    }

    // ── 헤더 '전체' 클릭 → 전원 선택/해제 토글 ──────────────
    void OnHeaderClick(object? s, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex != 0 || _workers.Count == 0) return;
        bool allSel = _workers.All(d => d[I_SELECT] == "1");
        string v = allSel ? "" : "1";               // 전부 선택돼 있으면 해제, 아니면 전체 선택
        for (int wi = 0; wi < _workers.Count; wi++)
        {
            _workers[wi][I_SELECT] = v;
            int r0 = wi * 2;
            if (r0 < _grid.Rows.Count) _grid.Rows[r0].Cells[0].Value = v == "1";
        }
        _grid.Columns[0].HeaderCell.Value = v == "1" ? "☑\n전체" : "☐\n전체";
        _grid.Invalidate();
    }

    void SetupColumns()
    {
        _grid.Columns.Clear();
        var chk = new DataGridViewCheckBoxColumn
        {
            Width = InfoWidths[0], ReadOnly = false,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        chk.HeaderCell = new MultiLineHeaderCell { Value = InfoHdrs[0] };
        _grid.Columns.Add(chk);

        var hidden = new DataGridViewTextBoxColumn { Width = 0, Visible = false, SortMode = DataGridViewColumnSortMode.NotSortable };
        hidden.HeaderText = InfoHdrs[1];
        _grid.Columns.Add(hidden);

        for (int i = 2; i < N_INFO; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Width = InfoWidths[i], ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            if (i == 4 || i == 5) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            col.HeaderCell = new MultiLineHeaderCell { Value = InfoHdrs[i] };
            _grid.Columns.Add(col);
        }
        for (int s = 0; s < N_DATES; s++)
        {
            var col = new DataGridViewTextBoxColumn { Width = 35, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            col.HeaderCell = new MultiLineHeaderCell { Value = "" };
            _grid.Columns.Add(col);
        }
        for (int i = 0; i < N_SUMS; i++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Width = SumWidths[i], ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
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
        if (_btnMyungse != null) _btnMyungse.Text = $"{month}월 명세서";
        LoadWorkers();
        LoadFullWorkers();
        LoadBusiSettings();
        RefreshGrid();
        UpdateInfo();
    }

    void LoadWorkers()
    {
        _workers.Clear();
        var dbPath = AppConfig.MonthlyDbPath(_year, _month, _project);
        if (!File.Exists(dbPath)) return;
        using var db = new Database(dbPath);
        if (!db.TableExists("지급대장")) return;
        var (payCols, payRows) = db.SelectStrings("지급대장");
        foreach (var row in payRows)
        {
            var d = new string[AppConfig.PayCols.Length];
            for (int i = 0; i < AppConfig.PayCols.Length; i++)
            {
                int ci = Array.IndexOf(payCols, AppConfig.PayCols[i]);
                d[i] = ci >= 0 && ci < row.Length ? (row[ci] ?? "") : "";
            }
            if (string.IsNullOrEmpty(d[I_SELECT])) d[I_SELECT] = "1";
            _workers.Add(d);
        }
    }

    void LoadFullWorkers()
    {
        _workerFull.Clear();
        _fullCols = [];
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            if (!db.TableExists("전체근로자")) return;
            var (cols, rows) = db.SelectStrings("전체근로자");
            _fullCols = cols;
            foreach (var row in rows)
            {
                int ni = Array.IndexOf(cols, "이름");
                if (ni >= 0 && !string.IsNullOrEmpty(row[ni]))
                    _workerFull[row[ni]] = row;
            }
        }
        catch { }
    }

    void LoadBusiSettings()
    {
        _busiSet.Clear();
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            if (!db.TableExists("사업설정")) return;
            var (cols, rows) = db.SelectStrings("사업설정");
            int ni = Array.IndexOf(cols, "사업명");
            foreach (var row in rows)
            {
                if (ni >= 0 && row[ni] == _project)
                {
                    for (int i = 0; i < cols.Length && i < row.Length; i++)
                        _busiSet[cols[i]] = row[i] ?? "";
                    break;
                }
            }
        }
        catch { }
    }

    // ── 보조 데이터 헬퍼 ─────────────────────────────────
    string WL(string name, string field)
    {
        if (!_workerFull.TryGetValue(name, out var row)) return "";
        int i = Array.IndexOf(_fullCols, field);
        return i >= 0 && i < row.Length ? (row[i] ?? "") : "";
    }

    string BS(string field) =>
        _busiSet.TryGetValue(field, out var v) ? v : "";

    // 임금지급일(매월 며칠). 매장관리에 설정돼 있으면 그 값, 없으면 25.
    int PayDay()
    {
        var s = new string(BS("임금지급일").Where(char.IsDigit).ToArray());
        return int.TryParse(s, out var d) && d is >= 1 and <= 31 ? d : 25;
    }

    // ── 그리드 렌더링 ─────────────────────────────────────
    void RefreshGrid()
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        for (int wi = 0; wi < _workers.Count; wi++)
        {
            var d = _workers[wi];
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
        row.Cells[0].Value = subRow == 0 && d[I_SELECT] == "1";
        row.Cells[2].Value = subRow == 0 ? d[2] == "1" ? "●" : "" : "";
        row.Cells[3].Value = subRow == 0 ? d[I_SEQ]   : "";
        row.Cells[4].Value = subRow == 0 ? d[I_NAME]  : d[I_PHONE];
        row.Cells[5].Value = subRow == 0 ? d[I_JUMIN] : d[I_ADDR];
        row.Cells[6].Value = subRow == 0 ? d[I_START] : "";

        for (int s = 0; s < N_DATES; s++)
        {
            int day = subRow == 0 ? (s < 15 ? s + 1 : 0) : (s < 15 ? s + 16 : _lastDay);
            row.Cells[D_DATE_COL + s].Value = (day > 0 && day <= _lastDay) ? WorkerScreen.FmtRange(d[D_DATE_DB + day - 1]) : "";
        }
        for (int i = 0; i < N_SUMS; i++)
        {
            int dbIdx = D_SUM_DB + i * 2 + subRow;
            row.Cells[D_SUM_COL + i].Value = dbIdx < d.Length ? FmtComma(d[dbIdx]) : "";
        }
    }

    // ── 셀 클릭 (체크박스 토글) ───────────────────────────
    void OnCellClick(object? s, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
        int wi = e.RowIndex / 2;
        if (wi >= _workers.Count) return;
        _workers[wi][I_SELECT] = _workers[wi][I_SELECT] == "1" ? "" : "1";
        int r0 = wi * 2, r1 = r0 + 1;
        _grid.Rows[r0].Cells[0].Value = _workers[wi][I_SELECT] == "1";
        _grid.InvalidateRow(r0);
        if (r1 < _grid.Rows.Count) _grid.InvalidateRow(r1);
    }

    // 병합 컬럼: 0(선택), 2(휴게자동), 3(순번), 6(근무시작일)
    static bool IsMergedCol(int c) => c is 0 or 2 or 3 or 6;

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

            if (e.ColumnIndex == 0) // 선택 체크박스
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

    // ── 셀 페인팅 ─────────────────────────────────────────
    void OnCellPainting(object? s, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is null || e.CellStyle is null) return;

        // ── 셀병합: sub-row 0 → 2행 합친 사각형에 가운데 정렬 ──────────
        if (e.RowIndex >= 0 && IsMergedCol(e.ColumnIndex))
        {
            int wi = e.RowIndex / 2;
            int sr = e.RowIndex % 2;
            if (wi < _workers.Count)
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

    void DrawDayWatermark(Graphics g, Rectangle rect, int day)
    {
        using var f  = new Font("맑은 고딕", 7f);
        using var wp = new SolidBrush(ThemeManager.IsDark ? Color.FromArgb(90, 180, 195, 210) : Color.FromArgb(90, 120, 130, 160));
        using var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
        g.DrawString(day.ToString(), f, wp, rect, sf);
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

    // ── 상태 표시 ─────────────────────────────────────────
    void UpdateInfo()
    {
        double tot = _workers.Sum(d => ParseD(d.Length > I_TOTAL ? d[I_TOTAL] : ""));
        double net = _workers.Sum(d => ParseD(d.Length > I_NET   ? d[I_NET]   : ""));
        _lblInfo.Text = $"  {_year}년 {_month:D2}월 | {_project} | {_workers.Count}명 | 임금총액: {tot:N0}원 | 차감지급: {net:N0}원";
    }

    List<int> CheckedWorkers() =>
        Enumerable.Range(0, _workers.Count).Where(i => _workers[i][I_SELECT] == "1").ToList();

    // ── Excel 저장 ────────────────────────────────────────
    void ExportExcel()
    {
        using var dlg = new SaveFileDialog
        {
            Title    = "지급대장 Excel 저장",
            Filter   = "Excel 파일|*.xlsx",
            FileName = $"지급대장_{_year}{_month:D2}_{_project}.xlsx",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("지급대장");
            var headers = AppConfig.PayCols.Take(N_INFO).Concat(
                Enumerable.Range(1, _lastDay).Select(d => d.ToString())).Concat(
                SumHdrs.Select(h => h.Replace("\n", "/"))).ToList();
            for (int c = 0; c < headers.Count; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            int row = 2;
            foreach (var d in _workers)
            {
                for (int i = 0; i < AppConfig.PayCols.Length; i++)
                    ws.Cell(row, i + 1).Value = d[i];
                row++;
            }
            wb.SaveAs(dlg.FileName);
            MessageBox.Show($"Excel 저장 완료:\n{dlg.FileName}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Excel 저장 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    // ── 인쇄 공통 헬퍼 ───────────────────────────────────

    // Excel COM으로 PDF 변환 시도. 성공하면 pdfPath에 파일 생성 후 true 반환.
    static bool TryExportPdf(string xlsxPath, string pdfPath)
    {
        try
        {
            var t = Type.GetTypeFromProgID("Excel.Application");
            if (t == null) return false;
            dynamic xl = Activator.CreateInstance(t)!;
            xl.Visible       = false;
            xl.DisplayAlerts = false;
            try
            {
                var wb = xl.Workbooks.Open(Path.GetFullPath(xlsxPath));
                wb.ExportAsFixedFormat(0, Path.GetFullPath(pdfPath)); // 0 = xlTypePDF
                wb.Close(false);
                return File.Exists(pdfPath);
            }
            finally
            {
                try { xl.Quit(); } catch { }
                try { Marshal.ReleaseComObject(xl); } catch { }
            }
        }
        catch { return false; }
    }

    // 템플릿 채우기 → XLSX 임시저장 → PDF 변환 시도 → 기본 뷰어로 열기
    void RunPrint(string tplName, string filePrefix, Action<XLWorkbook, List<int>> fill)
    {
        var chk = CheckedWorkers();
        if (chk.Count == 0) { MessageBox.Show("인쇄할 인원을 선택하세요.", "알림"); return; }

        // 설정(상호 등)·근로자 정보가 그새 바뀌었을 수 있으니 최신값으로 다시 읽는다.
        LoadBusiSettings();
        LoadFullWorkers();

        var tpl    = AppConfig.SamplePath(tplName);
        bool hasTpl = File.Exists(tpl);
        if (!hasTpl)
        {
            MessageBox.Show($"템플릿 파일을 찾을 수 없습니다.\n{tpl}\n\n프로그램을 재설치하면 해결됩니다.",
                "출력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var ts      = DateTime.Now.ToString("HHmmss");
        var tmpXlsx = Path.Combine(Path.GetTempPath(), $"__{filePrefix}_{ts}.xlsx");
        var tmpPdf  = Path.ChangeExtension(tmpXlsx, ".pdf");

        Cursor = Cursors.WaitCursor;
        try
        {
            if (hasTpl) File.Copy(tpl, tmpXlsx, true);
            using (var wb = hasTpl ? new XLWorkbook(tmpXlsx) : new XLWorkbook())
            {
                if (!hasTpl) wb.Worksheets.Add(tplName);
                fill(wb, chk);
                wb.SaveAs(tmpXlsx);
            }

            bool hasPdf = TryExportPdf(tmpXlsx, tmpPdf);
            var openPath = hasPdf ? tmpPdf : tmpXlsx;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(openPath) { UseShellExecute = true });

            if (MessageBox.Show("엑셀 파일로 저장하시겠습니까?", "저장",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                using var dlg = new SaveFileDialog
                {
                    Filter   = "Excel 파일|*.xlsx",
                    FileName = $"{_project}_{_year}{_month:D2}_{filePrefix}.xlsx",
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                    File.Copy(tmpXlsx, dlg.FileName, true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ── 문자 발송 ─────────────────────────────────────────
    void OpenSmsDialog()
    {
        var selected = _workers.Where(d => d[I_SELECT] == "1").ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("발송할 근로자를 체크(선택)해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var workers = selected.Select(d => (name: d[I_NAME], phone: d[I_PHONE])).ToList();
        var payroll = selected.Select(d => new Dictionary<string, string>
        {
            ["일수"]      = d[I_DAYS],
            ["주휴수당"]  = d[I_JUHU_PAY],
            ["임금총액"]  = d[I_TOTAL],
            ["국민연금"]  = d[I_KUKMIN],
            ["건강보험"]  = d[I_HEALTH],
            ["고용보험"]  = d[I_EMPLOY],
            ["요양보험"]  = d[I_CARE],
            ["소득세"]    = d[I_TAX],
            ["주민세"]    = d[I_MINTAX],
            ["공제합계"]  = d[I_DEDUCT],
            ["차감지급액"]= d[I_NET],
        }).ToList();
        var project     = BS("매장유형").Length > 0 ? BS("매장유형") : _project;
        var companyName = BS("상호");
        using var dlg = new manHours.Forms.SmsDialog(workers, payroll, project, _year, _month, companyName);
        dlg.ShowDialog(FindForm());
    }

    // ── 인쇄 버튼들 ──────────────────────────────────────

    void PrintSinbun() => RunPrint("신분증사본", "sinbun", FillSinbun);

    void PrintGyeyak()   => RunPrint("근로계약서", "gyeyak", FillGyeyak);

    void PrintMyungseo() => RunPrint("임금명세서", "myungseo", FillMyungseo);


    void PrintGyobu() => RunPrint("교부확인서", "gyobu", FillGyobu);


    // ── 신분증사본 채우기 ─────────────────────────────────
    void FillSinbun(XLWorkbook wb, List<int> chk)
    {
        var sheets = CloneSheets(wb, chk.Count, "신분증");
        for (int i = 0; i < chk.Count; i++)
        {
            var ws = sheets[i];
            var d  = _workers[chk[i]];
            string nm    = d[I_NAME];
            string blood = WL(nm, "자격증");
            string bank  = WL(nm, "은행");
            string acct  = WL(nm, "계좌번호");
            string holder = WL(nm, "예금주"); if (string.IsNullOrEmpty(holder)) holder = nm;

            ws.Cell("D11").Value = nm;
            ws.Cell("I11").Value = blood;
            ws.Cell("M11").Value = d[I_JUMIN];
            ws.Cell("D12").Value = holder;
            ws.Cell("M12").Value = d[I_PHONE];
            ws.Cell("D13").Value = bank;
            ws.Cell("M13").Value = acct;
            ws.Cell("D14").Value = d[I_ADDR];

            // 사진 삽입 — 테두리 박스 전체를 가로·세로 꽉 채운다.
            // 주민등록증 박스=A4:I6, 자격증 박스=J4:R6, 통장 박스=A8:R10
            var folder = PhotoFolder(nm, d[I_PHONE]);
            if (folder != null)
            {
                InsertPhoto(ws, Path.Combine(folder, "주민.jpg"),   "A4", "J7");
                InsertPhoto(ws, Path.Combine(folder, "자격증.jpg"), "J4", "S7");
                InsertPhoto(ws, Path.Combine(folder, "통장.jpg"),   "A8", "S11");
            }
        }
    }

    // 근로자 사진 폴더 (근로자 대화상자와 동일 규칙: 이름 또는 이름_전화)
    static string? PhotoFolder(string name, string phone)
    {
        name = (name ?? "").Trim(); phone = (phone ?? "").Trim();
        if (name.Length == 0) return null;
        var folder = phone.Length == 0 ? name : $"{name}_{phone}";
        return Path.Combine(AppConfig.ImageDir, folder);
    }

    // fromCell~toCell 범위(테두리 박스)를 채우되, 테두리 선이 보이도록 안쪽 여백을 둔다
    static void InsertPhoto(IXLWorksheet ws, string path, string fromCell, string toCell)
    {
        if (!File.Exists(path)) return;
        try
        {
            const int pad = 9;   // 박스 안쪽 여백(px) — 테두리 가림 방지
            using var ms = new MemoryStream(File.ReadAllBytes(path));
            ws.AddPicture(ms).MoveTo(ws.Cell(fromCell), pad, pad, ws.Cell(toCell), -pad, -pad);
        }
        catch { }
    }

    // ── 표준근로계약서 채우기 ─────────────────────────────
    // ── 토큰 치환 엔진 ────────────────────────────────────
    // 템플릿 셀에 {{이름}} 처럼 적어두면 그 자리를 값으로 바꾼다.
    // 서식을 바꿔도 코드는 손댈 필요가 없다.
    static void ApplyTokens(IXLWorksheet ws, Dictionary<string, string> map)
    {
        var used = ws.RangeUsed();
        if (used == null) return;
        foreach (var cell in used.CellsUsed())
        {
            if (cell.DataType != XLDataType.Text) continue;
            var text = cell.GetString();
            if (!text.Contains("{{")) continue;

            foreach (var kv in map)
                text = text.Replace("{{" + kv.Key + "}}", kv.Value);

            // 숫자만 남으면 숫자로 넣어야 엑셀에서 합계·서식이 먹는다
            if (long.TryParse(text.Replace(",", ""), out var n))
            {
                cell.Value = n;
                // 값에 콤마가 있었다면 금액(Won) 이므로 천단위 서식을 유지한다.
                // 년도(2026)·사번 같은 값은 콤마가 없어 그대로 둔다.
                if (text.Contains(',')) cell.Style.NumberFormat.Format = "#,##0";
            }
            else cell.Value = text;
        }
    }

    static string Won(string raw) =>
        long.TryParse((raw ?? "").Replace(",", ""), out var v) && v != 0 ? v.ToString("N0") : "0";

    static string Hrs(string raw) =>
        double.TryParse(raw, out var v) && v > 0 ? v.ToString("0.##") : "0";

    /// <summary>근로자 1명의 모든 토큰 값을 만든다 (계약서·명세서 공용).</summary>
    Dictionary<string, string> BuildTokens(string[] d)
    {
        string nm    = d[I_NAME];
        string wage  = Won(d[I_WAGE]);
        double wageV = double.TryParse(d[I_WAGE], out var wv) ? wv : 0;

        // 산출식(근로기준법상 임금명세서 필수 기재)
        string Calc(string hourRaw, string amtRaw, double mult, string label)
        {
            double h = double.TryParse(hourRaw, out var hh) ? hh : 0;
            if (h <= 0 || wageV <= 0) return "-";
            return mult == 1
                ? $"시급 {wage}원 × {h:0.##}시간 = {Won(amtRaw)}원"
                : $"시급 {wage}원 × {h:0.##}시간 × {mult * 100:0}% = {Won(amtRaw)}원";
        }

        var m = new Dictionary<string, string>
        {
            // 근로자
            ["이름"]       = nm,
            ["생년월일"]   = d[I_JUMIN],
            ["주소"]       = d[I_ADDR],
            ["전화번호"]   = d[I_PHONE],
            ["사번"]       = d[I_SEQ],
            ["근무구분"]   = WL(nm, "근무구분"),
            ["자격증"]     = WL(nm, "자격증"),
            ["은행"]       = WL(nm, "은행"),
            ["계좌번호"]   = WL(nm, "계좌번호"),
            ["예금주"]     = WL(nm, "예금주"),

            // 사업장
            ["상호"]       = BS("상호"),
            ["대표자"]     = BS("대표자"),
            ["사업장주소"] = BS("거주지역"),
            ["사업장전화"] = BS("전화번호"),
            ["매장유형"]   = BS("매장유형"),
            ["근무장소"]   = string.IsNullOrEmpty(BS("근무장소")) ? BS("소재지") : BS("근무장소"),
            ["업무내용"]   = WL(nm, "근무구분"),

            // 기간
            ["년도"]       = _year.ToString(),
            ["월"]         = _month.ToString(),
            ["지급일"]     = $"{_year}-{_month:D2}-{PayDay():D2}",
            ["계약일"]     = $"{_year}년 {_month:D2}월 {FirstDay(d)}일",
            ["근로개시일"] = $"{_year}년 {_month:D2}월 {FirstDay(d)}일부터",
            ["임금지급일"] = PayDay().ToString(),
            ["가산임금률"] = "50",
            ["사회보험"]   = "☑ 고용보험  ☑ 산재보험  ☑ 국민연금  ☑ 건강보험",
            ["주휴일"]     = "일",

            // 근무 집계
            ["시급"]         = wage,
            ["총근무시간"]   = Hrs(d[I_HOURS]),
            ["근무일수"]     = Hrs(d[I_DAYS]),
            ["주휴시간"]     = Hrs(d[I_JUHU_H]),
            ["연장시간"]     = Hrs(d[I_OT_H]),
            ["야간시간"]     = Hrs(d[I_NIGHT_H]),
            ["휴일시간"]     = Hrs(d[I_HOL_H]),

            // 지급
            ["기본급"]     = Won(d[I_BASIC]),
            ["주휴수당"]   = Won(d[I_JUHU_PAY]),
            ["연장수당"]   = Won(d[I_OT_PAY]),
            ["야간수당"]   = Won(d[I_NIGHT_PAY]),
            ["휴일수당"]   = Won(d[I_HOL_PAY]),
            ["지급액계"]   = Won(d[I_TOTAL]),
            ["임금총액"]   = Won(d[I_TOTAL]),

            // 공제
            ["국민연금"]   = Won(d[I_KUKMIN]),
            ["건강보험"]   = Won(d[I_HEALTH]),
            ["고용보험"]   = Won(d[I_EMPLOY]),
            ["요양보험"]   = Won(d[I_CARE]),
            ["소득세"]     = Won(d[I_TAX]),
            ["주민세"]     = Won(d[I_MINTAX]),
            ["공제액계"]   = Won(d[I_DEDUCT]),
            ["실지급액"]   = Won(d[I_NET]),

            // 산출식 (법정 필수)
            ["기본급_산출"]   = Calc(d[I_HOURS],   d[I_BASIC],     1.0,  "기본급"),
            ["주휴수당_산출"] = Calc(d[I_JUHU_H],  d[I_JUHU_PAY],  1.0,  "주휴수당"),
            ["연장수당_산출"] = Calc(d[I_OT_H],    d[I_OT_PAY],    0.5,  "연장수당"),
            ["야간수당_산출"] = Calc(d[I_NIGHT_H], d[I_NIGHT_PAY], 0.5,  "야간수당"),
            ["휴일수당_산출"] = Calc(d[I_HOL_H],   d[I_HOL_PAY],   0.5,  "휴일수당"),
        };

        // 요일별 근로시간 (계약서 4항 — 단시간근로자는 반드시 기재해야 함)
        var wk = WeekdayPattern(d);
        string[] days = ["월", "화", "수", "목", "금", "토", "일"];
        for (int i = 0; i < 7; i++)
        {
            var p = wk[i];
            m[$"{days[i]}_근로시간"] = p.Has ? $"{p.Work:0.##}시간" : "-";
            m[$"{days[i]}_시업"]     = p.Has ? Clock(p.St)          : "-";
            m[$"{days[i]}_종업"]     = p.Has ? Clock(p.En)          : "-";
            m[$"{days[i]}_휴게"]     = p.Has && p.Break > 0
                                       ? $"{p.Break * 60:0}분"      : "-";
        }
        return m;
    }

    int FirstDay(string[] d)
    {
        for (int day = 1; day <= _lastDay; day++)
            if (WorkerScreen.ParseRange(d[9 + day - 1]).st >= 0) return day;
        return 1;
    }

    static string Clock(double v)
    {
        int h = (int)v, mi = (int)Math.Round((v - h) * 60);
        if (mi == 60) { h++; mi = 0; }
        return $"{h % 24:D2}:{mi:D2}";
    }

    readonly record struct DayPat(bool Has, double St, double En, double Work, double Break);

    /// <summary>이 달 기록에서 요일별 대표 근무패턴을 뽑는다(월~일). 각 요일의 첫 근무일 기준.</summary>
    DayPat[] WeekdayPattern(string[] d)
    {
        var res = new DayPat[7];
        for (int day = 1; day <= _lastDay; day++)
        {
            var (st, en) = WorkerScreen.ParseRange(d[9 + day - 1]);
            if (st < 0) continue;
            int idx = ((int)new DateTime(_year, _month, day).DayOfWeek + 6) % 7;  // 월=0
            if (res[idx].Has) continue;

            double gross = en - st;
            double brk   = d[I_BREAK] != "0" ? (gross >= 8 ? 1.0 : gross >= 4 ? 0.5 : 0) : 0;
            res[idx] = new DayPat(true, st, en, gross - brk, brk);
        }
        return res;
    }

    /// <summary>근로자 1명 = A4 1페이지. ClosedXML 이 시트 복사 시 맞춤설정을 잃어버려서
    /// 템플릿에만 맡기지 않고 시트마다 코드로 강제한다.</summary>
    static void FitOnePage(IXLWorksheet ws)
    {
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize       = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 1);
        ws.PageSetup.Margins.Left   = 0.3;
        ws.PageSetup.Margins.Right  = 0.3;
        ws.PageSetup.Margins.Top    = 0.35;
        ws.PageSetup.Margins.Bottom = 0.35;
        ws.PageSetup.Margins.Header = 0.15;
        ws.PageSetup.Margins.Footer = 0.15;
    }

    // ── 근로계약서 (단시간근로자 표준근로계약서) ────────────
    void FillGyeyak(XLWorkbook wb, List<int> chk)
    {
        // ⚠ 반드시 '채우기 전에' 필요한 만큼 먼저 복제한다.
        //    (원본을 먼저 채우면 그 뒤 복사본은 이미 값이 박힌 시트를 복사 → 전원 첫 사람으로 나옴)
        var sheets = CloneSheets(wb, chk.Count, "계약서");
        for (int i = 0; i < chk.Count; i++)
        {
            ApplyTokens(sheets[i], BuildTokens(_workers[chk[i]]));
            FitOnePage(sheets[i]);
        }
    }

    // 원본(빈 템플릿) 시트에서 n개 시트를 만들어 반환 [0]=원본, [1..]=복사본
    static List<IXLWorksheet> CloneSheets(XLWorkbook wb, int n, string namePrefix)
    {
        var tplWs = wb.Worksheet(1);
        var list = new List<IXLWorksheet> { tplWs };
        for (int i = 1; i < n; i++)
            list.Add(tplWs.CopyTo(wb, $"{namePrefix}{i + 1}"));
        return list;
    }

    // ── 임금명세서 (계산방법 포함 — 근로기준법 제48조) ──────
    void FillMyungseo(XLWorkbook wb, List<int> chk)
    {
        var sheets = CloneSheets(wb, chk.Count, "명세서");
        for (int i = 0; i < chk.Count; i++)
        {
            ApplyTokens(sheets[i], BuildTokens(_workers[chk[i]]));
            FitOnePage(sheets[i]);
        }
    }

    // ── N월 명세서 (pmis) 채우기 ──────────────────────────
    // ── 입금명세표 채우기 (명세표 템플릿, 4인/시트) ────────
    // ── 교부확인서 채우기 ─────────────────────────────────
    void FillGyobu(XLWorkbook wb, List<int> chk)
    {
        string site    = BS("명칭"); if (string.IsNullOrEmpty(site)) site = BS("매장유형");
        string company = BS("상호");

        var ws = wb.Worksheet(1);
        ws.Cell(1, 1).Value = $"노무비 명세표 교부확인서({_year}년{_month:D2}월)";
        ws.Cell(3, 1).Value = $"현장명 : {site}";
        ws.Cell(4, 1).Value = $"회사명 : {company}";
        ws.Cell(5, 1).Value = $"   '{_year}년 {_month:D2}월분 노무비 명세표를 수령하였음을 확인합니다.";

        const int DATA_START = 7;
        for (int i = 0; i < chk.Count; i++)
        {
            var d  = _workers[chk[i]];
            int r  = DATA_START + i;
            // 행이 부족하면 이전 행 서식 복사
            if (ws.LastRowUsed()?.RowNumber() < r)
                ws.Row(r - 1).CopyTo(ws.Row(r));
            ws.Cell(r, 1).Value = i + 1;
            ws.Cell(r, 2).Value = d[I_NAME];
            ws.Cell(r, 3).Value = d[I_JUMIN];
            ws.Cell(r, 4).Value = d[I_PHONE];
        }
    }

    // ── 고용산재신고서 채우기 ─────────────────────────────
    // ── 헬퍼 ─────────────────────────────────────────────
    // 날짜 값: 비어있거나 0이면 0, 아니면 double
    static double ParseDayVal(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.Replace(",", "").Trim();
        if (s is "0" or "0.0") return 0;
        return double.TryParse(s, out double d) ? d : 0;
    }

    static long ParseLong(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return long.TryParse(s.Replace(",", "").Trim(), out var v) ? v : 0;
    }

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

    static Button MakePrintBtn(string text, int w, Action onClick)
    {
        var b = new Button
        {
            Text = text, Width = w, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 30, 90), ForeColor = Color.FromArgb(205, 180, 255),
            Font = ThemeManager.F(9f), Cursor = Cursors.Hand, Margin = new Padding(2, 0, 2, 0),
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 60, 140);
        b.Click += (_, _) => onClick();
        return b;
    }
}

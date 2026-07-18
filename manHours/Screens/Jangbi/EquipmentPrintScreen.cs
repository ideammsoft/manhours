using ClosedXML.Excel;
using System.Runtime.InteropServices;
using manHours.Screens.Noim;

namespace manHours.Screens.Jangbi;

public class EquipmentPrintScreen : UserControl
{
    public event Action? GoPrev;

    // ── 레이아웃 상수 ─────────────────────────────────────
    const int N_INFO  = 7;
    const int N_DATES = 16;
    const int N_SUMS  = 3;
    const int D_DATE_COL = 7;
    const int D_SUM_COL  = 23;

    static readonly int[] InfoWidths = [36, 34, 140, 110, 120, 110, 72];
    static readonly string[] InfoHdrs =
        ["☑\n전체", "순번", "사업장명\n사업자번호", "건설기계명\n근무구분", "장비운전자\n주민등록번호", "연락처\n건설기계번호", "보수\n단가"];
    static readonly string[] SumHdrs =
        ["작업일수\n보수총액", "고용보험률\n산재보험률", "고용주\n특고자"];

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
    Label        _lblInfo = null!;

    public EquipmentPrintScreen()
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
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent,
            Padding = new Padding(8, 6, 8, 6),
        };
        flow.Controls.Add(MakeBtn("↺ 새로고침", 96, () => Load(_year, _month, _project)));
        var btnXl = MakeBtn("Excel 저장", 90, ExportExcel);
        btnXl.BackColor = Color.FromArgb(20, 100, 50);
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
            Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 4, 4, 4),
        };

        var btnPrev = MakeBtn("← 이전 단계", 108, () => GoPrev?.Invoke());
        btnPrev.Dock      = DockStyle.Left;
        btnPrev.BackColor = ThemeManager.IsDark ? ThemeManager.BtnSide : Color.FromArgb(140, 146, 175);
        btnPrev.ForeColor = ThemeManager.IsDark ? ThemeManager.BtnText : Color.White;

        var printFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, WrapContents = false, AutoSize = true,
            BackColor = Color.Transparent, FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 0, 0, 4),
        };
        printFlow.Controls.Add(MakePrintBtn("특고근로대장", 110, PrintTukgo));

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
            Dock = DockStyle.Fill, BackgroundColor = ThemeManager.BgCell,
            GridColor = ThemeManager.GridLine, BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 52,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false, ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
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
        _grid.CellPainting += OnCellPainting;
        _grid.CellClick    += OnCellClick;
        SetupColumns();
        return _grid;
    }

    void SetupColumns()
    {
        _grid.Columns.Clear();
        // INFO
        for (int i = 0; i < N_INFO; i++)
        {
            DataGridViewColumn col = i == 0
                ? new DataGridViewCheckBoxColumn { Width = InfoWidths[i], ReadOnly = false }
                : new DataGridViewTextBoxColumn  { Width = InfoWidths[i], ReadOnly = true };
            if (col is DataGridViewTextBoxColumn tc && (i == 2 || i == 3 || i == 4 || i == 5))
                tc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            col.HeaderText = InfoHdrs[i];
            col.HeaderCell = new MultiLineHeaderCell { Value = InfoHdrs[i] };
            col.SortMode   = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(col);
        }
        // DATE
        for (int s = 0; s < N_DATES; s++)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = "", Width = 30, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell = new MultiLineHeaderCell { Value = "" };
            _grid.Columns.Add(col);
        }
        // SUM
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
        UpdateInfo();
    }

    void LoadWorkers()
    {
        _equipWorkers.Clear();
        var dbPath = AppConfig.EquipMonthlyDbPath(_year, _month, _project);
        if (!File.Exists(dbPath)) return;
        using var db = new Database(dbPath);
        if (!db.TableExists("장비지급대장")) return;
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
        row.Cells[0].Value = subRow == 0 && d[I_SEL] == "1";
        row.Cells[1].Value = subRow == 0 ? d[I_SEQ]    : "";
        row.Cells[2].Value = subRow == 0 ? d[I_SITE]   : d[I_BIZNO];
        row.Cells[3].Value = subRow == 0 ? d[I_MACH]   : d[I_JIKJ];
        row.Cells[4].Value = subRow == 0 ? d[I_WORKER] : d[I_JUMIN];
        row.Cells[5].Value = subRow == 0 ? d[I_PHONE]  : d[I_MNO];
        row.Cells[6].Value = subRow == 0 ? FmtComma(d[I_DANAKA]) : "";
        for (int s = 0; s < N_DATES; s++)
        {
            int day = subRow == 0 ? (s < 15 ? s + 1 : 0) : (s < 15 ? s + 16 : _lastDay);
            row.Cells[D_DATE_COL + s].Value =
                (day > 0 && day <= _lastDay) ? d[AppConfig.EQ_DATE_DB + day - 1] : "";
        }
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
                CheckBoxRenderer.DrawCheckBox(e.Graphics,
                    new Point(combined.X + (combined.Width - sz.Width) / 2,
                              combined.Y + (combined.Height - sz.Height) / 2), st);
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
        var g = e.Graphics; if (g == null || e.CellStyle == null) { e.Handled = true; return; }
        int slot = e.ColumnIndex - D_DATE_COL;
        int day1 = slot + 1; int day2 = slot + 16; int halfH = e.CellBounds.Height / 2;

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

    void UpdateInfo()
    {
        double tot = _equipWorkers.Sum(d => ParseD(d.Length > I_TOTAL ? d[I_TOTAL] : ""));
        _lblInfo.Text = $"  {_year}년 {_month:D2}월 | {_project} | {_equipWorkers.Count}건 | 보수총액: {tot:N0}원";
    }

    List<int> CheckedWorkers() =>
        Enumerable.Range(0, _equipWorkers.Count).Where(i => _equipWorkers[i][I_SEL] == "1").ToList();

    // ── Excel 저장 ────────────────────────────────────────
    void ExportExcel()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "특고근로대장 Excel 저장", Filter = "Excel 파일|*.xlsx",
            FileName = $"특고근로대장_{_year}{_month:D2}_{_project}.xlsx",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("특고근로대장");
            var headers = AppConfig.EquipPayCols;
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            int row = 2;
            foreach (var d in _equipWorkers)
            {
                for (int i = 0; i < AppConfig.EquipPayCols.Length; i++)
                    ws.Cell(row, i + 1).Value = d[i];
                row++;
            }
            wb.SaveAs(dlg.FileName);
            MessageBox.Show($"Excel 저장 완료:\n{dlg.FileName}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel 저장 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── 특고근로대장 인쇄 ─────────────────────────────────
    void PrintTukgo()
    {
        var chk = CheckedWorkers();
        if (chk.Count == 0) { MessageBox.Show("인쇄할 항목을 선택하세요.", "알림"); return; }

        var tpl = AppConfig.SamplePath("특고근로대장");
        if (!File.Exists(tpl))
        {
            MessageBox.Show($"템플릿 파일이 없습니다:\n{tpl}", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var ts      = DateTime.Now.ToString("HHmmss");
        var tmpXlsx = Path.Combine(Path.GetTempPath(), $"__tukgo_{ts}.xlsx");
        var tmpPdf  = Path.ChangeExtension(tmpXlsx, ".pdf");
        Cursor = Cursors.WaitCursor;
        try
        {
            bool ok = TryFillTukgoAndPdf(tpl, tmpXlsx, tmpPdf, chk);
            var openPath = ok ? tmpPdf : tmpXlsx;
            if (!ok && !File.Exists(tmpXlsx))
            {
                MessageBox.Show("Excel이 설치되어 있지 않습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(openPath) { UseShellExecute = true });

            if (MessageBox.Show("엑셀 파일로 저장하시겠습니까?", "저장",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                using var save = new SaveFileDialog
                {
                    Filter = "Excel 파일|*.xlsx",
                    FileName = $"{_project}_{_year}{_month:D2}_특고근로대장.xlsx",
                };
                if (save.ShowDialog() == DialogResult.OK) File.Copy(tmpXlsx, save.FileName, true);
            }
        }
        catch (Exception ex) { MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { Cursor = Cursors.Default; }
    }

    bool TryFillTukgoAndPdf(string tplPath, string tmpXlsx, string tmpPdf, List<int> chk)
    {
        var t = Type.GetTypeFromProgID("Excel.Application");
        if (t == null) return false;

        dynamic xl = Activator.CreateInstance(t)!;
        xl.Visible       = false;
        xl.DisplayAlerts = false;
        try
        {
            File.Copy(tplPath, tmpXlsx, true);
            var wb = xl.Workbooks.Open(Path.GetFullPath(tmpXlsx));
            var ws = wb.Sheets[1];

            // ── 헤더 ──
            var busi = GetBusiSettings();
            string company  = busi.GetValueOrDefault("상호", "");
            string siteName = busi.GetValueOrDefault("매장유형", _project);
            if (string.IsNullOrEmpty(siteName)) siteName = _project;
            ws.Cells[3, 2].Value = company;    // B3 상호
            ws.Cells[3, 4].Value = siteName;   // D3 현장명
            // F3:AC3 병합셀 — 수식 제거 후 제목 기입
            try { ws.Cells[3, 6].Formula = ""; } catch { }
            ws.Cells[3, 6].Value = $"[{_year}년 {_month:D2}월분] 특수형태근로종사자(건설기계)근로대장";

            // ── 데이터: row 8부터 2행씩 (숨겨진 행 언숨김 포함) ──
            const int START_ROW = 8;
            for (int si = 0; si < chk.Count; si++)
            {
                var d     = _equipWorkers[chk[si]];
                int baseR = START_ROW + si * 2;
                int midR  = baseR + 1;

                // 템플릿에서 person 2+ 행은 hidden=1 → 언숨김
                try { ws.Rows[baseR].Hidden = false; } catch { }
                try { ws.Rows[midR].Hidden  = false; } catch { }

                ws.Cells[baseR, 1].Value = si + 1;      // A 연번
                ws.Cells[baseR, 2].Value = _project;    // B 사업장명
                ws.Cells[baseR, 3].Value = d[I_BIZNO];  // C 사업자등록번호
                ws.Cells[baseR, 4].Value = d[I_JIKJ];   // D 근무구분
                ws.Cells[baseR, 5].Value = d[I_MACH];   // E 건설기계명
                for (int day = 1; day <= Math.Min(15, _lastDay); day++)
                    ws.Cells[baseR, 8 + day].Value = ParseD(d[AppConfig.EQ_DATE_DB + day - 1]);

                ws.Cells[midR, 2].Value = d[I_WORKER];  // B 장비운전자
                ws.Cells[midR, 3].Value = d[I_JUMIN];   // C 주민등록번호
                ws.Cells[midR, 4].Value = d[I_PHONE];   // D 연락처
                ws.Cells[midR, 5].Value = d[I_MNO];     // E 건설기계번호
                for (int day = 16; day <= Math.Min(31, _lastDay); day++)
                    ws.Cells[midR, 8 + (day - 15)].Value = ParseD(d[AppConfig.EQ_DATE_DB + day - 1]);
            }

            wb.Save();  // xlsx 저장
            try { wb.ExportAsFixedFormat(0, Path.GetFullPath(tmpPdf)); } catch { }
            wb.Close(false);
            return File.Exists(tmpPdf);
        }
        finally
        {
            try { xl.Quit(); } catch { }
            try { Marshal.ReleaseComObject(xl); } catch { }
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    Dictionary<string, string> GetBusiSettings()
    {
        var result = new Dictionary<string, string>();
        try
        {
            using var db = new Database(AppConfig.BaseDbPath);
            if (!db.TableExists("사업설정")) return result;
            var (cols, rows) = db.SelectStrings("사업설정");
            int ni = Array.IndexOf(cols, "사업명");
            foreach (var row in rows)
            {
                if (ni >= 0 && ni < row.Length && row[ni] == _project)
                {
                    for (int i = 0; i < cols.Length && i < row.Length; i++)
                        result[cols[i]] = row[i] ?? "";
                    break;
                }
            }
        }
        catch { }
        return result;
    }

    static double ParseD(string? s) =>
        !string.IsNullOrEmpty(s) && double.TryParse(s.Replace(",", ""), out var v) ? v : 0;

    static long ParseLong(string? s) =>
        !string.IsNullOrEmpty(s) && long.TryParse(s.Replace(",", "").Trim(), out var v) ? v : 0;

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

namespace manHours;

public static class ThemeManager
{
    public static bool  IsDark   => AppSettings.Instance.Theme == "dark";
    public static float FontSize => AppSettings.Instance.FontSize;

    // ── 색상 (dark = catppuccin mocha, light = catppuccin latte) ──────
    public static Color BgMain     => IsDark ? Color.FromArgb(30, 30, 46)    : Color.FromArgb(239, 241, 245);
    public static Color BgSidebar  => IsDark ? Color.FromArgb(20, 20, 35)    : Color.FromArgb(204, 208, 218);
    public static Color BgCell     => IsDark ? Color.FromArgb(36, 36, 52)    : Color.White;
    public static Color BgAltCell  => IsDark ? Color.FromArgb(33, 33, 48)    : Color.FromArgb(238, 240, 248);
    public static Color BgInput    => IsDark ? Color.FromArgb(45, 45, 65)    : Color.White;
    public static Color BgHeader   => IsDark ? Color.FromArgb(20, 20, 35)    : Color.FromArgb(188, 192, 220);
    public static Color BgPanel    => IsDark ? Color.FromArgb(22, 22, 38)    : Color.FromArgb(220, 224, 232);
    public static Color BgBottom   => IsDark ? Color.FromArgb(20, 20, 35)    : Color.FromArgb(195, 199, 215);
    public static Color TextMain   => IsDark ? Color.FromArgb(205, 214, 244) : Color.FromArgb(76, 79, 105);
    public static Color TextSub    => IsDark ? Color.FromArgb(147, 153, 178) : Color.FromArgb(108, 111, 133);
    public static Color Accent     => Color.FromArgb(137, 180, 250);
    public static Color Border     => IsDark ? Color.FromArgb(50, 50, 75)    : Color.FromArgb(172, 176, 190);
    public static Color GridLine   => IsDark ? Color.FromArgb(50, 52, 72)    : Color.FromArgb(188, 192, 210);
    public static Color SelectBg   => IsDark ? Color.FromArgb(50, 70, 120)   : Color.FromArgb(180, 210, 245);
    public static Color SelectFg   => IsDark ? Color.White                   : Color.Black;
    public static Color TabBack    => IsDark ? Color.FromArgb(22, 22, 38)    : Color.FromArgb(200, 204, 218);
    public static Color TabSel     => IsDark ? Color.FromArgb(30, 30, 46)    : Color.FromArgb(239, 241, 245);
    public static Color BtnSide    => IsDark ? Color.FromArgb(40, 44, 68)    : Color.FromArgb(170, 175, 200);
    public static Color BtnText    => IsDark ? Color.FromArgb(166, 173, 200) : Color.FromArgb(40, 45, 65);
    public static Color HeaderText => IsDark ? Accent                        : Color.FromArgb(40, 55, 120);

    // ── 폰트 헬퍼 (FontSize 기준 9.5pt 비례 조정) ────────────
    public static Font F(float size, FontStyle style = FontStyle.Regular) =>
        new Font("맑은 고딕", Math.Max(6f, size * FontSize / 9.5f), style);

    // ── 그리드 일괄 적용 ─────────────────────────────────────────────
    public static void ApplyGrid(DataGridView g)
    {
        g.BackgroundColor                                        = BgCell;
        g.GridColor                                              = GridLine;
        g.DefaultCellStyle.BackColor                             = BgCell;
        g.DefaultCellStyle.ForeColor                             = TextMain;
        g.DefaultCellStyle.SelectionBackColor                    = SelectBg;
        g.DefaultCellStyle.SelectionForeColor                    = SelectFg;
        g.AlternatingRowsDefaultCellStyle.BackColor              = BgAltCell;
        g.AlternatingRowsDefaultCellStyle.SelectionBackColor     = SelectBg;
        g.AlternatingRowsDefaultCellStyle.SelectionForeColor     = SelectFg;
        g.ColumnHeadersDefaultCellStyle.BackColor                = BgHeader;
        g.ColumnHeadersDefaultCellStyle.ForeColor                = HeaderText;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor       = BgHeader;
        g.EnableHeadersVisualStyles                              = false;
    }

    // ── TabControl owner-draw ────────────────────────────────────────
    public static void ApplyTabControl(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.DrawItem += DrawTab;
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var tabs = (TabControl)sender!;
        var page = tabs.TabPages[e.Index];
        bool sel = e.Index == tabs.SelectedIndex;
        var bg   = sel ? TabSel : TabBack;
        var fg   = sel ? Accent : BtnText;

        using var b  = new SolidBrush(bg);
        e.Graphics.FillRectangle(b, e.Bounds);

        using var sf = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var font = new Font("맑은 고딕", 9.5f, sel ? FontStyle.Bold : FontStyle.Regular);
        using var brush = new SolidBrush(fg);
        e.Graphics.DrawString(page.Text, font, brush, e.Bounds, sf);
    }
}

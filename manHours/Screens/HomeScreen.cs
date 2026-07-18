namespace manHours.Screens;

public class HomeScreen : UserControl
{
    public HomeScreen()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        var lbl = new Label
        {
            Text      = "좌측에서 작업할 메뉴를 선택 해주세요",
            Font      = new Font("맑은 고딕", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(90, 95, 120),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
        };
        Controls.Add(lbl);
    }
}

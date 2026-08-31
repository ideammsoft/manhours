using System.Text;
using manHours;
using manHours.Forms;
using Mmsoft.Login;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // 공지사항 euc-kr 디코딩
        AppConfig.EnsureBaseDirs();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // ── 로그인 (공용 로그인 라이브러리) ────────────────────
        var cfg = LoginSettings.Load();
        var opt = new LoginOptions
        {
            ProgramName      = "manHours",
            WindowTitle      = "맨아워즈(manHours) v1.0 - 로그인",
            BrandLeft        = "맨",
            BrandMid         = "",
            BrandRight       = "아워즈",
            Subtitle         = "manHours  v1.0  —  통합 일일 근로 임금계산",
            AppIcon          = TryLoadIcon(),
            HeaderImage      = TryLoadEmbedded("app_face.png") ?? TryLoadImage("app_face.png"),
            ApiBase          = cfg.ApiBase,
            AllowServerEdit  = IsDevStartFolder(),
            LogFolder        = Path.Combine(AppConfig.BaseDir, "log"),
            RememberLogin    = cfg.RememberLogin,
            SavedLoginId     = cfg.SavedLoginId,
            SavedLoginPw     = cfg.SavedLoginPw,
            ApiKey           = cfg.ApiKey,
            GuestMessage     = "비회원은 1개월간 무료로 이용하실 수 있습니다.\n회원가입 시에는 6개월간 무료 이용 혜택이 있습니다.\n계속 하시려면 확인을 눌러주세요.",
            GuestTrialMonths  = IsMyPc() ? 12 : 1,   // 내 PC만 비회원 1년
            MemberTrialMonths = 6,
            GuestTrialStart  = DateTime.TryParse(cfg.GuestTrialStart, out var gs) ? gs : null,
            MemberTrialStart = DateTime.TryParse(cfg.MemberTrialStart, out var ms) ? ms : null,
            // 지난 로그인 세션(소셜 포함) — [로그인]만 눌러 다시 입장할 수 있게
            SavedAccountId    = cfg.LoginAccountId,
            SavedProvider     = cfg.LoginProvider,
            SavedSessionUser  = cfg.SessionUser,
            SavedSessionToken = cfg.SessionToken,
        };

        LoginResult r;
        using (var login = new LoginForm(opt))
        {
            if (login.ShowDialog() != DialogResult.OK) return;
            r = login.Result;
        }
        cfg.ApiBase        = opt.ApiBase;
        cfg.ApiKey         = string.IsNullOrEmpty(r.ApiKey) ? opt.ApiKey : r.ApiKey;
        cfg.RememberLogin  = r.RememberLogin;
        cfg.SavedLoginId   = r.SavedLoginId;
        cfg.SavedLoginPw   = r.SavedLoginPw;
        cfg.LoginAccountId = r.AccountId;
        cfg.LoginProvider  = r.Provider;
        cfg.SessionUser    = r.SessionUser;
        cfg.SessionToken   = r.SessionToken;
        if (r.GuestTrialStart != null) cfg.GuestTrialStart = r.GuestTrialStart.Value.ToString("yyyy-MM-dd");
        if (r.MemberTrialStart != null) cfg.MemberTrialStart = r.MemberTrialStart.Value.ToString("yyyy-MM-dd");
        cfg.Save();

        // 무료 이용기간 만료 → 근무시간 입력·주간 근무표만 잠근다(나머지는 사용 가능)
        AppConfig.TrialExpired = r.TrialExpired;

        // ── 중복 실행 감지 ─────────────────────────────────────
        var session = SessionClient.Create(cfg.ApiBase, "manHours", cfg.LoginAccountId, cfg.ApiKey);
        if (session != null)
        {
            var a = session.Acquire(force: false);
            if (a.Status == "in_use")
            {
                var info = string.IsNullOrEmpty(a.MachineName) ? "" : $"\n(사용 중: {a.MachineName}";
                if (info.Length > 0 && !string.IsNullOrEmpty(a.StartedAt)) info += $", 시작 {a.StartedAt}";
                if (info.Length > 0) info += ")";
                var ans = MessageBox.Show(
                    "이미 사용중이거나 정상종료가 되지 않은 소프트웨어가 실행중입니다." + info +
                    "\n\n종료시키고 사용할까요?",
                    "중복 실행 감지", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ans != DialogResult.Yes) return;
                session.Acquire(force: true);
            }
        }

        // ── 메인 폼 실행 (테마 변경 시 재시작 루프) ─────────────
        Form? form = new MainForm();
        while (form != null)
        {
            AppConfig.PendingNextForm = null;
            Application.Run(form);
            form = AppConfig.PendingNextForm;
        }

        session?.Release();
    }

    static Icon? TryLoadIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(path)) return new Icon(path);
            var exe = Environment.ProcessPath;
            return exe != null ? Icon.ExtractAssociatedIcon(exe) : null;
        }
        catch { return null; }
    }

    static Image? TryLoadEmbedded(string fileName)
    {
        try
        {
            var asm = typeof(Program).Assembly;
            var res = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (res == null) return null;
            using var s = asm.GetManifestResourceStream(res);
            return s == null ? null : Image.FromStream(s);
        }
        catch { return null; }
    }

    static Image? TryLoadImage(string name)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            return File.Exists(path) ? Image.FromFile(path) : null;
        }
        catch { return null; }
    }

    // 내 PC(개발 머신)에서만 비회원 무료기간을 길게 잡기 위한 판별
    static bool IsMyPc()
    {
        try
        {
            return string.Equals(Environment.MachineName, "DESKTOP-VP8UE0J", StringComparison.OrdinalIgnoreCase)
                || IsDevStartFolder();
        }
        catch { return false; }
    }

    static bool IsDevStartFolder()
    {
        try
        {
            return AppContext.BaseDirectory.TrimStart()
                .StartsWith(@"c:\_mm_project", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

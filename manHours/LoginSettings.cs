using System.Text.Json;

namespace manHours;

// 로그인 관련 설정 저장소 (%AppData%\manHours\login.json).
// 공용 로그인 라이브러리(Mmsoft.Login)는 저장 정책을 갖지 않으므로, 저장/복원은 앱이 담당한다.
sealed class LoginSettings
{
    public string ApiBase        { get; set; } = "https://www.mmsoft.co.kr/api";
    public string ApiKey         { get; set; } = "";
    public bool   RememberLogin  { get; set; }
    public string SavedLoginId   { get; set; } = "";
    public string SavedLoginPw   { get; set; } = ""; // base64
    public string LoginAccountId { get; set; } = ""; // 세션/중복감지용
    public string LoginProvider  { get; set; } = ""; // 소셜 제공자(google/kakao/naver)
    public string SessionUser    { get; set; } = "";
    public string SessionToken   { get; set; } = "";
    public string GuestTrialStart { get; set; } = ""; // 비회원 최초 사용일(yyyy-MM-dd)
    public string MemberTrialStart{ get; set; } = ""; // 회원(소셜) 무료 이용 시작일(yyyy-MM-dd)

    static readonly string CfgPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "manHours", "login.json");

    public static LoginSettings Load()
    {
        try
        {
            if (File.Exists(CfgPath))
                return JsonSerializer.Deserialize<LoginSettings>(File.ReadAllText(CfgPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CfgPath)!);
            File.WriteAllText(CfgPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

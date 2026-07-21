using System.Reflection;
using Microsoft.Data.Sqlite;

namespace manHours;

static class AppConfig
{
    public static Form? PendingNextForm { get; set; }
    public const string AppName = "맨아워즈";
    public const string WebUrl  = "http://www.mmsoft.co.kr";

    public static string Version =>
        typeof(AppConfig).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.00";

    static readonly string DefaultBaseDir = @"C:\mmsoft\맨아워즈";
    static string? _baseDir;

    static string CfgFile =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "noim.cfg");

    public static string BaseDir
    {
        get { _baseDir ??= LoadBaseDir(); return _baseDir; }
    }

    public static string DataDir    => Path.Combine(BaseDir, "Data");
    public static string BaseDbPath => Path.Combine(DataDir, "noim_base.db");
    public static string ImageDir   => Path.Combine(BaseDir, "image");

    /// <summary>무료 이용기간 만료 여부(로그인 시 결정). true 면 근무시간 입력·주간 근무표를 잠근다.</summary>
    public static bool TrialExpired;

    // 샘플 템플릿 파일 경로 (exe 옆 templates\ 우선, 없으면 개발 경로 폴백)
    static string TemplateDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");

    public static string SamplePath(string name)
    {
        var local = Path.Combine(TemplateDir, $"샘플({name}).xlsx");
        if (File.Exists(local)) return local;
        return $@"C:\_mm_project\noim\샘플({name}).xlsx";
    }

    public static string MonthlyDbPath(int year, int month, string project)
    {
        var safe = project.Trim().Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
        var suffix = safe.Length > 0 ? $"_{safe}" : "";
        return Path.Combine(DataDir, $"noim{year:D4}{month:D2}{suffix}.db");
    }

    public static string EquipMonthlyDbPath(int year, int month, string project)
    {
        var safe = project.Trim().Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
        var suffix = safe.Length > 0 ? $"_{safe}" : "";
        return Path.Combine(DataDir, $"equip{year:D4}{month:D2}{suffix}.db");
    }

    // ── 전체장비목록 컬럼 ────────────────────────────────
    public static readonly string[] EquipmentCols =
    [
        "자재장비업체명", "사업자번호", "대표자명", "연락처", "주민등록번호",
        "자재장비구분", "자재장비코드", "건설기계구분", "건설기계코드",
        "임차구매품목", "청구금액", "은행명", "은행코드", "계좌번호",
        "타인명의계좌사유", "지급유형",
    ];

    // ── 장비 지급대장 컬럼 ───────────────────────────────
    static readonly string[] _EquipInfo = [
        "선택", "순번", "사업장명", "사업자등록번호", "근무구분", "건설기계명",
        "공급가", "거주지역", "장비운전자", "주민등록번호", "연락처", "건설기계번호", "세금계산서총액", "보수단가",
    ];
    static readonly string[] _EquipDate = Enumerable.Range(1, 31).Select(i => $"{i}일").ToArray();
    static readonly string[] _EquipSum  = [
        "작업일수", "보수총액", "고용보험률", "산재보험률", "고용주", "특고자",
    ];
    public static string[] EquipPayCols => [.. _EquipInfo, .. _EquipDate, .. _EquipSum];

    // 장비 지급대장 표시 레이아웃 상수
    public const int EQ_INFO_DB   = 0;   // INFO 시작 DB 인덱스
    public const int EQ_INFO_CNT  = 14;  // INFO 컬럼 수
    public const int EQ_DATE_DB   = 14;  // DATE 시작 DB 인덱스
    public const int EQ_SUM_DB    = 45;  // SUM 시작 DB 인덱스 (14+31)

    // ── 코드 테이블 컬럼 ─────────────────────────────────
    public static readonly string[] CodeEquipTypeCols = ["코드", "구분명"];
    public static readonly string[] CodeMachineCols   = ["코드", "건설기계명"];
    public static readonly string[] CodeBankCols      = ["코드", "은행명"];
    public static readonly string[] CodeWorkTypeCols  = ["코드", "근무구분"];
    public static readonly string[] CodeCertCols      = ["코드", "자격증"];

    public static readonly string[][] DefaultWorkTypeCodes =
    [
        ["001", "오전"], ["002", "오후"], ["003", "종일"],
        ["004", "점심3시간"], ["005", "저녁4시간"],
    ];

    public static readonly string[][] DefaultCertCodes =
    [
        ["001", "없음"], ["002", "한식조리사"], ["003", "양식조리사"],
        ["004", "제과제빵"], ["005", "바리스타"], ["006", "위생교육"], ["007", "지게차"],
    ];

    // ── 초기 코드 데이터 ─────────────────────────────────
    public static readonly string[][] DefaultMachineCodes =
    [
        ["001","불도저"],["002","스크레이퍼"],["003","로더"],["004","모터그레이더"],
        ["005","타워크레인"],["006","콘크리트펌프"],["007","콘크리트믹서트럭"],
        ["008","콘크리트뱃칭플랜트"],["009","공기압축기"],["010","천공기"],
        ["011","지게차"],["012","아스팔트피니셔"],["013","아스팔트살포기"],
        ["014","콘크리트피니셔"],["015","콘크리트살포기"],["016","골재살포기"],
        ["017","쇄석기"],["018","노상안정기"],["019","준설선"],["020","특수건설기계"],
        ["021","굴착기"],["022","덤프트럭"],["023","기중기"],["024","롤러"],
        ["025","항타및항발기"],["026","자갈채취기"],["027","아스팔트믹싱플랜트"],["028","도로보수트럭"],
    ];

    public static readonly string[][] DefaultBankCodes =
    [
        ["001","한국은행"],["002","산업은행"],["003","기업은행"],["004","국민은행"],
        ["007","수협"],["011","NH농협"],["012","농협중앙"],["020","우리은행"],
        ["023","SC제일은행"],["027","한국씨티"],["031","대구은행"],["032","부산은행"],
        ["034","광주은행"],["035","제주은행"],["037","전북은행"],["039","경남은행"],
        ["045","새마을금고"],["048","신협"],["050","저축은행"],["064","산림조합"],
        ["071","우체국"],
        ["081","KEB하나은행"],["088","신한은행"],["089","케이뱅크"],["090","카카오뱅크"],
        ["092","토스뱅크"],
    ];

    public static readonly string[][] DefaultEquipTypeCodes =
    [
        ["001","장비"],["002","자재"],
    ];

    // ── 근로자 컬럼 (noim 호환) ──────────────────────────
    public static readonly string[] WorkerCols =
    [
        "이름", "생년월일6자리", "거주지역", "은행", "계좌번호",
        "예금주", "전화번호", "근무구분", "자격증", "시급", "총 일급여", "비고", "상태",
    ];

    // 근무구분 선택 목록
    public static readonly string[] WorkTypes =
        ["점심3시간", "저녁4시간", "종일", "오전", "오후"];

    // 근로자 상태
    public static readonly string[] WorkStatuses = ["현재근무", "휴직", "퇴사"];

    /// <summary>동명이인 구분 키. 이름 + 전화번호(숫자만).</summary>
    public static string WorkerKey(string? name, string? phone) =>
        $"{(name ?? "").Trim()}|{new string((phone ?? "").Where(char.IsDigit).ToArray())}";

    // ── 주간 근무표 ──────────────────────────────────────
    public static readonly string[] WeeklyCols =
        ["사업명", "이름", "전화번호", "월", "화", "수", "목", "금", "토", "일"];

    // 요일칸에 빠르게 넣는 시간 프리셋 (라벨, 저장형 시간)
    public static readonly (string Label, string Time)[] WorkTimePresets =
    [
        ("종일 09-18",  "09:00-18:00"),
        ("오전 09-13",  "09:00-13:00"),
        ("오후 14-22",  "14:00-22:00"),
        ("저녁 18-22",  "18:00-22:00"),
    ];

    public static readonly string[] BusiSettingCols =
    [
        "사업명", "상호", "대표자", "거주지역", "전화번호", "현장대리인",
        "소득세", "주민세", "국민연금", "건강보험", "고용보험", "장기요양",
        "사업장관리번호", "명칭", "사업장등록번호", "소재지", "유선전화번호",
        "FAX번호", "매장유형", "근무장소", "상시근로자수", "임금지급일",
    ];

    // ── 지급대장 컬럼 (매장 알바: 시급 × 근무시간) ──────────
    // 날짜칸에는 출퇴근 시각을 넣는다 (예: "18:00-22:00").
    // 합계는 22개(11쌍) — 그리드가 쌍 단위로 2줄에 나눠 그린다.
    static readonly string[] _PayInfo = [
        "선택", "금액자동", "휴게자동", "순번", "이름", "생년월일6자리", "근무시작일", "전화번호", "거주지역"
    ];
    static readonly string[] _PayDate = Enumerable.Range(1, 31).Select(i => $"{i}일").ToArray();
    static readonly string[] _PaySum  = [
        "총근무시간", "근무일수", "주휴시간", "휴일시간", "연장시간", "야간시간",
        "시급", "최저임금미달", "기본급", "주휴수당", "연장수당", "야간수당",
        "휴일수당", "임금총액",
        "국민연금", "건강보험", "고용보험", "요양보험",
        "소득세", "주민세", "공제합계", "차감지급액",
    ];
    public static string[] PayCols => [.. _PayInfo, .. _PayDate, .. _PaySum];

    // 지급대장 표시 레이아웃 상수
    public const int D_INFO_DISP  = 7;   // 표시 INFO 컬럼 수
    public const int N_DATE_SLOTS = 16;  // 날짜 슬롯 수
    public const int D_DATE_DISP  = 7;   // 날짜 슬롯 시작 표시 컬럼
    public const int N_SUM_PAIRS  = 11;  // 합계 표시 컬럼 수 (쌍)
    public const int N_DISP       = 34;  // 총 표시 컬럼 (7+16+11)
    public const int D_DATE_DB    = 9;   // PAY_DATE 시작 DB 인덱스
    public const int D_SUM_DB     = 40;  // PAY_SUM 시작 DB 인덱스

    // ── 장비 샘플 데이터 ─────────────────────────────────
    static readonly string[][] DummyEquipment =
    [
        ["(주)건설기계","123-45-67890","김건설","010-1111-0001","800101-1000001","장비","0001","굴착기","021","20톤 굴착기","500000","국민은행","004","123-456-001","","일반지급"],
        ["(주)건설기계","123-45-67890","김건설","010-1111-0001","800101-1000001","장비","0002","덤프트럭","022","15톤 덤프","400000","국민은행","004","123-456-002","","일반지급"],
        ["한국중장비","234-56-78901","이중기","010-2222-0002","850215-1000002","장비","0003","타워크레인","005","50톤 타워","800000","신한은행","088","234-567-001","","선지급"],
        ["한국중장비","234-56-78901","이중기","010-2222-0002","850215-1000002","장비","0004","기중기","023","25톤 기중기","600000","신한은행","088","234-567-002","","선지급"],
        ["대성장비임대","345-67-89012","박임대","010-3333-0003","900320-1000003","장비","0005","롤러","024","10톤 롤러","350000","우리은행","020","345-678-001","","일반지급"],
        ["대성장비임대","345-67-89012","박임대","010-3333-0003","900320-1000003","장비","0006","로더","003","2.5㎥ 로더","420000","우리은행","020","345-678-002","","일반지급"],
        ["현대건설자재","456-78-90123","최자재","010-4444-0004","780505-1000004","자재","0007","","","철근 D13","1200000","하나은행","081","456-789-001","","기관직접지급"],
        ["현대건설자재","456-78-90123","최자재","010-4444-0004","780505-1000004","자재","0008","","","시멘트 포대","850000","하나은행","081","456-789-002","","기관직접지급"],
        ["삼성레미콘","567-89-01234","정레미","010-5555-0005","830825-1000005","장비","0009","콘크리트믹서트럭","007","6㎥ 믹서","380000","기업은행","003","567-890-001","","일반지급"],
        ["삼성레미콘","567-89-01234","정레미","010-5555-0005","830825-1000005","장비","0010","콘크리트펌프","006","45m 펌프","550000","기업은행","003","567-890-002","","일반지급"],
    ];

    // ── 샘플 데이터 ──────────────────────────────────────
    // 거주지역은 개인정보 보호를 위해 번지수 없이 시/구 단위까지만
    static readonly string[][] DummyWorkers =
    [
        ["홍길동", "800101-1******", "서울시 종로구",         "국민은행", "123-456-789001", "홍길동", "010-1234-0001", "종일",      "한식조리사", "10000", "80000", "",         "현재근무"],
        ["김철수", "850215-1******", "서울시 중구",           "신한은행", "123-456-789002", "김철수", "010-1234-0002", "오전",      "바리스타",   "10500", "42000", "",         "현재근무"],
        ["이영희", "900320-2******", "경기도 수원시 팔달구",  "우리은행", "123-456-789003", "이영희", "010-1234-0003", "오후",      "위생교육",   "10500", "42000", "",         "현재근무"],
        ["박민준", "780505-1******", "경기도 고양시 일산동구","하나은행", "123-456-789004", "박민준", "010-1234-0004", "점심3시간", "지게차",     "11000", "33000", "",         "휴직"],
        ["최수연", "920710-2******", "인천시 남동구",         "기업은행", "123-456-789005", "최수연", "010-1234-0005", "저녁4시간", "제과제빵",   "12000", "48000", "주말 가능", "현재근무"],
        ["정대한", "830825-1******", "부산시 해운대구",       "농협은행", "123-456-789006", "정대한", "010-1234-0006", "종일",      "한식조리사", "9500",  "76000", "",         "퇴사"],
        ["강지훈", "870912-1******", "대구시 수성구",         "국민은행", "123-456-789007", "강지훈", "010-1234-0007", "저녁4시간", "없음",       "11500", "46000", "",         "현재근무"],
        ["윤미래", "950418-2******", "광주시 서구",           "신한은행", "123-456-789008", "윤미래", "010-1234-0008", "오전",      "바리스타",   "10000", "40000", "",         "현재근무"],
        ["조현우", "761130-1******", "대전시 유성구",         "우리은행", "123-456-789009", "조현우", "010-1234-0009", "종일",      "양식조리사", "9000",  "72000", "야간 가능", "현재근무"],
        ["임나래", "010203-3******", "경기도 성남시 분당구",  "하나은행", "123-456-789010", "임나래", "010-1234-0010", "점심3시간", "위생교육",   "10000", "30000", "",         "현재근무"],
    ];

    // ── 연도별 법정기준 ──────────────────────────────────
    // 법정 수치는 매년 바뀌므로 하드코딩하지 않고 연도별로 보관한다.
    // 조회 시 해당 연도가 없으면 그보다 이전 중 가장 최근 연도를 승계한다.
    public static readonly string[] LegalStdCols =
    [
        "연도", "최저임금", "주휴기준시간", "보험월최소시간", "보험월최소일수",
        "가산수당최소인원", "일용공제액", "소득세율", "소액부징수",
        "국민연금률", "건강보험률", "장기요양률", "고용보험률", "주민세율",
        "휴게4시간분", "휴게8시간분",
    ];

    public sealed class LegalStandard
    {
        public int    Year;
        public double MinWage;                 // 최저임금(시급). 0 = 미입력
        public double JuhyuHours      = 15;    // 주휴 발생 기준(주 근로시간)
        public double InsMinHours     = 60;    // 4대보험 가입기준(월 근로시간)
        public double InsMinDays      = 8;     // 4대보험 가입기준(월 근로일수)
        public int    ExtraPayMinStaff = 5;    // 연장·야간·휴일 가산수당 의무 최소 인원
        public double DailyDeduct     = 150_000; // 일용근로소득 일 공제액
        public double TaxRate         = 2.7;   // 소득세율(%)
        public double MinTaxCut       = 1_000; // 소액부징수 기준(원)
        public double Kukmin          = 4.5;   // 국민연금(보수 대비 %)
        public double Health          = 3.545; // 건강보험(보수 대비 %)
        public double Care            = 12.95; // 장기요양(★건강보험료 대비 %)
        public double Employ          = 0.9;   // 고용보험(보수 대비 %)
        public double LocalTax        = 10.0;  // 지방소득세(소득세 대비 %)
        public double Break4Min       = 30;    // 4시간 이상 근무 시 휴게(분)
        public double Break8Min       = 60;    // 8시간 이상 근무 시 휴게(분)
    }

    /// <summary>연도별 법정기준 조회. 해당 연도가 없으면 그 이전 중 최신 연도를 승계.</summary>
    public static LegalStandard GetLegalStandard(int year)
    {
        var std = new LegalStandard { Year = year };
        try
        {
            using var db = new Database(BaseDbPath);
            db.EnsureTable("법정기준", LegalStdCols);
            var (cols, rows) = db.SelectStrings("법정기준");
            if (rows.Length == 0) return std;

            int yi = Array.IndexOf(cols, "연도");
            if (yi < 0) return std;

            // year 이하 중 가장 큰 연도, 없으면 가장 작은 연도
            string[]? best = null;
            int bestY = int.MinValue, minY = int.MaxValue;
            string[]? minRow = null;
            foreach (var r in rows)
            {
                if (!int.TryParse(r[yi], out var y)) continue;
                if (y <= year && y > bestY) { bestY = y; best = r; }
                if (y < minY) { minY = y; minRow = r; }
            }
            var row = best ?? minRow;
            if (row == null) return std;

            double D(string col, double dflt)
            {
                int i = Array.IndexOf(cols, col);
                return i >= 0 && i < row.Length && double.TryParse(row[i], out var v) && v > 0 ? v : dflt;
            }
            std.Year             = best != null ? bestY : minY;
            std.MinWage          = D("최저임금", 0);
            std.JuhyuHours       = D("주휴기준시간",   std.JuhyuHours);
            std.InsMinHours      = D("보험월최소시간", std.InsMinHours);
            std.InsMinDays       = D("보험월최소일수", std.InsMinDays);
            std.ExtraPayMinStaff = (int)D("가산수당최소인원", std.ExtraPayMinStaff);
            std.DailyDeduct      = D("일용공제액",  std.DailyDeduct);
            std.TaxRate          = D("소득세율",    std.TaxRate);
            std.MinTaxCut        = D("소액부징수",  std.MinTaxCut);
            std.Kukmin           = D("국민연금률",  std.Kukmin);
            std.Health           = D("건강보험률",  std.Health);
            std.Care             = D("장기요양률",  std.Care);
            std.Employ           = D("고용보험률",  std.Employ);
            std.LocalTax         = D("주민세율",    std.LocalTax);
            std.Break4Min        = D("휴게4시간분", std.Break4Min);
            std.Break8Min        = D("휴게8시간분", std.Break8Min);
        }
        catch { }
        return std;
    }

    static string LoadBaseDir()
    {
        if (!File.Exists(CfgFile)) return DefaultBaseDir;
        foreach (var line in File.ReadAllLines(CfgFile))
            if (line.StartsWith("base_dir="))
                return line["base_dir=".Length..].Trim();
        return DefaultBaseDir;
    }

    public static void SaveBaseDir(string newDir)
    {
        _baseDir = newDir;
        File.WriteAllLines(CfgFile, [$"base_dir={newDir}"]);
    }

    public static void EnsureBaseDirs()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ImageDir);
        using var db = new Database(BaseDbPath);

        db.EnsureTable("설정",       ["키", "값"]);
        db.EnsureTable("전체근로자",  WorkerCols);
        db.EnsureTable("사업명",      ["이름"]);
        db.EnsureTable("사업설정",    BusiSettingCols);
        db.EnsureTable("전체장비목록", EquipmentCols);
        db.EnsureTable("코드_건설기계", CodeMachineCols);
        db.EnsureTable("코드_은행",    CodeBankCols);
        db.EnsureTable("코드_자재장비", CodeEquipTypeCols);
        db.EnsureTable("코드_근무구분", CodeWorkTypeCols);
        db.EnsureTable("코드_자격증",   CodeCertCols);
        db.EnsureTable("법정기준",     LegalStdCols);
        db.EnsureTable("주간근무",     WeeklyCols);

        // 근무구분·자격증 코드는 나중에 추가된 표라 '코드초기화완료' 와 별개로 채운다.
        var (_, wtRows) = db.Select("코드_근무구분");
        if (wtRows.Length == 0)
            foreach (var row in DefaultWorkTypeCodes)
                db.InsertRow("코드_근무구분", CodeWorkTypeCols, row);
        var (_, ctRows) = db.Select("코드_자격증");
        if (ctRows.Length == 0)
            foreach (var row in DefaultCertCodes)
                db.InsertRow("코드_자격증", CodeCertCols, row);

        // 연도별 법정기준: 올해 행이 없으면 기본값으로 한 줄 만들어 둔다.
        // ※ 최저임금은 매년 바뀌고 임의로 채우면 위험하므로 비워 두고 사용자가 입력한다.
        int curYear = DateTime.Now.Year;
        var (_, stdRows) = db.SelectStrings("법정기준", $"\"연도\"='{curYear}'");
        if (stdRows.Length == 0)
        {
            var d = new LegalStandard();
            db.InsertRow("법정기준", LegalStdCols,
            [
                curYear.ToString(),
                "",                                   // 최저임금 (직접 입력)
                d.JuhyuHours.ToString(),
                d.InsMinHours.ToString(),
                d.InsMinDays.ToString(),
                d.ExtraPayMinStaff.ToString(),
                d.DailyDeduct.ToString(),
                d.TaxRate.ToString(),
                d.MinTaxCut.ToString(),
                d.Kukmin.ToString(),
                d.Health.ToString(),
                d.Care.ToString(),
                d.Employ.ToString(),
                d.LocalTax.ToString(),
                d.Break4Min.ToString(),
                d.Break8Min.ToString(),
            ]);
        }

        var (_, projRows) = db.Select("사업명");
        if (projRows.Length == 0)
        {
            db.Execute("INSERT INTO \"사업명\" (\"이름\") VALUES (@p0)", "기본 매장1");
            db.Execute("INSERT INTO \"사업명\" (\"이름\") VALUES (@p0)", "기본 매장2");
        }

        // 코드 테이블 초기 데이터
        var codeInit = db.Scalar(
            "SELECT COUNT(*) FROM \"설정\" WHERE \"키\"='코드초기화완료'") is long c0 && c0 > 0;
        if (!codeInit)
        {
            var (_, mc) = db.Select("코드_건설기계");
            if (mc.Length == 0)
                foreach (var row in DefaultMachineCodes)
                    db.InsertRow("코드_건설기계", CodeMachineCols, row);
            var (_, bc) = db.Select("코드_은행");
            if (bc.Length == 0)
                foreach (var row in DefaultBankCodes)
                    db.InsertRow("코드_은행", CodeBankCols, row);
            var (_, ec) = db.Select("코드_자재장비");
            if (ec.Length == 0)
                foreach (var row in DefaultEquipTypeCodes)
                    db.InsertRow("코드_자재장비", CodeEquipTypeCols, row);
            db.Execute("INSERT INTO \"설정\" (\"키\",\"값\") VALUES (@p0,@p1)", "코드초기화완료", "1");
        }
        else
        {
            // 이미 초기화된 DB에 021~028 코드 누락분 보충
            var mcCnt = db.Scalar("SELECT COUNT(*) FROM \"코드_건설기계\"");
            int existing = mcCnt is long lc ? (int)lc : 0;
            if (existing < DefaultMachineCodes.Length)
            {
                var (_, existRows) = db.SelectStrings("코드_건설기계");
                var existCodes = existRows.Select(r => r.Length > 0 ? r[0] : "").ToHashSet();
                foreach (var row in DefaultMachineCodes)
                    if (!existCodes.Contains(row[0]))
                        db.InsertRow("코드_건설기계", CodeMachineCols, row);
            }
        }

        var alreadyInit = db.Scalar(
            "SELECT COUNT(*) FROM \"설정\" WHERE \"키\"='더미초기화완료'") is long n && n > 0;
        if (!alreadyInit)
        {
            var (_, wrows) = db.Select("전체근로자");
            if (wrows.Length == 0)
            {
                foreach (var w in DummyWorkers)
                    db.InsertRow("전체근로자", WorkerCols, w);
                db.Execute("INSERT INTO \"설정\" (\"키\",\"값\") VALUES (@p0,@p1)",
                    "더미초기화완료", "1");
            }
        }

        var equipDummyInit = db.Scalar(
            "SELECT COUNT(*) FROM \"설정\" WHERE \"키\"='장비더미초기화완료'") is long ne && ne > 0;
        if (!equipDummyInit)
        {
            var (_, erows) = db.Select("전체장비목록");
            if (erows.Length == 0)
            {
                foreach (var e in DummyEquipment)
                    db.InsertRow("전체장비목록", EquipmentCols, e);
                db.Execute("INSERT INTO \"설정\" (\"키\",\"값\") VALUES (@p0,@p1)",
                    "장비더미초기화완료", "1");
            }
        }
    }

    // ── 공휴일 (2020-2030) ──────────────────────────────
    static readonly HashSet<(int, int)> SolarHolidays =
        [(1, 1), (3, 1), (5, 5), (6, 6), (8, 15), (10, 3), (10, 9), (12, 25)];

    static readonly Dictionary<int, HashSet<(int, int)>> LunarHolidays = new()
    {
        [2020] = [(1,24),(1,25),(1,26),(1,27),(4,30),(9,30),(10,1),(10,2)],
        [2021] = [(2,11),(2,12),(2,13),(2,14),(2,15),(5,19),(9,20),(9,21),(9,22)],
        [2022] = [(1,31),(2,1),(2,2),(2,3),(5,8),(9,9),(9,10),(9,11),(9,12)],
        [2023] = [(1,21),(1,22),(1,23),(1,24),(5,27),(5,29),(9,28),(9,29),(9,30),(10,2)],
        [2024] = [(2,9),(2,10),(2,11),(2,12),(5,15),(9,16),(9,17),(9,18),(9,19)],
        [2025] = [(1,28),(1,29),(1,30),(1,31),(5,5),(5,6),(10,5),(10,6),(10,7),(10,8)],
        [2026] = [(2,17),(2,18),(2,19),(2,20),(5,24),(9,24),(9,25),(9,26),(9,27)],
        [2027] = [(2,8),(2,9),(2,10),(2,11),(5,13),(10,14),(10,15),(10,16)],
        [2028] = [(1,27),(1,28),(1,29),(1,30),(5,2),(10,2),(10,3),(10,4)],
        [2029] = [(2,12),(2,13),(2,14),(2,15),(2,16),(5,21),(9,22),(9,23),(9,24),(9,25)],
        [2030] = [(2,3),(2,4),(2,5),(2,6),(5,11),(9,12),(9,13),(9,14)],
    };

    public static HashSet<int> GetHolidayDays(int year, int month)
    {
        var result = new HashSet<int>();
        int lastDay = DateTime.DaysInMonth(year, month);
        for (int d = 1; d <= lastDay; d++)
        {
            if (SolarHolidays.Contains((month, d))) { result.Add(d); continue; }
            if (LunarHolidays.TryGetValue(year, out var lunar) && lunar.Contains((month, d)))
                result.Add(d);
        }
        return result;
    }

    public static string GetDayKind(int year, int month, int day, HashSet<int> holidays, HashSet<int> customHolidays)
    {
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return "";
        if (holidays.Contains(day) || customHolidays.Contains(day)) return "sun";
        var wd = (int)new DateTime(year, month, day).DayOfWeek;
        if (wd == 0) return "sun";
        if (wd == 6) return "sat";
        return "";
    }
}

using System.Text.Json;
using System.Text;

namespace manHours;

public static class Updater
{
    const string ConfigUrl = "https://www.mmsoft.co.kr/autoupdate_new/config.json";
    const string BaseUrl   = "https://www.mmsoft.co.kr/autoupdate_new";
    const string ProgramId = "manHours";

    public record UpdateInfo(string NewVersion, bool Required, string Notes, string FileName);

    public static async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        var json = await http.GetStringAsync(ConfigUrl);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.GetProperty("programs").TryGetProperty(ProgramId, out var prog))
            return null;
        var remoteVer = prog.GetProperty("version").GetString() ?? "";
        if (CompareVersion(remoteVer, currentVersion) <= 0) return null;
        return new UpdateInfo(
            remoteVer,
            prog.TryGetProperty("required", out var r) && r.GetBoolean(),
            prog.TryGetProperty("notes",    out var n) ? n.GetString() ?? "" : "",
            prog.TryGetProperty("file",     out var f) ? f.GetString() ?? $"{ProgramId}.exe" : $"{ProgramId}.exe"
        );
    }

    public static async Task DownloadAsync(
        string fileName, string destPath,
        IProgress<(long done, long total)>? progress = null,
        CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{ProgramId}/{fileName}";
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new IOException($"서버에 업데이트 파일이 없습니다.\n({url})");
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? 0;
        using var src = await resp.Content.ReadAsStreamAsync(ct);

        byte b0 = 0, b1 = 0;
        using (var dest = File.Create(destPath))
        {
            var buf = new byte[65536];
            long done = 0;
            bool isFirst = true;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                if (isFirst) { b0 = read > 0 ? buf[0] : (byte)0; b1 = read > 1 ? buf[1] : (byte)0; isFirst = false; }
                await dest.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                progress?.Report((done, total));
            }
        }

        if (b0 != 0x4D || b1 != 0x5A)
            throw new InvalidDataException("다운로드된 파일이 올바른 실행파일이 아닙니다.\n(서버 파일을 확인해주세요)");
    }

    public static void ApplyAndRestart(string newExePath, string currentExePath)
    {
        var bat = Path.Combine(Path.GetTempPath(), "_mm_update.bat");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            $"set \"SRC={newExePath}\"\r\n" +
            $"set \"DEST={currentExePath}\"\r\n" +
            "for %%f in (\"%DEST%\") do taskkill /F /IM %%~nxf /T > nul 2>&1\r\n" +
            "if not exist \"%SRC%\" goto :done\r\n" +
            "set RETRY=0\r\n" +
            ":retry\r\n" +
            "move /y \"%SRC%\" \"%DEST%\" > nul 2>&1\r\n" +
            "if not errorlevel 1 goto :moved\r\n" +
            "set /a RETRY+=1\r\n" +
            "if %RETRY% geq 10 goto :done\r\n" +
            "ping -n 2 127.0.0.1 > nul\r\n" +
            "goto :retry\r\n" +
            ":moved\r\n" +
            "del /f \"%DEST%:Zone.Identifier\" > nul 2>&1\r\n" +
            "start \"\" \"%DEST%\"\r\n" +
            ":done\r\n" +
            "del \"%~f0\"\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe", Arguments = $"/c \"{bat}\"",
            CreateNoWindow = true, UseShellExecute = false,
        });
        Application.Exit();
    }

    static int CompareVersion(string a, string b)
    {
        var ta = ParseVer(a); var tb = ParseVer(b);
        for (int i = 0; i < Math.Max(ta.Length, tb.Length); i++)
        {
            int av = i < ta.Length ? ta[i] : 0;
            int bv = i < tb.Length ? tb[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }

    static int[] ParseVer(string v)
        => v.TrimStart('v').Split('.')
            .Select(p => int.TryParse(p, out var n) ? n : 0)
            .ToArray();
}

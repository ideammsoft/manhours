namespace manHours.Forms;

public class UpdateDialog : Form
{
    readonly string           _currentVer;
    readonly Updater.UpdateInfo _info;

    ProgressBar _progress    = null!;
    Label       _lblProgress = null!;
    Button      _btnUpdate   = null!;
    Button      _btnLater    = null!;

    public UpdateDialog(string currentVer, Updater.UpdateInfo info)
    {
        _currentVer = currentVer;
        _info       = info;
        Build();
    }

    void Build()
    {
        Text            = "업데이트";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        Size            = new Size(420, 280);
        MaximizeBox     = false;
        MinimizeBox     = false;
        BackColor       = Color.FromArgb(30, 30, 46);

        int y = 18;

        Controls.Add(new Label
        {
            Text = "새 버전이 출시되었습니다!", AutoSize = true,
            Font = new Font("맑은 고딕", 13f, FontStyle.Bold),
            Location = new Point(20, y),
            ForeColor = Color.FromArgb(137, 180, 250),
            BackColor = Color.Transparent,
        });
        y += 38;

        Controls.Add(new Label
        {
            Text = $"v{_currentVer}  →  v{_info.NewVersion}", AutoSize = true,
            Font = new Font("맑은 고딕", 11f),
            Location = new Point(20, y),
            ForeColor = Color.FromArgb(205, 214, 244),
            BackColor = Color.Transparent,
        });
        y += 30;

        if (_info.Required)
        {
            Controls.Add(new Label
            {
                Text = "[ 필수 업데이트 ]", AutoSize = true,
                Font = new Font("맑은 고딕", 9f, FontStyle.Bold),
                Location = new Point(20, y),
                ForeColor = Color.FromArgb(243, 139, 168),
                BackColor = Color.Transparent,
            });
            y += 26;
        }

        if (!string.IsNullOrEmpty(_info.Notes))
        {
            Controls.Add(new Label
            {
                Text = _info.Notes, Size = new Size(370, 36),
                Font = new Font("맑은 고딕", 9f),
                Location = new Point(20, y),
                ForeColor = Color.FromArgb(166, 173, 200),
                BackColor = Color.Transparent,
            });
            y += 42;
        }

        _progress = new ProgressBar
        {
            Location = new Point(20, y), Size = new Size(370, 16),
            Visible = false, Style = ProgressBarStyle.Continuous,
        };
        _lblProgress = new Label
        {
            Text = "", AutoSize = true,
            Location = new Point(20, y + 18),
            Font = new Font("맑은 고딕", 8f),
            ForeColor = Color.FromArgb(147, 153, 178),
            BackColor = Color.Transparent,
            Visible = false,
        };
        Controls.Add(_progress);
        Controls.Add(_lblProgress);
        y += 42;

        _btnLater = new Button
        {
            Text = "나중에", Location = new Point(200, y + 6), Size = new Size(85, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 65),
            ForeColor = Color.FromArgb(166, 173, 200),
            Font = new Font("맑은 고딕", 9f),
        };
        _btnUpdate = new Button
        {
            Text = "지금 업데이트", Location = new Point(290, y + 6), Size = new Size(112, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 100, 200),
            ForeColor = Color.White,
            Font = new Font("맑은 고딕", 9f, FontStyle.Bold),
        };
        _btnLater.Click  += BtnLater_Click;
        _btnUpdate.Click += BtnUpdate_Click;
        Controls.Add(_btnLater);
        Controls.Add(_btnUpdate);
    }

    void BtnLater_Click(object? s, EventArgs e)
    {
        if (_info.Required)
        {
            var r = MessageBox.Show(
                "필수 업데이트입니다. 나중에 하려면 프로그램이 종료됩니다.\n계속하시겠습니까?",
                "필수 업데이트", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes) Application.Exit();
        }
        else { DialogResult = DialogResult.Cancel; Close(); }
    }

    async void BtnUpdate_Click(object? s, EventArgs e)
    {
        _btnUpdate.Enabled = false;
        _btnLater.Enabled  = false;
        _progress.Visible     = true;
        _lblProgress.Visible  = true;

        var tmpPath = Path.Combine(Path.GetTempPath(), $"manHours_new_{Guid.NewGuid():N}.exe");
        try
        {
            var prog = new Progress<(long done, long total)>(p =>
            {
                if (p.total > 0)
                {
                    _progress.Value   = (int)(p.done * 100 / p.total);
                    _lblProgress.Text = $"{p.done / 1024.0 / 1024:F1} MB / {p.total / 1024.0 / 1024:F1} MB";
                }
            });
            await Updater.DownloadAsync(_info.FileName, tmpPath, prog);

            var currentExe = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? "";
            _lblProgress.Text = "다운로드 완료";

            // 백그라운드 사전 추출 (self-contained 첫 실행 속도 개선)
            var preExtractDir = Path.Combine(Path.GetTempPath(), "mm_preextract");
            var preExtractExe = Path.Combine(preExtractDir, Path.GetFileName(currentExe));
            var preExtractTask = Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(preExtractDir);
                    File.Copy(tmpPath, preExtractExe, overwrite: true);
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = preExtractExe, Arguments = "--extract-only",
                        CreateNoWindow = true, UseShellExecute = false,
                    });
                    p?.WaitForExit(20000);
                    if (p?.HasExited == false) p?.Kill();
                }
                catch { }
                finally { try { File.Delete(preExtractExe); } catch { } }
            });

            MessageBox.Show(
                $"v{_info.NewVersion} 업데이트가 준비되었습니다.\n확인을 클릭하면 프로그램이 재시작됩니다.",
                "업데이트 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (!preExtractTask.IsCompleted)
            {
                _lblProgress.Text = "재시작 준비 중...";
                _progress.Style = ProgressBarStyle.Marquee;
                _progress.MarqueeAnimationSpeed = 30;
                await preExtractTask;
            }
            Updater.ApplyAndRestart(tmpPath, currentExe);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            MessageBox.Show(ex.Message, "업데이트 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnUpdate.Enabled = true;
            _btnLater.Enabled  = true;
            _progress.Visible     = false;
            _lblProgress.Visible  = false;
        }
    }
}

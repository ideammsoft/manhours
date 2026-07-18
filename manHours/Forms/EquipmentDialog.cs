namespace manHours.Forms;

public class EquipmentDialog : Form
{
    readonly TextBox[]  _fields;
    readonly ComboBox   _cmbEquipType = new();
    readonly ComboBox   _cmbMachine   = new();
    readonly ComboBox   _cmbBank      = new();
    readonly ComboBox   _cmbPayType   = new();

    // 콤보박스가 대응하는 컬럼 인덱스
    int _iEquipType, _iMachineCode, _iMachineName, _iBankName, _iBankCode, _iPayType;

    public string[] Values => Enumerable.Range(0, AppConfig.EquipmentCols.Length)
        .Select(i => _fields[i].Text.Trim()).ToArray();

    public EquipmentDialog(string[]? existingValues = null)
    {
        Text            = existingValues == null ? "장비 추가" : "장비 정보 수정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        Size            = new Size(560, 560);
        MaximizeBox     = false; MinimizeBox = false;
        BackColor       = ThemeManager.BgMain;

        // 인덱스 계산
        var cols = AppConfig.EquipmentCols;
        _iEquipType   = Array.IndexOf(cols, "자재장비구분");
        _iMachineName = Array.IndexOf(cols, "건설기계구분");
        _iMachineCode = Array.IndexOf(cols, "건설기계코드");
        _iBankName    = Array.IndexOf(cols, "은행명");
        _iBankCode    = Array.IndexOf(cols, "은행코드");
        _iPayType     = Array.IndexOf(cols, "지급유형");

        _fields = new TextBox[cols.Length];
        for (int i = 0; i < cols.Length; i++)
            _fields[i] = new TextBox();

        Build(existingValues);
    }

    void Build(string[]? vals)
    {
        var cols = AppConfig.EquipmentCols;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            BackColor = Color.Transparent, Padding = new Padding(14, 10, 14, 6),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, BackColor = Color.Transparent,
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // 좌측/우측 2열 레이아웃으로 배치 (슬라이드6 "좌측, 우측 똑같이")
        var half = cols.Length / 2;
        int row = 0;
        for (int i = 0; i < cols.Length; i += 2)
        {
            tbl.Controls.Add(MakeLabel(cols[i]), 0, row);
            tbl.Controls.Add(MakeControl(i, vals), 1, row);
            if (i + 1 < cols.Length)
            {
                tbl.Controls.Add(MakeLabel(cols[i + 1]), 2, row);
                tbl.Controls.Add(MakeControl(i + 1, vals), 3, row);
            }
            row++;
        }
        tbl.RowCount = row;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(tbl);
        scroll.Resize += (_, _) => tbl.Width = Math.Max(400, scroll.ClientSize.Width - 4);
        root.Controls.Add(scroll, 0, 0);

        // 하단 버튼
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0),
        };
        var btnOk = new Button
        {
            Text = "저장", Width = 90, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 100, 200), ForeColor = Color.White,
            Font = ThemeManager.F(10f, FontStyle.Bold),
        };
        btnOk.Click += (_, _) =>
        {
            // 선택된 콤보박스 값을 필드에 반영
            SyncCombos();
            DialogResult = DialogResult.OK;
        };
        var btnCancel = new Button
        {
            Text = "취소", Width = 80, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.BtnSide, ForeColor = ThemeManager.BtnText,
            Font = ThemeManager.F(10f),
        };
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        btnRow.Controls.Add(btnOk);
        btnRow.Controls.Add(btnCancel);
        root.Controls.Add(btnRow, 0, 1);

        Controls.Add(root);

        // 콤보 초기값 설정
        LoadCombos(vals);
    }

    Control MakeControl(int idx, string[]? vals)
    {
        string v = vals != null && idx < vals.Length ? vals[idx] : "";
        var cols = AppConfig.EquipmentCols;

        if (idx == _iEquipType)
        {
            SetupCombo(_cmbEquipType, ["장비", "자재"], v);
            _fields[idx].Text = v;
            return _cmbEquipType;
        }
        if (idx == _iMachineName)
        {
            _fields[idx].Text = v;
            SetupCombo(_cmbMachine, [], v); // populated in LoadCombos
            return _cmbMachine;
        }
        if (idx == _iBankName)
        {
            _fields[idx].Text = v;
            SetupCombo(_cmbBank, [], v); // populated in LoadCombos
            return _cmbBank;
        }
        if (idx == _iPayType)
        {
            SetupCombo(_cmbPayType, ["일반지급", "선지급", "기관직접지급"], v);
            _fields[idx].Text = v;
            return _cmbPayType;
        }

        var txt = new TextBox
        {
            Text = v, Dock = DockStyle.Fill, Height = 26,
            BackColor = ThemeManager.BgInput, ForeColor = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle, Font = ThemeManager.F(9.5f),
            Margin = new Padding(0, 2, 0, 2),
        };
        _fields[idx] = txt;
        return txt;
    }

    void SetupCombo(ComboBox cmb, string[] items, string val)
    {
        cmb.Dock = DockStyle.Fill; cmb.Height = 26;
        cmb.BackColor = ThemeManager.BgInput; cmb.ForeColor = ThemeManager.TextMain;
        cmb.Font = ThemeManager.F(9.5f); cmb.DropDownStyle = ComboBoxStyle.DropDown;
        cmb.Margin = new Padding(0, 2, 0, 2);
        cmb.Items.Clear();
        cmb.Items.Add("");
        foreach (var it in items) cmb.Items.Add(it);
        cmb.Text = val;
    }

    void LoadCombos(string[]? vals)
    {
        using var db = new Database(AppConfig.BaseDbPath);

        // 건설기계명 콤보
        db.EnsureTable("코드_건설기계", AppConfig.CodeMachineCols);
        var (_, mc) = db.SelectStrings("코드_건설기계");
        _cmbMachine.Items.Clear(); _cmbMachine.Items.Add("");
        foreach (var row in mc)
            if (row.Length > 1 && !string.IsNullOrEmpty(row[1]))
                _cmbMachine.Items.Add(row[1]);
        _cmbMachine.Text = vals != null && _iMachineName < vals.Length ? vals[_iMachineName] : "";

        _cmbMachine.SelectedIndexChanged += (_, _) =>
        {
            string name = _cmbMachine.Text;
            // 코드 자동 채우기
            foreach (var row in mc)
                if (row.Length > 1 && row[1] == name)
                {
                    _fields[_iMachineCode].Text = row[0];
                    break;
                }
        };

        // 은행명 콤보
        db.EnsureTable("코드_은행", AppConfig.CodeBankCols);
        var (_, bc) = db.SelectStrings("코드_은행");
        _cmbBank.Items.Clear(); _cmbBank.Items.Add("");
        foreach (var row in bc)
            if (row.Length > 1 && !string.IsNullOrEmpty(row[1]))
                _cmbBank.Items.Add(row[1]);
        _cmbBank.Text = vals != null && _iBankName < vals.Length ? vals[_iBankName] : "";

        _cmbBank.SelectedIndexChanged += (_, _) =>
        {
            string name = _cmbBank.Text;
            foreach (var row in bc)
                if (row.Length > 1 && row[1] == name)
                {
                    _fields[_iBankCode].Text = row[0];
                    break;
                }
        };

        // 자재장비코드 자동 채우기 (신규일 때)
        if (vals == null)
        {
            db.EnsureTable("전체장비목록", AppConfig.EquipmentCols);
            var cnt = db.Scalar("SELECT COUNT(*) FROM \"전체장비목록\"");
            int nextCode = (cnt is long l ? (int)l : 0) + 1;
            int iEqCode = Array.IndexOf(AppConfig.EquipmentCols, "자재장비코드");
            if (iEqCode >= 0) _fields[iEqCode].Text = nextCode.ToString("D4");
        }
    }

    void SyncCombos()
    {
        _fields[_iEquipType].Text   = _cmbEquipType.Text;
        _fields[_iMachineName].Text = _cmbMachine.Text;
        _fields[_iBankName].Text    = _cmbBank.Text;
        _fields[_iPayType].Text     = _cmbPayType.Text;
    }

    static Label MakeLabel(string text) => new()
    {
        Text = text, AutoSize = false, Height = 28, Dock = DockStyle.Fill,
        Font = ThemeManager.F(9.5f), ForeColor = ThemeManager.TextSub,
        BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight,
        Margin = new Padding(0, 2, 6, 2),
    };
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinterRoadLauncher;

public partial class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private TextBox txtServerPath = null!;
    private TextBox txtClientPath = null!;
    private TextBox txtServerIP = null!;
    private NumericUpDown numPort = null!;
    private NumericUpDown numWaitTime = null!;
    private ComboBox cmbMonitor = null!;
    private CheckBox chkDdnsEnabled = null!;
    private TextBox txtDdnsHostname = null!;
    private TextBox txtDdnsPassword = null!;
    private TextBox txtOscHost = null!;
    private NumericUpDown numOscPort = null!;
    private CheckBox chkAutoStart = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form settings
        this.Text = "⚙ 설정";
        this.Size = new Size(550, 620);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 10F);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 15,
            Padding = new Padding(15)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Row 0: Server Path
        mainPanel.Controls.Add(new Label { Text = "서버 스크립트:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        txtServerPath = new TextBox { Dock = DockStyle.Fill };
        mainPanel.Controls.Add(txtServerPath, 1, 0);
        var btnBrowseServer = new Button { Text = "찾기", Width = 60 };
        btnBrowseServer.Click += BtnBrowseServer_Click;
        mainPanel.Controls.Add(btnBrowseServer, 2, 0);

        // Row 1: Client Path
        mainPanel.Controls.Add(new Label { Text = "클라이언트 exe:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        txtClientPath = new TextBox { Dock = DockStyle.Fill };
        mainPanel.Controls.Add(txtClientPath, 1, 1);
        var btnBrowseClient = new Button { Text = "찾기", Width = 60 };
        btnBrowseClient.Click += BtnBrowseClient_Click;
        mainPanel.Controls.Add(btnBrowseClient, 2, 1);

        // Row 2: Server IP
        mainPanel.Controls.Add(new Label { Text = "서버 IP:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        txtServerIP = new TextBox { Dock = DockStyle.Fill };
        mainPanel.Controls.Add(txtServerIP, 1, 2);

        // Row 3: Port (고정: 3000)
        mainPanel.Controls.Add(new Label { Text = "서버 포트 (고정):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        numPort = new NumericUpDown { Minimum = 3000, Maximum = 3000, Value = 3000, Width = 100, Enabled = false, ReadOnly = true };
        mainPanel.Controls.Add(numPort, 1, 3);

        // Row 4: Wait Time
        mainPanel.Controls.Add(new Label { Text = "대기 시간 (초):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        numWaitTime = new NumericUpDown { Minimum = 5, Maximum = 120, Value = 30, Width = 100 };
        mainPanel.Controls.Add(numWaitTime, 1, 4);

        // Row 5: Monitor Selection
        mainPanel.Controls.Add(new Label { Text = "대상 모니터:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        cmbMonitor = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        mainPanel.SetColumnSpan(cmbMonitor, 2);
        mainPanel.Controls.Add(cmbMonitor, 1, 5);
        LoadMonitorList();

        // Row 6: TD (OSC) Separator
        var tdSeparator = new Label
        {
            Text = "── TD 연동 (OSC) ──",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9F)
        };
        mainPanel.SetColumnSpan(tdSeparator, 3);
        mainPanel.Controls.Add(tdSeparator, 0, 6);

        // Row 7: TD Host
        mainPanel.Controls.Add(new Label { Text = "TD 서버 IP:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 7);
        txtOscHost = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "192.168.0.13" };
        mainPanel.SetColumnSpan(txtOscHost, 2);
        mainPanel.Controls.Add(txtOscHost, 1, 7);

        // Row 8: TD Port
        mainPanel.Controls.Add(new Label { Text = "TD 서버 포트:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 8);
        numOscPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 50013, Width = 100 };
        mainPanel.Controls.Add(numOscPort, 1, 8);

        // Row 9: DDNS Separator
        var separator = new Label
        {
            Text = "── DDNS 설정 ──",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9F)
        };
        mainPanel.SetColumnSpan(separator, 3);
        mainPanel.Controls.Add(separator, 0, 9);

        // Row 10: DDNS Enabled
        mainPanel.Controls.Add(new Label { Text = "DDNS 활성화:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 10);
        chkDdnsEnabled = new CheckBox { Text = "Dynu DDNS 자동 IP 업데이트", AutoSize = true };
        mainPanel.SetColumnSpan(chkDdnsEnabled, 2);
        mainPanel.Controls.Add(chkDdnsEnabled, 1, 10);
        chkDdnsEnabled.CheckedChanged += (s, ev) => {
            txtDdnsHostname.Enabled = chkDdnsEnabled.Checked;
            txtDdnsPassword.Enabled = chkDdnsEnabled.Checked;
        };

        // Row 11: DDNS Hostname
        mainPanel.Controls.Add(new Label { Text = "DDNS 호스트:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 11);
        txtDdnsHostname = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "example.dynu.net" };
        mainPanel.SetColumnSpan(txtDdnsHostname, 2);
        mainPanel.Controls.Add(txtDdnsHostname, 1, 11);

        // Row 12: DDNS Password
        mainPanel.Controls.Add(new Label { Text = "DDNS 비밀번호:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 12);
        txtDdnsPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        mainPanel.SetColumnSpan(txtDdnsPassword, 2);
        mainPanel.Controls.Add(txtDdnsPassword, 1, 12);

        // Row 13: Auto Start
        mainPanel.Controls.Add(new Label { Text = "자동 시작:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 13);
        chkAutoStart = new CheckBox { Text = "런처 시작 시 자동으로 시작 (5초 후, Esc로 취소)", AutoSize = true };
        mainPanel.SetColumnSpan(chkAutoStart, 2);
        mainPanel.Controls.Add(chkAutoStart, 1, 13);

        // Row 14: Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };
        mainPanel.SetColumnSpan(buttonPanel, 3);

        var btnCancel = new Button { Text = "취소", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        var btnSave = new Button { Text = "저장", Size = new Size(80, 30) };
        btnSave.Click += BtnSave_Click;

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnSave);
        mainPanel.Controls.Add(buttonPanel, 0, 14);

        this.Controls.Add(mainPanel);
        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;

        this.ResumeLayout();
    }

    private void LoadMonitorList()
    {
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var b = s.Bounds;
            string primary = s.Primary ? " [주 모니터]" : "";
            cmbMonitor.Items.Add($"모니터 {i + 1}: {b.Width}×{b.Height} ({b.X},{b.Y}){primary}");
        }
    }

    private void LoadSettings()
    {
        txtServerPath.Text = _settings.ServerBatPath;
        txtClientPath.Text = _settings.ClientExePath;
        txtServerIP.Text = _settings.ServerIP;
        numPort.Value = _settings.ServerPort;
        numWaitTime.Value = _settings.ServerWaitSeconds;

        // Monitor selection
        int idx = _settings.SelectedMonitorIndex;
        if (idx >= 0 && idx < cmbMonitor.Items.Count)
            cmbMonitor.SelectedIndex = idx;
        else if (cmbMonitor.Items.Count > 0)
            cmbMonitor.SelectedIndex = 0;

        // DDNS settings
        chkDdnsEnabled.Checked = _settings.DdnsEnabled;
        txtDdnsHostname.Text = _settings.DdnsHostname;
        txtDdnsPassword.Text = _settings.DdnsPassword;
        txtDdnsHostname.Enabled = _settings.DdnsEnabled;
        txtDdnsPassword.Enabled = _settings.DdnsEnabled;

        // TD (OSC) settings
        txtOscHost.Text = _settings.OscHost;
        int oscPortClamped = Math.Clamp(_settings.OscPort, (int)numOscPort.Minimum, (int)numOscPort.Maximum);
        numOscPort.Value = oscPortClamped;

        // Auto start
        chkAutoStart.Checked = _settings.AutoStartOnLaunch;
    }

    private void BtnBrowseServer_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "서버 스크립트 선택",
            Filter = "Batch 파일 (*.bat)|*.bat|모든 파일 (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(_settings.ServerBatPath) ?? ""
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtServerPath.Text = dialog.FileName;
        }
    }

    private void BtnBrowseClient_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "클라이언트 실행 파일 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(_settings.ClientExePath) ?? ""
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtClientPath.Text = dialog.FileName;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Validate
        if (!File.Exists(txtServerPath.Text))
        {
            MessageBox.Show("서버 스크립트 파일이 존재하지 않습니다.", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(txtClientPath.Text))
        {
            MessageBox.Show("클라이언트 실행 파일이 존재하지 않습니다.", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Save
        _settings.ServerBatPath = txtServerPath.Text;
        _settings.ClientExePath = txtClientPath.Text;
        _settings.ServerIP = txtServerIP.Text;
        _settings.ServerPort = (int)numPort.Value;
        _settings.ServerWaitSeconds = (int)numWaitTime.Value;
        _settings.SelectedMonitorIndex = cmbMonitor.SelectedIndex >= 0 ? cmbMonitor.SelectedIndex : 0;

        // DDNS settings
        _settings.DdnsEnabled = chkDdnsEnabled.Checked;
        _settings.DdnsHostname = txtDdnsHostname.Text.Trim();
        _settings.DdnsPassword = txtDdnsPassword.Text;

        // TD (OSC) settings
        _settings.OscHost = txtOscHost.Text.Trim();
        _settings.OscPort = (int)numOscPort.Value;

        // Auto start
        _settings.AutoStartOnLaunch = chkAutoStart.Checked;

        // Sync settings to server config.json
        SyncDdnsToServerConfig();
        SyncOscToServerConfig();

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
    private void SyncDdnsToServerConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.ServerBatPath)) return;

            var serverDir = Path.GetDirectoryName(_settings.ServerBatPath);
            if (serverDir == null) return;

            var configPath = Path.Combine(serverDir, "config.json");
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            var root = JsonNode.Parse(json);
            if (root == null) return;

            root["ddns"] = new JsonObject
            {
                ["enabled"] = _settings.DdnsEnabled,
                ["provider"] = "dynu",
                ["hostname"] = _settings.DdnsHostname,
                ["password"] = _settings.DdnsPassword,
                ["updateIntervalMinutes"] = 5
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(options));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DDNS 설정을 서버 config.json에 반영하지 못했습니다:\n{ex.Message}",
                "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // 서버 config.json의 osc.host / osc.port만 덮어쓴다. enabled 등 다른 osc 필드는 보존.
    // OscHost가 비어있으면 실수로 기존 서버 설정을 날리지 않도록 스킵.
    private void SyncOscToServerConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.ServerBatPath)) return;
            if (string.IsNullOrWhiteSpace(_settings.OscHost)) return;

            var serverDir = Path.GetDirectoryName(_settings.ServerBatPath);
            if (serverDir == null) return;

            var configPath = Path.Combine(serverDir, "config.json");
            if (!File.Exists(configPath)) return;

            var json = File.ReadAllText(configPath);
            var root = JsonNode.Parse(json);
            if (root == null) return;

            // osc 섹션이 없으면 기본 구조로 생성
            if (root["osc"] is not JsonObject oscNode)
            {
                oscNode = new JsonObject { ["enabled"] = true };
                root["osc"] = oscNode;
            }

            oscNode["host"] = _settings.OscHost;
            oscNode["port"] = _settings.OscPort;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, root.ToJsonString(options));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"TD(OSC) 설정을 서버 config.json에 반영하지 못했습니다:\n{ex.Message}",
                "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

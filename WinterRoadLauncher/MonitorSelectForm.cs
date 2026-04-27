namespace WinterRoadLauncher;

public partial class MonitorSelectForm : Form
{
    private ListView lstMonitors = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    public Screen? SelectedScreen { get; private set; }

    private readonly int _defaultIndex;

    public MonitorSelectForm(int defaultMonitorIndex = 0)
    {
        _defaultIndex = defaultMonitorIndex;
        InitializeComponent();
        LoadMonitors();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form settings
        this.Text = "🖥 모니터 선택";
        this.Size = new Size(520, 350);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Font = new Font("Segoe UI", 10F);

        // Description label
        var lblDesc = new Label
        {
            Text = "클라이언트를 실행할 모니터를 선택하세요:",
            Dock = DockStyle.Top,
            Height = 35,
            Padding = new Padding(10, 8, 10, 0),
            AutoSize = false
        };

        // ListView
        lstMonitors = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 9.5F)
        };

        lstMonitors.Columns.Add("모니터", 140);
        lstMonitors.Columns.Add("해상도", 130);
        lstMonitors.Columns.Add("위치", 100);
        lstMonitors.Columns.Add("주 모니터", 80);

        lstMonitors.DoubleClick += (s, e) =>
        {
            if (lstMonitors.SelectedItems.Count > 0)
                AcceptSelection();
        };

        var listPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 5)
        };
        listPanel.Controls.Add(lstMonitors);

        // Button Panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 5, 10, 10)
        };

        btnCancel = new Button
        {
            Text = "취소",
            Size = new Size(90, 35),
            DialogResult = DialogResult.Cancel
        };

        btnOk = new Button
        {
            Text = "✅ 확인",
            Size = new Size(90, 35)
        };
        btnOk.Click += (s, e) => AcceptSelection();

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOk);

        // Add controls
        this.Controls.Add(listPanel);
        this.Controls.Add(lblDesc);
        this.Controls.Add(buttonPanel);

        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        this.ResumeLayout();
    }

    private void LoadMonitors()
    {
        var screens = Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var bounds = screen.Bounds;

            var item = new ListViewItem($"모니터 {i + 1} ({screen.DeviceName.TrimStart('\\', '.')})");
            item.SubItems.Add($"{bounds.Width} × {bounds.Height}");
            item.SubItems.Add($"({bounds.X}, {bounds.Y})");
            item.SubItems.Add(screen.Primary ? "✔ 예" : "");
            item.Tag = screen;

            lstMonitors.Items.Add(item);
        }

        // Select default monitor
        int selectIndex = _defaultIndex;
        if (selectIndex < 0 || selectIndex >= lstMonitors.Items.Count)
            selectIndex = 0;

        if (lstMonitors.Items.Count > 0)
        {
            lstMonitors.Items[selectIndex].Selected = true;
            lstMonitors.Items[selectIndex].Focused = true;
            lstMonitors.Items[selectIndex].EnsureVisible();
        }
    }

    private void AcceptSelection()
    {
        if (lstMonitors.SelectedItems.Count == 0)
        {
            MessageBox.Show("모니터를 선택해주세요.", "알림",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedScreen = (Screen)lstMonitors.SelectedItems[0].Tag!;
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    public int GetSelectedIndex()
    {
        if (lstMonitors.SelectedItems.Count > 0)
            return lstMonitors.SelectedItems[0].Index;
        return 0;
    }
}

namespace SlideLe;

internal sealed class SettingsDialog : Form
{
    private static readonly Color PowerPointOrange = Color.FromArgb(208, 68, 35);
    private readonly TextBox _sourceUrlTextBox = new();

    public SettingsDialog(string sourceUrl)
    {
        Text = "Thiết lập Slide Lễ";
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(248, 249, 250);
        ClientSize = new Size(660, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var content = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(24)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular),
            ForeColor = PowerPointOrange,
            Margin = new Padding(0, 0, 0, 10),
            Text = "URL thư mục GitHub"
        };

        _sourceUrlTextBox.AccessibleName = "URL thư mục GitHub";
        _sourceUrlTextBox.AccessibleDescription =
            "Nhập liên kết HTTPS của thư mục GitHub chứa slide.";
        _sourceUrlTextBox.Dock = DockStyle.Top;
        _sourceUrlTextBox.Height = 42;
        _sourceUrlTextBox.Text = sourceUrl;

        var descriptionLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 0),
            Text = "Ví dụ: https://github.com/chutai/kho/tree/main/Slide"
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            WrapContents = false
        };

        var cancelButton = new Button
        {
            AutoSize = false,
            DialogResult = DialogResult.Cancel,
            Height = 38,
            Margin = new Padding(8, 0, 0, 0),
            Text = "Hủy",
            Width = 92
        };

        var saveButton = new Button
        {
            AutoSize = false,
            BackColor = PowerPointOrange,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Regular),
            ForeColor = Color.White,
            Height = 38,
            Text = "Lưu",
            UseVisualStyleBackColor = false,
            Width = 92
        };
        saveButton.FlatAppearance.BorderColor = PowerPointOrange;
        saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 55, 27);
        saveButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(154, 45, 23);
        saveButton.Click += SaveButton_Click;

        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(saveButton);
        content.Controls.Add(titleLabel, 0, 0);
        content.Controls.Add(_sourceUrlTextBox, 0, 1);
        content.Controls.Add(descriptionLabel, 0, 2);
        content.Controls.Add(buttonsPanel, 0, 4);
        Controls.Add(content);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    public string SourceUrl { get; private set; } = string.Empty;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _sourceUrlTextBox.Focus();
        _sourceUrlTextBox.SelectAll();
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        string sourceUrl = _sourceUrlTextBox.Text.Trim();

        try
        {
            GitHubFolderAddress.Parse(sourceUrl);
            AppSettingsStore.SaveSourceUrl(sourceUrl);
            SourceUrl = sourceUrl;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, "URL GitHub không hợp lệ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _sourceUrlTextBox.Focus();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            MessageBox.Show(this, $"Không thể lưu thiết lập.\n{exception.Message}", "Slide Lễ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

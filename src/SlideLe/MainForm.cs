using System.ComponentModel;
using System.Diagnostics;

namespace SlideLe;

internal sealed class MainForm : Form
{
    private const string DefaultSourceUrl =
        "https://github.com/lythanhngodev64/SlideLe/tree/master/Slide";
    private static readonly Color PowerPointOrange = Color.FromArgb(208, 68, 35);
    private static readonly Color PowerPointHighlight = Color.FromArgb(255, 240, 237);
    private static readonly Color ApplicationBackground = Color.FromArgb(248, 249, 250);
    private static readonly Font DownloadButtonFont = new("Segoe UI Semibold", 11F, FontStyle.Regular);
    private const string ActionsColumnName = "actionsColumn";

    private readonly GitHubFolderClient _gitHubFolderClient = new();
    private readonly List<PresentationFile> _allPresentations = [];
    private readonly BindingList<PresentationFile> _presentations = [];
    private readonly CheckBox _includeSubdirectoriesCheckBox = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Button _scanButton = new();
    private readonly Button _settingsButton = new();
    private readonly DataGridView _presentationsGrid = new();
    private readonly Label _scopeHintLabel = new();
    private readonly Label _statusLabel = new();
    private int _hoveredActionRowIndex = -1;
    private PresentationAction _hoveredAction = PresentationAction.None;
    private bool _searchFilterQueued;
    private string _sourceUrl = DefaultSourceUrl;

    private enum PresentationAction
    {
        None,
        Download,
        OpenNow
    }

    public MainForm()
    {
        Text = "SlideLe - Tải slide PowerPoint";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            ?? System.Drawing.SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(980, 620);
        Size = new Size(1120, 720);
        BackColor = ApplicationBackground;
        _sourceUrl = AppSettingsStore.LoadSourceUrl(DefaultSourceUrl);

        BuildUserInterface();
        AcceptButton = _scanButton;
    }

    private void BuildUserInterface()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ApplicationBackground
        };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerPanel = new Panel
        {
            AutoSize = true,
            BackColor = PowerPointOrange,
            Dock = DockStyle.Top,
            MinimumSize = new Size(0, 86),
            Padding = new Padding(28, 14, 28, 14)
        };

        var headerContent = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        headerContent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerContent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerContent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var logoPictureBox = new PictureBox
        {
            AccessibleName = "Logo SlideLe",
            Image = LoadBrandMark(),
            Margin = new Padding(0),
            Size = new Size(54, 54),
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 21F, FontStyle.Regular),
            ForeColor = Color.White,
            Margin = new Padding(14, 5, 0, 0),
            Text = "Slide Lễ",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var helpButton = new Button
        {
            AccessibleName = "Trợ giúp",
            AccessibleDescription = "Xem hướng dẫn sử dụng Slide Lễ.",
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular),
            ForeColor = Color.White,
            Margin = new Padding(8, 9, 0, 0),
            Size = new Size(38, 38),
            Text = "?",
            UseVisualStyleBackColor = false
        };
        helpButton.FlatAppearance.BorderColor = Color.White;
        helpButton.FlatAppearance.BorderSize = 1;
        helpButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 255, 255, 255);
        helpButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 255, 255, 255);
        helpButton.Click += HelpButton_Click;

        _settingsButton.AccessibleName = "Thiết lập nguồn slide";
        _settingsButton.AccessibleDescription =
            "Thiết lập URL thư mục GitHub dùng để quét slide.";
        _settingsButton.Cursor = Cursors.Hand;
        _settingsButton.FlatStyle = FlatStyle.Flat;
        _settingsButton.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Regular);
        _settingsButton.ForeColor = Color.White;
        _settingsButton.Margin = new Padding(8, 9, 0, 0);
        _settingsButton.Size = new Size(108, 38);
        _settingsButton.Text = "Thiết lập";
        _settingsButton.UseVisualStyleBackColor = false;
        _settingsButton.FlatAppearance.BorderColor = Color.White;
        _settingsButton.FlatAppearance.BorderSize = 1;
        _settingsButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 255, 255, 255);
        _settingsButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 255, 255, 255);
        _settingsButton.Click += SettingsButton_Click;

        headerContent.Controls.Add(logoPictureBox, 0, 0);
        headerContent.Controls.Add(titleLabel, 1, 0);
        headerContent.Controls.Add(_settingsButton, 2, 0);
        headerContent.Controls.Add(helpButton, 3, 0);
        headerPanel.Controls.Add(headerContent);

        var workspacePanel = new Panel
        {
            BackColor = Color.White,
            Dock = DockStyle.Fill,
            Margin = new Padding(20),
            Padding = new Padding(24)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _scanButton.AccessibleName = "Quét tài liệu";
        _scanButton.AutoSize = false;
        _scanButton.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _scanButton.Margin = new Padding(16, 0, 0, 0);
        _scanButton.MinimumSize = new Size(160, 42);
        _scanButton.Padding = new Padding(12, 5, 12, 5);
        _scanButton.Text = "Quét tài liệu";
        ConfigurePrimaryButton(_scanButton);
        _scanButton.Click += ScanButton_Click;

        _includeSubdirectoriesCheckBox.AccessibleName =
            "Slide khác - quét cả thư mục con";
        _includeSubdirectoriesCheckBox.AccessibleDescription =
            "Chọn để quét cả tệp PowerPoint trong các thư mục con.";
        _includeSubdirectoriesCheckBox.AutoSize = true;
        _includeSubdirectoriesCheckBox.Anchor = AnchorStyles.Left;
        _includeSubdirectoriesCheckBox.Margin = new Padding(16, 0, 0, 0);
        _includeSubdirectoriesCheckBox.MinimumSize = new Size(0, 38);
        _includeSubdirectoriesCheckBox.Text = "Slide khác";
        _includeSubdirectoriesCheckBox.CheckedChanged += IncludeSubdirectoriesCheckBox_CheckedChanged;

        _scopeHintLabel.AutoSize = true;
        _scopeHintLabel.ForeColor = SystemColors.GrayText;
        _scopeHintLabel.Margin = new Padding(0, 0, 0, 10);

        var searchPanel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 4,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var searchLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 12, 0),
            Text = "Tìm kiếm:"
        };

        _searchTextBox.AccessibleName = "Tìm kiếm slide";
        _searchTextBox.AccessibleDescription =
            "Gõ tên, đường dẫn hoặc dung lượng để lọc danh sách slide.";
        _searchTextBox.AutoSize = false;
        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.Height = 42;
        _searchTextBox.PlaceholderText = "Nhập tên, đường dẫn hoặc dung lượng slide...";
        _searchTextBox.KeyDown += SearchTextBox_KeyDown;
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        searchPanel.Controls.Add(searchLabel, 0, 0);
        searchPanel.Controls.Add(_searchTextBox, 1, 0);
        searchPanel.Controls.Add(_includeSubdirectoriesCheckBox, 2, 0);
        searchPanel.Controls.Add(_scanButton, 3, 0);

        ConfigurePresentationsGrid();

        _statusLabel.AutoSize = true;
        _statusLabel.AccessibleName = "Trạng thái thao tác";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 10, 0, 0);
        _statusLabel.Text = "Sẵn sàng. Nhấn “Quét tài liệu” để lấy danh sách slide.";

        root.Controls.Add(searchPanel, 0, 0);
        root.Controls.Add(_scopeHintLabel, 0, 1);
        root.Controls.Add(_presentationsGrid, 0, 2);
        root.Controls.Add(_statusLabel, 0, 3);
        workspacePanel.Controls.Add(root);
        shell.Controls.Add(headerPanel, 0, 0);
        shell.Controls.Add(workspacePanel, 0, 1);
        Controls.Add(shell);

        UpdateScanScopeHint();
    }

    private void UpdateScanScopeHint()
    {
        _scopeHintLabel.Text = _includeSubdirectoriesCheckBox.Checked
            ? "Đang chọn quét cả các tệp .pptx trong thư mục con."
            : "Chỉ quét các tệp .pptx ngay trong thư mục ở URL đã thiết lập.";
    }

    private void IncludeSubdirectoriesCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateScanScopeHint();
    }

    private void ConfigurePresentationsGrid()
    {
        _presentationsGrid.AccessibleName = "Danh sách slide PowerPoint";
        _presentationsGrid.AccessibleDescription =
            "Danh sách slide PowerPoint. Mỗi dòng có thao tác Tải về hoặc Mở ngay bằng ứng dụng mặc định của Windows.";
        _presentationsGrid.AllowUserToAddRows = false;
        _presentationsGrid.AllowUserToDeleteRows = false;
        _presentationsGrid.AllowUserToResizeRows = false;
        _presentationsGrid.AutoGenerateColumns = false;
        _presentationsGrid.BackgroundColor = Color.White;
        _presentationsGrid.BorderStyle = BorderStyle.FixedSingle;
        _presentationsGrid.ColumnHeadersHeight = 46;
        _presentationsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _presentationsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            BackColor = PowerPointHighlight,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Regular),
            ForeColor = PowerPointOrange,
            Padding = new Padding(4, 0, 4, 0)
        };
        _presentationsGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Padding = new Padding(4, 2, 4, 2),
            SelectionBackColor = PowerPointHighlight,
            SelectionForeColor = SystemColors.ControlText
        };
        _presentationsGrid.Dock = DockStyle.Fill;
        _presentationsGrid.MultiSelect = false;
        _presentationsGrid.ReadOnly = true;
        _presentationsGrid.RowTemplate.Height = 42;
        _presentationsGrid.RowHeadersVisible = false;
        _presentationsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _presentationsGrid.DataSource = _presentations;
        _presentationsGrid.CellMouseClick += PresentationsGrid_CellMouseClick;
        _presentationsGrid.CellPainting += PresentationsGrid_CellPainting;
        _presentationsGrid.CellMouseMove += PresentationsGrid_CellMouseMove;
        _presentationsGrid.MouseLeave += PresentationsGrid_MouseLeave;

        _presentationsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DataPropertyName = nameof(PresentationFile.Name),
            FillWeight = 38,
            HeaderText = "Tên tệp",
            MinimumWidth = 180,
            Name = "nameColumn"
        });
        _presentationsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DataPropertyName = nameof(PresentationFile.RelativePath),
            FillWeight = 45,
            HeaderText = "Đường dẫn",
            MinimumWidth = 180,
            Name = "pathColumn"
        });
        _presentationsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PresentationFile.DisplaySize),
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Padding = new Padding(4, 2, 8, 2)
            },
            HeaderText = "Dung lượng",
            Name = "sizeColumn",
            Width = 130
        });
        _presentationsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            HeaderText = "Thao tác",
            Name = ActionsColumnName,
            ToolTipText = "Chọn Tải về để lưu tệp hoặc Mở ngay để mở bằng ứng dụng PowerPoint mặc định.",
            Width = 210
        });
    }

    private static void ConfigurePrimaryButton(Button button)
    {
        button.BackColor = PowerPointOrange;
        button.Cursor = Cursors.Hand;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Regular);
        button.ForeColor = Color.White;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderColor = PowerPointOrange;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 55, 27);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(154, 45, 23);
    }

    private void HelpButton_Click(object? sender, EventArgs e)
    {
        MessageBox.Show(this,
            "Nhấn “Thiết lập” để chọn URL thư mục GitHub, nhấn “Quét tài liệu”, rồi chọn “Tải về” để lưu slide hoặc “Mở ngay” để mở bằng ứng dụng PowerPoint mặc định.",
            "Hướng dẫn Slide Lễ", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var settingsDialog = new SettingsDialog(_sourceUrl);
        if (settingsDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _sourceUrl = settingsDialog.SourceUrl;
        _allPresentations.Clear();
        ApplySearchFilter();
        _statusLabel.Text = "Đã cập nhật nguồn slide. Nhấn “Quét tài liệu” để lấy danh sách mới.";
    }

    private void PresentationsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _presentationsGrid.Columns[ActionsColumnName]!.Index)
        {
            return;
        }

        Graphics? graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
        PaintActionButton(graphics, GetActionButtonBounds(e.CellBounds, PresentationAction.Download),
            "Tải về", e.RowIndex, PresentationAction.Download);
        PaintActionButton(graphics, GetActionButtonBounds(e.CellBounds, PresentationAction.OpenNow),
            "Mở ngay", e.RowIndex, PresentationAction.OpenNow);
        e.Handled = true;
    }

    private void PaintActionButton(
        Graphics graphics,
        Rectangle buttonBounds,
        string text,
        int rowIndex,
        PresentationAction action)
    {
        bool isHovered = _hoveredActionRowIndex == rowIndex && _hoveredAction == action;
        Color buttonBackColor = isHovered ? PowerPointOrange : Color.White;
        Color buttonForeColor = isHovered ? Color.White : PowerPointOrange;

        using var backgroundBrush = new SolidBrush(buttonBackColor);
        using var borderPen = new Pen(PowerPointOrange);
        graphics.FillRectangle(backgroundBrush, buttonBounds);
        graphics.DrawRectangle(borderPen, buttonBounds);
        TextRenderer.DrawText(graphics, text, DownloadButtonFont, buttonBounds, buttonForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private void PresentationsGrid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        PresentationAction hoveredAction = GetPresentationActionAt(e.RowIndex, e.ColumnIndex);
        int hoveredRowIndex = hoveredAction == PresentationAction.None ? -1 : e.RowIndex;
        if (_hoveredActionRowIndex == hoveredRowIndex && _hoveredAction == hoveredAction)
        {
            return;
        }

        InvalidateActionCell(_hoveredActionRowIndex);
        _hoveredActionRowIndex = hoveredRowIndex;
        _hoveredAction = hoveredAction;
        InvalidateActionCell(_hoveredActionRowIndex);
        _presentationsGrid.Cursor = hoveredAction == PresentationAction.None ? Cursors.Default : Cursors.Hand;
    }

    private void PresentationsGrid_MouseLeave(object? sender, EventArgs e)
    {
        if (_hoveredActionRowIndex < 0)
        {
            return;
        }

        InvalidateActionCell(_hoveredActionRowIndex);
        _hoveredActionRowIndex = -1;
        _hoveredAction = PresentationAction.None;
        _presentationsGrid.Cursor = Cursors.Default;
    }

    private void InvalidateActionCell(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < _presentationsGrid.RowCount)
        {
            _presentationsGrid.InvalidateCell(_presentationsGrid.Columns[ActionsColumnName]!.Index, rowIndex);
        }
    }

    private PresentationAction GetPresentationActionAt(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || columnIndex != _presentationsGrid.Columns[ActionsColumnName]!.Index)
        {
            return PresentationAction.None;
        }

        Rectangle cellBounds = _presentationsGrid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
        Point mouseLocation = _presentationsGrid.PointToClient(Cursor.Position);
        return GetActionButtonBounds(cellBounds, PresentationAction.Download).Contains(mouseLocation)
            ? PresentationAction.Download
            : GetActionButtonBounds(cellBounds, PresentationAction.OpenNow).Contains(mouseLocation)
                ? PresentationAction.OpenNow
                : PresentationAction.None;
    }

    private static Rectangle GetActionButtonBounds(Rectangle cellBounds, PresentationAction action)
    {
        Rectangle contentBounds = Rectangle.Inflate(cellBounds, -8, -7);
        const int gap = 6;
        int buttonWidth = (contentBounds.Width - gap) / 2;
        return action == PresentationAction.Download
            ? new Rectangle(contentBounds.Left, contentBounds.Top, buttonWidth, contentBounds.Height)
            : new Rectangle(contentBounds.Left + buttonWidth + gap, contentBounds.Top,
                contentBounds.Width - buttonWidth - gap, contentBounds.Height);
    }

    private static Image LoadBrandMark()
    {
        using Stream resourceStream = typeof(MainForm).Assembly.GetManifestResourceStream(
            "SlideLe.Assets.SlideLe-mark.png")
            ?? throw new InvalidOperationException("Không tìm thấy logo SlideLe.");
        using Image sourceImage = Image.FromStream(resourceStream);

        return new Bitmap(sourceImage);
    }

    private async void ScanButton_Click(object? sender, EventArgs e)
    {
        await ScanAsync();
    }

    private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        QueueSearchFilter();
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        QueueSearchFilter();
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_searchTextBox.Focused && (keyData & Keys.KeyCode) == Keys.Enter)
        {
            ApplySearchFilterAndUpdateStatus();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    private void QueueSearchFilter()
    {
        if (_searchTextBox.ReadOnly || _searchFilterQueued || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        _searchFilterQueued = true;
        BeginInvoke((MethodInvoker)(() =>
        {
            _searchFilterQueued = false;
            ApplySearchFilterAndUpdateStatus();
        }));
    }

    private async Task ScanAsync()
    {
        bool includeSubdirectories = _includeSubdirectoriesCheckBox.Checked;
        SetBusy(true);

        try
        {
            _statusLabel.Text = "Đang kiểm tra kết nối Internet...";
            if (!await EnsureInternetConnectionAsync())
            {
                return;
            }

            _statusLabel.Text = includeSubdirectories
                ? "Đang quét danh sách tệp .pptx, bao gồm thư mục con..."
                : "Đang quét danh sách tệp .pptx trong thư mục hiện tại...";
            IReadOnlyList<PresentationFile> files = await _gitHubFolderClient.GetPptxFilesAsync(
                _sourceUrl,
                includeSubdirectories,
                CancellationToken.None);

            _allPresentations.Clear();
            _allPresentations.AddRange(files);
            int displayedCount = ApplySearchFilter();
            UpdateScanStatus(files.Count, displayedCount, includeSubdirectories);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _statusLabel.Text = "Không thể quét danh sách tệp.";
            ShowError("Không thể quét tài liệu", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> EnsureInternetConnectionAsync()
    {
        if (await InternetConnectionChecker.CanReachInternetAsync(CancellationToken.None))
        {
            return true;
        }

        _statusLabel.Text = "Không thể kết nối Internet.";
        MessageBox.Show(this,
            "Không thể kết nối Internet. Vui lòng kiểm tra mạng rồi thử lại.",
            "Không có kết nối Internet",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private int ApplySearchFilter()
    {
        string searchText = _searchTextBox.Text.Trim();
        IReadOnlyList<PresentationFile> displayedFiles = string.IsNullOrEmpty(searchText)
            ? _allPresentations
            : _allPresentations.Where(file => MatchesSearch(file, searchText)).ToArray();

        _presentations.RaiseListChangedEvents = false;
        try
        {
            _presentations.Clear();
            foreach (PresentationFile file in displayedFiles)
            {
                _presentations.Add(file);
            }
        }
        finally
        {
            _presentations.RaiseListChangedEvents = true;
        }

        _presentations.ResetBindings();
        return displayedFiles.Count;
    }

    private void ApplySearchFilterAndUpdateStatus()
    {
        if (_searchTextBox.ReadOnly)
        {
            return;
        }

        int displayedCount = ApplySearchFilter();
        string searchText = _searchTextBox.Text.Trim();
        string searchTextForStatus = FormatSearchTextForStatus(searchText);

        if (_allPresentations.Count == 0)
        {
            _statusLabel.Text = string.IsNullOrEmpty(searchText)
                ? "Chưa có danh sách slide để tìm kiếm."
                : $"Không có slide phù hợp với “{searchTextForStatus}”.";
            return;
        }

        _statusLabel.Text = string.IsNullOrEmpty(searchText)
            ? $"Đang hiển thị toàn bộ {_allPresentations.Count:N0} tệp .pptx."
            : displayedCount == 0
                ? $"Không có slide phù hợp với “{searchTextForStatus}”."
                : $"Đang hiển thị {displayedCount:N0}/{_allPresentations.Count:N0} tệp phù hợp.";
    }

    private void UpdateScanStatus(int totalCount, int displayedCount, bool includeSubdirectories)
    {
        string searchText = _searchTextBox.Text.Trim();
        string searchTextForStatus = FormatSearchTextForStatus(searchText);
        if (totalCount == 0)
        {
            _statusLabel.Text = "Không tìm thấy tệp .pptx nào trong thư mục này.";
            return;
        }

        if (string.IsNullOrEmpty(searchText))
        {
            _statusLabel.Text = includeSubdirectories
                ? $"Đã tìm thấy {totalCount:N0} tệp .pptx, bao gồm thư mục con."
                : $"Đã tìm thấy {totalCount:N0} tệp .pptx trong thư mục hiện tại.";
            return;
        }

        _statusLabel.Text = displayedCount == 0
            ? $"Đã tìm thấy {totalCount:N0} tệp .pptx, nhưng không có tệp phù hợp với “{searchTextForStatus}”."
            : $"Đã tìm thấy {totalCount:N0} tệp .pptx. Đang hiển thị {displayedCount:N0} tệp phù hợp.";
    }

    private static string FormatSearchTextForStatus(string searchText) =>
        searchText.Length <= 60 ? searchText : $"{searchText[..60]}…";

    private static bool MatchesSearch(PresentationFile file, string searchText) =>
        file.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        file.RelativePath.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        file.DisplaySize.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private async void PresentationsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        PresentationAction action = GetPresentationActionAt(e.RowIndex, e.ColumnIndex);
        if (action == PresentationAction.None)
        {
            return;
        }

        if (_presentationsGrid.Rows[e.RowIndex].DataBoundItem is not PresentationFile file)
        {
            return;
        }

        SetBusy(true);
        try
        {
            _statusLabel.Text = "Đang kiểm tra kết nối Internet...";
            if (!await EnsureInternetConnectionAsync())
            {
                return;
            }
        }
        finally
        {
            SetBusy(false);
        }

        if (action == PresentationAction.Download)
        {
            await DownloadPresentationAsync(file);
            return;
        }

        await OpenPresentationAsync(file);
    }

    private async Task DownloadPresentationAsync(PresentationFile file)
    {
        using var saveDialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "pptx",
            FileName = GetSafeFileName(file.Name),
            Filter = "PowerPoint Presentation (*.pptx)|*.pptx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt = true,
            Title = "Lưu slide PowerPoint"
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);
        _statusLabel.Text = $"Đang tải “{file.Name}”...";

        try
        {
            await _gitHubFolderClient.DownloadAsync(file, saveDialog.FileName, CancellationToken.None);
            _statusLabel.Text = $"Đã tải “{file.Name}”.";
            MessageBox.Show(this, $"Đã tải tệp về:\n{saveDialog.FileName}", "SlideLe",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _statusLabel.Text = $"Không thể tải “{file.Name}”.";
            ShowError("Không thể tải tệp", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task OpenPresentationAsync(PresentationFile file)
    {
        string openFilePath = CreateOpenDestinationPath(file.Name);
        SetBusy(true);
        _statusLabel.Text = $"Đang tải “{file.Name}” để mở ngay...";

        try
        {
            try
            {
                await _gitHubFolderClient.DownloadAsync(file, openFilePath, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _statusLabel.Text = $"Không thể tải “{file.Name}” để mở ngay.";
                ShowError("Không thể tải tệp", exception.Message);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = openFilePath,
                    UseShellExecute = true
                });

                _statusLabel.Text = $"Đã chuyển “{file.Name}” cho Windows để mở.";
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                _statusLabel.Text = $"Không thể mở “{file.Name}”.";
                ShowError("Không thể mở tệp",
                    $"Windows chưa có ứng dụng mặc định để mở tệp .pptx, hoặc ứng dụng đó không thể khởi chạy.\n\n{exception.Message}");
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string CreateOpenDestinationPath(string fileName)
    {
        string safeFileName = GetSafeFileName(fileName);
        string fileExtension = Path.GetExtension(safeFileName);
        if (!fileExtension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".pptx";
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(safeFileName);
        return Path.Combine(Path.GetTempPath(), "SlideLe", "Opened",
            $"{fileNameWithoutExtension}-{Guid.NewGuid():N}{fileExtension}");
    }

    private void SetBusy(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _scanButton.Enabled = !isBusy;
        _settingsButton.Enabled = !isBusy;
        _includeSubdirectoriesCheckBox.Enabled = !isBusy;
        _searchTextBox.ReadOnly = isBusy;
        _presentationsGrid.Enabled = !isBusy;
    }

    private static string GetSafeFileName(string name)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string safeName = string.Concat(name.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character));

        return string.IsNullOrWhiteSpace(safeName) ? "slide.pptx" : safeName;
    }

    private void ShowError(string title, string message)
    {
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

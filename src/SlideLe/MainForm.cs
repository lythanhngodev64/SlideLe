using System.ComponentModel;

namespace SlideLe;

internal sealed class MainForm : Form
{
    private const string DefaultSourceUrl =
        "https://github.com/lythanhngodev64/SlideLe/tree/master/Slide";

    private readonly GitHubFolderClient _gitHubFolderClient = new();
    private readonly List<PresentationFile> _allPresentations = [];
    private readonly BindingList<PresentationFile> _presentations = [];
    private readonly TextBox _sourceUrlTextBox = new();
    private readonly CheckBox _includeSubdirectoriesCheckBox = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Button _scanButton = new();
    private readonly DataGridView _presentationsGrid = new();
    private readonly Label _scopeHintLabel = new();
    private readonly Label _statusLabel = new();
    private bool _searchFilterQueued;

    public MainForm()
    {
        Text = "SlideLe - Tải slide PowerPoint";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            ?? System.Drawing.SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(980, 620);
        Size = new Size(1120, 720);

        BuildUserInterface();
        AcceptButton = _scanButton;
    }

    private void BuildUserInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(20),
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brandPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 12),
            WrapContents = false
        };

        var logoPictureBox = new PictureBox
        {
            AccessibleName = "Logo SlideLe",
            Image = LoadBrandMark(),
            Margin = new Padding(0),
            Size = new Size(56, 56),
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        };

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 20F, FontStyle.Bold),
            Margin = new Padding(12, 12, 0, 0),
            Text = "Slide Lễ"
        };
        brandPanel.Controls.Add(logoPictureBox);
        brandPanel.Controls.Add(titleLabel);

        var sourcePanel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10)
        };
        sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sourcePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sourcePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var sourceLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
            Text = "URL thư mục GitHub:"
        };

        _sourceUrlTextBox.AccessibleName = "URL thư mục GitHub";
        _sourceUrlTextBox.AccessibleDescription = "Nhập liên kết thư mục GitHub cần quét.";
        _sourceUrlTextBox.AutoSize = false;
        _sourceUrlTextBox.Dock = DockStyle.Fill;
        _sourceUrlTextBox.Height = 42;
        _sourceUrlTextBox.Margin = new Padding(0, 0, 12, 0);
        _sourceUrlTextBox.Text = DefaultSourceUrl;
        _sourceUrlTextBox.KeyDown += SourceUrlTextBox_KeyDown;

        _scanButton.AccessibleName = "Quét tài liệu";
        _scanButton.AutoSize = false;
        _scanButton.Dock = DockStyle.Fill;
        _scanButton.Margin = new Padding(0);
        _scanButton.MinimumSize = new Size(160, 42);
        _scanButton.Padding = new Padding(12, 5, 12, 5);
        _scanButton.Text = "Quét tài liệu";
        _scanButton.UseVisualStyleBackColor = true;
        _scanButton.Click += ScanButton_Click;

        sourcePanel.Controls.Add(sourceLabel, 0, 0);
        sourcePanel.SetColumnSpan(sourceLabel, 2);
        sourcePanel.Controls.Add(_sourceUrlTextBox, 0, 1);
        sourcePanel.Controls.Add(_scanButton, 1, 1);

        _includeSubdirectoriesCheckBox.AccessibleName =
            "Slide khác - quét cả thư mục con";
        _includeSubdirectoriesCheckBox.AccessibleDescription =
            "Chọn để quét cả tệp PowerPoint trong các thư mục con.";
        _includeSubdirectoriesCheckBox.AutoSize = true;
        _includeSubdirectoriesCheckBox.Margin = new Padding(0, 0, 0, 4);
        _includeSubdirectoriesCheckBox.MinimumSize = new Size(0, 38);
        _includeSubdirectoriesCheckBox.Text = "Slide khác";
        _includeSubdirectoriesCheckBox.CheckedChanged += IncludeSubdirectoriesCheckBox_CheckedChanged;

        _scopeHintLabel.AutoSize = true;
        _scopeHintLabel.ForeColor = SystemColors.GrayText;
        _scopeHintLabel.Margin = new Padding(0, 0, 0, 10);

        var searchPanel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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

        ConfigurePresentationsGrid();

        _statusLabel.AutoSize = true;
        _statusLabel.AccessibleName = "Trạng thái thao tác";
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Margin = new Padding(0, 10, 0, 0);
        _statusLabel.Text = "Sẵn sàng. Nhấn “Quét tài liệu” để lấy danh sách slide.";

        root.Controls.Add(brandPanel, 0, 0);
        root.Controls.Add(sourcePanel, 0, 1);
        root.Controls.Add(_includeSubdirectoriesCheckBox, 0, 2);
        root.Controls.Add(_scopeHintLabel, 0, 3);
        root.Controls.Add(searchPanel, 0, 4);
        root.Controls.Add(_presentationsGrid, 0, 5);
        root.Controls.Add(_statusLabel, 0, 6);
        Controls.Add(root);

        UpdateScanScopeHint();
    }

    private void UpdateScanScopeHint()
    {
        _scopeHintLabel.Text = _includeSubdirectoriesCheckBox.Checked
            ? "Đang chọn quét cả các tệp .pptx trong thư mục con."
            : "Chỉ quét các tệp .pptx ngay trong thư mục ở URL đã nhập.";
    }

    private void IncludeSubdirectoriesCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateScanScopeHint();
    }

    private void ConfigurePresentationsGrid()
    {
        _presentationsGrid.AccessibleName = "Danh sách slide PowerPoint";
        _presentationsGrid.AllowUserToAddRows = false;
        _presentationsGrid.AllowUserToDeleteRows = false;
        _presentationsGrid.AllowUserToResizeRows = false;
        _presentationsGrid.AutoGenerateColumns = false;
        _presentationsGrid.BackgroundColor = SystemColors.Window;
        _presentationsGrid.BorderStyle = BorderStyle.FixedSingle;
        _presentationsGrid.ColumnHeadersHeight = 46;
        _presentationsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _presentationsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 12F, FontStyle.Bold),
            Padding = new Padding(4, 0, 4, 0)
        };
        _presentationsGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            Padding = new Padding(4, 2, 4, 2)
        };
        _presentationsGrid.Dock = DockStyle.Fill;
        _presentationsGrid.MultiSelect = false;
        _presentationsGrid.ReadOnly = true;
        _presentationsGrid.RowTemplate.Height = 42;
        _presentationsGrid.RowHeadersVisible = false;
        _presentationsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _presentationsGrid.DataSource = _presentations;
        _presentationsGrid.CellContentClick += PresentationsGrid_CellContentClick;

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
        _presentationsGrid.Columns.Add(new DataGridViewButtonColumn
        {
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 12F, FontStyle.Bold)
            },
            HeaderText = "Tải xuống",
            Name = "downloadColumn",
            Text = "Tải về",
            UseColumnTextForButtonValue = true,
            Width = 145
        });
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

    private async void SourceUrlTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || !_scanButton.Enabled)
        {
            return;
        }

        e.SuppressKeyPress = true;
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
        _statusLabel.Text = includeSubdirectories
            ? "Đang quét danh sách tệp .pptx, bao gồm thư mục con..."
            : "Đang quét danh sách tệp .pptx trong thư mục hiện tại...";

        try
        {
            IReadOnlyList<PresentationFile> files = await _gitHubFolderClient.GetPptxFilesAsync(
                _sourceUrlTextBox.Text,
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

    private async void PresentationsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _presentationsGrid.Columns["downloadColumn"].Index)
        {
            return;
        }

        if (_presentationsGrid.Rows[e.RowIndex].DataBoundItem is not PresentationFile file)
        {
            return;
        }

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

    private void SetBusy(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _scanButton.Enabled = !isBusy;
        _sourceUrlTextBox.ReadOnly = isBusy;
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

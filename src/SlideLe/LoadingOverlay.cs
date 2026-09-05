using System.Drawing.Drawing2D;

namespace SlideLe;

/// <summary>
/// An owned, semi-transparent window that blocks the owner while a scan is in progress.
/// </summary>
internal sealed class LoadingOverlay : Form
{
    private static readonly Color OverlayBackground = Color.FromArgb(232, 236, 240);
    private static readonly Color MessageColor = Color.FromArgb(45, 52, 59);
    private static readonly Color SpinnerColor = Color.FromArgb(208, 68, 35);

    private readonly System.Windows.Forms.Timer _animationTimer;
    private Form? _ownerForm;
    private int _spinnerFrame;
    private string _message = "Đang tải...";

    public LoadingOverlay()
    {
        AccessibleName = "Đang quét tài liệu";
        AccessibleDescription = "Vui lòng chờ trong khi SlideLe quét danh sách slide.";
        AccessibleRole = AccessibleRole.ProgressBar;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = OverlayBackground;
        ControlBox = false;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Opacity = 0.72;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Đang quét tài liệu";

        _animationTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    protected override bool ShowWithoutActivation => true;

    public void ShowLoading(Form owner, string message)
    {
        ArgumentNullException.ThrowIfNull(owner);

        AttachToOwner(owner);
        UpdateMessage(message);
        UpdateOwnerBounds();

        if (!Visible)
        {
            Show(owner);
        }

        BringToFront();
        _animationTimer.Start();
    }

    public void UpdateMessage(string message)
    {
        _message = message;
        AccessibleDescription = message;
        Invalidate();
    }

    public void HideLoading()
    {
        _animationTimer.Stop();
        Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(OverlayBackground);

        float scale = DeviceDpi / 96F;
        int spinnerRadius = (int)(24 * scale);
        Point spinnerCenter = new(ClientSize.Width / 2, (ClientSize.Height / 2) - (int)(20 * scale));
        DrawSpinner(e.Graphics, spinnerCenter, spinnerRadius, scale);

        Rectangle messageBounds = new(
            (int)(24 * scale),
            spinnerCenter.Y + (int)(38 * scale),
            Math.Max(1, ClientSize.Width - (int)(48 * scale)),
            (int)(56 * scale));
        using Font messageFont = new("Segoe UI Semibold", 12F, FontStyle.Regular);
        TextRenderer.DrawText(
            e.Graphics,
            _message,
            messageFont,
            messageBounds,
            MessageColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.WordBreak |
            TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
            DetachFromOwner();
        }

        base.Dispose(disposing);
    }

    private void AttachToOwner(Form owner)
    {
        if (ReferenceEquals(_ownerForm, owner))
        {
            return;
        }

        DetachFromOwner();
        _ownerForm = owner;
        _ownerForm.LocationChanged += OwnerBoundsChanged;
        _ownerForm.SizeChanged += OwnerBoundsChanged;
        _ownerForm.ClientSizeChanged += OwnerBoundsChanged;
        _ownerForm.FormClosed += OwnerFormClosed;
    }

    private void DetachFromOwner()
    {
        if (_ownerForm is null)
        {
            return;
        }

        _ownerForm.LocationChanged -= OwnerBoundsChanged;
        _ownerForm.SizeChanged -= OwnerBoundsChanged;
        _ownerForm.ClientSizeChanged -= OwnerBoundsChanged;
        _ownerForm.FormClosed -= OwnerFormClosed;
        _ownerForm = null;
    }

    private void OwnerBoundsChanged(object? sender, EventArgs e)
    {
        if (Visible)
        {
            UpdateOwnerBounds();
        }
    }

    private void OwnerFormClosed(object? sender, FormClosedEventArgs e)
    {
        HideLoading();
    }

    private void UpdateOwnerBounds()
    {
        if (_ownerForm is null || _ownerForm.IsDisposed || _ownerForm.WindowState == FormWindowState.Minimized)
        {
            return;
        }

        Bounds = _ownerForm.RectangleToScreen(_ownerForm.ClientRectangle);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _spinnerFrame = (_spinnerFrame + 1) % 12;
        Invalidate();
    }

    private void DrawSpinner(Graphics graphics, Point center, int radius, float scale)
    {
        const int segmentCount = 12;
        float penWidth = Math.Max(2F, 4F * scale);

        for (int index = 0; index < segmentCount; index++)
        {
            int age = (index - _spinnerFrame + segmentCount) % segmentCount;
            float intensity = 0.22F + ((segmentCount - 1 - age) / (float)(segmentCount - 1) * 0.78F);
            Color segmentColor = Blend(OverlayBackground, SpinnerColor, intensity);
            double angle = (Math.PI * 2 * index / segmentCount) - (Math.PI / 2);
            float innerRadius = radius * 0.48F;
            float outerRadius = radius;
            PointF start = new(
                center.X + (float)(Math.Cos(angle) * innerRadius),
                center.Y + (float)(Math.Sin(angle) * innerRadius));
            PointF end = new(
                center.X + (float)(Math.Cos(angle) * outerRadius),
                center.Y + (float)(Math.Sin(angle) * outerRadius));

            using Pen segmentPen = new(segmentColor, penWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(segmentPen, start, end);
        }
    }

    private static Color Blend(Color background, Color foreground, float amount)
    {
        int BlendChannel(int backgroundChannel, int foregroundChannel) =>
            (int)Math.Round(backgroundChannel + ((foregroundChannel - backgroundChannel) * amount));

        return Color.FromArgb(
            BlendChannel(background.R, foreground.R),
            BlendChannel(background.G, foreground.G),
            BlendChannel(background.B, foreground.B));
    }
}

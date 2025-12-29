using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.ComponentModel;

namespace DropGoLine {
  public class ModernCard : Panel {
    // === 屬性區 ===

    [Category("Appearance")]
    [Description("設定圓角的弧度")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int BorderRadius { get; set; } = 10;

    [Category("Appearance")]
    [Description("設定邊框的寬度")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int BorderSize { get; set; } = 4;

    // ⚠️ 關鍵改變 1：這是卡片真正的顏色
    // ⚠️ 關鍵改變 1：卡片顏色改為「半透明黑色」，增強對比度
    [Category("Appearance")]
    [Description("設定卡片的背景顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color CardColor { get; set; } = Color.FromArgb(75, 40, 40, 40); // 預設半透明深色

    [Category("Appearance")]
    [Description("設定邊框的顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color BorderColor { get; set; } = Color.FromArgb(50, 255, 255, 255); // 預設微亮邊框

    [Category("Appearance")]
    [Description("設定滑鼠懸停時的邊框顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color HoverBorderColor { get; set; } = Color.FromArgb(100, 255, 255, 255);

    private Color currentBorderColor;

    public event Action<IDataObject> OnDataDrop;
    private bool isDragEnter = false;

    public ModernCard() {
      this.DoubleBuffered = true;
      this.SetStyle(ControlStyles.UserPaint, true);
      this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
      this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
      this.SetStyle(ControlStyles.ResizeRedraw, true);
      // 讓控制項支援透明背景
      this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);

      // ⚠️ 關鍵改變 2：維持背景透明
      this.BackColor = Color.Transparent;
      
      this.AllowDrop = true; // 啟用拖曳

      this.ForeColor = Color.White;
      currentBorderColor = BorderColor;

      this.MouseEnter += (s, e) => { currentBorderColor = HoverBorderColor; this.Invalidate(); };
      this.MouseLeave += (s, e) => { currentBorderColor = BorderColor; this.Invalidate(); };
    }

    protected override void OnDragEnter(DragEventArgs e) {
        base.OnDragEnter(e);
        e.Effect = DragDropEffects.Copy;
        isDragEnter = true;
        this.Invalidate();
    }

    protected override void OnDragLeave(EventArgs e) {
        base.OnDragLeave(e);
        isDragEnter = false;
        this.Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs e) {
        base.OnDragDrop(e);
        isDragEnter = false;
        OnDataDrop?.Invoke(e.Data);
        this.Invalidate();
    }

    // ⚠️ 關鍵改變 3：確保背景繪製正確
    protected override void OnPaintBackground(PaintEventArgs e) {
       // 不呼叫 base，我們自己透過 OnPaint 的 SourceCopy 來處理
    }

    protected override void OnPaint(PaintEventArgs e) {
      // 開啟高品質繪圖
      e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
      e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; 
      // 確保文字不會因為 ClearType 在透明背景上出現破碎 (黑邊或破洞)
      e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

      // 🌟 MAGIC：使用 SourceCopy 模式，強制將 Alpha 值寫入
      e.Graphics.CompositingMode = CompositingMode.SourceCopy;

      // 為了防止邊框被切掉，矩形要縮一點點
      RectangleF rect = new RectangleF(0, 0, this.Width, this.Height);

      // 判斷是否正在拖曳中，調整邊框樣式
      Color targetBorderColor = isDragEnter ? Color.Cyan : currentBorderColor;
      float targetBorderSize = isDragEnter ? BorderSize + 2 : BorderSize;
      DashStyle targetDashStyle = isDragEnter ? DashStyle.Dash : DashStyle.Solid;

      using (GraphicsPath path = GetRoundedPath(rect, BorderRadius))
      using (Pen pen = new Pen(targetBorderColor, targetBorderSize))
      using (SolidBrush brush = new SolidBrush(CardColor))
      {
        pen.DashStyle = targetDashStyle;

        // 1. 填滿半透明區域
        e.Graphics.FillPath(brush, path);
        
        // 切換回正常混合模式畫邊框與文字
        e.Graphics.CompositingMode = CompositingMode.SourceOver;

        // 2. 畫邊框
        if (targetBorderSize > 0)
            e.Graphics.DrawPath(pen, path);

        // 3. 手動繪製文字 (解決 Label 白底問題)
        if (!string.IsNullOrEmpty(Text)) {
            // 設定文字格式 (置中)
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            using (Brush textBrush = new SolidBrush(this.ForeColor)) {
                e.Graphics.DrawString(Text, this.Font, textBrush, rect, sf);
            }
        }
      }
    }

    private GraphicsPath GetRoundedPath(RectangleF rect, float radius) {
      GraphicsPath path = new GraphicsPath();
      float diameter = radius * 2;

      if (diameter > rect.Width)
        diameter = rect.Width;
      if (diameter > rect.Height)
        diameter = rect.Height;

      path.StartFigure();
      path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
      path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
      path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
      path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
      path.CloseFigure();
      return path;
    }
  }
}
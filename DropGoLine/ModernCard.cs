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

    // 新增圖片預覽屬性
    public Image? PreviewImage { get; set; } = null;

    // 進度條屬性 (0.0 - 1.0)
    public float Progress { get; set; } = 0f;
    public Color ProgressColor { get; set; } = Color.FromArgb(100, 0, 255, 0);
    
    // 檔案大小 (用於計算進度)
    public long FileSize { get; set; } = -1;

    // ⚠️ 關鍵改變 1：這是卡片真正的顏色
    [Category("Appearance")]
    [Description("設定卡片的背景顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color CardColor { get; set; } = Color.FromArgb(75, 40, 40, 40); // 預設半透明深色

    [Category("Appearance")]
    [Description("設定邊框的顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color BorderColor { get; set; } = Color.FromArgb(50, 255, 255, 255); // 預設微亮邊框

    [Category("Appearance")]
    [Description("滑鼠懸停時的邊框顏色")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color HoverBorderColor { get; set; } = Color.FromArgb(100, 255, 255, 255);

    private Color currentBorderColor;
    
    public enum ContentType {
        None,
        Text,
        File_Offer,
        File_Request,
        File_Transferring
    }
    
    public ContentType CurrentType { get; private set; } = ContentType.None;

    public event Action<IDataObject>? OnDataDrop;
    public event Action<string>? OnDragRequest;
    
    public void SetContent(string displayText, ContentType type, object? data) {
        this.Text = displayText;
        this.CurrentType = type;
        this.Tag = data; 
        
        // 🌟 FIX 1: 清除舊的圖片預覽，除非稍後被 Form1 再次設定
        // 如果沒有這行，之前傳的圖片會一直卡在卡片上，導致文字訊息顯示錯誤
        this.PreviewImage = null;

        // 根據類型變更樣式 (可選)
        if (type == ContentType.File_Offer) {
           // 例如變更文字顏色或加前綴
        }
        this.Invalidate();
    }
    private bool isDragEnter = false;
    private Point dragStartPoint;
    private bool isMouseDown = false;
    private bool isDragging = false; // 🌟 FIX 2: 新增變數標記是否正在拖曳，若是則阻止 Click

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

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) {
            isMouseDown = true;
            isDragging = false; // Reset
            dragStartPoint = e.Location;
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        // 🌟 FIX 2: 如果剛剛觸發了拖曳，就不要觸發 Click
        if (isDragging) return;
        base.OnMouseClick(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        isMouseDown = false;
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
        // 確保離開時重置顏色並重繪
        currentBorderColor = BorderColor; 
        this.Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs e) {
        base.OnDragDrop(e);
        isDragEnter = false;
        // 確保放下後重置顏色並重繪
        currentBorderColor = BorderColor; 
        OnDataDrop?.Invoke(e.Data);
        this.Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
       // 不呼叫 base，我們自己透過 OnPaint 的 SourceCopy 來處理
    }

    protected override void OnPaint(PaintEventArgs e) {
      // 開啟高品質繪圖
      e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
      e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; 
      e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

      // 🌟 MAGIC：使用 SourceCopy 模式，強制將 Alpha 值寫入
      e.Graphics.CompositingMode = CompositingMode.SourceCopy;
      
      e.Graphics.Clear(Color.Transparent);

      float halfBorder = BorderSize / 2.0f;
      RectangleF rect = new RectangleF(halfBorder, halfBorder, this.Width - BorderSize, this.Height - BorderSize);

      Color targetBorderColor = isDragEnter ? Color.Cyan : currentBorderColor;
      float targetBorderSize = isDragEnter ? BorderSize + 2 : BorderSize;
      
      if (isDragEnter) {
          float extra = 1.0f; 
          rect.Inflate(-extra, -extra);
      }
      
      DashStyle targetDashStyle = isDragEnter ? DashStyle.Dash : DashStyle.Solid;
      
      float minDimension = Math.Min(rect.Width, rect.Height);
      float adjustedRadius = Math.Min(BorderRadius, minDimension / 2 - 1); 
      if (adjustedRadius < 1) adjustedRadius = 1;

      using (GraphicsPath path = GetRoundedPath(rect, adjustedRadius))
      using (Pen pen = new Pen(targetBorderColor, targetBorderSize))
      using (SolidBrush brush = new SolidBrush(CardColor))
      {
        pen.DashStyle = targetDashStyle;

        // 1. 填滿背景
        e.Graphics.FillPath(brush, path);
        
        // 切換回正常混合模式畫邊框與文字/圖片
        e.Graphics.CompositingMode = CompositingMode.SourceOver;

        // 1.5 畫進度條 (如果有)
        if (Progress > 0 && Progress <= 1.0f) {
             // 強化進度條視覺：高度 10px，位於底部
             int h = 10; 
             RectangleF progressRect = new RectangleF(rect.X, rect.Bottom - h, rect.Width * Progress, h);
             
             // 使用 SetClip 確保進度條都在圓角內
             Region oldClip = e.Graphics.Clip;
             e.Graphics.SetClip(path);
             using (SolidBrush pBrush = new SolidBrush(ProgressColor)) {
                 e.Graphics.FillRectangle(pBrush, progressRect);
             }
             // Add border for progress bar?
             using (Pen pPen = new Pen(Color.White, 1)) {
                 e.Graphics.DrawRectangle(pPen, rect.X, rect.Bottom - h, rect.Width, h);
             }
             e.Graphics.Clip = oldClip;
        }

        // 2. 畫圖片預覽 (如果有)
        if (PreviewImage != null) {
            // 設定圖片繪製區域 (保留邊距)
            RectangleF imgRect = rect;
            imgRect.Inflate(-5, -5); 
            
            // 保持比例繪製
            // 計算縮放比例
            float ratioX = imgRect.Width / PreviewImage.Width;
            float ratioY = imgRect.Height / PreviewImage.Height;
            float ratio = Math.Min(ratioX, ratioY);
            
            float w = PreviewImage.Width * ratio;
            float h = PreviewImage.Height * ratio;
            float x = imgRect.X + (imgRect.Width - w) / 2;
            float y = imgRect.Y + (imgRect.Height - h) / 2;

            // 使用 SetClip 確保圖片不會超出圓角
            Region oldClip = e.Graphics.Clip;
            e.Graphics.SetClip(path);
            e.Graphics.DrawImage(PreviewImage, x, y, w, h);
            e.Graphics.Clip = oldClip;
        }

        // 3. 畫邊框
        if (targetBorderSize > 0)
            e.Graphics.DrawPath(pen, path);

        // 4. 繪製文字 (如果有圖片，文字顯示在下方或覆蓋? 簡單起見，沒圖片才畫文字，或者畫在角落)
        // 為了簡單，如果沒有圖片才畫大文字，有圖片則不畫或畫小標題?
        // 使用者希望「圖片顯示在框框」，假設是取代文字。但如果有檔名呢？
        // 暫定：如果有 PreviewImage，就不畫中間的文字。
        if (PreviewImage == null && !string.IsNullOrEmpty(Text)) {
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            sf.Trimming = StringTrimming.EllipsisCharacter;

            using (Brush textBrush = new SolidBrush(this.ForeColor)) {
                e.Graphics.DrawString(Text, this.Font, textBrush, rect, sf);
            }
        }
      }
    }

    // ... (GetRoundedPath)

    // 拖曳邏輯修改
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (isMouseDown && e.Button == MouseButtons.Left) {
            if (Math.Abs(e.X - dragStartPoint.X) > SystemInformation.DragSize.Width ||
                Math.Abs(e.Y - dragStartPoint.Y) > SystemInformation.DragSize.Height) {
                
                isMouseDown = false; 
                isDragging = true; // 🌟 FIX: 標記為拖曳，抑制 Click

                // 判斷拖曳類型
                if (CurrentType == ContentType.File_Offer) {
                     if (Tag is string filePath && System.IO.File.Exists(filePath)) {
                         // 檔案存在 -> 直接拖曳
                         var dataObj = new DataObject(DataFormats.FileDrop, new string[] { filePath });
                         this.DoDragDrop(dataObj, DragDropEffects.Copy);
                     } else {
                        // 檔案不存在 (或 Tag 不是路徑) -> 觸發下載請求
                        string fname = Text.Replace("📄 ", "").Trim();
                        // 觸發事件讓 Form1 處理下載
                        OnDragRequest?.Invoke(fname);
                     }
                }
                else {
                    // 純文字拖曳
                    string contentToDrag = "";
                    if (CurrentType == ContentType.Text && Tag is string text) {
                        contentToDrag = text;
                    } else if (Tag is string s) {
                         contentToDrag = s; // Fallback
                    } else {
                        contentToDrag = Text; 
                    }

                    if (!string.IsNullOrEmpty(contentToDrag)) {
                        this.DoDragDrop(contentToDrag, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                }
            }
        }
    }

    private GraphicsPath GetRoundedPath(RectangleF rect, float radius) {
      GraphicsPath path = new GraphicsPath();
      float diameter = radius * 2;
      
      // GetRoundedPath 內部不再需要縮小 diameter，因為傳入前已經處理過 adjustedRadius
      // 但保留防呆以防萬一
      if (diameter > rect.Width) diameter = rect.Width;
      if (diameter > rect.Height) diameter = rect.Height;

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
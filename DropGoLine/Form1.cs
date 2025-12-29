using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DropGoLine {
  public partial class Form1 : Form {
    // === Windows API & DWM Constants ===
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3;
    private const int DWMWCP_ROUND = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS {
      public int cxLeftWidth;
      public int cxRightWidth;
      public int cyTopHeight;
      public int cyBottomHeight;
    }

    // === Window Resizing Constants ===
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private int resizeBorderWidth = 10;

    // === State Variables ===
    private bool isWin11 = false;
    private Rectangle dragBarRect;
    private bool isDragBarHover = false;
    private bool isDraggingWindow = false;
    private Point dragStartOffset;
    private Color dragBarColorDefault = Color.Gray;
    private Color dragBarColorHover = Color.DodgerBlue;

    // === Animation Variables ===
    private System.Windows.Forms.Timer widthAnimTimer;
    private int targetWindowWidth;
    private int prevCols = 1; // Track previous column count

    private LayoutAnimator animator = new LayoutAnimator();
    private int testCount = 1;


    public Form1() {
      InitializeComponent();
      this.DoubleBuffered = true;
      this.SetStyle(ControlStyles.ResizeRedraw, true);

      // Window Properties
      this.TopMost = true;
      this.ShowInTaskbar = false;
      this.FormBorderStyle = FormBorderStyle.None;
      this.Padding = new Padding(10, 25, 10, 10);

      // Animation Timer Init
      widthAnimTimer = new System.Windows.Forms.Timer();
      widthAnimTimer.Interval = 15; // ~60 FPS
      widthAnimTimer.Tick += WidthAnimTimer_Tick;
    }

    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      isWin11 = Environment.OSVersion.Version.Build >= 22000;

      if (isWin11) {
        this.TransparencyKey = Color.Empty;
        this.BackColor = Color.Black;

        int backdropType = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(this.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(this.Handle, ref margins);

        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
      } else {
        this.BackColor = Color.FromArgb(32, 32, 32);
      }

      int useDarkMode = 1;
      DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

      if (modernCard1 != null) {
        modernCard1.OnDataDrop += HandleDataDrop;
      }

      UpdateMemberLayout(testCount);

      this.Resize += (s, args) => {
        if (!widthAnimTimer.Enabled)
          UpdateMemberLayout(testCount);
      };

      this.Shown += (s, args) => UpdateMemberLayout(testCount);
    }

    protected override void WndProc(ref Message m) {
      if (m.Msg == WM_NCHITTEST) {
        int x = (int)(m.LParam.ToInt64() & 0xFFFF);
        int y = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);
        Point pt = PointToClient(new Point(x, y));
        Size clientSize = ClientSize;

        bool atLeft = pt.X <= resizeBorderWidth;
        bool atRight = pt.X >= clientSize.Width - resizeBorderWidth;
        bool atTop = pt.Y <= resizeBorderWidth;
        bool atBottom = pt.Y >= clientSize.Height - resizeBorderWidth;

        if (atTop && atLeft) { m.Result = (IntPtr)HTTOPLEFT; return; }
        if (atTop && atRight) { m.Result = (IntPtr)HTTOPRIGHT; return; }
        if (atBottom && atLeft) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
        if (atBottom && atRight) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
        if (atLeft) { m.Result = (IntPtr)HTLEFT; return; }
        if (atRight) { m.Result = (IntPtr)HTRIGHT; return; }
        if (atTop) { m.Result = (IntPtr)HTTOP; return; }
        if (atBottom) { m.Result = (IntPtr)HTBOTTOM; return; }
      }
      base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e) {
      base.OnPaint(e);

      int barWidth = 80;
      int barHeight = 5;
      int topPadding = 10;
      dragBarRect = new Rectangle((this.ClientSize.Width - barWidth) / 2, topPadding, barWidth, barHeight);

      Color currentColor = isDragBarHover || isDraggingWindow ? dragBarColorHover : dragBarColorDefault;

      e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
      using (GraphicsPath path = GetRoundedPath(dragBarRect, 2))
      using (SolidBrush brush = new SolidBrush(currentColor)) {
        e.Graphics.FillPath(brush, path);
      }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, int radius) {
      GraphicsPath path = new GraphicsPath();
      int diameter = radius * 2;
      if (diameter > rect.Width) diameter = rect.Width; 
      if (diameter > rect.Height) diameter = rect.Height;

      path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
      path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
      path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
      path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
      path.CloseFigure();
      return path;
    }

    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);

      if (isDraggingWindow && e.Button == MouseButtons.Left) {
        Point currentScreenPos = Cursor.Position;
        this.Location = new Point(currentScreenPos.X - dragStartOffset.X, currentScreenPos.Y - dragStartOffset.Y);
        return;
      }

      Rectangle hitBox = dragBarRect;
      hitBox.Inflate(5, 5);
      bool isHoverNow = hitBox.Contains(e.Location);

      if (isDragBarHover != isHoverNow) {
        isDragBarHover = isHoverNow;
        this.Invalidate(); 
        this.Cursor = isDragBarHover ? Cursors.SizeAll : Cursors.Default;
      }
    }

    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      if (e.Button == MouseButtons.Left && isDragBarHover) {
        isDraggingWindow = true;
        dragStartOffset = e.Location;
      }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
      base.OnMouseUp(e);
      if (isDraggingWindow) {
        isDraggingWindow = false;
        Rectangle hitBox = dragBarRect;
        hitBox.Inflate(5, 5);
        bool isHoverNow = hitBox.Contains(e.Location);
        if (isDragBarHover != isHoverNow) {
          isDragBarHover = isHoverNow;
          this.Invalidate();
          this.Cursor = isDragBarHover ? Cursors.SizeAll : Cursors.Default;
        }
      }
    }

    // === Layout & Logic ===

    private void btnTestLayout_Click(object sender, EventArgs e) {
      testCount++;
      if (testCount > 6) testCount = 0;
      UpdateMemberLayout(testCount);
      btnTestLayout.Text = $"測試: {testCount}";
    }

    private void UpdateMemberLayout(int count) {
      this.MinimumSize = new Size(0, 0);
      this.MaximumSize = new Size(0, 0);

      if (count < 0) return;

      int rows = 1;
      int cols = 1;

      if (count <= 1) { count = (count == 0) ? 0 : 1; rows = 1; cols = 1; }
      else if (count == 2) { rows = 2; cols = 1; }
      else if (count == 3) { rows = 3; cols = 1; }
      else if (count == 4) { rows = 2; cols = 2; }
      else { rows = 3; cols = 2; }

      // Constraints values
      int minW = (cols == 1) ? 200 : 350;
      int maxW = (cols == 1) ? 600 : 1000;
      
      int gap = 10;
      int minCardH = 60;
      int topSectionH = modernCard1.Bottom + gap;
      int memberSectionMinH = (rows * minCardH) + ((rows - 1) * gap) + gap;
      int totalMinH = topSectionH + memberSectionMinH + (this.Height - this.ClientSize.Height);
      
      // Animation Trigger
      bool shouldAnimateWidth = (cols > prevCols && this.Width < minW);
      
      if (shouldAnimateWidth) {
        targetWindowWidth = minW; 
        widthAnimTimer.Start();
      } else {
        if (!widthAnimTimer.Enabled) {
          if (this.Width < minW) this.Width = minW;
          this.MinimumSize = new Size(minW, totalMinH);
          this.MaximumSize = new Size(maxW, 1200);
        }
      }

      prevCols = cols;

      // Layout Calculation
      int w = pnlMembers.Width;
      int h = pnlMembers.Height;

      int totalGapW = (cols - 1) * gap;
      int cardW = (w - totalGapW) / cols;
      
      int topOffset = gap;
      int totalGapH = (rows - 1) * gap;
      int availableH = h - topOffset;
      int cardH = (availableH - totalGapH) / rows;
      if (cardH < 10) cardH = 10;

      // Create Cards
      while (pnlMembers.Controls.Count < count) {
        // 修正：計算新卡片的目標位置，讓它從垂直正下方生成
        int newIndex = pnlMembers.Controls.Count;
        int newRow = newIndex / cols;
        int newCol = newIndex % cols;
        int newTargetX = newCol * (cardW + gap);
        
        ModernCard newCard = new ModernCard();
        newCard.CardColor = Color.FromArgb(100, 50, 50, 50);
        newCard.BorderColor = Color.FromArgb(100, 255, 255, 255);
        newCard.BorderRadius = 8;
        newCard.BorderSize = 1;
        newCard.Size = new Size(cardW, cardH);
        // 設定初始位置為 TargetX 的正下方
        newCard.Location = new Point(newTargetX, h + 50);
        newCard.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        newCard.Text = $"Member {pnlMembers.Controls.Count + 1}";
        pnlMembers.Controls.Add(newCard);
        newCard.BringToFront();
      }

      // Remove Cards
      while (pnlMembers.Controls.Count > count) {
        pnlMembers.Controls.RemoveAt(pnlMembers.Controls.Count - 1);
      }

      // Update positions - Reverse Order 1-2-3-4
      for (int i = 0; i < pnlMembers.Controls.Count; i++) {
        Control card = pnlMembers.Controls[pnlMembers.Controls.Count - 1 - i];
        
        int row = i / cols;
        int col = i % cols;

        int targetX = col * (cardW + gap);
        int targetY = topOffset + row * (cardH + gap);

        Rectangle targetBounds = new Rectangle(targetX, targetY, cardW, cardH);
        
        if (widthAnimTimer.Enabled) {
          card.Bounds = targetBounds;
        } else {
          animator.Animate(card, targetBounds);
        }
      }
    }

    private void WidthAnimTimer_Tick(object sender, EventArgs e) {
      int currentW = this.Width;
      
      this.Invalidate(); 

      if (Math.Abs(targetWindowWidth - currentW) <= 2) {
        this.Width = targetWindowWidth;
        widthAnimTimer.Stop();
        UpdateMemberLayout(testCount);
      } else {
        int nextW = currentW + (int)((targetWindowWidth - currentW) * 0.15);
        if (nextW == currentW) nextW += (targetWindowWidth > currentW) ? 1 : -1;
        this.Width = nextW;
      }
      UpdateMemberLayout(testCount);
    }

    private void HandleDataDrop(IDataObject data) {
      MessageBox.Show("收到拖曳資料", "DragDrop");
    }

    private void Form1_Load(object sender, EventArgs e) { }
    private void 開啟拖曳板ToolStripMenuItem_Click(object sender, EventArgs e) { ShowForm(); }
    private void 關閉拖曳板ToolStripMenuItem_Click(object sender, EventArgs e) { this.Hide(); }
    private void 結束ToolStripMenuItem1_Click(object sender, EventArgs e) { Application.Exit(); }
    private void ShowForm() {
      this.Show();
      this.WindowState = FormWindowState.Normal;
      this.Activate();
    }
    protected override void OnFormClosing(FormClosingEventArgs e) {
      if (e.CloseReason == CloseReason.UserClosing) {
        e.Cancel = true;
        this.Hide();
      } else {
        base.OnFormClosing(e);
      }
    }
    private void modernCard1_Paint(object sender, PaintEventArgs e) { }
  }
}

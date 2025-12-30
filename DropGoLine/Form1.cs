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
    // testCount removed

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

      // Wire up Context Menu Events
      建立連線ToolStripMenuItem.Click += 建立連線ToolStripMenuItem_Click;
      斷開連線ToolStripMenuItem.Click += 斷開連線ToolStripMenuItem_Click;
      其他設定ToolStripMenuItem.Click += 其他設定ToolStripMenuItem_Click;
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

      UpdateMemberLayout();

      this.Resize += (s, args) => {
        if (!widthAnimTimer.Enabled)
          UpdateMemberLayout();
      };

      this.Shown += (s, args) => UpdateMemberLayout();

      // Initialize AppSettings and P2PManager
      string serverIP = AppSettings.Current.ServerIP;
      P2PManager.Instance.OnIDChanged += (s, id) => {
        this.Invoke((MethodInvoker)(() => toolStripMenuItemID.Text = $"ID: {id}"));
      };

      P2PManager.Instance.OnMessageReceived += (s, msg) => {
        this.Invoke((MethodInvoker)(() => {
          if (pnlMembers.Controls.ContainsKey(msg.Sender)) {
            var card = pnlMembers.Controls[msg.Sender] as ModernCard;
            if (card != null) {
              string display = msg.Type == ModernCard.ContentType.Text ? msg.Content : $"📄 {msg.Content}";
              card.SetContent(display, msg.Type, msg.Type == ModernCard.ContentType.File_Offer ? msg.Tag : msg.Content);
            }
          }

          // Handle File Port/Relay Response (Trigger Download)
          if (msg.Type == ModernCard.ContentType.File_Transferring && !string.IsNullOrEmpty(pendingSavePath)) {
            
            if (msg.Tag is string transId) {
                // Relay Mode
                _ = P2PManager.Instance.StartRelayReceiver(transId, pendingSavePath);
            } else if (msg.Tag is int port) {
                // Direct Mode
                 string ip = msg.Content; // Content stores IP in this case
                 _ = P2PManager.Instance.DownloadFileDirect(ip, port, pendingSavePath);
            }

            pendingSavePath = null; // Reset
          }
        }));
      };

      P2PManager.Instance.OnPeerConnected += (s, name) => {
        AddMember(name);
      };

      P2PManager.Instance.OnPeerDisconnected += (s, name) => {
        RemoveMember(name);
      };

      P2PManager.Instance.Initialize(serverIP);
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

        if (atTop && atLeft) {
          m.Result = (IntPtr)HTTOPLEFT;
          return;
        }
        if (atTop && atRight) {
          m.Result = (IntPtr)HTTOPRIGHT;
          return;
        }
        if (atBottom && atLeft) {
          m.Result = (IntPtr)HTBOTTOMLEFT;
          return;
        }
        if (atBottom && atRight) {
          m.Result = (IntPtr)HTBOTTOMRIGHT;
          return;
        }
        if (atLeft) {
          m.Result = (IntPtr)HTLEFT;
          return;
        }
        if (atRight) {
          m.Result = (IntPtr)HTRIGHT;
          return;
        }
        if (atTop) {
          m.Result = (IntPtr)HTTOP;
          return;
        }
        if (atBottom) {
          m.Result = (IntPtr)HTBOTTOM;
          return;
        }
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
      if (diameter > rect.Width)
        diameter = rect.Width;
      if (diameter > rect.Height)
        diameter = rect.Height;

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

    
    private void Form1_Load(object? sender, EventArgs e) { }

    private void AddMember(string name) {
      if (this.InvokeRequired) {
        this.Invoke(new Action<string>(AddMember), name);
        return;
      }

      if (pnlMembers.Controls.ContainsKey(name))
        return;

      ModernCard newCard = new ModernCard();
      newCard.Name = name;
      newCard.CardColor = Color.FromArgb(100, 50, 50, 50);
      newCard.BorderColor = Color.FromArgb(100, 255, 255, 255);
      newCard.BorderRadius = 8;
      newCard.BorderSize = 1;
      newCard.Size = new Size(200, 60);
      newCard.Text = name;
      newCard.Anchor = AnchorStyles.Top | AnchorStyles.Left;
      newCard.Visible = true;

      // Wire up Click Event
      newCard.Click += OnCardClick;

      pnlMembers.Controls.Add(newCard);
      newCard.BringToFront();

      UpdateMemberLayout();
      this.Refresh();
    }

    private void OnCardClick(object? sender, EventArgs e) {
      var card = sender as ModernCard;
      if (card == null || card.CurrentType == ModernCard.ContentType.None)
        return;

      if (card.CurrentType == ModernCard.ContentType.Text) {
        string text = card.Tag as string ?? card.Text;
        Clipboard.SetText(text);
        MessageBox.Show("文字已複製", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); // Can be replaced with Toast later
      } else if (card.CurrentType == ModernCard.ContentType.File_Offer) {
        // Initiate Download Flow
        string fileName = card.Text.Replace("📄 ", "");
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.FileName = fileName;
        if (sfd.ShowDialog() == DialogResult.OK) {
          // Determine source IP from P2PManager? 
          // For this demo, we assume P2PManager knows the IP or we need to ask P2PManager to handle the handshake.
          // Since P2PManager.DownloadFileDirect needs IP/Port, we should trigger a REQUEST first via P2PManager.
          // Or simplified: P2PManager handles the request logic internally if we pass the sender name.

          // Let's implement Request via Broadcast for now
          // "FILE_REQ|FileName"
          P2PManager.Instance.BroadcastDirect(card.Name, $"FILE_REQ|{fileName}");

          // We need to store the save path temporarily until we get FILE_PORT
          // OR simpler: just wait for the FILE_PORT event which contains the IP/Port to download
          // But the save path is needed then.
          // Let's store it in Tag or a temporary dict in Form1? 
          // For simplicity, let's put it in P2PManager or handle it here via a closure if possible? No.
          // Let's use a sync Context or just blocking wait? Blocking bad.
          // Revised logic: P2PManager should expose a method "RequestFile(peerName, fileName, savePathCallback)"
          // But P2PManager is singleton. 

          // Let's keep it simple: 
          // 1. Send REQ.
          // 2. Peer sends PORT.
          // 3. OnMessageReceived (FILE_PORT) -> Check if we have a pending save.
          // This requires state in Form1.
          pendingSavePath = sfd.FileName;
        }
      }
    }

    private string? pendingSavePath; // Temporary state for download

    private void HandleDataDrop(IDataObject data) {
      if (data.GetDataPresent(DataFormats.FileDrop)) {
        string[]? files = (string[]?)data.GetData(DataFormats.FileDrop);
        if (files != null && files.Length > 0) {
            ProcessFileDrop(files[0]);
        }
      } else {
        // Fix: Creating priority for UnicodeText to avoid ANSI corruption (garbled text)
        string text = "";
        if (data.GetDataPresent(DataFormats.UnicodeText)) {
             text = data.GetData(DataFormats.UnicodeText) as string ?? "";
        } else if (data.GetDataPresent(DataFormats.Text)) {
             text = data.GetData(DataFormats.Text) as string ?? "";
        }

        if (!string.IsNullOrEmpty(text)) {
           // Robustness: If text is a valid file path, treat as File Offer
           if (System.IO.File.Exists(text)) {
               ProcessFileDrop(text);
           } else {
               P2PManager.Instance.Broadcast("TEXT", text);
           }
        }
      }
    }

    private void ProcessFileDrop(string path) {
          string fname = System.IO.Path.GetFileName(path);
          long size = new System.IO.FileInfo(path).Length;
          P2PManager.Instance.StartFileServer(path);
          P2PManager.Instance.Broadcast("FILE_OFFER", fname, size.ToString());
    }

    private void RemoveMember(string name) {
      if (this.InvokeRequired) {
        this.Invoke(new Action<string>(RemoveMember), name);
        return;
      }

      if (pnlMembers.Controls.ContainsKey(name)) {
        pnlMembers.Controls.RemoveByKey(name);
        UpdateMemberLayout();
        this.Refresh();
      }
    }

    // === Layout & Logic ===

    private void UpdateMemberLayout() {
      int count = pnlMembers.Controls.Count;
      this.MinimumSize = new Size(0, 0);
      this.MaximumSize = new Size(0, 0);

      if (count == 0)
        return;

      int rows = 1;
      int cols = 1;

      if (count <= 1) {
        rows = 1;
        cols = 1;
      } else if (count == 2) {
        rows = 2;
        cols = 1;
      } else if (count == 3) {
        rows = 3;
        cols = 1;
      } else if (count == 4) {
        rows = 2;
        cols = 2;
      } else {
        rows = 3;
        cols = 2;
      }

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
          if (this.Width < minW)
            this.Width = minW;
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
      if (cardH < 10)
        cardH = 10;

      // Update positions
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

    private void WidthAnimTimer_Tick(object? sender, EventArgs e) {
      int currentW = this.Width;

      this.Invalidate();

      if (Math.Abs(targetWindowWidth - currentW) <= 2) {
        this.Width = targetWindowWidth;
        widthAnimTimer.Stop();
        UpdateMemberLayout();
      } else {
        int nextW = currentW + (int)((targetWindowWidth - currentW) * 0.15);
        if (nextW == currentW)
          nextW += (targetWindowWidth > currentW) ? 1 : -1;
        this.Width = nextW;
      }
      UpdateMemberLayout();
    }

    private void ToolStripMenuItemID_Click(object? sender, EventArgs e) {
      if (!string.IsNullOrEmpty(P2PManager.Instance.CurrentCode)) {
        Clipboard.SetText(P2PManager.Instance.CurrentCode);
        MessageBox.Show("ID 已複製到剪貼簿", "提示");
      }
    }
    private void 開啟拖曳板ToolStripMenuItem_Click(object? sender, EventArgs e) {
      ShowForm();
    }
    private void 關閉拖曳板ToolStripMenuItem_Click(object? sender, EventArgs e) {
      this.Hide();
    }
    private void 建立連線ToolStripMenuItem_Click(object? sender, EventArgs e) {
      using (var form = new ConnectionForm()) {
        form.ShowDialog();
      }
    }

    private void 斷開連線ToolStripMenuItem_Click(object? sender, EventArgs e) {
      P2PManager.Instance.Disconnect(); // Assume Disconnect method exists or just placeholder
      MessageBox.Show("已嘗試斷開連線 (功能待完善)", "訊息");
    }

    private void 其他設定ToolStripMenuItem_Click(object? sender, EventArgs e) {
      using (var form = new SettingsForm()) {
        form.ShowDialog();
      }
    }

    private void 結束ToolStripMenuItem1_Click(object? sender, EventArgs e) {
      Application.Exit();
    }
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
    private void modernCard1_Paint(object sender, PaintEventArgs e) {
    }
  }
}

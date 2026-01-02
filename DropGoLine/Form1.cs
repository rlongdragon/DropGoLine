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

    // === Global Hotkey ===
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private const int MOD_ALT = 0x0001;
    private const int VK_D = 0x44;
    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;

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

    // === Clipboard Logic ===
    private bool isInternalClipboardChange = false;
    private string lastReceivedClipboardText = "";
    private string lastReceivedClipboardHash = "";

    private string ComputeHash(string input) {
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create()) {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    // === Animation Variables ===
    private System.Windows.Forms.Timer widthAnimTimer;
    private int targetWindowWidth;
    private int prevCols = 1; // Track previous column count

    private LayoutAnimator animator = new LayoutAnimator();
    // testCount removed

    public Form1() {
      InitializeComponent();
      // 因為 ModernCard 會繪製 Name，這裡將靜態卡片的 Name 清空以避免顯示 "modernCard1"
      modernCard1.Name = "";

      this.DoubleBuffered = true;
      this.SetStyle(ControlStyles.ResizeRedraw, true);
      this.KeyPreview = true; // Enable Key Handling for ESC

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
      
      try {
          AddClipboardFormatListener(this.Handle);
      } catch { }

      // Register Hotkey Alt+D
      RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_D);

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

      P2PManager.Instance.OnDownloadProgress += (s, args) => {
        this.Invoke((MethodInvoker)(() => {
          // args.Sender is now guaranteed to be the Name of the peer.
          if (pnlMembers.Controls.ContainsKey(args.Sender)) {
            if (pnlMembers.Controls[args.Sender] is ModernCard card) {
              card.Progress = args.Progress;
              card.Invalidate();
            }
          }
        }));
      };

      P2PManager.Instance.OnMessageReceived += (s, msg) => {
        this.Invoke((MethodInvoker)(() => {
          if (pnlMembers.Controls.ContainsKey(msg.Sender)) {
            var card = pnlMembers.Controls[msg.Sender] as ModernCard;
            if (card != null) {
              string display = msg.Type == ModernCard.ContentType.Text ? msg.Content : $"📄 {msg.Content}";
              
              // === Auto Clipboard Copy Logic ===
              if (msg.Type == ModernCard.ContentType.Text && AppSettings.Current.AutoClipboardCopy) {
                  // Avoid re-broadcasting what we just received
                  string receivedHash = ComputeHash(msg.Content);
                  string currentClipboardHash = "";
                  try {
                      if (Clipboard.ContainsText()) {
                          currentClipboardHash = ComputeHash(Clipboard.GetText());
                      }
                  } catch { }

                  // Loop Prevention Logic:
                  // 1. If received content is same as what we just received (duplicate msg), ignore.
                  // 2. If received content is same as what is ALREADY in clipboard (maybe we sent it?), ignore.
                  if (receivedHash != lastReceivedClipboardHash && receivedHash != currentClipboardHash) {
                      lastReceivedClipboardText = msg.Content;
                      lastReceivedClipboardHash = receivedHash;
                      isInternalClipboardChange = true;
                      try {
                          Clipboard.SetText(msg.Content);
                      } catch { }
                  }
              }

              // 若是 FILE_OFFER,把 Size 存入 card.FileSize
              if (msg.Type == ModernCard.ContentType.File_Offer && long.TryParse(msg.Tag as string, out long size)) {
                card.FileSize = size;
              }

              // SetContent 更新文字與類型,但不應影響 FileSize
              // 🌟 FIX: 若是正在傳輸 (File_Transferring)，則保留原本的預覽圖 (keepPreview = true)
              bool keepPreview = (msg.Type == ModernCard.ContentType.File_Transferring);
              card.SetContent(display, msg.Type, msg.Type == ModernCard.ContentType.File_Offer ? msg.Tag : msg.Content, keepPreview);

              if (msg.ExtraData is Image img) {
                card.PreviewImage = img;
                card.Invalidate();
              }
            }
          }

          // Handle File Port/Relay Response (Trigger Download)
          if (msg.Type == ModernCard.ContentType.File_Transferring) {



            // 2. Legacy / SaveDialog Flow
            if (!string.IsNullOrEmpty(pendingSavePath)) {

              // Capture synchronously
              string currentSavePath = pendingSavePath;

              Task.Run(async () => {
                string savedFile = currentSavePath;
                bool success = false;

                try {
                  // Start download
                  // Use the FileSize we stored in the card
                  long size = -1;
                  this.Invoke((MethodInvoker)(() => {
                    if (pnlMembers.Controls[msg.Sender] is ModernCard card) {
                      size = card.FileSize;
                      // Fallback if -1?
                      if (size <= 0 && long.TryParse(card.Tag as string, out long s))
                        size = s;
                    }
                  }));

                  if (msg.Tag is string transId) {
                    // Relay Mode
                    await P2PManager.Instance.StartRelayReceiver(msg.Sender, transId, savedFile, size);
                    success = true;
                  } else if (msg.Tag is int port) {
                    // Direct Mode
                    string ip = msg.Content; // Content stores IP in this case
                    await P2PManager.Instance.DownloadFileDirect(msg.Sender, ip, port, savedFile, size);
                    success = true;
                  }
                } catch { success = false; }

                if (success) {
                  // Update UI or Signal Completion
                  this.Invoke((MethodInvoker)(() => {

                    // Log History for Downloaded File
                    // Determine Type
                    string ext = System.IO.Path.GetExtension(savedFile).ToLower();
                    string fileType = "File";
                    if (ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".bmp") fileType = "Image";
                    
                    HistoryManager.Instance.AddRecord(msg.Sender, true, fileType, System.IO.Path.GetFileName(savedFile), savedFile);

                    // Update Card UI
                    if (pnlMembers.Controls[msg.Sender] is ModernCard card) {
                      card.Tag = savedFile; // Update Tag just in case
                      card.LocalFilePath = savedFile; // 🌟 Set Local Path
                      card.IsDownloaded = true; // 🌟 Mark as Ready
                      card.Progress = 0; // Hide bar
                      card.Invalidate();
                    }
                  }));
                }
              });

              pendingSavePath = null; // Reset
            }
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

      
      if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) {
          if (this.Visible) {
              this.Hide();
          } else {
              this.Show();
              this.WindowState = FormWindowState.Normal;
              this.Activate();
          }
      }
      

      
      if (m.Msg == WM_CLIPBOARDUPDATE) {
          if (AppSettings.Current.AutoClipboardSync) {
             if (isInternalClipboardChange) {
                 isInternalClipboardChange = false; // Reset flag and ignore this update
             } else {
                 try {
                     if (Clipboard.ContainsText()) {
                         string text = Clipboard.GetText();
                         if (!string.IsNullOrEmpty(text)) {
                             string currentHash = ComputeHash(text);
                             if (currentHash != lastReceivedClipboardHash) {
                             // This is a local copy action
                             // Mark as ours to avoid echo if it comes back (conceptually)
                             // But actually we just broadcast.
                             // Update lastReceivedClipboardText so if we receive it back we ignore?
                             // No, if we send it, peers receive it.
                             
                             // Debounce or simple check?
                             // Just broadcast.
                             P2PManager.Instance.Broadcast("TEXT", text);
                         }
                         }
                     }
                 } catch { } // Clipboard busy
             }
          }
      }
      
      base.WndProc(ref m);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) {
            this.Hide();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
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


    private void Form1_Load(object? sender, EventArgs e) {
        // Clear all history on fresh app start as per user request
        HistoryManager.Instance.ClearAllHistory();
    }

    private Dictionary<string, HistoryForm> openHistoryForms = new Dictionary<string, HistoryForm>();

    private void ShowHistory(string peerName) {
        if (openHistoryForms.ContainsKey(peerName)) {
            var existing = openHistoryForms[peerName];
            if (existing.IsDisposed) {
                openHistoryForms.Remove(peerName);
            } else {
                existing.BringToFront();
                existing.Focus();
                return;
            }
        }

        HistoryForm form = new HistoryForm(peerName);
        openHistoryForms[peerName] = form;
        form.FormClosed += (s, e) => {
            if (openHistoryForms.ContainsKey(peerName))
                openHistoryForms.Remove(peerName);
        };
        form.Show(this);
    }

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
      newCard.MouseClick += OnCardClick;

      // 🌟 Wire up Drop Event for Single Peer Transfer
      newCard.OnDataDrop += (data) => HandlePeerDrop(name, data);

      // 🌟 Wire up History Request
      newCard.OnHistoryRequest += (n) => {
          if (this.InvokeRequired) {
              this.Invoke(new Action(() => ShowHistory(n)));
          } else {
              ShowHistory(n);
          }
      };

      pnlMembers.Controls.Add(newCard);
      newCard.BringToFront();

      UpdateMemberLayout();
      this.Refresh();
    }

    private void HandlePeerDrop(string targetName, IDataObject data) {
         string text = "";
         // 1. Check for File Drop
         if (data.GetDataPresent(DataFormats.FileDrop)) {
            string[]? files = (string[]?)data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0) {
               string path = files[0];
               string ext = System.IO.Path.GetExtension(path).ToLower();
               
               if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp") {
                   // Image Direct Offer
                   P2PManager.Instance.SendImageOfferDirect(targetName, path);
               } else {
                   // File Direct Offer (Force Relay for stability or just direct offer?)
                   // Implementation Plan said: StartRelaySender(target, filePath)
                   // But wait, StartRelaySender sends PUSH? No, it starts a channel.
                   // The original "StartRelaySender" sends a FILE_RELAY_READY.
                   // Does it offer? 
                   // Let's check P2PManager.StartRelaySender logic again.
                   // It sends: BroadcastDirect(targetPeer, $"FILE_RELAY_READY|...");
                   // So it notifies the peer to download immediately?
                   // Yes, P2PManager Line 174: BroadcastDirect...
                   // User wants "Offer" model?
                   // No, user said "Drag to specific device". Usually implies sending it.
                   // Existing ProcessFileDrop uses StartFileServer + Broadcast("FILE_OFFER").
                   // If we want "Single Peer Offer", we should behave similar to FILE_OFFER but only to one person.
                   
                   // Let's look at P2PManager again. 
                   // FILE_OFFER expects the receiver to click download? 
                   // Users usually want automatic transfer or at least an offer that only THEY see.
                   
                   // Plan said: "Directly call P2PManager.StartRelaySender(peerName, filePath)"
                   // This pushes the file (or at least the ready signal) via Relay.
                   // This seems robust.
                   
                   _ = Task.Run(() => P2PManager.Instance.StartRelaySender(targetName, path));
               }
               return;
            }
         }
         
         // 2. Check for Text
        if (data.GetDataPresent(DataFormats.UnicodeText)) {
          text = data.GetData(DataFormats.UnicodeText) as string ?? "";
        } else if (data.GetDataPresent(DataFormats.Text)) {
          text = data.GetData(DataFormats.Text) as string ?? "";
        }

        if (!string.IsNullOrEmpty(text)) {
            // Check if it's a file path text
             if (System.IO.File.Exists(text)) {
                 string path = text;
                 string ext = System.IO.Path.GetExtension(path).ToLower();
                  if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp") {
                       P2PManager.Instance.SendImageOfferDirect(targetName, path);
                   } else {
                       _ = Task.Run(() => P2PManager.Instance.StartRelaySender(targetName, path));
                   }
             } else {
                 // Text Message Direct
                 P2PManager.Instance.BroadcastDirect(targetName, $"TEXT|{text}");
             }
        }
    }





    private void OnCardClick(object? sender, MouseEventArgs e) {
      if (e.Button != MouseButtons.Left) return;

      var card = sender as ModernCard;
      if (card == null || card.CurrentType == ModernCard.ContentType.None)
        return;

      if (card.CurrentType == ModernCard.ContentType.Text) {
        string text = card.Tag as string ?? card.Text;
        
        // 🌟 Duplicate Copy Prevention
        string newHash = ComputeHash(text);
        string currentHash = "";
        try {
            if (Clipboard.ContainsText()) {
               currentHash = ComputeHash(Clipboard.GetText());
            }
        } catch {}

        if (newHash != currentHash) {
             Clipboard.SetText(text);
             // Toast logic or simple feedback
        }
      } 
      else if (card.CurrentType == ModernCard.ContentType.File_Offer) {
        // 🌟 Click-to-Download Logic
        if (card.IsDownloaded && !string.IsNullOrEmpty(card.LocalFilePath) && System.IO.File.Exists(card.LocalFilePath)) {
             // Already downloaded -> Open File or Folder?
             // User said: "It becomes a draggable item". So click does nothing or opens location.
             // Let's Open Explorer with file selected
             try {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{card.LocalFilePath}\"");
             } catch {}
             return;
        }

        // Prepare Download
        string fileName = card.Text.Replace("📄 ", "").Trim();
        string downloadsFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DropGoLine");
        if (!System.IO.Directory.Exists(downloadsFolder))
             System.IO.Directory.CreateDirectory(downloadsFolder);
             
        string savePath = System.IO.Path.Combine(downloadsFolder, fileName);
        
        // Prevent overwrite or handle unique names? (Simple overwrite for now as it's a temp-like folder)
        
        // Trigger Download via P2P
        // We need to request the file using P2PManager.
        // But P2PManager.BroadcastDirect triggers the OTHER side to send us a message?
        // Wait, the flow is: We send FILE_REQ -> Peer sends FILE_PORT/RELAY -> We start download.
        // We reuse logic from HandleDragRequest but without the VirtualFileDataObject wrapper.
        
        pendingSavePath = savePath; 
        
        // Send Request to Peer
        P2PManager.Instance.BroadcastDirect(card.Name, $"FILE_REQ|{fileName}");
        
        // Visual Feedback: Indeterminate or 0 progress
        card.Progress = 0.01f; // Show bar
        card.Invalidate();
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
      string ext = System.IO.Path.GetExtension(path).ToLower();
      if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp") {
        // Image Mode
        P2PManager.Instance.SendImageOffer(path);
      } else {
        // Normal File Mode
        string fname = System.IO.Path.GetFileName(path);
        long size = new System.IO.FileInfo(path).Length;
        P2PManager.Instance.StartFileServer(path);
        P2PManager.Instance.Broadcast("FILE_OFFER", fname, size.ToString());
      }
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
        // MessageBox.Show("ID 已複製到剪貼簿", "提示");
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
      P2PManager.Instance.Disconnect();
      
      // 🌟 UI Cleanup
      pnlMembers.Controls.Clear();
      UpdateMemberLayout();
      this.Refresh();
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
        UnregisterHotKey(this.Handle, HOTKEY_ID);
        try { RemoveClipboardFormatListener(this.Handle); } catch { }
        base.OnFormClosing(e);
      }
    }
    private void modernCard1_Paint(object sender, PaintEventArgs e) {
    }
  }
}

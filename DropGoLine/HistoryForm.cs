using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;

namespace DropGoLine {
    public class HistoryForm : Form {
        
        // === Windows API & DWM Constants ===
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_TRANSIENTWINDOW = 3; 
        private const int DWMWCP_ROUND = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        // === Drag Logic ===
        private Rectangle dragBarRect;
        private bool isDragBarHover = false;
        private bool isDraggingWindow = false;
        private Point dragStartOffset;
        private Color dragBarColorDefault = Color.Gray;
        private Color dragBarColorHover = Color.DodgerBlue;

        private string peerName;
        
        // Custom Scroll Containers
        private Panel scrollContainer;
        private FlowLayoutPanel flowPanel;
        
        private bool isWin11 = false;

        public HistoryForm(string peerName) {
            this.peerName = peerName;
            


            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.KeyPreview = true;
            
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            // Matches Form1 Padding somewhat, but we need room for drag bar
            this.Padding = new Padding(10, 30, 10, 10); 

            this.Text = peerName;
            // 2. Reduce Width (Matches Form1 "thin" look)
            this.Size = new Size(320, 500); 
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeCustomScrollLayout();
            LoadHistory(); // Load existing items
            
            HistoryManager.Instance.OnHistoryAdded += OnHistoryAdded;
        }

        private void LoadHistory() {
            var items = HistoryManager.Instance.GetHistory(peerName);
            foreach (var item in items) {
                AddItem(item);
            }
        }

        private void OnHistoryAdded(string peer, HistoryItem item) {
             if (peer == this.peerName) {
                 AddItem(item);
             }
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e) {
             HistoryManager.Instance.OnHistoryAdded -= OnHistoryAdded;
             base.OnFormClosed(e);
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
        }

        private void InitializeCustomScrollLayout() {
            // Container Panel (Clips Content)
            scrollContainer = new Panel();
            scrollContainer.Dock = DockStyle.Fill;
            scrollContainer.BackColor = Color.Transparent;
            scrollContainer.AutoScroll = false; // 🌟 Hide Scrollbar
            
            // Mouse Wheel Logic to scroll flowPanel
            scrollContainer.MouseWheel += (s, e) => {
                PerformScroll(e.Delta);
            };

            // Content Panel
            flowPanel = new FlowLayoutPanel();
            flowPanel.AutoSize = true; 
            flowPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowPanel.FlowDirection = FlowDirection.TopDown;
            flowPanel.WrapContents = false;
            flowPanel.BackColor = Color.Transparent;
            flowPanel.MaximumSize = new Size(this.Width - 20, 0); // Constrain width
            flowPanel.MinimumSize = new Size(this.Width - 20, 0);
            flowPanel.Location = new Point(0, 0);
            
            // Forward MouseWheel from FlowPanel to Container logic
            flowPanel.MouseWheel += (s, e) => {
                PerformScroll(e.Delta);
            };

            scrollContainer.Controls.Add(flowPanel);
            this.Controls.Add(scrollContainer);
        }

        private void PerformScroll(int delta) {
            // Scroll Speed
            int scrollAmount = delta; 
            
            int newTop = flowPanel.Top + scrollAmount;
            
            // Clamp
            // Max Top is 0
            if (newTop > 0) newTop = 0;
            
            // Min Top is (ContainerHeight - ContentHeight)
            int minTop = scrollContainer.Height - flowPanel.Height;
            if (minTop > 0) minTop = 0; // Content smaller than view
            
            if (newTop < minTop) newTop = minTop;

            flowPanel.Top = newTop;
        }

        // Add Record Dynamically (Since we clear on open, we rely on Live Updates)
        // Wait, Form1 doesn't call AddRecord here. Form1 logs to Manager.
        // We need a way to refresh or events?
        // Ah, `HistoryManager` doesn't have an event.
        // But since user wants "Start Empty", we will only see NEW items if we poll or have event.
        // Or did the user mean "Clear PREVIOUS sessions"? 
        // If I keep the window open, I should see incoming messages.
        // But `LoadHistory` is only called at Start.
        // If I want LIVE updates, I need to subscribe to something.
        // Form1.cs has `P2PManager.OnMessageReceived`.
        // I should expose an event in `P2PManager`? Or `HistoryManager`?
        // Or verify if user expects live updates in this window?
        // User said "每次開啟都清除前一次" (Clear previous on open).
        // It implies looking at logs *as they come in*?
        // If I don't hook up live updates, this window will be forever empty after clearing.
        // I MUST hook up live updates.
        // I'll add `OnHistoryAdded` event to `HistoryManager`.
        
        // Temporary: I will Modify HistoryManager to fire event.

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            // Drag Bar Drawing (Same as Form1)
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

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) {
                if (dragBarRect.Contains(e.Location)) {
                    isDraggingWindow = true;
                    dragStartOffset = e.Location; 
                    this.Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (isDraggingWindow && e.Button == MouseButtons.Left) {
                Point currentScreenPos = Cursor.Position;
                this.Location = new Point(currentScreenPos.X - dragStartOffset.X, currentScreenPos.Y - dragStartOffset.Y);
                return;
            }
            bool hover = dragBarRect.Contains(e.Location);
            if (hover != isDragBarHover) {
                isDragBarHover = hover;
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (isDraggingWindow) {
                isDraggingWindow = false;
                this.Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) {
                this.Close();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
        
        // Live Update Hook (Called by Form1 or Manager?)
        // Better: Make public method `AppendItem` and have Form1 call it?
        // Or subscribe to Manager.
        // Let's rely on Reloading? No, flickering.
        // I will add a method `AddItem` and let `HistoryManager` event trigger it.
        
        public void AddItem(HistoryItem item) {
             if (this.InvokeRequired) {
                 this.Invoke(new Action<HistoryItem>(AddItem), item);
                 return;
             }
             
             ModernCard card = new ModernCard();
             // Standard Width
             card.Size = new Size(flowPanel.Width - 10, 80); 
             card.Margin = new Padding(0, 0, 0, 8); 
             card.Name = peerName; 
             
             // Unified Style (Semi-Transparent)
             card.CardColor = Color.FromArgb(100, 50, 50, 50);
             card.BorderColor = Color.FromArgb(100, 255, 255, 255);
             card.BorderSize = 1;
             card.BorderRadius = 8;

             ModernCard.ContentType cType = ModernCard.ContentType.Text;
             string display = item.Content;
             if (item.Type == "File" || item.Type == "Image") {
                 cType = ModernCard.ContentType.File_Offer;
             }

             card.Name = item.Timestamp.ToString("HH:mm");

             object? tagData = item.Content;
             bool isDownloaded = false;
             if (!string.IsNullOrEmpty(item.FilePath)) {
                 card.LocalFilePath = item.FilePath;
                 tagData = item.FilePath;
                 if (File.Exists(item.FilePath)) {
                      isDownloaded = true;
                      if (item.Type == "Image") {
                          try {
                             using (var bmp = new Bitmap(item.FilePath))
                                 card.PreviewImage = new Bitmap(bmp);
                          } catch {}
                      }
                 }
             }

             card.SetContent(display, cType, tagData);
             if (isDownloaded) card.IsDownloaded = true;

             flowPanel.Controls.Add(card);
             
             // Auto Scroll Bottom?
             // If we are at bottom, keep at bottom?
             // Simple: Scroll to it.
             int minTop = scrollContainer.Height - flowPanel.Height;
             if (minTop < 0) flowPanel.Top = minTop;
        }

    }
}

using System;
using System.Drawing;
using System.Runtime.InteropServices; // 這是關鍵，用來呼叫 Windows API
using System.Windows.Forms;


namespace DropGoLine
{
  public partial class Form1 : Form {
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // 定義 DWM 屬性常數
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // 定義背景材質 (0=Auto, 1=None, 2=Mica, 3=Acrylic, 4=MicaAlt)
    private const int DWMSBT_MAINWINDOW = 2; // Mica (雲母)
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic (壓克力/毛玻璃)

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private bool isWin11 = false;

    public Form1() {
      InitializeComponent();
    }

    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      isWin11 = Environment.OSVersion.Version.Build >= 22000;

      if (isWin11) {
        // 強制清除 TransparencyKey，避免與 DWM Glass 衝突
        this.TransparencyKey = Color.Empty;
        // 設定背景全黑，這是 DWM 玻璃效果的關鍵
        this.BackColor = Color.Black;

        // 1. 設定背景材質為 Acrylic (透視窗)
        int backdropType = DWMSBT_TRANSIENTWINDOW; 
        DwmSetWindowAttribute(this.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        
        // 2. 將玻璃邊框延伸到整個視窗
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(this.Handle, ref margins);
      } else {
        // Win10 模式
        this.BackColor = Color.FromArgb(32, 32, 32);
      }

      // 設定深色模式
      int useDarkMode = 1;
      DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

      // 訂閱 ModernCard 的拖曳事件
      if (modernCard1 != null) {
          modernCard1.OnDataDrop += HandleDataDrop;
      }
      
      // 初始化測試狀態
      UpdateMemberLayout(testCount);

      // 處理視窗大小改變
      this.Resize += (s, args) => UpdateMemberLayout(testCount);
      
      // 確保第一次顯示時佈局正確 (修正初次渲染邊距問題)
      this.Shown += (s, args) => {
          UpdateMemberLayout(testCount);
      };
    }

    private void HandleDataDrop(IDataObject data) {
        System.Text.StringBuilder info = new System.Text.StringBuilder();
        info.AppendLine("【收到拖曳資料】");
        info.AppendLine("--------------------------------------------------");

        // 1. 檔案列表
        if (data.GetDataPresent(DataFormats.FileDrop)) {
            string[] files = (string[])data.GetData(DataFormats.FileDrop);
            info.AppendLine($"[檔案] 共 {files.Length} 個：");
            foreach (var file in files) {
                info.AppendLine($" - {System.IO.Path.GetFileName(file)}");
            }
        }

        // 2. 純文字
        if (data.GetDataPresent(DataFormats.Text)) {
            string text = (string)data.GetData(DataFormats.Text);
            info.AppendLine($"[文字]：{text}");
        }
        else if (data.GetDataPresent(DataFormats.UnicodeText)) {
            string text = (string)data.GetData(DataFormats.UnicodeText);
            info.AppendLine($"[文字(Unicode)]：{text}");
        }

        // 3. 圖片
        if (data.GetDataPresent(DataFormats.Bitmap)) {
            Bitmap bmp = (Bitmap)data.GetData(DataFormats.Bitmap);
            info.AppendLine($"[圖片]：{bmp.Width} x {bmp.Height} pixels");
        }

        // 4. HTML
        if (data.GetDataPresent(DataFormats.Html)) {
             string html = (string)data.GetData(DataFormats.Html);
             info.AppendLine($"[HTML]：(長度 {html.Length})");
        }

        // 5. 所有的格式 (Debug 用)
        info.AppendLine("--------------------------------------------------");
        info.AppendLine("可用格式：");
        foreach (var fmt in data.GetFormats()) {
            info.AppendLine($" - {fmt}");
        }

        MessageBox.Show(info.ToString(), "拖曳內容測試");
    }

    private void Form1_Load(object sender, EventArgs e) {
    }

    // 顯示視窗的共用邏輯
    private void ShowForm() {
      this.Show(); // 顯示 Form
      this.WindowState = FormWindowState.Normal; // 恢復正常大小 (如果原本是縮小)
      this.Activate(); // 讓視窗變成焦點
    }

    // ==========================================
    //  Form 視窗行為控制 (進階技巧)
    // ==========================================

    // 讓使用者按右上角 "X" 時，不是真的關閉，而是縮小到右下角
    // (如果不想要這個功能，這段可以拿掉)
    protected override void OnFormClosing(FormClosingEventArgs e) {
      // 檢查關閉原因，如果是使用者按 X (UserClosing)
      if (e.CloseReason == CloseReason.UserClosing) {
        e.Cancel = true; // 取消關閉動作
        this.Hide();     // 只是隱藏視窗

        // 可以選擇性顯示一個氣泡提示 (Balloon Tip)
        // notifyIcon1.ShowBalloonTip(3000, "提示", "程式仍在背景執行中", ToolTipIcon.Info);
      } else {
        base.OnFormClosing(e);
      }
    }

    private void 開啟拖曳板ToolStripMenuItem_Click(object sender, EventArgs e) {
      ShowForm();
    }

    private void 關閉拖曳板ToolStripMenuItem_Click(object sender, EventArgs e) {
      this.Hide();
    }

    private void 結束ToolStripMenuItem1_Click(object sender, EventArgs e) {
      Application.Exit();
    }

    private void textBox1_TextChanged(object sender, EventArgs e) {

    }
    private int testCount = 1;

    private void btnTestLayout_Click(object sender, EventArgs e) {
        testCount++;
        if (testCount > 6) testCount = 0;
        
        UpdateMemberLayout(testCount);
        btnTestLayout.Text = $"測試: {testCount}";
    }

    private LayoutAnimator animator = new LayoutAnimator();

    private void UpdateMemberLayout(int count) {
                // 為了讓視窗可以縮小，先重置最小尺寸限制，避免被之前的限制卡住
        this.MinimumSize = new Size(0, 0); 
        this.MaximumSize = new Size(0, 0); // 也要重置最大限制

        if (count < 0) return; // 防呆

        int w = pnlMembers.Width;
        int h = pnlMembers.Height;
        int gap = 10; // 間距 (與視窗邊緣保持一致)
        
        // --- 策略定義 ---
        int rows = 1;
        int cols = 1;

        if (count <= 1) { count = (count == 0) ? 0 : 1; rows = 1; cols = 1; }
        else if (count == 2) { rows = 2; cols = 1; }
        else if (count == 3) { rows = 3; cols = 1; }
        else if (count == 4) { rows = 2; cols = 2; }
        else { rows = 3; cols = 2; } // 5 or 6

        // --- 尺寸限制 ---
        // 根據不同模式設定視窗的 Min/Max
        int minW = (cols == 1) ? 200 : 350; 
        int maxW = (cols == 1) ? 600 : 1000;

        int minCardH = 60;
        int topSectionH = modernCard1.Bottom + gap; 
        int memberSectionMinH = (rows * minCardH) + ((rows - 1) * gap) + gap; 
        int totalMinH = topSectionH + memberSectionMinH + (this.Height - this.ClientSize.Height); 
        
        this.MinimumSize = new Size(minW, totalMinH);
        this.MaximumSize = new Size(maxW, 1200);

        // --- 排版計算 ---
        int totalGapW = (cols - 1) * gap;
        int cardW = (w - totalGapW) / cols;

        int topOffset = gap; 
        int totalGapH = (rows - 1) * gap;
        int availableH = h - topOffset; 
        int cardH = (availableH - totalGapH) / rows;

        if (cardH < 10) cardH = 10;

        // --- 處理控制項 ---
        
        // 1. 補足不夠的卡片 (新增)
        while (pnlMembers.Controls.Count < count) {
            // 需要先知道這張卡片即將是第幾個，才能算出它的那一行在哪裡
            int index = pnlMembers.Controls.Count; 
            int row = index / cols; 
            int col = index % cols; 
            int targetX = col * (cardW + gap);
            
            ModernCard newCard = new ModernCard();
            newCard.CardColor = Color.FromArgb(100, 50, 50, 50); 
            newCard.BorderColor = Color.FromArgb(100, 255, 255, 255);
            newCard.BorderRadius = 8;
            newCard.BorderSize = 1;
            
            // 初始位置：X 軸對齊目標，Y 軸在視窗下方 -> 垂直滑入
            newCard.Size = new Size(cardW, cardH);
            newCard.Location = new Point(targetX, h + 50); 
            
            newCard.Anchor = AnchorStyles.Top | AnchorStyles.Left; 
            pnlMembers.Controls.Add(newCard);
            // 注意：剛加入的卡片沒有 Name 或特定識別，我們假設 Index 序就是成員序
            newCard.Text = $"Member {pnlMembers.Controls.Count}";
            newCard.BringToFront(); // 確保新加入的在最上層 (或依需求調整 Z-Order)
        }

        // 2. 移除多餘的卡片 (減少)
        while (pnlMembers.Controls.Count > count) {
             // 簡單起見，直接移除最後一個
             // 未來可以做淡出動畫
             pnlMembers.Controls.RemoveAt(pnlMembers.Controls.Count - 1);
        }

        // 3. 更新所有卡片的目標位置並啟動動畫
        for (int i = 0; i < pnlMembers.Controls.Count; i++) {
            // 修正排序問題：
            // Controls[0] 是最新的 (Member N)，Controls[Count-1] 是最舊的 (Member 1)
            // 我們希望 i=0 (左上角) 對應到 Member 1
            Control card = pnlMembers.Controls[pnlMembers.Controls.Count - 1 - i];
            
            int row = i / cols; 
            int col = i % cols; 

            int targetX = col * (cardW + gap);
            int targetY = topOffset + row * (cardH + gap);
            
            Rectangle targetBounds = new Rectangle(targetX, targetY, cardW, cardH);
            
            // 呼叫動畫管理器
            animator.Animate(card, targetBounds);
        }
    }
  }
}

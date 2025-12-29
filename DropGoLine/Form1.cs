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
  }
}

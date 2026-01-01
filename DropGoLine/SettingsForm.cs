using System;
using System.Drawing;
using System.Windows.Forms;

namespace DropGoLine {
  public partial class SettingsForm : Form {
    private Label lblPlaceholder;
    private TextBox txtServerIP;
    private Label lblDeviceName; // Added
    private TextBox txtDeviceName; // Added
    private CheckBox chkAutoCopy;
    private CheckBox chkAutoSync;
    private Button btnSave;

    public SettingsForm() {
      InitializeComponent();
    }

    private void InitializeComponent() {
      this.lblPlaceholder = new System.Windows.Forms.Label();
      this.txtServerIP = new System.Windows.Forms.TextBox();
      this.lblDeviceName = new System.Windows.Forms.Label(); // Added
      this.txtDeviceName = new System.Windows.Forms.TextBox(); // Added
      this.chkAutoCopy = new System.Windows.Forms.CheckBox();
      this.chkAutoSync = new System.Windows.Forms.CheckBox();
      this.btnSave = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // lblPlaceholder
      // 
      this.lblPlaceholder.AutoSize = true;
      this.lblPlaceholder.Location = new System.Drawing.Point(30, 20); // Adjusted Y
      this.lblPlaceholder.Name = "lblPlaceholder";
      this.lblPlaceholder.Size = new System.Drawing.Size(90, 15);
      this.lblPlaceholder.TabIndex = 0; // Adjusted TabIndex
      this.lblPlaceholder.Text = "同步伺服器 IP";
      // 
      // txtServerIP
      // 
      this.txtServerIP.Location = new System.Drawing.Point(33, 40); // Adjusted Y
      this.txtServerIP.Name = "txtServerIP";
      this.txtServerIP.Size = new System.Drawing.Size(200, 23);
      this.txtServerIP.TabIndex = 1; // Adjusted TabIndex
      // 
      // lblDeviceName
      // 
      this.lblDeviceName.AutoSize = true;
      this.lblDeviceName.Location = new System.Drawing.Point(30, 75);
      this.lblDeviceName.Name = "lblDeviceName";
      this.lblDeviceName.Size = new System.Drawing.Size(67, 15);
      this.lblDeviceName.TabIndex = 2; // Added
      this.lblDeviceName.Text = "裝置名稱";
      // 
      // txtDeviceName
      // 
      this.txtDeviceName.Location = new System.Drawing.Point(33, 95);
      this.txtDeviceName.Name = "txtDeviceName";
      this.txtDeviceName.Size = new System.Drawing.Size(200, 23);
      this.txtDeviceName.TabIndex = 3; // Added
      // 
      // chkAutoCopy
      // 
      this.chkAutoCopy.AutoSize = true;
      this.chkAutoCopy.Location = new System.Drawing.Point(33, 130);
      this.chkAutoCopy.Name = "chkAutoCopy";
      this.chkAutoCopy.Size = new System.Drawing.Size(150, 19);
      this.chkAutoCopy.TabIndex = 4;
      this.chkAutoCopy.Text = "接收文字自動複製";
      this.chkAutoCopy.UseVisualStyleBackColor = true;
      // 
      // chkAutoSync
      // 
      this.chkAutoSync.AutoSize = true;
      this.chkAutoSync.Location = new System.Drawing.Point(33, 155);
      this.chkAutoSync.Name = "chkAutoSync";
      this.chkAutoSync.Size = new System.Drawing.Size(150, 19);
      this.chkAutoSync.TabIndex = 5;
      this.chkAutoSync.Text = "本機複製自動發送";
      this.chkAutoSync.UseVisualStyleBackColor = true;

      // 
      // btnSave
      // 
      // 
      // btnSave
      // 
      this.btnSave.Location = new System.Drawing.Point(85, 190); // Adjusted Y
      this.btnSave.Name = "btnSave";
      this.btnSave.Size = new System.Drawing.Size(100, 30);
      this.btnSave.TabIndex = 4; // Adjusted TabIndex
      this.btnSave.Text = "儲存";
      this.btnSave.UseVisualStyleBackColor = true;
      this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
      // 
      // SettingsForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.Controls.Add(this.chkAutoSync);
      this.Controls.Add(this.chkAutoCopy);
      this.Controls.Add(this.btnSave);
      this.AcceptButton = this.btnSave; // Enter to Save
      this.Controls.Add(this.txtDeviceName);
      this.Controls.Add(this.lblDeviceName);
      this.Controls.Add(this.txtServerIP);
      this.Controls.Add(this.lblPlaceholder);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "SettingsForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "其他設定";
      this.ClientSize = new System.Drawing.Size(284, 240);
      this.KeyPreview = true; // Catch ESC
      this.ResumeLayout(false);
      this.PerformLayout();

      // Load Settings
      this.txtServerIP.Text = AppSettings.Current.ServerIP;
      this.txtDeviceName.Text = AppSettings.Current.DeviceName;
      this.chkAutoCopy.Checked = AppSettings.Current.AutoClipboardCopy;
      this.chkAutoSync.Checked = AppSettings.Current.AutoClipboardSync;
    }

    private void BtnSave_Click(object sender, EventArgs e) {
      AppSettings.Current.ServerIP = txtServerIP.Text;
      AppSettings.Current.DeviceName = txtDeviceName.Text;
      AppSettings.Current.AutoClipboardCopy = chkAutoCopy.Checked;
      AppSettings.Current.AutoClipboardSync = chkAutoSync.Checked;
      AppSettings.Current.Save();
      MessageBox.Show("設定已儲存，請重新啟動程式以套用新設定。", "提示");
      this.Close();
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) {
            this.Close();
        }
        base.OnKeyDown(e);
    }
  }
}

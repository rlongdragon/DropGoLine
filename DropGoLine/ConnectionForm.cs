using System;
using System.Drawing;
using System.Windows.Forms;

namespace DropGoLine {
  public partial class ConnectionForm : Form {
    private Label? lblCode;
    private TextBox? txtCode;
    private Button? btnConnect;

    public ConnectionForm() {
      InitializeComponent();
    }

    private void InitializeComponent() {
      this.lblCode = new System.Windows.Forms.Label();
      this.txtCode = new System.Windows.Forms.TextBox();
      this.btnConnect = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // lblCode
      // 
      this.lblCode.AutoSize = true;
      this.lblCode.Location = new System.Drawing.Point(30, 30);
      this.lblCode.Name = "lblCode";
      this.lblCode.Size = new System.Drawing.Size(55, 15);
      this.lblCode.TabIndex = 0;
      this.lblCode.Text = "輸入代碼";
      // 
      // txtCode
      // 
      this.txtCode.Location = new System.Drawing.Point(33, 58);
      this.txtCode.Name = "txtCode";
      this.txtCode.Size = new System.Drawing.Size(200, 23);
      this.txtCode.TabIndex = 1;
      // 
      // btnConnect
      // 
      this.btnConnect.Location = new System.Drawing.Point(85, 100);
      this.btnConnect.Name = "btnConnect";
      this.btnConnect.Size = new System.Drawing.Size(100, 30);
      this.btnConnect.TabIndex = 2;
      this.btnConnect.Text = "建立";
      this.btnConnect.UseVisualStyleBackColor = true;
      this.btnConnect.Click += new System.EventHandler(this.BtnConnect_Click);
      // 
      // ConnectionForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(284, 161);
      this.Controls.Add(this.btnConnect);
      this.Controls.Add(this.txtCode);
      this.Controls.Add(this.lblCode);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "ConnectionForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "建立連線";
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    private void BtnConnect_Click(object? sender, EventArgs e) {
      if (txtCode == null) return;
      string code = txtCode.Text;
      if (!string.IsNullOrWhiteSpace(code)) {
        P2PManager.Instance.Join(code);
        this.DialogResult = DialogResult.OK;
        this.Close();
      } else {
        MessageBox.Show("請輸入代碼", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }
  }
}

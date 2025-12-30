namespace DropGoLine {
  partial class Form1 {
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
      components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
      notifyIcon = new NotifyIcon(components);
      contextMenuStrip1 = new ContextMenuStrip(components);
      toolStripMenuItemID = new ToolStripMenuItem();
      開啟拖曳板ToolStripMenuItem = new ToolStripMenuItem();
      關閉拖曳板ToolStripMenuItem = new ToolStripMenuItem();
      結束ToolStripMenuItem = new ToolStripMenuItem();
      建立連線ToolStripMenuItem = new ToolStripMenuItem();
      斷開連線ToolStripMenuItem = new ToolStripMenuItem();
      其他設定ToolStripMenuItem = new ToolStripMenuItem();
      結束ToolStripMenuItem1 = new ToolStripMenuItem();
      panel1 = new Panel();
      pnlMembers = new Panel();
      modernCard1 = new ModernCard();
      contextMenuStrip1.SuspendLayout();
      panel1.SuspendLayout();
      modernCard1.SuspendLayout();
      SuspendLayout();
      // 
      // notifyIcon
      // 
      notifyIcon.ContextMenuStrip = contextMenuStrip1;
      notifyIcon.Icon = (Icon)resources.GetObject("notifyIcon.Icon");
      notifyIcon.Text = "notifyIcon1";
      notifyIcon.Visible = true;
      // 
      // contextMenuStrip1
      // 
      contextMenuStrip1.ImeMode = ImeMode.NoControl;
      contextMenuStrip1.Items.AddRange(new ToolStripItem[] { toolStripMenuItemID, 開啟拖曳板ToolStripMenuItem, 關閉拖曳板ToolStripMenuItem, 結束ToolStripMenuItem, 結束ToolStripMenuItem1 });
      contextMenuStrip1.Name = "contextMenuStrip1";
      contextMenuStrip1.RenderMode = ToolStripRenderMode.System;
      contextMenuStrip1.Size = new Size(135, 114);
      // 
      // toolStripMenuItemID
      // 
      toolStripMenuItemID.Name = "toolStripMenuItemID";
      toolStripMenuItemID.Size = new Size(134, 22);
      toolStripMenuItemID.Text = "ID: 載入中...";
      toolStripMenuItemID.Click += ToolStripMenuItemID_Click;
      // 
      // 開啟拖曳板ToolStripMenuItem
      // 
      開啟拖曳板ToolStripMenuItem.Name = "開啟拖曳板ToolStripMenuItem";
      開啟拖曳板ToolStripMenuItem.Size = new Size(134, 22);
      開啟拖曳板ToolStripMenuItem.Text = "開啟拖曳板";
      開啟拖曳板ToolStripMenuItem.Click += 開啟拖曳板ToolStripMenuItem_Click;
      // 
      // 關閉拖曳板ToolStripMenuItem
      // 
      關閉拖曳板ToolStripMenuItem.Name = "關閉拖曳板ToolStripMenuItem";
      關閉拖曳板ToolStripMenuItem.Size = new Size(134, 22);
      關閉拖曳板ToolStripMenuItem.Text = "關閉拖曳板";
      關閉拖曳板ToolStripMenuItem.Click += 關閉拖曳板ToolStripMenuItem_Click;
      // 
      // 結束ToolStripMenuItem
      // 
      結束ToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 建立連線ToolStripMenuItem, 斷開連線ToolStripMenuItem, 其他設定ToolStripMenuItem });
      結束ToolStripMenuItem.Name = "結束ToolStripMenuItem";
      結束ToolStripMenuItem.Size = new Size(134, 22);
      結束ToolStripMenuItem.Text = "設定";
      // 
      // 建立連線ToolStripMenuItem
      // 
      建立連線ToolStripMenuItem.Name = "建立連線ToolStripMenuItem";
      建立連線ToolStripMenuItem.Size = new Size(122, 22);
      建立連線ToolStripMenuItem.Text = "建立連線";
      // 
      // 斷開連線ToolStripMenuItem
      // 
      斷開連線ToolStripMenuItem.Name = "斷開連線ToolStripMenuItem";
      斷開連線ToolStripMenuItem.Size = new Size(122, 22);
      斷開連線ToolStripMenuItem.Text = "斷開連線";
      // 
      // 其他設定ToolStripMenuItem
      // 
      其他設定ToolStripMenuItem.Name = "其他設定ToolStripMenuItem";
      其他設定ToolStripMenuItem.Size = new Size(122, 22);
      其他設定ToolStripMenuItem.Text = "其他設定";
      // 
      // 結束ToolStripMenuItem1
      // 
      結束ToolStripMenuItem1.Name = "結束ToolStripMenuItem1";
      結束ToolStripMenuItem1.Size = new Size(134, 22);
      結束ToolStripMenuItem1.Text = "結束";
      結束ToolStripMenuItem1.Click += 結束ToolStripMenuItem1_Click;
      // 
      // panel1
      // 
      panel1.Controls.Add(pnlMembers);
      panel1.Controls.Add(modernCard1);
      panel1.Dock = DockStyle.Fill;
      panel1.Location = new Point(10, 10);
      panel1.Name = "panel1";
      panel1.Size = new Size(318, 430);
      panel1.TabIndex = 1;
      // 
      // pnlMembers
      // 
      pnlMembers.Dock = DockStyle.Fill;
      pnlMembers.Location = new Point(0, 120);
      pnlMembers.Name = "pnlMembers";
      pnlMembers.Size = new Size(318, 310);
      pnlMembers.TabIndex = 2;
      // 
      // 
      // modernCard1
      // 
      modernCard1.AllowDrop = true;
      modernCard1.BackColor = Color.Transparent;
      modernCard1.BorderColor = Color.FromArgb(50, 255, 255, 255);
      modernCard1.BorderRadius = 10;
      modernCard1.BorderSize = 1;
      modernCard1.CardColor = Color.FromArgb(100, 40, 40, 40);
      modernCard1.Dock = DockStyle.Top;
      modernCard1.ForeColor = Color.White;
      modernCard1.HoverBorderColor = Color.FromArgb(100, 255, 255, 255);
      modernCard1.Location = new Point(0, 0);
      modernCard1.Name = "modernCard1";
      modernCard1.Size = new Size(318, 120);
      modernCard1.TabIndex = 1;
      modernCard1.Text = "將選取拖曳到這個區塊";
      modernCard1.Paint += modernCard1_Paint;
      // 
      // btnTestLayout REMOVED
      // 
      // 
      // Form1
      // 
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode = AutoScaleMode.Font;
      BackColor = SystemColors.HighlightText;
      ClientSize = new Size(338, 450);
      Controls.Add(panel1);
      ForeColor = SystemColors.MenuHighlight;
      Name = "Form1";
      Padding = new Padding(10);
      Text = "Form1";
      TransparencyKey = Color.White;
      Load += Form1_Load;
      contextMenuStrip1.ResumeLayout(false);
      panel1.ResumeLayout(false);
      modernCard1.ResumeLayout(false);
      ResumeLayout(false);
    }

    #endregion

    private NotifyIcon notifyIcon;
    private ContextMenuStrip contextMenuStrip1;
    private ToolStripMenuItem toolStripMenuItemID;
    private ToolStripMenuItem 開啟拖曳板ToolStripMenuItem;
    private ToolStripMenuItem 關閉拖曳板ToolStripMenuItem;
    private ToolStripMenuItem 結束ToolStripMenuItem;
    private ToolStripMenuItem 結束ToolStripMenuItem1;
    private ToolStripMenuItem 建立連線ToolStripMenuItem;
    private ToolStripMenuItem 斷開連線ToolStripMenuItem;
    private ToolStripMenuItem 其他設定ToolStripMenuItem;
    private Panel panel1;
    private Panel pnlMembers;
    private ModernCard modernCard1;
  }
}

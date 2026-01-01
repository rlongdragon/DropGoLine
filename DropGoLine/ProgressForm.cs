using System;
using System.Windows.Forms;
using System.Drawing;

namespace DropGoLine {
    public class ProgressForm : Form {
        private ProgressBar progressBar;
        private Label lblStatus;

        public ProgressForm(string fileName) {
            this.Text = "Downloading File...";
            this.Size = new Size(350, 120);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ControlBox = false; // No close button
            this.TopMost = true;

            lblStatus = new Label() {
                Text = $"Preparing: {fileName}",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            progressBar = new ProgressBar() {
                Location = new Point(15, 45),
                Size = new Size(300, 25),
                Style = ProgressBarStyle.Marquee, // Default to marquee until progress starts
                Maximum = 100
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(progressBar);
        }

        public void UpdateProgress(float progress) { // 0.0 to 1.0
            if (this.IsDisposed) return;
            
            if (this.InvokeRequired) {
                this.Invoke(new Action(() => UpdateProgress(progress)));
                return;
            }

            int val = (int)(progress * 100);
            if (val < 0) {
                 if (progressBar.Style != ProgressBarStyle.Marquee)
                    progressBar.Style = ProgressBarStyle.Marquee;
                 lblStatus.Text = "Waiting for connection...";
            } else {
                 if (progressBar.Style != ProgressBarStyle.Continuous)
                    progressBar.Style = ProgressBarStyle.Continuous;
                 
                 progressBar.Value = Math.Min(100, Math.Max(0, val));
                 lblStatus.Text = $"Downloading: {val}%";
            }
        }
    }
}

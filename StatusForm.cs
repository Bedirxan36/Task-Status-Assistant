using System.Drawing;
using System.Windows.Forms;

namespace TrayStatusHelper
{
    public class StatusForm : Form
    {
        private readonly Label _lbl;

        public StatusForm()
        {
            Text = "Task Status Assistant";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;

            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.Gainsboro;
            ClientSize = new Size(320, 160);

            _lbl = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Padding = new Padding(14),
            };

            Controls.Add(_lbl);
        }

        public void UpdateText(bool numOn, bool capsOn, bool fanOn, bool notifOn, bool autostartOn)
        {
            _lbl.Text =
                $"Num Lock: {(numOn ? "AÇIK ✅" : "KAPALI ❌")}\n" +
                $"Caps Lock: {(capsOn ? "AÇIK ✅" : "KAPALI ❌")}\n" +
                $"Fan (Fn+1): {(fanOn ? "AÇIK ✅" : "KAPALI ❌")}\n\n" +
                $"Bildirimler: {(notifOn ? "AÇIK" : "KAPALI")}\n" +
                $"Otomatik Başlat: {(autostartOn ? "AÇIK" : "KAPALI")}";
        }
    }
}

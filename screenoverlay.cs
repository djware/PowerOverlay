using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OverlayApp
{
    static class Program
    {
        private static OverlayForm form;
        private static TouchDetector detector;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form = new OverlayForm();
            detector = new TouchDetector(form);
            Application.Run(form);
        }
    }

    public class TouchDetector
    {
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public const int VK_LBUTTON = 0x01;
        private static readonly int TopScreenTolerance = 20;
        private static readonly int DragThreshold = 50;

        private static bool isMouseDown = false;
        private static int startY = 0;

        private OverlayForm _form;

        public TouchDetector(OverlayForm form)
        {
            _form = form;
            StartTouchDetection();
        }

        private void StartTouchDetection()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                    {
                        int y = Cursor.Position.Y;
                        if (y < TopScreenTolerance)
                        {
                            if (!isMouseDown)
                            {
                                isMouseDown = true;
                                startY = y;
                            }
                        }
                        else
                        {
                            if (isMouseDown && y - startY > DragThreshold)
                            {
                                _form.ToggleVisibility();
                                isMouseDown = false;
                            }
                        }
                    }
                    else
                    {
                        isMouseDown = false;
                    }
                    Thread.Sleep(16);
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }
    }

    public class OverlayForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int HWND_TOPMOST = -1;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_L = 0x4C;
        private const int HOTKEY_ID = 9000;

        private System.Windows.Forms.Timer _timer;
        private string _displayText;
        private bool _isVisible;

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.TransparencyKey = this.BackColor;

            _timer = new System.Windows.Forms.Timer()
            {
                Interval = 1000,
                Enabled = true,
            };
            _timer.Tick += Timer_Tick;

            _isVisible = true;

            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_SHIFT, VK_L);
            this.FormClosing += (s, e) => UnregisterHotKey(this.Handle, HOTKEY_ID);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Font font = new Font("Arial", 14))
            using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(128, Color.Gray)))
            using (Brush fontBrush = new SolidBrush(Color.Green))
            using (Pen pen = new Pen(Color.Black, 2))
            {
                if (_isVisible)
                {
                    e.Graphics.FillRectangle(backgroundBrush, 10, 10, e.Graphics.MeasureString(_displayText, font).Width, font.Height);
                    e.Graphics.DrawString(_displayText, font, fontBrush, 10, 10);
                    e.Graphics.DrawRectangle(pen, 10, 10, e.Graphics.MeasureString(_displayText, font).Width, font.Height);
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isVisible)
                return;

            string time = DateTime.Now.ToString("h:mm tt");

            PowerStatus powerStatus = SystemInformation.PowerStatus;
            string batteryStatus = (powerStatus.BatteryLifePercent * 100).ToString();
            int batteryTimeMinutes = (int)(powerStatus.BatteryLifeRemaining / 60);
            int batteryTimeHours = batteryTimeMinutes / 60;
            int batteryTimeRemainingMinutes = batteryTimeMinutes % 60;
            string batteryTime;

            if (powerStatus.PowerLineStatus == PowerLineStatus.Online)
            {
                batteryTime = "- Charging";
            }
            else
            {
                batteryTime = batteryTimeHours + "H " + batteryTimeRemainingMinutes + " mins";
            }

            _displayText = time + ", " + batteryStatus + "%, " + batteryTime;

            this.Invalidate();
        }

        public void ToggleVisibility()
        {
            _isVisible = !_isVisible;
            UpdateWindowPosition();
            this.Invalidate();
        }

        private void UpdateWindowPosition()
        {
            const int flags = SWP_NOMOVE | SWP_NOSIZE;
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, flags);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
            {
                ToggleVisibility();
            }
        }
    }
}

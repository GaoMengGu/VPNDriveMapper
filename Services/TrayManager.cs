using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VPNDriveMapper.Services
{
    public enum TrayMenuIconKind
    {
        StatusConnected,
        StatusDisconnected,
        Vpn,
        Ip,
        Clock,
        Drive,
        Check,
        Map,
        Disconnect,
        Add,
        Edit,
        Delete,
        Settings,
        Power,
        Exit
    }

    public class TrayManager : IDisposable
    {
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private Icon _currentIcon;

        public event EventHandler DoubleClickRequested;
        public event EventHandler ExitRequested;

        public TrayManager()
        {
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = false,
                Text = "VPN共享盘映射工具"
            };

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            UpdateStatus(false, 0, 0);
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            if (DoubleClickRequested != null)
            {
                DoubleClickRequested(this, EventArgs.Empty);
            }
        }

        public void SetContextMenu(ContextMenuStrip contextMenu)
        {
            ContextMenuStrip previousMenu = _contextMenu;
            _contextMenu = contextMenu;

            if (_notifyIcon != null)
            {
                _notifyIcon.ContextMenuStrip = _contextMenu;
            }

            if (previousMenu != null && previousMenu != _contextMenu)
            {
                previousMenu.Dispose();
            }
        }

        public ContextMenuStrip CreateStyledMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(250, 252, 253);
            menu.ForeColor = Color.FromArgb(32, 43, 54);
            menu.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            menu.Padding = new Padding(6, 7, 6, 7);
            menu.ImageScalingSize = new Size(16, 16);
            menu.ShowImageMargin = true;
            menu.Renderer = new TrayMenuRenderer();
            return menu;
        }

        public ToolStripMenuItem CreateMenuItem(string text, TrayMenuIconKind iconKind)
        {
            return new ToolStripMenuItem(text, CreateMenuImage(iconKind));
        }

        public ToolStripMenuItem CreateHeaderItem(string text, TrayMenuIconKind iconKind)
        {
            ToolStripMenuItem item = CreateMenuItem(text, iconKind);
            item.Enabled = false;
            item.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            return item;
        }

        public ToolStripMenuItem CreateExitMenuItem()
        {
            ToolStripMenuItem exitItem = CreateMenuItem("退出", TrayMenuIconKind.Exit);
            exitItem.Click += (s, e) =>
            {
                if (ExitRequested != null)
                {
                    ExitRequested(this, EventArgs.Empty);
                }
            };
            return exitItem;
        }

        public void UpdateStatus(bool isConnected, int configuredMappings, int mappedMappings)
        {
            string statusText = isConnected ? "已连接" : "未连接";
            string text = string.Format("VPN共享盘映射工具 - {0} - 映射 {1}/{2}",
                statusText,
                mappedMappings,
                configuredMappings);

            if (_notifyIcon != null)
            {
                _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            }

            UpdateIcon(isConnected, configuredMappings, mappedMappings);
        }

        public void UpdateIcon(bool isConnected, int configuredMappings, int mappedMappings)
        {
            if (_notifyIcon == null) return;

            Icon previousIcon = _currentIcon;
            _currentIcon = CreateIcon(isConnected, configuredMappings, mappedMappings);
            _notifyIcon.Icon = _currentIcon;

            if (previousIcon != null)
            {
                previousIcon.Dispose();
            }
        }

        private Icon CreateIcon(bool isConnected, int configuredMappings, int mappedMappings)
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color baseColor;
                if (!isConnected)
                {
                    baseColor = Color.FromArgb(88, 98, 111);
                }
                else if (configuredMappings > 0 && mappedMappings < configuredMappings)
                {
                    baseColor = Color.FromArgb(229, 125, 23);
                }
                else
                {
                    baseColor = Color.FromArgb(22, 128, 91);
                }

                using (GraphicsPath background = RoundedRectangle(new Rectangle(2, 2, 28, 28), 7))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(2, 2, 28, 28),
                    ControlPaint.Light(baseColor, 0.20f),
                    ControlPaint.Dark(baseColor, 0.08f),
                    LinearGradientMode.ForwardDiagonal))
                {
                    g.FillPath(brush, background);
                }

                using (Pen shinePen = new Pen(Color.FromArgb(65, Color.White), 1))
                {
                    g.DrawArc(shinePen, 6, 5, 17, 11, 205, 95);
                }

                using (Pen pen = new Pen(Color.White, 2.2f))
                {
                    if (!isConnected)
                    {
                        DrawDriveGlyph(g, Color.White);
                        g.DrawLine(pen, 10, 9, 23, 22);
                    }
                    else
                    {
                        DrawDriveGlyph(g, Color.White);
                        using (Pen linkPen = new Pen(Color.White, 1.8f))
                        {
                            g.DrawArc(linkPen, 10, 7, 12, 10, 210, 120);
                            g.DrawArc(linkPen, 8, 5, 16, 14, 210, 120);
                        }
                    }
                }

                if (configuredMappings > 0)
                {
                    string countText = mappedMappings > 9 ? "9+" : mappedMappings.ToString();
                    using (Brush badgeBrush = new SolidBrush(Color.FromArgb(250, 252, 253)))
                    using (Brush textBrush = new SolidBrush(baseColor))
                    using (Font font = new Font("Arial", countText.Length > 1 ? 6.5f : 8.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        g.FillEllipse(badgeBrush, 18, 18, 11, 11);
                        SizeF textSize = g.MeasureString(countText, font);
                        g.DrawString(countText, font, textBrush, 23.5f - textSize.Width / 2, 23.2f - textSize.Height / 2);
                    }
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            bitmap.Dispose();
            return icon;
        }

        private static void DrawDriveGlyph(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 2f))
            using (Brush brush = new SolidBrush(Color.FromArgb(230, color)))
            using (GraphicsPath body = RoundedRectangle(new Rectangle(8, 16, 16, 8), 2))
            {
                g.DrawPath(pen, body);
                g.FillEllipse(brush, 19, 19, 2, 2);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Image CreateMenuImage(TrayMenuIconKind iconKind)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color green = Color.FromArgb(22, 128, 91);
                Color blue = Color.FromArgb(30, 111, 214);
                Color gray = Color.FromArgb(94, 108, 121);
                Color orange = Color.FromArgb(229, 125, 23);
                Color red = Color.FromArgb(202, 55, 55);

                switch (iconKind)
                {
                    case TrayMenuIconKind.StatusConnected:
                        DrawCircleIcon(g, green, true);
                        break;
                    case TrayMenuIconKind.StatusDisconnected:
                        DrawCircleIcon(g, gray, false);
                        break;
                    case TrayMenuIconKind.Vpn:
                        DrawShieldIcon(g, blue);
                        break;
                    case TrayMenuIconKind.Ip:
                        DrawTextIcon(g, blue, "IP");
                        break;
                    case TrayMenuIconKind.Clock:
                        DrawClockIcon(g, gray);
                        break;
                    case TrayMenuIconKind.Drive:
                        DrawSmallDriveIcon(g, green);
                        break;
                    case TrayMenuIconKind.Check:
                        DrawCircleIcon(g, blue, true);
                        break;
                    case TrayMenuIconKind.Map:
                        DrawArrowIcon(g, green, true);
                        break;
                    case TrayMenuIconKind.Disconnect:
                        DrawArrowIcon(g, orange, false);
                        break;
                    case TrayMenuIconKind.Add:
                        DrawPlusIcon(g, green);
                        break;
                    case TrayMenuIconKind.Edit:
                        DrawPencilIcon(g, blue);
                        break;
                    case TrayMenuIconKind.Delete:
                        DrawDeleteIcon(g, red);
                        break;
                    case TrayMenuIconKind.Settings:
                        DrawGearIcon(g, gray);
                        break;
                    case TrayMenuIconKind.Power:
                        DrawPowerIcon(g, green);
                        break;
                    case TrayMenuIconKind.Exit:
                        DrawExitIcon(g, red);
                        break;
                }
            }

            return bitmap;
        }

        private static void DrawCircleIcon(Graphics g, Color color, bool check)
        {
            using (Brush brush = new SolidBrush(color))
            using (Pen pen = new Pen(Color.White, 1.6f))
            {
                g.FillEllipse(brush, 2, 2, 12, 12);
                if (check)
                {
                    g.DrawLine(pen, 5, 8, 7, 10);
                    g.DrawLine(pen, 7, 10, 11, 5);
                }
                else
                {
                    g.DrawLine(pen, 5, 5, 11, 11);
                    g.DrawLine(pen, 11, 5, 5, 11);
                }
            }
        }

        private static void DrawShieldIcon(Graphics g, Color color)
        {
            Point[] points = new Point[]
            {
                new Point(8, 1),
                new Point(13, 3),
                new Point(12, 9),
                new Point(8, 14),
                new Point(4, 9),
                new Point(3, 3)
            };
            using (Brush brush = new SolidBrush(color))
            using (Pen pen = new Pen(Color.White, 1.3f))
            {
                g.FillPolygon(brush, points);
                g.DrawLine(pen, 5, 8, 7, 10);
                g.DrawLine(pen, 7, 10, 11, 5);
            }
        }

        private static void DrawTextIcon(Graphics g, Color color, string text)
        {
            using (GraphicsPath path = RoundedRectangle(new Rectangle(1, 3, 14, 10), 3))
            using (Brush brush = new SolidBrush(color))
            using (Brush textBrush = new SolidBrush(Color.White))
            using (Font font = new Font("Arial", 6.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.FillPath(brush, path);
                SizeF size = g.MeasureString(text, font);
                g.DrawString(text, font, textBrush, 8 - size.Width / 2, 8 - size.Height / 2);
            }
        }

        private static void DrawClockIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 1.6f))
            {
                g.DrawEllipse(pen, 2.5f, 2.5f, 11, 11);
                g.DrawLine(pen, 8, 8, 8, 5);
                g.DrawLine(pen, 8, 8, 11, 9);
            }
        }

        private static void DrawSmallDriveIcon(Graphics g, Color color)
        {
            using (GraphicsPath path = RoundedRectangle(new Rectangle(2, 5, 12, 8), 2))
            using (Pen pen = new Pen(color, 1.6f))
            using (Brush brush = new SolidBrush(Color.FromArgb(35, color)))
            using (Brush dotBrush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
                g.FillEllipse(dotBrush, 10, 9, 2, 2);
            }
        }

        private static void DrawArrowIcon(Graphics g, Color color, bool forward)
        {
            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (forward)
                {
                    g.DrawLine(pen, 3, 8, 12, 8);
                    g.DrawLine(pen, 9, 5, 12, 8);
                    g.DrawLine(pen, 9, 11, 12, 8);
                }
                else
                {
                    g.DrawLine(pen, 13, 8, 4, 8);
                    g.DrawLine(pen, 7, 5, 4, 8);
                    g.DrawLine(pen, 7, 11, 4, 8);
                }
            }
        }

        private static void DrawPlusIcon(Graphics g, Color color)
        {
            using (Brush brush = new SolidBrush(color))
            using (Pen pen = new Pen(Color.White, 1.7f))
            {
                g.FillEllipse(brush, 2, 2, 12, 12);
                g.DrawLine(pen, 8, 5, 8, 11);
                g.DrawLine(pen, 5, 8, 11, 8);
            }
        }

        private static void DrawPencilIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 2f))
            {
                g.DrawLine(pen, 4, 12, 12, 4);
                g.DrawLine(pen, 3, 13, 6, 12);
            }
        }

        private static void DrawDeleteIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 1.6f))
            {
                g.DrawLine(pen, 5, 5, 11, 11);
                g.DrawLine(pen, 11, 5, 5, 11);
            }
        }

        private static void DrawGearIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 1.6f))
            {
                g.DrawEllipse(pen, 4, 4, 8, 8);
                g.DrawEllipse(pen, 7, 7, 2, 2);
                g.DrawLine(pen, 8, 1, 8, 4);
                g.DrawLine(pen, 8, 12, 8, 15);
                g.DrawLine(pen, 1, 8, 4, 8);
                g.DrawLine(pen, 12, 8, 15, 8);
            }
        }

        private static void DrawPowerIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 1.8f))
            {
                g.DrawArc(pen, 3, 4, 10, 10, 130, 280);
                g.DrawLine(pen, 8, 2, 8, 8);
            }
        }

        private static void DrawExitIcon(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 1.8f))
            {
                g.DrawRectangle(pen, 2, 3, 8, 10);
                g.DrawLine(pen, 8, 8, 14, 8);
                g.DrawLine(pen, 11, 5, 14, 8);
                g.DrawLine(pen, 11, 11, 14, 8);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public void Show()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }

        public void Hide()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon icon)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(3000, title, text, icon);
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }

            if (_contextMenu != null)
            {
                _contextMenu.Dispose();
                _contextMenu = null;
            }
        }

        private class TrayMenuRenderer : ToolStripProfessionalRenderer
        {
            public TrayMenuRenderer()
                : base(new TrayMenuColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected && e.Item.Enabled)
                {
                    Rectangle bounds = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
                    using (GraphicsPath path = RoundedRectangle(bounds, 4))
                    using (Brush brush = new SolidBrush(Color.FromArgb(232, 241, 252)))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillPath(brush, path);
                    }
                }
                else
                {
                    base.OnRenderMenuItemBackground(e);
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(30, e.Item.Height / 2, e.Item.Width - 38, 1);
                using (Pen pen = new Pen(Color.FromArgb(222, 229, 235)))
                {
                    e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
                }
            }
        }

        private class TrayMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground
            {
                get { return Color.FromArgb(250, 252, 253); }
            }

            public override Color ImageMarginGradientBegin
            {
                get { return Color.FromArgb(242, 246, 249); }
            }

            public override Color ImageMarginGradientMiddle
            {
                get { return Color.FromArgb(242, 246, 249); }
            }

            public override Color ImageMarginGradientEnd
            {
                get { return Color.FromArgb(242, 246, 249); }
            }

            public override Color MenuBorder
            {
                get { return Color.FromArgb(202, 213, 224); }
            }

            public override Color MenuItemBorder
            {
                get { return Color.FromArgb(187, 211, 241); }
            }
        }
    }
}

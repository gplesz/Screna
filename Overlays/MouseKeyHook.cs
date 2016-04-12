using Screna.Native;
using System.Drawing;
using System.Windows.Forms;

namespace Screna
{
    /// <summary>
    /// Draws MouseClicks and/or Keystrokes on an Image
    /// </summary>
    public class MouseKeyHook : IOverlay
    {
        MouseListener _clickHook;
        KeyListener _keyHook;

        bool _mouseClicked,
            _control,
            _shift,
            _alt;

        Keys _lastKeyPressed = Keys.None;

        public Pen ClickStrokePen { get; set; }
        public double ClickRadius { get; set; }
        public Font KeyStrokeFont { get; set; }
        public Brush KeyStrokeBrush { get; set; }
        public Point KeyStrokeLocation { get; set; }

        public MouseKeyHook(bool CaptureMouseClicks, bool CaptureKeystrokes)
        {
            ClickStrokePen = new Pen(Color.Black, 1);
            ClickRadius = 40;
            KeyStrokeFont = new Font(FontFamily.GenericMonospace, 60);
            KeyStrokeBrush = Brushes.Black;
            KeyStrokeLocation = new Point(100, 100);

            if (CaptureMouseClicks)
            {
                _clickHook = new MouseListener();
                _clickHook.MouseDown += (s, e) => _mouseClicked = true;
            }

            if (!CaptureKeystrokes)
                return;

            _keyHook = new KeyListener();
            _keyHook.KeyDown += OnKeyPressed;
        }

        void OnKeyPressed(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Shift:
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    _lastKeyPressed = Keys.Shift;
                    break;

                case Keys.Control:
                case Keys.ControlKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                    _lastKeyPressed = Keys.Control;
                    break;

                case Keys.Alt:
                case Keys.Menu:
                case Keys.LMenu:
                case Keys.RMenu:
                    _lastKeyPressed = Keys.Alt;
                    break;

                default:
                    _lastKeyPressed = e.KeyCode;
                    break;
            }

            _control = e.Control;
            _shift = e.Shift;
            _alt = e.Alt;
        }

        public void Draw(Graphics g, Point Offset = default(Point))
        {
            if (_mouseClicked)
            {
                var curPos = MouseCursor.CursorPosition;
                var d = (float)(ClickRadius * 2);

                g.DrawArc(ClickStrokePen,
                    curPos.X - 40 - Offset.X,
                    curPos.Y - 40 - Offset.Y,
                    d, d,
                    0, 360);

                _mouseClicked = false;
            }

            if (_lastKeyPressed == Keys.None)
                return;

            string toWrite = null;

            if (_control) toWrite += "Ctrl+";
            if (_shift) toWrite += "Shift+";
            if (_alt) toWrite += "Alt+";

            toWrite += _lastKeyPressed.ToString();

            g.DrawString(toWrite,
                KeyStrokeFont,
                KeyStrokeBrush,
                KeyStrokeLocation.X,
                KeyStrokeLocation.Y);

            _lastKeyPressed = Keys.None;
        }

        public void Dispose()
        {
            if (_clickHook != null)
            {
                _clickHook.Dispose();
                _clickHook = null;
            }

            if (_keyHook == null)
                return;

            _keyHook.Dispose();
            _keyHook = null;
        }
    }
}

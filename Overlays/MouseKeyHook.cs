using Screna.Native;
using System.Drawing;
using System.Windows.Forms;

namespace Screna
{
    /// <summary>
    /// Draws Mouse Clicks and/or Keystrokes on an Image.
    /// </summary>
    public class MouseKeyHook : IOverlay
    {
        #region Fields
        MouseListener _clickHook;
        KeyListener _keyHook;

        bool _mouseClicked,
            _control,
            _shift,
            _alt;

        Keys _lastKeyPressed = Keys.None;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or Sets the <see cref="Brush"/> used to fill Click circles.
        /// </summary>
        public Brush ClickBrush { get; set; }

        /// <summary>
        /// Gets or Sets the radius of Click circles.
        /// </summary>
        public double ClickRadius { get; set; }

        /// <summary>
        /// Gets or Sets the Keystroke <see cref="Font"/>.
        /// </summary>
        public Font KeyStrokeFont { get; set; }

        /// <summary>
        /// Gets or Sets the Keystroke <see cref="Brush"/>.
        /// </summary>
        public Brush KeyStrokeBrush { get; set; }

        /// <summary>
        /// Gets or Sets the Keystroke Location.
        /// </summary>
        public Point KeyStrokeLocation { get; set; }
        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="MouseKeyHook"/>.
        /// </summary>
        /// <param name="CaptureMouseClicks">Whether to capture Mouse CLicks.</param>
        /// <param name="CaptureKeystrokes">Whether to capture Keystrokes.</param>
        public MouseKeyHook(bool CaptureMouseClicks, bool CaptureKeystrokes)
        {
            ClickBrush = new SolidBrush(Color.FromArgb(100, Color.DarkGray));
            ClickRadius = 25;
            KeyStrokeBrush = Brushes.Black;
            KeyStrokeFont = new Font(FontFamily.GenericMonospace, 60);
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

        /// <summary>
        /// Draws overlay.
        /// </summary>
        public void Draw(Graphics g, Point Offset = default(Point))
        {
            if (_mouseClicked)
            {
                var curPos = MouseCursor.CursorPosition;
                var d = (float)(ClickRadius * 2);
                
                g.FillEllipse(ClickBrush,
                    curPos.X - (float)ClickRadius - Offset.X,
                    curPos.Y - (float)ClickRadius - Offset.Y,
                    d, d);

                _mouseClicked = false;
            }

            if (_lastKeyPressed == Keys.None)
                return;

            string toWrite = null;

            if (_control)
                toWrite += "Ctrl+";

            if (_shift)
                toWrite += "Shift+";

            if (_alt)
                toWrite += "Alt+";

            toWrite += _lastKeyPressed.ToString();

            g.DrawString(toWrite,
                KeyStrokeFont,
                KeyStrokeBrush,
                KeyStrokeLocation.X,
                KeyStrokeLocation.Y);

            _lastKeyPressed = Keys.None;
        }

        /// <summary>
        /// Frees all resources used by this object.
        /// </summary>
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

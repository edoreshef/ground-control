using System.Drawing;
using System.Windows.Forms;

namespace GroundControl
{
    public class Palette
    {
        private static Palette Init(Palette p)
        {
            p.PlayheadPen = new Pen(p.Playhead);
            p.SelectedTextBrush = new SolidBrush(p.SelectedTextColor);
            p.TextBrush = new SolidBrush(p.TextColor);
            return p;
        }
        public static Palette Dark = Init(new Palette
        {
            BackgroundSelected = Utils.RGB(0xfafafa),
            BackgroundSelectedLight = Utils.RGB(0xc7c7c7),
            Background = Utils.RGB(0x121212),
            BackgroundAlt = Utils.RGB(0x191919),
            BackgroundAlt2 = Utils.RGB(0x383838),
            InterpolationLinear = Utils.RGB(0xed553b),
            InterpolationSmooth = Utils.RGB(0x2491FF),
            InterpolationRamp = Utils.RGB(0x43a047),
            Playhead = Utils.RGB(0xf6d55c),
            SelectedTextColor = Color.Black,
            TextColor = Color.White,
            TextColorAlt = Utils.Gray(200),
            TextColorAlt2 = Utils.Gray(150),
        });
        public static Palette Light = Init(new Palette
        {
            BackgroundSelected = Utils.RGB(0xFFE082),
            BackgroundSelectedLight = Utils.RGB(0xFFC107),
            Background = Utils.RGB(0xFAFAFA),
            BackgroundAlt = Utils.RGB(0xEEEEEE),
            BackgroundAlt2 = Utils.RGB(0xE0E0E0),
            InterpolationLinear = Utils.RGB(0xed553b),
            InterpolationSmooth = Utils.RGB(0x2491FF),
            InterpolationRamp = Utils.RGB(0x43a047),
            Playhead = Utils.RGB(0xFFC107),
            SelectedTextColor = Color.Black,
            TextColor = Color.Black,
            TextColorAlt = Utils.Gray(50),
            TextColorAlt2 = Utils.Gray(100),
        });
        public Color BackgroundSelected;
        public Color BackgroundSelectedLight;
        public Color Background;
        public Color BackgroundAlt;
        public Color BackgroundAlt2;
        public Color InterpolationLinear;
        public Color InterpolationSmooth;
        public Color InterpolationRamp;
        public Color Playhead;
        public Pen PlayheadPen;
        public Color SelectedTextColor;
        public Color TextColor;
        public Color TextColorAlt;
        public Color TextColorAlt2;
        public Brush SelectedTextBrush;
        public Brush TextBrush;
    }
}
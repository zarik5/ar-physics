using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MColor = System.Windows.Media.Color;

namespace ArPhysics2
{
    public partial class GameView : UserControl
    {
        static readonly MColor IDLE_COLOR = MColor.FromArgb(0xAA, 0x00, 0x00, 0x00);
        static readonly MColor SELECTED_COLOR = MColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        const string RESOURCE_PREFIX = "pack://application:,,,/Resources/";

        // do not cache directly ImageSources
        static readonly IReadOnlyDictionary<Shape, string> SHAPE_ICON_STRINGS = new Dictionary<Shape, string>
        {
            [Shape.RANDOM] = "dice.png",
            [Shape.CUBE] = "square.png",
            [Shape.SPHERE] = "circle.png",
            [Shape.BOX] = "rectangle.png",
        };

        static readonly IReadOnlyDictionary<Color, string> COLOR_ICON_STRINGS = new Dictionary<Color, string>
        {
            [Color.RANDOM] = "rainbow.png",
            [Color.RED] = "red.png",
            [Color.ORANGE] = "orange.png",
            [Color.YELLOW] = "yellow.png",
            [Color.GREEN] = "green.png",
            [Color.BLUE] = "blue.png",
            [Color.PURPLE] = "purple.png",
        };

        public Action ShapeSelected { get; set; }
        public Action ColorSelected { get; set; }

        ColorAnimation selectAnim = new ColorAnimation(IDLE_COLOR, SELECTED_COLOR,
            new Duration(new TimeSpan(0, 0, 0, 0, milliseconds: 700)));

        public GameView()
        {
            InitializeComponent();
            
            shapeBtn.Background = new SolidColorBrush(IDLE_COLOR);
            colorBtn.Background = new SolidColorBrush(IDLE_COLOR);
            SetShapeIcon(SHAPE_ICON_STRINGS.Keys.First());
            SetColor(COLOR_ICON_STRINGS.Keys.First());

            selectAnim.Completed += SelectAnim_Completed;
        }

        public void SetShapeIcon(Shape shape)
        {
            shapeIcon.Source = (ImageSource)new ImageSourceConverter()
                .ConvertFrom(new Uri(RESOURCE_PREFIX + SHAPE_ICON_STRINGS[shape]));
        }

        public void SetColor(Color color)
        {
            colorIcon.Source = (ImageSource)new ImageSourceConverter()
                .ConvertFrom(new Uri(RESOURCE_PREFIX + COLOR_ICON_STRINGS[color]));
        }

        private void SelectAnim_Completed(object sender, EventArgs e)
        {
            if (((SolidColorBrush)shapeBtn.Background).Color != IDLE_COLOR)
            {
                ShapeSelected?.Invoke();
                Button_MouseUp(shapeBtn, null);
            }
            else if (((SolidColorBrush)colorBtn.Background).Color != IDLE_COLOR)
            {
                ColorSelected?.Invoke();
                Button_MouseUp(colorBtn, null);
            }
        }

        public void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid btn)
                btn.Background.BeginAnimation(SolidColorBrush.ColorProperty, selectAnim);
        }

        public void Button_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid btn)
            {
                btn.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                btn.Background = new SolidColorBrush(IDLE_COLOR);
            }
        }
    }
}

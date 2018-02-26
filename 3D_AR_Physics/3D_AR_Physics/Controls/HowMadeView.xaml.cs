using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows;
using System;
using System.Windows.Media;
using System.Collections.Generic;
namespace AR_Physics
{
    public partial class HowMadeView : UserControl
    {
        public int PageTurn = -1;

        ColorAnimation reset = new ColorAnimation() { Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)) };
        ColorAnimation hovering = new ColorAnimation() { Duration = new Duration(new TimeSpan(0, 0, 0, 0, 700)) };

        Grid button;
        Dictionary<Grid, int> getTurned = new Dictionary<Grid, int>();

        public HowMadeView()
        {
            InitializeComponent();
            getTurned.Add(leftPage, 0);
            getTurned.Add(rightPage, 1);
            hovering.Completed += new EventHandler(hovering_Completed);
        }

        void hovering_Completed(object sender, EventArgs e)
        {
            if (Color.AreClose(((SolidColorBrush)button.Background).Color, ((Color[])button.Tag)[1]))
            {
                PageTurn = getTurned[button];
            }
        }

        public void LeaveButtons()
        {
            Button_MouseUp(leftPage, null);
            Button_MouseUp(rightPage, null);
        }

        private void Button_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            hovering.To = ((Color[])((Grid)sender).Tag)[1];
            ((Grid)sender).Background.BeginAnimation(SolidColorBrush.ColorProperty, hovering);
            hovering.To = ((Color[])((Grid)sender).Tag)[0];
            ((TextBlock)((Grid)sender).Children[0]).Foreground.BeginAnimation(SolidColorBrush.ColorProperty, hovering);
            button = (Grid)sender;
        }
        private void Button_Loaded(object sender, RoutedEventArgs e)
        {
            ((Grid)sender).Tag = new Color[] { ((SolidColorBrush)((Grid)sender).Background).Color,
                ((SolidColorBrush)((TextBlock)((Grid)sender).Children[0]).Foreground).Color };
            ((Grid)sender).Background = new SolidColorBrush(((Color[])((Grid)sender).Tag)[0]);
            ((TextBlock)((Grid)sender).Children[0]).Foreground = new SolidColorBrush(((Color[])((Grid)sender).Tag)[1]);
        }

        private void Button_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            hovering.To = ((Color[])((Grid)sender).Tag)[0];
            ((Grid)sender).Background.BeginAnimation(SolidColorBrush.ColorProperty, reset);
            hovering.To = ((Color[])((Grid)sender).Tag)[1];
            ((TextBlock)((Grid)sender).Children[0]).Foreground.BeginAnimation(SolidColorBrush.ColorProperty, reset);
        }
    }
}

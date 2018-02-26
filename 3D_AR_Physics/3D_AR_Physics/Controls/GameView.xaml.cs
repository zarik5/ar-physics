using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using XColor = Microsoft.Xna.Framework.Color;

namespace AR_Physics
{
	public partial class GameView : UserControl
	{
		public XColor ShapeColor = XColor.Red;
		public int ShapeType = 0;
		public bool CanCreate;

		ColorAnimation reset = new ColorAnimation() { Duration = new Duration(new TimeSpan(0, 0, 0, 0, 300)) };
		ColorAnimation hovering = new ColorAnimation() { Duration = new Duration(new TimeSpan(0, 0, 0, 0, 700)) };
		ThicknessAnimation select = new ThicknessAnimation(new Thickness(20), new Duration(new TimeSpan(0, 0, 0, 0, 300)));
		ThicknessAnimation unselect = new ThicknessAnimation(new Thickness(5), new Duration(new TimeSpan(0, 0, 0, 0, 300)));

		Grid button;
		Dictionary<Grid, XColor> getCol = new Dictionary<Grid, XColor>();
		Dictionary<Grid, int> getShape = new Dictionary<Grid, int>();

		public GameView()
		{
			InitializeComponent();
			getCol.Add(red, XColor.Red);
			getCol.Add(orange, XColor.Orange);
			getCol.Add(yellow, XColor.Yellow);
			getCol.Add(green, XColor.Green);
			getCol.Add(blue, XColor.Blue);
			getCol.Add(purple, XColor.Purple);
			getShape.Add(box, 0);
			getShape.Add(sphere, 1);
			hovering.Completed += new EventHandler(hovering_Completed);
		}

		void hovering_Completed(object sender, EventArgs e)
		{
			if (Color.AreClose(((SolidColorBrush)button.Background).Color, ((Color[])button.Tag)[1]))
			{
				if (((Color[])button.Tag)[1] != Color.FromArgb(255, 106, 74, 60))
				{
					UnSelectColors();
					ShapeColor = getCol[button];
				}
				else
				{
					UnSelectShapes();
					ShapeType = getShape[button];
					CanCreate = true;
				}
				button.BeginAnimation(MarginProperty, select);
			}
		}

		public void LeaveButtons()
		{
			Button_MouseUp(red, null);
			Button_MouseUp(orange, null);
			Button_MouseUp(yellow, null);
			Button_MouseUp(green, null);
			Button_MouseUp(blue, null);
			Button_MouseUp(purple, null);
			Button_MouseUp(box, null);
			Button_MouseUp(sphere, null);
		}

		public void UnSelectShapes()
		{
			box.BeginAnimation(MarginProperty, unselect);
			sphere.BeginAnimation(MarginProperty, unselect);
		}

		void UnSelectColors()
		{
			red.BeginAnimation(MarginProperty, unselect);
			orange.BeginAnimation(MarginProperty, unselect);
			yellow.BeginAnimation(MarginProperty, unselect);
			green.BeginAnimation(MarginProperty, unselect);
			blue.BeginAnimation(MarginProperty, unselect);
			purple.BeginAnimation(MarginProperty, unselect);
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

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MColor = System.Windows.Media.Color;

namespace ArPhysics2
{
    public partial class HowItWorksView : UserControl
    {
        static readonly MColor IDLE_COLOR = MColor.FromArgb(0xAA, 0x00, 0x00, 0x00);
        static readonly MColor SELECTED_COLOR = MColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        static readonly Dictionary<GameMode, string> DESCRIPTIONS = new Dictionary<GameMode, string>
        {
            [GameMode.HIW_PHYSICS] = "Per poter interagire con gli oggetti virtuali," +
                                     " con i dati restituiti dal sensore vengono modellati persone," +
                                     " piani ed altri oggetti reali, ed inseriti nella scena virtuale.",
            [GameMode.HIW_DEPTH] = "Qui puoi vedere la mappa di profondità," +
                                   " ottenuta grazie al sensore infrarosso del Kinect."
        };

        public Action BackSelected { get; set; }
        public Action NextSelected { get; set; }

        ColorAnimation selectAnim = new ColorAnimation(IDLE_COLOR, SELECTED_COLOR,
            new Duration(new TimeSpan(0, 0, 0, 0, milliseconds: 700)));

        public HowItWorksView()
        {
            InitializeComponent();
            backBtn.Background = new SolidColorBrush(IDLE_COLOR);
            nextBtn.Background = new SolidColorBrush(IDLE_COLOR);
            selectAnim.Completed += SelectAnim_Completed;
        }

        public void SetGameMode(GameMode mode)
        {
            if (DESCRIPTIONS.ContainsKey(mode))
                message.Text = DESCRIPTIONS[mode];
        }

        private void SelectAnim_Completed(object sender, EventArgs e)
        {
            if (((SolidColorBrush)backBtn.Background).Color != IDLE_COLOR)
            {
                BackSelected?.Invoke();
                Button_MouseUp(backBtn, null);
            }
            else if (((SolidColorBrush)nextBtn.Background).Color != IDLE_COLOR)
            {
                NextSelected?.Invoke();
                Button_MouseUp(nextBtn, null);
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

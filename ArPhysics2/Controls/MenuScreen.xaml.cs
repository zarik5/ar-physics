using Microsoft.Kinect.Wpf.Controls;
using System.Collections.Generic;
using System.Windows.Controls;
using MColor = System.Windows.Media.Color;

namespace ArPhysics2
{
    public partial class MenuScreen : UserControl
    {

        public MenuScreen()
        {
            InitializeComponent();

            // set these properties in code behind, otherwise they will revert to default.
            kinectViewer.EngagedUserColor = MColor.FromArgb(0, 0, 0, 0);
            kinectViewer.UserColoringMode = UserColoringMode.Manual;
            kinectViewer.DefaultUserColor = MColor.FromArgb(0x1A, 0, 0, 0);
            kinectViewer.UserColors = new Dictionary<ulong, MColor>();
        }
    }
}

using System;
using System.Windows;

namespace AR_Physics
{
    public partial class App : Application
    {
        [STAThread]
        static void Main()
        {
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}

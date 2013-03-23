using System;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;

using System.Net;
using System.Net.Sockets;

using Bentley.GenerativeComponents.Nodes.Specific;
using Bentley.GenerativeComponents.Nodes;

namespace CERVER.Hardware.Network
{
    /// <summary>
    /// Interaction logic for timer controls
    /// </summary>
    public partial class GCNControl : UserControl 
    {

        public static  readonly DependencyProperty NameOfActiveTechnique = DependencyProperty.Register("NameOfActiveTechnique", typeof(string), typeof(GCNControl), new PropertyMetadata(techniqueChanged));

        public GCNControl()
        {
            InitializeComponent();
            SetBinding(NameOfActiveTechnique, "NameOfActiveTechnique");
            
        }

        private string getIP()
        {
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                }
            }
            return localIP;

        }

        private void HideShowControl(bool show)
        {
            if (show)
            {
                OptionsExpander.Visibility = System.Windows.Visibility.Visible;
                btSend.Visibility = System.Windows.Visibility.Visible;
                btReceive.Visibility = System.Windows.Visibility.Visible;
                this.Height = 30;
            }else
            {
                OptionsExpander.Visibility = System.Windows.Visibility.Hidden;
                btSend.Visibility = System.Windows.Visibility.Hidden;
                btReceive.Visibility = System.Windows.Visibility.Hidden;
                this.Height = 0;
            }
        }
    
        private static void techniqueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            //Bentley.GenerativeComponents.Features.Feature.Print(e.NewValue.ToString());
            GCNControl thisObj = (GCNControl)obj;

            switch ((string)e.NewValue)
            {
                case "TouchOSCConnect":
                    thisObj.HideShowControl(true);
                    break;
                default:
                    thisObj.HideShowControl(false);
                    break;
            }

        }

        private void OptionsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            ThisIP.Content = getIP();
        }

    }
}

using ShadowrunLauncher.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShadowrunLauncher
{
    /// <summary>
    /// Interaction logic for KeyDisplay.xaml
    /// </summary>
    public partial class KeyDisplay : Window
    {
        private GenerateKeyLogic _generateKeyLogic;
        private InstallLogic _installLogic;
        private static string currentkey = " ";
        static Random random = new Random();
        internal KeyDisplay(InstallLogic installLogic, string key = "CMCY6-TPV4Y-4HYWP-Q2TFJ-R8BW3", bool IsGen = false)
        {
            InitializeComponent();
            _installLogic = installLogic;
            _generateKeyLogic = new GenerateKeyLogic(installLogic);
            SetKey(key);
        }
        private void SetKey(string key)
        {
            currentkey = key;
            string[] Keysector = key.Split('-');
            r1.Text = Keysector[0];
            r2.Text = Keysector[1];
            r3.Text = Keysector[2];
            r4.Text = Keysector[3];
            r5.Text = Keysector[4];
        }
        private void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void copyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

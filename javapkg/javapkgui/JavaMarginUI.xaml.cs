// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace javapkgui
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class JavaMarginUI : UserControl
    {
        public JavaMarginUI()
        {
            InitializeComponent();

            ProgressBarArea.Visibility = Visibility.Hidden;
        }
        public string BusyProgressMessage
        {
            get { return ProgressBarArea.ToolTip.ToString();  }
            set { ProgressBarArea.ToolTip = value;  }
        }
        public bool BusyProgressBar
        {
            get { return ProgressBarArea.Visibility == Visibility.Visible; }
            set { ProgressBarArea.Visibility = value ? Visibility.Visible : Visibility.Hidden; }
        }
        public string MessageBanner
        {
            get { return MessageText.Content.ToString(); }
            set 
            { 
                MessageText.Content = value;
                if (String.IsNullOrEmpty(value))
                {
                    // Hide
                    MessageArea.Visibility = Visibility.Collapsed;
                    ActionArea.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Show
                    MessageArea.Visibility = Visibility.Visible;
                    ActionArea.Visibility = Visibility.Visible;
                }
            }
        }
    }
}

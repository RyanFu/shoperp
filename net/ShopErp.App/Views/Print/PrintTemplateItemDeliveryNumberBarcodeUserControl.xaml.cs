﻿using System;
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

namespace ShopErp.App.Views.Print
{
    /// <summary>
    /// Interaction logic for PrintTemplateItemDeliveryNumberBarcodeUserControl.xaml
    /// </summary>
    public partial class PrintTemplateItemDeliveryNumberBarcodeUserControl : UserControl
    {
        public PrintTemplateItemDeliveryNumberBarcodeUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.cbbPrintText.ItemsSource = new string[] {"是", "否"};
        }
    }
}
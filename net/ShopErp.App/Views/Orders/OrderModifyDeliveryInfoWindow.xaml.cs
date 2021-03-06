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
using System.Windows.Shapes;
using ShopErp.App.Service.Restful;
using ShopErp.App.Views.Extenstions;
using ShopErp.Domain;

namespace ShopErp.App.Views.Orders
{
    /// <summary>
    /// OrdeDeliveryNumberUpate.xaml 的交互逻辑
    /// </summary>
    public partial class OrderModifyDeliveryInfoWindow : Window
    {
        public string DeliveryCompany { get; set; }

        public string DeliveryNumber { get; set; }

        public OrderModifyDeliveryInfoWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.cbbDeliveryCompany.ItemsSource = ServiceContainer.GetService<DeliveryCompanyService>().GetByAll().Datas.Select(obj => obj.Name).ToArray();
                this.cbbDeliveryCompany.SelectedItem = DeliveryCompany;
                this.tbDeliveryNumber.Text = DeliveryNumber;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string number = this.tbDeliveryNumber.Text.Trim();
                var dt = this.cbbDeliveryCompany.Text.Trim();
                this.DeliveryCompany = dt;
                this.DeliveryNumber = number;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
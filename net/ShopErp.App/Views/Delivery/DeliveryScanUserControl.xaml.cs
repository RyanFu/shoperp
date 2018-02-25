﻿using ShopErp.App.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
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
using ShopErp.App.Device;
using ShopErp.App.Service;
using ShopErp.App.Service.Restful;
using ShopErp.App.Utils;
using ShopErp.Domain;

namespace ShopErp.App.Views.Delivery
{
    /// <summary>
    /// DeliveryScanUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeliveryScanUserControl : UserControl
    {
        private bool myLoaded = false;
        private IDevice device = null;
        private OrderService os = ServiceContainer.GetService<OrderService>();

        private System.Collections.ObjectModel.ObservableCollection<DeliveryScanViewModel> scanedViewModels =
            new System.Collections.ObjectModel.ObservableCollection<DeliveryScanViewModel>();

        public Control UIControl
        {
            get { return this; }
        }

        public DeliveryScanUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.myLoaded)
            {
                return;
            }
            this.dgvScanedItems.ItemsSource = this.scanedViewModels;
            try
            {
                string deviceType = LocalConfigService.GetValue(SystemNames.CONFIG_WEIGHT_DEVICE, "");

                if (string.IsNullOrWhiteSpace(deviceType) == false)
                {
                    Type t = Type.GetType(deviceType, true);
                    this.device = Activator.CreateInstance(t) as IDevice;
                    if (this.device == null)
                    {
                        throw new Exception("无法创建指定的称重设备:" + "");
                    }
                }
                myLoaded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "打开设备失败");
            }
        }

        private void tbDeliveryNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            e.Handled = true;

            this.tbResult.Text = "请扫描条码";
            this.tbResultWeight.Text = "0 KG";
            this.tbResult.Background = null;
            try
            {
                string number = this.tbDeliveryNumber.Text.Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(number))
                {
                    return;
                }
                float weight = 0;
                if (this.chkEnableInput.IsChecked.Value)
                {
                    try
                    {
                        weight = float.Parse(this.tbWeight.Text.Trim());
                    }
                    catch
                    {
                        throw new Exception("你开启的手动输入重量，但输入值不正确");
                    }
                }
                else
                {
                    this.tbWeight.Text = "";
                    if (this.device == null)
                    {
                        throw new Exception("请配置称重设备");
                    }
                    weight = (float) this.device.ReadWeight();
                }

                weight = (float) Math.Round(weight, 2);
                if (weight <= 0)
                {
                    throw new Exception("重量必须大于0");
                }
                this.tbResultWeight.Text = weight.ToString("F2") + "KG";
                this.tbWeight.Text = weight.ToString("F2");
                var orders = this.os.MarkDelivery(number, weight,
                    this.chkIngorePopError.IsChecked.Value, this.chkIngoreWeightDetect.IsChecked.Value,
                    this.chkIngoreStateCheck.IsChecked.Value);
                //更新后台发货
                foreach (var order in orders)
                {
                    string comment = "";
                    if (order.PopPayType == PopPayType.COD)
                    {
                        comment = string.Format("【发货{0}:{1} {2}】", DateTime.Now.ToString("MM-dd HH:mm"),
                            order.DeliveryCompany[0], order.DeliveryNumber);
                    }
                    else
                    {
                        comment = string.Format("【发货{0}】", DateTime.Now.ToString("MM-dd HH:mm"));
                    }
                    //检查当前是否有标记发货信息
                    int startIndex = order.PopSellerComment.IndexOf("【发货");
                    int endIndex = order.PopSellerComment.IndexOf('】', startIndex < 0 ? 0 : startIndex);

                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        comment = order.PopSellerComment.Replace(
                            order.PopSellerComment.Substring(startIndex, endIndex - startIndex + 1), comment);
                    }
                    else
                    {
                        comment = order.PopSellerComment + comment;
                    }
                    try
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                this.os.ModifyPopSellerComment(order.Id,order.PopFlag == ColorFlag.UN_LABEL ? ColorFlag.GREEN : order.PopFlag, comment);
                                break;
                            }
                            catch (Exception me)
                            {
                                if (i == 2)
                                    throw new Exception("更新订单备注失败:" + me.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (chkIngorePopError.IsChecked.Value == false)
                        {
                            throw ex;
                        }
                    }

                    //生成发货记录
                    DeliveryScanViewModel svm = new DeliveryScanViewModel
                    {
                        DeliveryCompany = order.DeliveryCompany,
                        DeliveryNumber = number,
                        OrderId = order.Id.ToString(),
                        Time = DateTime.Now,
                        Weight = weight,
                        ReceiverInfo = order.ReceiverName + "," + order.ReceiverPhone + "," + order.ReceiverMobile +
                                       "," + order.ReceiverAddress,
                    };

                    if (order.OrderGoodss != null)
                    {
                        svm.OrderGoodsInfo = String.Join(",",
                            order.OrderGoodss
                                .Select(obj => obj.Vendor + " " + obj.Number + " " + obj.Color + " " + obj.Size + " " +
                                               obj.Count + ("件")).ToArray());
                    }
                    var first = this.scanedViewModels.FirstOrDefault(obj => obj.OrderId == order.Id.ToString());
                    if (first != null)
                    {
                        this.scanedViewModels.Remove(first);
                    }
                    this.scanedViewModels.Add(svm);
                }

                var or = orders[0];
                if (orders.Length > 1)
                {
                    var ogs = new List<OrderGoods>();
                    foreach (var o in orders)
                    {
                        ogs.AddRange(o.OrderGoodss.Where(obj => (int) obj.State <= (int) OrderState.SHIPPED));
                    }
                    or.OrderGoodss = ogs;
                }
                this.lstItems.ItemsSource = or.OrderGoodss;
                this.spReceiver.DataContext = or;
                var count = this.scanedViewModels.GroupBy(obj => obj.DeliveryCompany).ToArray();
                string message = string.Join(",",
                    count.Select(obj => obj.Key + ": " + obj.Select(o => o.DeliveryNumber).Distinct().Count()));
                this.tbTotal.Text = string.Format("订单总数：{0},快递总数:{1},{2}", this.scanedViewModels.Count,
                    this.scanedViewModels.Select(obj => obj.DeliveryNumber).Distinct().Count(), message);
                this.tbResult.Text = "允许发货";
                Speaker.Speak(or.DeliveryCompany);
            }
            catch (IndexOutOfRangeException ie)
            {
                Debug.WriteLine(ie.StackTrace);
                this.lstItems.ItemsSource = null;
                this.spReceiver.DataContext = null;
            }
            catch (Exception ex)
            {
                Speaker.Speak("出现错误");
                this.tbResult.Background = Brushes.Red;
                this.tbResult.Text = ex.Message;
                this.lstItems.ItemsSource = null;
            }
            finally
            {
                this.tbDeliveryNumber.Text = "";
                this.chkIngoreWeightDetect.IsChecked = false;
                this.chkIngoreStateCheck.IsChecked = false;
            }
        }

        private void btnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show("是否清空当前记录?", "确定", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
                    MessageBoxResult.Yes)
                {
                    return;
                }
                this.scanedViewModels.Clear();
                this.tbTotal.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

    }
}
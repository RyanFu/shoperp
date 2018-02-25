﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using mshtml;
using ShopErp.App.Service.Spider;
using ShopErp.App.Service.Restful;
using ShopErp.Domain;

namespace ShopErp.App.Views.Goods
{
    /// <summary>
    /// Interaction logic for GoodUploadUpdateWindow.xaml
    /// </summary>
    public partial class GoodUpdateWindow : Window
    {
        private GoodsService shoesService = ServiceContainer.GetService<GoodsService>();

        private System.Collections.ObjectModel.ObservableCollection<GoodUpdateViewModel> shoes =
            new System.Collections.ObjectModel.ObservableCollection<GoodUpdateViewModel>();

        private int current = 0;
        private bool isStop = false;
        private bool isRunning = false;
        private SpiderBase sb = SpiderBase.CreateSpider("go2.cn", 80, 0);


        public GoodUpdateWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.dgvShoes.ItemsSource = this.shoes;
            this.sb.Busy += Sb_Busy;
            this.sb.WaitingRetryMessage += SbWaitingRetryMessage;
        }

        private void SbWaitingRetryMessage(object sender, string e)
        {
            this.Dispatcher.Invoke(() => this.tbProgress.Text = e);
        }

        private void Sb_Busy(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(() => this.wb1.Refresh());
        }

        private void Open_Click(object sender, EventArgs e)
        {
            try
            {
                TextBlock tb = sender as TextBlock;
                if (tb == null)
                {
                    throw new Exception("Edit_Click错误，事件源不是TextBlock");
                }
                GoodUpdateViewModel shoes = tb.DataContext as GoodUpdateViewModel;
                Process.Start(shoes.Source.Url.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnQuery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.shoes.Clear();

                string strId = this.tbIdOrUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(strId) == false)
                {
                    int id = 0;
                    int.TryParse(strId, out id);
                    if (id > 0)
                    {
                        var d = this.shoesService.GetById(id);
                        if (d != null)
                        {
                            this.shoes.Add(new GoodUpdateViewModel { Source = d });
                        }
                    }
                    else
                    {
                        var d = this.shoesService.GetByAll(0, GoodsState.NONE, 0, DateTime.MinValue, DateTime.MinValue,
                            strId, "", GoodsType.GOODS_SHOES_NONE, "", ColorFlag.None, "", 0, 0).Datas;
                        foreach (var gu in d)
                        {
                            this.shoes.Add(new GoodUpdateViewModel { Source = gu });
                        }
                    }
                    return;
                }

                int pageIndex = 0;
                do
                {
                    var data = this.shoesService.GetByAll(0, GoodsState.NONE, 0, DateTime.MinValue, DateTime.MinValue,
                        "", "", GoodsType.GOODS_SHOES_NONE, "", ColorFlag.None, "", pageIndex, 500);
                    if (data.Datas.Count < 1)
                    {
                        break;
                    }
                    foreach (var d in data.Datas)
                    {
                        this.shoes.Add(new GoodUpdateViewModel { Source = d });
                    }
                    this.tbProgress.Text = "已经下载:" + (pageIndex + 1) + "/" + (data.Total + 499) / 500 + "页，当前共:" +
                                           this.shoes.Count;
                    if (this.shoes.Count > 20)
                    {
                        this.dgvShoes.ScrollIntoView(this.shoes.Last());
                    }
                    WPFHelper.DoEvents();
                } while (++pageIndex > 0);
            }
            catch (Exception ex)
            {
                if (ex.Message.Equals("NO_MORE_DATA") == false)
                    MessageBox.Show(ex.Message);
            }
            finally
            {
                this.tbProgress.Text = "读取到数据:" + this.shoes.Count;
            }
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (this.isRunning)
            {
                this.isStop = true;
            }
            else
            {
                Task.Factory.StartNew(UpdateTask);
            }
        }

        private void UpdateTask()
        {
            try
            {
                this.isStop = false;
                this.isRunning = true;
                this.current = 0;
                this.Dispatcher.BeginInvoke(new Action(() => this.btnUpdate.Content = "停止"));
                string vendor = "";
                foreach (var gu in shoes)
                {
                    if (this.isStop)
                    {
                        break;
                    }

                    if (gu.Source.UpdateEnabled == false)
                    {
                        continue;
                    }

                    string state = null;
                    try
                    {
                        if (string.IsNullOrWhiteSpace(gu.Source.Url))
                        {
                            throw new Exception("商品没有URL地址");
                        }
                        var g = this.sb.GetGoodsInfoByUrl(gu.Source.Url, ref vendor, true);
                        if (g == null)
                        {
                            throw new Exception("获取商品方法返回NULL");
                        }
                        gu.Source.Price = g.Price; 
                        gu.Source.Colors = g.Colors;
                        gu.Source.UpdateTime = DateTime.Now;
                        gu.Source.Material = g.Material;
                        this.shoesService.Update(gu.Source);
                        state = "更新成功";
                    }
                    catch (Exception ex)
                    {
                        state = "错误:" + ex.Message;
                    }
                    finally
                    {
                        lock (this.shoes)
                        {
                            current++;
                        }
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            this.tbProgress.Text = "已经更新:" + current + "/" + this.shoes.Count;
                            gu.State = state;
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                this.isRunning = false;
                this.isStop = true;
                this.Dispatcher.BeginInvoke(new Action(() => this.btnUpdate.Content = "更新商品"));
            }
        }

        private void wb1_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            try
            {
                string content =
                    "window.onerror=noError; function fixahref(){$(\"a\").each(function(index){$(this).attr(\"target\", \"_self\"); }); } function noError(){ return true; }";
                HTMLDocument doc2 = this.wb1.Document as HTMLDocument;
                IHTMLElementCollection nodes = doc2.getElementsByTagName("head");
                IHTMLScriptElement injectNode = (IHTMLScriptElement)doc2.createElement("SCRIPT");
                injectNode.type = "text/javascript";
                injectNode.text = content;

                foreach (IHTMLElement elem in nodes)
                {
                    HTMLHeadElement head = (HTMLHeadElement)elem;
                    head.appendChild((IHTMLDOMNode)injectNode);
                    head.appendChild((IHTMLDOMNode)injectNode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "页面加载完成，无法注入JS代码");
            }
        }
    }
}
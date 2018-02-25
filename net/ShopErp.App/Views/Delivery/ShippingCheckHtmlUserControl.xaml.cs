﻿using ShopErp.App.Views.Orders.Taobao;
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
using CefSharp;
using ShopErp.App.Service;
using ShopErp.App.Service.Restful;
using ShopErp.App.Utils;
using ShopErp.Domain;
using ShopErp.Domain.Pop;
using ShopErp.App.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ShopErp.App.Views.Delivery
{
    /// <summary>
    /// Interaction logic for OrderSyncUserControl.xaml
    /// </summary>
    public partial class ShippingCheckHtmlUserControl : UserControl
    {
        private bool myLoaded = false;
        string jspath = System.IO.Path.Combine(EnvironmentDirHelper.DIR_DATA + "\\TAOBAOJS.js");
        CefSharp.WinForms.ChromiumWebBrowser wb1 = null;
        private ObservableCollection<OrderViewModel> orders = new ObservableCollection<OrderViewModel>();

        public ShippingCheckHtmlUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.myLoaded)
                {
                    return;
                }
                this.myLoaded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private ColorFlag ConvertFlag(int flag)
        {
            if (flag == 0)
            {
                return ColorFlag.UN_LABEL;
            }
            if (flag == 1)
            {
                return ColorFlag.RED;
            }
            if (flag == 2)
            {
                return ColorFlag.YELLOW;
            }
            if (flag == 3)
            {
                return ColorFlag.GREEN;
            }
            if (flag == 4)
            {
                return ColorFlag.BLUE;
            }
            if (flag == 5)
            {
                return ColorFlag.PINK;
            }
            return ColorFlag.UN_LABEL;
        }

        private OrderState ConveretState(string state)
        {
            if (state == "等待买家付款" || state.Contains("商品已拍下，等待买家付款"))
            {
                return OrderState.WAITPAY;
            }
            if (state == "买家已付款" || state.Contains("买家已付款，等待商家发货"))
            {
                return OrderState.PAYED;
            }
            if (state == "卖家已发货" || state.Contains("商家已发货，等待买家确认"))
            {
                return OrderState.SHIPPED;
            }
            if (state.Contains("订单部分退款中"))
            {
                return OrderState.RETURNING;
            }
            if (state == "交易关闭")
            {
                return OrderState.CANCLED;
            }
            if (state == "交易成功")
            {
                return OrderState.SUCCESS;
            }

            return OrderState.WAITPAY;
        }

        private void ParseOrder(TaobaoQueryOrdersResponseOrder v, long shopId)
        {
            var dm = ServiceContainer.GetService<SystemConfigService>().Get(-1, "DELIVERY_MONEY", "7");
            if (dm == null)
            {
                throw new Exception("数据库没有快递运费：DELIVERY_MONEY");
            }
            var deliveryMoney = float.Parse(dm);
            var dbMineTime = ServiceContainer.GetService<OrderService>().GetDBMinTime();
            var order = new Order
            {
                CloseOperator = "",
                CloseTime = dbMineTime,
                CreateOperator = "",
                CreateTime = DateTime.Now,
                CreateType = OrderCreateType.DOWNLOAD,
                DeliveryCompany = "",
                DeliveryNumber = "",
                DeliveryOperator = "",
                DeliveryTime = dbMineTime,
                DeliveryMoney = deliveryMoney,
                Id = 0,
                PopDeliveryTime = dbMineTime,
                OrderGoodss = new List<OrderGoods>(),
                ParseResult = true,
                PopBuyerComment = "",
                PopBuyerId = v.buyer.nick,
                PopBuyerPayMoney = v.payInfo.actualFee,
                PopCodNumber = "",
                PopCodSevFee = 0,
                PopCreateTime = DateTime.Parse(v.orderInfo.createTime),
                PopFlag = ConvertFlag(v.extra.sellerFlag),
                PopOrderId = v.id,
                PopOrderTotalMoney = v.payInfo.actualFee,
                PopPayTime = dbMineTime,
                PopPayType = PopPayType.ONLINE,
                PopSellerComment = "",
                PopSellerGetMoney = v.payInfo.actualFee,
                PopState = "",
                PopType = PopType.TMALL,
                PrintOperator = "",
                PrintPaperType = PaperType.NONE,
                PrintTime = dbMineTime,
                ReceiverAddress = "",
                ReceiverMobile = "",
                ReceiverName = "",
                ReceiverPhone = "",
                ShopId = shopId,
                State = ConveretState(v.statusInfo.text.Trim()),
                Type = OrderType.NORMAL,
                Weight = 0,
            };

            //订单信息
            var js = ScriptManager.GetBody(jspath, "//TAOBAO_GET_ORDER").Replace("###bizOrderId", v.id);
            var task = wb1.GetBrowser().MainFrame.EvaluateScriptAsync(js, "", 1, new TimeSpan(0, 0, 30));
            var ret = task.Result;
            if (ret.Success == false || (ret.Result != null && ret.Result.ToString().StartsWith("ERROR")))
            {
                throw new Exception("执行操作失败：" + ret.Message);
            }

            var content = ret.Result.ToString();
            int si = content.IndexOf("var detailData");
            if (si <= 0)
            {
                throw new Exception("未找到订单详情数据");
            }

            int ei = content.IndexOf("</script>", si);
            if (ei <= si)
            {
                throw new Exception("未找到详情结尾数据");
            }

            string orderInfo = content.Substring(si + "var detailData".Length, ei - si - "var detailData".Length).Trim().TrimStart('=');

            var oi = Newtonsoft.Json.JsonConvert.DeserializeObject<Views.Orders.Taobao.TaobaoQueryOrderDetailResponse>(orderInfo);

            string time = oi.stepbar.options.First(obj => obj.content == "买家付款").time;
            order.PopPayTime = string.IsNullOrWhiteSpace(time) ? dbMineTime : DateTime.Parse(time);

            time = oi.stepbar.options.First(obj => obj.content == "发货").time;
            order.PopDeliveryTime = string.IsNullOrWhiteSpace(time) ? dbMineTime : DateTime.Parse(time);

            //time = oi.stepbar.options.First(obj => obj.content == "买家确认收货").time;
            //order.pop = string.IsNullOrWhiteSpace(time) ? dbMineTime : DateTime.Parse(time);

            order.PopBuyerComment = oi.basic.lists.First(obj => obj.key == "买家留言").content[0].text;
            if (order.PopBuyerComment == "-")
            {
                order.PopBuyerComment = "";
            }

            var addN = oi.basic.lists.First(obj => obj.key == "收货地址").content[0];

            Debug.WriteLine(oi.orders.id + ":" + addN.type + " : " + addN.text);

            if (addN.type.Equals("label", StringComparison.OrdinalIgnoreCase))
            {
                string add = "";
                string reinfo = "";
                reinfo = addN.text;
                string[] reinfos = reinfo.Split(',');
                order.ReceiverName = reinfos[0].Trim();
                order.ReceiverMobile = reinfos[1].Replace("86-", "");
                if (reinfos[2].All(c => Char.IsDigit(c) || c == '-'))
                {
                    order.ReceiverPhone = reinfos[2];
                    for (int i = 3; i < reinfos.Length - 1; i++)
                    {
                        add += reinfos[i];
                    }
                }
                else
                {
                    for (int i = 2; i < reinfos.Length - 1; i++)
                    {
                        add += reinfos[i];
                    }
                }
                order.ReceiverAddress = add;
            }
            else if (addN.type.Equals("html", StringComparison.OrdinalIgnoreCase))
            {
                string PROVINCE_MARK = "'detail-oversea-info'>";
                string tt = addN.text;
                int iStart = tt.IndexOf(">");
                int iEnd = tt.IndexOf("<span");
                string ss = tt.Substring(iStart + 1, iEnd - iStart - 1);
                string[] sss = ss.Split(new char[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                order.ReceiverName = sss[0];
                order.ReceiverPhone = "";
                iStart = tt.IndexOf(PROVINCE_MARK);
                iEnd = tt.IndexOf("<span", iStart);
                string pro = tt.Substring(iStart + PROVINCE_MARK.Length, iEnd - iStart - PROVINCE_MARK.Length).Trim();
                iStart = tt.IndexOf("转运仓库", iEnd);
                iStart = tt.IndexOf("<span>", iStart);
                iEnd = tt.IndexOf("</span>", iStart);
                string addfull = tt.Substring(iStart + "<span>".Length, iEnd - (iStart + "<span>".Length));
                string[] adds = addfull.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string add = pro;
                for (int i = 0; i < adds.Length - 3; i++)
                {
                    if (adds[i].All(c => char.IsDigit(c)))
                    {
                        break;
                    }
                    add += " " + adds[i].Trim();
                }
                order.ReceiverMobile = adds[adds.Length - 1];
                order.ReceiverPhone = adds[adds.Length - 2];
                order.ReceiverAddress = add;
            }
            else
            {
                throw new Exception("无法解析的地址格式:" + addN.type);
            }

            //订单金额
            var contents = new List<TaobaoQueryOrderDetailResponseAmountCountContent>();
            foreach (var c in oi.amount.count)
            {
                foreach (var cc in c)
                {
                    contents.AddRange(cc.content);
                }
            }
            var goodsPrice = contents.FirstOrDefault(obj => obj.data.titleLink.text == "商品总价").data.money.text.Replace("￥", "").Trim();
            var deliveryPrice = contents.FirstOrDefault(obj => obj.data.titleLink.text.Contains("运费")).data.money.text.Replace("￥", "").Trim();
            var buyerPayPrice = contents.FirstOrDefault(obj => obj.data.titleLink.text.Contains("订单总价")).data.money.text.Replace("￥", "").Trim();
            var sellerGetMoney = contents.FirstOrDefault(obj => obj.data.titleLink != null && (obj.data.titleLink.text.Contains("应收款") || obj.data.titleLink.text.Contains("实收款")));

            order.PopOrderTotalMoney = float.Parse(goodsPrice) + float.Parse(deliveryPrice);
            order.PopBuyerPayMoney = float.Parse(buyerPayPrice);
            order.PopSellerGetMoney = float.Parse(sellerGetMoney.data.dotPrefixMoney.text + sellerGetMoney.data.dotSufixMoney.text);

            if (oi.overStatus.prompt != null && oi.overStatus.prompt.FirstOrDefault(obj => obj.key == "物流") != null)
            {
                order.DeliveryCompany = oi.overStatus.prompt.FirstOrDefault(obj => obj.key == "物流").content[0].companyName;
                order.DeliveryNumber = oi.overStatus.prompt.FirstOrDefault(obj => obj.key == "物流").content[0].mailNo;
            }
            if (oi.overStatus.operate.FirstOrDefault(obj => string.IsNullOrWhiteSpace(obj.key)) != null)
            {
                string comment = oi.overStatus.operate.FirstOrDefault(obj => string.IsNullOrWhiteSpace(obj.key)).content[0].text;
                si = comment.IndexOf("备忘：</span><span>");
                ei = comment.IndexOf("</span>", si + "备忘：</span><span>".Length);
                string sellerComment = comment.Substring(si + "备忘：</span><span>".Length, ei - si - "备忘：</span><span>".Length);
                order.PopSellerComment = sellerComment.TrimStart('#');
            }
            Dictionary<string, float> namePrice = new Dictionary<string, float>();
            foreach (var vv in oi.orders.list)
            {
                foreach (var vvv in vv.status)
                {
                    foreach (var vvvv in vvv.subOrders)
                    {
                        if (namePrice.ContainsKey(vvvv.itemInfo.title) == false)
                        {
                            namePrice.Add(vvvv.itemInfo.title.Trim(), float.Parse(vvvv.priceInfo[0].text.Trim()));
                        }
                    }
                }
            }
            foreach (var so in v.subOrders)
            {
                var og = new OrderGoods
                {
                    CloseOperator = "",
                    CloseTime = dbMineTime,
                    Color = so.itemInfo.skuText.FirstOrDefault(obj => obj.name.Contains("颜色")).value,
                    Comment = "",
                    Count = so.quantity,
                    Edtion = "",
                    GetedCount = 0,
                    Id = 0,
                    Image = so.itemInfo.pic,
                    Number = so.itemInfo.extra[0].value,
                    NumberId = 0,
                    OrderId = 0,
                    PopNumber = "",
                    PopOrderSubId = "",
                    PopPrice = 0,
                    PopUrl = "",
                    Price = 0,
                    Size = so.itemInfo.skuText.FirstOrDefault(obj => obj.name.Contains("尺码")).value,
                    State = OrderState.PAYED,
                    PopRefundState = PopRefundState.NOT,
                    Weight = 0,
                    StockOperator = "",
                    StockTime = dbMineTime,
                    Vendor = "",
                    IsPeijian = false,
                };
                og.PopPrice = namePrice[so.itemInfo.title];
                og.PopInfo = og.Number + "||颜色:" + og.Color + "|尺码:" + og.Size;
                order.OrderGoodss.Add(og);
            }
            ServiceContainer.GetService<OrderService>().Save(order);
        }

        private List<Order> GetOrders()
        {
            List<Order> orders = new List<Order>();

            int totalCount = 0, currentCount = 0;
            int totalPage = 0, currentPage = 1;
            string htmlRet = this.wb1.GetTextAsync().Result;
            var allShops = ServiceContainer.GetService<ShopService>().GetByAll().Datas;
            var shop = allShops.FirstOrDefault(obj => htmlRet.Contains(obj.PopSellerId));

            if (shop == null)
            {
                throw new Exception("系统中没有找到相应店铺");
            }

            while (true)
            {
                string script = ScriptManager.GetBody(jspath, "//TAOBAO_SEARCH_ORDER").Replace("###prePageNo", (currentPage - 1 >= 0 ? currentPage - 1 : 1).ToString()).Replace("###pageNum", currentPage.ToString());
                var task = wb1.GetBrowser().MainFrame.EvaluateScriptAsync(script, "", 1, new TimeSpan(0, 0, 30));
                var ret = task.Result;

                if (ret.Success == false || (ret.Result != null && ret.Result.ToString().StartsWith("ERROR")))
                {
                    throw new Exception("执行操作失败：" + ret.Message);
                }

                var or = Newtonsoft.Json.JsonConvert.DeserializeObject<Views.Orders.Taobao.TaobaoQueryOrdersResponse>(ret.Result.ToString());

                if (or.mainOrders == null || or.mainOrders.Length < 1)
                {
                    if (orders.Count < 1)
                    {
                        throw new Exception("没有订单");
                    }
                    else
                    {
                        break;
                    }
                }
                totalCount = or.page.totalNumber;
                totalPage = or.page.totalPage;
                foreach (var v in or.mainOrders)
                {
                    var o = ServiceContainer.GetService<OrderService>().GetByPopOrderId(v.id).First;
                    if (o == null)
                    {
                        ParseOrder(v, shop.Id);
                        o = ServiceContainer.GetService<OrderService>().GetByPopOrderId(v.id).First;
                        if (o == null)
                        {
                            throw new Exception("订单不在本地系统中，保存后重新读取也不存在");
                        }
                    }
                    orders.Add(o);
                    currentCount++;
                    this.tbMsg.Text = string.Format("已经下载：{0}/{1} {2} {3} ", currentCount, totalCount, v.id, v.orderInfo.createTime);
                    WPFHelper.DoEvents();
                }
                currentPage++;
            }
            return orders;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.orders.Clear();
                string htmlRet = this.wb1.GetTextAsync().Result;
                var shops = ServiceContainer.GetService<ShopService>().GetByAll().Datas;
                var shop = shops.FirstOrDefault(obj => htmlRet.Contains(obj.PopSellerId));
                var downloadOrders = GetOrders();
                if (downloadOrders == null || downloadOrders.Count < 1)
                {
                    MessageBox.Show("没有找到待发货的订单");
                    return;
                }
                var os = ServiceContainer.GetService<OrderService>();
                var orders = downloadOrders.Where(obj => string.IsNullOrWhiteSpace(obj.PopOrderId) == false && os.IsDBMinTime(obj.PopDeliveryTime)).Select(obj => new OrderViewModel(obj)).OrderBy(obj => obj.Source.PopPayTime).ToArray();
                if (orders.Length < 1)
                {
                    MessageBox.Show("没有找到待发货的订单");
                    return;
                }
                foreach (var v in downloadOrders)
                {
                    Debug.WriteLine(v.Id + " " + v.PopOrderId + v.PopDeliveryTime);
                }
                //分析
                foreach (var order in orders)
                {
                    var time = DateTime.Now.Subtract(order.Source.PopPayTime).TotalHours;
                    var sTime = shops.FirstOrDefault(obj => obj.Id == order.Source.ShopId).ShippingHours;
                    if (time >= sTime)
                    {
                        order.Background = Brushes.Red;
                        order.IsChecked = true;
                    }
                    else if (time - sTime >= -1)
                    {
                        order.Background = Brushes.Yellow;
                        order.IsChecked = true;
                    }
                    this.orders.Add(order);
                }
                this.dgvOrders.ItemsSource = this.orders;
                this.tbTotal.Text = "当前共 : " + orders.Length + " 条记录";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        private PopOrderState ParseOrder(string popOrderId)
        {
            var pos = new PopOrderState()
            {
                PopOrderId = popOrderId,
                PopOrderStateDesc = "",
                PopOrderStateValue = "",
                State = OrderState.NONE
            };

            //订单信息
            var js = ScriptManager.GetBody(jspath, "//TAOBAO_GET_ORDER").Replace("###bizOrderId", popOrderId);
            var task = wb1.GetBrowser().MainFrame.EvaluateScriptAsync(js, "", 1, new TimeSpan(0, 0, 30));
            var ret = task.Result;
            if (ret.Success == false || (ret.Result != null && ret.Result.ToString().StartsWith("ERROR")))
            {
                throw new Exception("执行操作失败：" + ret.Message);
            }

            var content = ret.Result.ToString();
            int si = content.IndexOf("var detailData");
            if (si <= 0)
            {
                throw new Exception("未找到订单详情数据");
            }

            int ei = content.IndexOf("</script>", si);
            if (ei <= si)
            {
                throw new Exception("未找到详情结尾数据");
            }
            string orderInfo = content.Substring(si + "var detailData".Length, ei - si - "var detailData".Length).Trim().TrimStart('=');

            var oi = Newtonsoft.Json.JsonConvert.DeserializeObject<ShopErp.App.Views.Orders.Taobao.TaobaoQueryOrderDetailResponse>(orderInfo);

            pos.PopOrderStateValue = oi.overStatus.status.content[0].text;
            pos.PopOrderStateDesc = oi.overStatus.status.content[0].text;
            pos.State = ConveretState(pos.PopOrderStateValue);
            return pos;
        }

        private void MarkPopDelivery(string popOrderId, string deliveryCompany, string deliveryNumber)
        {
            //订单信息
            var js = ScriptManager.GetBody(jspath, "//TAOBAO_MARK_DELIVERY").Replace("###companyCode", deliveryCompany).Replace("###mailNo", deliveryNumber).Replace("###taobaoTradeId", popOrderId).Replace("###trade_id", popOrderId);
            var task = wb1.GetBrowser().MainFrame.EvaluateScriptAsync(js, "", 1, new TimeSpan(0, 0, 30));
            var ret = task.Result;
            if (ret.Success == false || (ret.Result != null && ret.Result.ToString().StartsWith("ERROR")))
            {
                throw new Exception("执行操作失败：" + ret.Message);
            }
            var content = ret.Result.ToString();
            if (content.Contains("运单号不符合规则或已经被使用"))
            {
                throw new Exception("运单号不符合规则或已经被使用");
            }
        }


        private void btnMarkDelivery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var so = this.orders.Where(obj => obj.IsChecked).ToArray();
                if (so.Length < 1)
                {
                    throw new Exception("没有选择订单");
                }
                var dcs = ServiceContainer.GetService<DeliveryCompanyService>().GetByAll().Datas;
                var os = ServiceContainer.GetService<OrderService>();
                foreach (var o in so)
                {
                    WPFHelper.DoEvents();
                    try
                    {
                        var st = ParseOrder(o.Source.PopOrderId);
                        if ((int)st.State >= (int)(OrderState.SHIPPED))
                        {
                            o.State = "订单已经发货";
                            o.Background = null;
                            o.Source.PopDeliveryTime = DateTime.Now;
                        }
                        else
                        {
                            var dc = dcs.FirstOrDefault(obj => obj.Name == o.DeliveryCompany).PopMapTaobao;
                            MarkPopDelivery(o.Source.PopOrderId, dc, o.DeliveryNumber);
                            o.State = "标记成功";
                            o.Background = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        o.State = ex.Message;
                        o.Background = Brushes.Red;
                    }
                }
                MessageBox.Show("所有订单标记完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnGoToTaobao_Click(object sender, RoutedEventArgs e)
        {
            string tag = (sender as Button).Tag as string;
            wb1 = new CefSharp.WinForms.ChromiumWebBrowser("https://trade.taobao.com/trade/itemlist/list_sold_items.htm");
            this.wfh.Child = wb1;
        }

        #region 前选 后选 

        private OrderViewModel GetMIOrder(object sender)
        {
            MenuItem mi = sender as MenuItem;
            var dg = ((ContextMenu)mi.Parent).PlacementTarget as DataGrid;
            var cells = dg.SelectedCells;
            if (cells.Count < 1)
            {
                throw new Exception("未选择数据");
            }

            var item = cells[0].Item as OrderViewModel;
            if (item == null)
            {
                throw new Exception("数据对象不正确");
            }
            return item;
        }

        private void miSelectPre_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = this.GetMIOrder(sender);
                MenuItem mi = sender as MenuItem;
                var orders = this.orders;
                int index = orders.IndexOf(item);
                for (int i = 0; i < orders.Count; i++)
                {
                    orders[i].IsChecked = i <= index ? true : false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void miSelectForward_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = this.GetMIOrder(sender);
                MenuItem mi = sender as MenuItem;
                var orders = this.orders;
                int index = orders.IndexOf(item);

                for (int i = 0; i < orders.Count; i++)
                {
                    orders[i].IsChecked = i >= index ? true : false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        #endregion

        private void dgvOrders_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            try
            {
                if (this.dgvOrders.SelectedCells.Count < 1)
                {
                    return;
                }
                var item = this.dgvOrders.SelectedCells[0].Item as OrderViewModel;
                if (item == null)
                {
                    throw new Exception("对象数据类型不为：" + typeof(OrderViewModel).FullName);
                }
                if (string.IsNullOrWhiteSpace(item.Source.PopOrderId))
                {
                    throw new Exception("该订单没有平台订单编号");
                }
                this.wb1.Load("https://wuliu.taobao.com/user/consign.htm?trade_id=" + item.Source.PopOrderId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
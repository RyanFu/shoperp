using ShopErp.Domain;
using ShopErp.Domain.RestfulResponse;
using ShopErp.Server.Dao.NHibernateDao;
using ShopErp.Server.Service.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Windows;

namespace ShopErp.Server.Service.Restful
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, AddressFilterMode = AddressFilterMode.Exact)]
    public class OrderGoodsService : ServiceBase<OrderGoods, OrderGoodsDao>
    {
        private static readonly char[] NUMBERS = "0123456789��������������������һ�����������߰˾�".ToArray();
        private static readonly List<char> NUMBERS_REPLAYCE = "��������������������".ToList();
        private static readonly List<char> NUMBERS_REPLAYCE1 = "һ�����������߰˾�".ToList();

        private GoodsCount[] MegerGoodsCount(IList<GoodsCount> gcs)
        {
            var goodsCountMarks = ServiceContainer.GetService<DeliveryCompanyService>().GetByAll().Datas;

            //�ϲ�������ͳ��
            var goodsCounts = new List<GoodsCount>();
            foreach (var orderGoods in gcs)
            {
                //Ԥ�������ݣ�ɾ���ո��
                orderGoods.Vendor = orderGoods.Vendor.Trim();
                orderGoods.Number = orderGoods.Number.Trim();
                orderGoods.Edtion = orderGoods.Edtion.Trim().Replace("�汾", "").Replace("��", "");
                //������ǰ�Ƿ������ͬ���Ե���Ϣ
                var count = goodsCounts.FirstOrDefault(obj => obj.Vendor == orderGoods.Vendor && obj.Number == orderGoods.Number && obj.Edtion == orderGoods.Edtion && obj.Color == orderGoods.Color && obj.Size == orderGoods.Size);
                if (count == null)
                {
                    count = orderGoods;
                    count.DeliveryCounts = new List<DeliveryCount>();
                    count.DeliveryCompany = count.DeliveryCompany ?? string.Empty;
                    count.LastPayTime = count.FirstPayTime;
                    count.DeliveryCounts.Add(new DeliveryCount { DeliveryCompany = count.DeliveryCompany, Count = orderGoods.Count });
                    goodsCounts.Add(count);
                }
                else
                {
                    count.OrderId += "," + orderGoods.OrderId;
                    count.Count += orderGoods.Count;
                    if (count.DeliveryCompany.Contains(orderGoods.DeliveryCompany) == false)
                    {
                        count.DeliveryCompany += "," + orderGoods.DeliveryCompany;
                    }

                    if (count.DeliveryCounts.FirstOrDefault(obj => obj.DeliveryCompany == count.DeliveryCompany) != null)
                    {
                        count.DeliveryCounts.FirstOrDefault(obj => obj.DeliveryCompany == count.DeliveryCompany).Count += orderGoods.Count;
                    }
                    else
                    {
                        string d = orderGoods.DeliveryCompany ?? String.Empty;
                        count.DeliveryCounts.Add(new DeliveryCount { DeliveryCompany = d, Count = count.Count });
                    }

                    //��Ҫ������è���߳��������ȼ�
                    if (count.PopType != PopType.TMALL && (orderGoods.PopType == PopType.TMALL || orderGoods.PopType == PopType.CHUCHUJIE))
                    {
                        count.PopType = orderGoods.PopType;
                    }
                }

                //������С���ֵ
                foreach (var og in goodsCounts.Where(obj => obj.Vendor == count.Vendor && obj.Number == count.Number))
                {
                    if (og.Money <= 0 || count.Money <= 0)
                    {
                        continue;
                    }

                    if (og.Money > count.Money)
                    {
                        og.Money = count.Money;
                    }
                    else
                    {
                        count.Money = og.Money;
                    }
                }
                //����ʱ��
                if (count.FirstPayTime >= orderGoods.FirstPayTime)
                {
                    count.FirstPayTime = orderGoods.FirstPayTime;
                }

                //����ʱ��
                if (count.LastPayTime <= orderGoods.LastPayTime)
                {
                    count.LastPayTime = orderGoods.LastPayTime;
                }
            }

            //���س��ҵ�ַ
            Vendor[] vendors = ServiceContainer.GetService<VendorService>().GetByAll("", "", "", "", 0, 0).Datas.ToArray();
            //����ַ
            foreach (var count in goodsCounts)
            {
                try
                {
                    Vendor vendor = vendors.FirstOrDefault(obj => obj.Name.Equals(count.Vendor));
                    if (vendor != null)
                    {
                        count.Address = vendor.MarketAddress;
                    }
                    else
                    {
                        count.Address = "";
                    }
                }
                catch
                {

                }
            }

            //��������,���㱸ע
            foreach (var goodsCount in goodsCounts)
            {
                //�������Ʊ��
                Vendor vendor = vendors.FirstOrDefault(obj => obj.Name.Equals(goodsCount.Vendor));
                string address = vendor == null ? "" : (vendor.MarketAddress ?? "");
                goodsCount.Vendor = VendorService.FormatVendorName(goodsCount.Vendor);

                try
                {
                    goodsCount.Door = int.Parse(VendorService.FindDoor(address));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("�����������ƺų���" + ex.Message);
                }
                try
                {
                    goodsCount.Area = int.Parse(VendorService.FindAreaOrStreet(address, "��"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("����������������" + ex.Message);
                }
                try
                {
                    goodsCount.Street = int.Parse(VendorService.FindAreaOrStreet(address, "��"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("�������ҽֵ�����" + ex.Message);
                }
                goodsCount.LianLang = address.Contains("����");
                goodsCount.Address = string.Format("{0}-{1}-{2}", goodsCount.Area, goodsCount.Door, goodsCount.Street);

                //�����Ƿ�����������
                if (string.IsNullOrWhiteSpace(goodsCount.DeliveryCompany) == false)
                {
                    int count = 0;
                    foreach (var vv in goodsCount.DeliveryCounts)
                    {
                        var dc = goodsCountMarks.FirstOrDefault(obj => obj.Name == vv.DeliveryCompany);
                        if (dc != null && dc.NormalPaperMark)
                        {
                            count += vv.Count;
                        }
                    }
                    if (count > 0)
                    {
                        goodsCount.Comment = "��" + count;
                    }
                }
            }
            return goodsCounts.ToArray();
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getgoodscount.html")]
        public DataCollectionResponse<GoodsCount> GetGoodsCount(ColorFlag[] flags, DateTime startTime, DateTime endTime, int pageIndex, int pageSize)
        {
            try
            {
                var gcs = this.dao.GetOrderGoodsCount(flags, startTime, endTime, pageIndex, pageSize).Datas;
                var ngcs = this.MegerGoodsCount(gcs);
                return new DataCollectionResponse<GoodsCount>(ngcs);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getsalecount.html")]
        public DataCollectionResponse<SaleCount> GetSaleCount(long shopId, OrderType type, int timeType, DateTime startTime, DateTime endTime, string popNumberId, int pageIndex, int pageSize)
        {
            try
            {
                return new DataCollectionResponse<SaleCount>(this.dao.GetSaleCount(shopId, type, timeType, startTime, endTime, popNumberId, pageIndex, pageSize).Datas);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        public DataCollectionResponse<OrderGoods> GetByOrderId(long orderId)
        {
            try
            {
                return new DataCollectionResponse<OrderGoods>(this.dao.GetAllByField("OrderId", orderId, 0, 0).Datas);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }
    }
}


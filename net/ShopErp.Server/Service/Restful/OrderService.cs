using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using ShopErp.Server.Service.Pop;
using ShopErp.Domain;
using ShopErp.Server.Dao.NHibernateDao;
using System.ServiceModel;
using System.ServiceModel.Web;
using ShopErp.Domain.Pop;
using ShopErp.Domain.RestfulResponse;
using ShopErp.Server.Log;
using ShopErp.Server.Service.Net;
using System.IO;
using NHibernate.Util;
using ShopErp.Domain.RestfulResponse.DomainResponse;

namespace ShopErp.Server.Service.Restful
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, AddressFilterMode = AddressFilterMode.Exact)]
    public class OrderService : ServiceBase<Order, OrderDao>
    {
        public const string UPDATE_RET_NOEXIST = "���ض����в�����";
        public const string UPDATE_RET_NOUPDATED = "������ƽ̨��δ����";
        public const string UPDATE_RET_UPDATED = "�Ѹ��¶���";

        protected static readonly char[] SPILTE_CHAR = new char[] { '(', '��', '[', '��' };

        private PopService ps = new PopService();

        private OrderGoodsDao ogDao = new OrderGoodsDao();

        private Order GetByIdWithException(long id)
        {
            Order or = this.dao.GetById(id);
            if (or == null)
            {
                throw new Exception("����������");
            }
            return or;
        }

        /// <summary>
        /// ȥ����ɫ����������()��[]��Χ������
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        protected static string FilterColorOrSize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            int indexL = value.IndexOfAny(new char[] { '(', '��' });
            int indexR = value.IndexOfAny(new char[] { ')', '��' });

            if (indexL >= 0 && indexR > indexL)
            {
                value = value.Substring(0, indexL);
            }

            indexL = value.IndexOfAny(new char[] { '[', '��' });
            indexR = value.IndexOfAny(new char[] { ']', '��' });

            if (indexL >= 0 && indexR > indexL)
            {
                value = value.Substring(0, indexL);
            }

            return value;
        }

        protected void ParseColorSizeEditon(string iColor, string iSize, out string oColor, out string oEdtion, out string oSize)
        {
            oSize = FilterColorOrSize(iSize);

            if (iColor.IndexOfAny(SPILTE_CHAR) >= 0)
            {
                oColor = iColor.Substring(0, iColor.IndexOfAny(SPILTE_CHAR));
                oEdtion = iColor.Substring(iColor.IndexOfAny(SPILTE_CHAR));
                oEdtion = oEdtion.Replace("(", "").Replace("��", "").Replace(")", "").Replace("��", "").Replace("[", "").Replace("��", "").Replace("]", "").Replace("��", "").Replace(" ", "");
            }
            else
            {
                oColor = iColor;
                oEdtion = "";
            }
            if (string.IsNullOrWhiteSpace(oEdtion) == false)
            {
                oEdtion = oEdtion.Replace("��", "");
            }
        }

        private void FillEmptyAndParseGoods(Order order)
        {
            if (order.ShopId < 1)
            {
                throw new Exception("����ShopIdС��1");
            }
            if (order.CreateType == OrderCreateType.NONE)
            {
                throw new Exception("����CreateType����ΪNONE");
            }
            if (order.Type == OrderType.NONE)
            {
                throw new Exception("����Type����ΪNONE");
            }
            if (order.PopType == PopType.None)
            {
                throw new Exception("����PopType����ΪNONE");
            }
            order.PopOrderId = order.PopOrderId ?? string.Empty;
            order.PopBuyerId = order.PopBuyerId ?? string.Empty;
            if (order.PopPayType == PopPayType.None)
            {
                throw new Exception("����PopPayType����ΪNONE");
            }
            order.PopCodSevFee = order.PopCodSevFee < 0 ? 0 : order.PopCodSevFee;
            order.PopCodNumber = order.PopCodNumber ?? string.Empty;
            order.PopSellerComment = order.PopSellerComment ?? string.Empty;
            order.PopBuyerComment = order.PopBuyerComment ?? string.Empty;
            order.PopState = order.PopState ?? String.Empty;
            if (order.PopFlag == ColorFlag.None)
            {
                throw new Exception("����PopFlag����ΪNONE");
            }
            if (string.IsNullOrWhiteSpace(order.ReceiverName))
            {
                throw new Exception("����ReceiverName����Ϊ��");
            }
            if (string.IsNullOrWhiteSpace(order.ReceiverAddress))
            {
                throw new Exception("����ReceiverAddress����Ϊ��");
            }
            if (string.IsNullOrWhiteSpace(order.ReceiverPhone) && string.IsNullOrWhiteSpace(order.ReceiverMobile))
            {
                throw new Exception("����ReceiverPhone,��ReceiverMobile ����ͬʱΪ��");
            }
            //if (order.DeliveryTemplateId < 1)
            //{
            //    throw new Exception("����DeliveryTemplateId����С��1");
            //}
            order.DeliveryCompany = order.DeliveryCompany ?? string.Empty;
            order.DeliveryNumber = order.DeliveryNumber ?? string.Empty;
            order.PopCreateTime = this.IsDbMinTime(order.PopCreateTime) ? DateTime.Now : order.PopCreateTime;
            order.PopPayTime = this.IsDbMinTime(order.PopPayTime) ? DateTime.Now : order.PopPayTime;
            order.PopDeliveryTime = this.IsDbMinTime(order.PopDeliveryTime) ? this.GetDbMinTime() : order.PopDeliveryTime;
            order.CreateTime = this.IsDbMinTime(order.CreateTime) ? DateTime.Now : order.CreateTime;
            order.PrintTime = this.IsDbMinTime(order.PrintTime) ? this.GetDbMinTime() : order.PrintTime;
            order.DeliveryTime = this.IsDbMinTime(order.DeliveryTime) ? this.GetDbMinTime() : order.DeliveryTime;
            order.CloseTime = this.IsDbMinTime(order.CloseTime) ? this.GetDbMinTime() : order.CloseTime;
            order.CreateOperator = ServiceContainer.GetCurrentLoginInfo().op.Number;
            order.PrintOperator = order.PrintOperator ?? string.Empty;
            order.DeliveryOperator = order.DeliveryOperator ?? string.Empty;
            order.CloseOperator = order.CloseOperator ?? string.Empty;
            order.ParseResult = true;

            if (order.State == OrderState.NONE)
            {
                throw new Exception("����State����ΪNONE");
            }

            if (order.OrderGoodss != null && order.OrderGoodss.Count > 0)
            {
                foreach (var item in order.OrderGoodss)
                {
                    try
                    {
                        ServiceContainer.GetService<GoodsService>().ParsePopOrderGoodsNumber(item);
                    }
                    catch
                    {
                    }
                    item.Vendor = item.Vendor ?? string.Empty;
                    item.Number = item.Number ?? string.Empty;
                    item.PopNumber = item.PopNumber ?? string.Empty;
                    item.Edtion = item.Edtion ?? string.Empty;
                    item.Color = item.Color ?? string.Empty;
                    item.Size = item.Size ?? string.Empty;
                    item.PopUrl = item.PopUrl ?? String.Empty;
                    item.PopInfo = item.PopInfo ?? string.Empty;
                    item.PopOrderSubId = item.PopOrderSubId ?? string.Empty;
                    item.CloseTime = this.IsDbMinTime(item.CloseTime) ? this.GetDbMinTime() : item.CloseTime;
                    item.CloseOperator = item.CloseOperator ?? string.Empty;
                    item.Comment = item.Comment ?? string.Empty;
                    item.StockTime = this.IsDbMinTime(item.StockTime) ? this.GetDbMinTime() : item.StockTime;
                    item.StockOperator = item.StockOperator ?? string.Empty;
                    item.Image = item.Image ?? string.Empty;
                    if (item.State == OrderState.NONE)
                    {
                        throw new Exception("������Ʒ״̬����ΪNONE��" + item.Vendor + " " + item.Number);
                    }
                    if (item.PopRefundState == PopRefundState.NONE)
                    {
                        throw new Exception("������Ʒ�˿��ΪNONE��" + item.Vendor + " " + item.Number);
                    }
                    //���а汾���ܰ�������ɫ��
                    string color = null, edtion = null, size = null;
                    ParseColorSizeEditon(item.Color, item.Size, out color, out edtion, out size);
                    item.Color = string.IsNullOrWhiteSpace(color) ? item.Color : color;
                    item.Edtion = string.IsNullOrWhiteSpace(edtion) ? item.Edtion : edtion;
                    item.Size = string.IsNullOrWhiteSpace(size) ? item.Size : size;
                }
                order.ParseResult = order.OrderGoodss.All(o => o.NumberId > 0);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbyid.html")]
        public DataCollectionResponse<Order> GetById(string id)
        {
            try
            {
                id = id.Trim();
                if (id.All(c => Char.IsDigit(c)) && id.Length < 14)
                {
                    var item = this.dao.GetById(long.Parse(id));
                    if (item != null)
                        return new DataCollectionResponse<Order>(item);
                }
                var items = this.dao.GetAllByField("PopOrderId", id, 0, 0).Datas;
                return new DataCollectionResponse<Order>(items);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbypoporderid.html")]
        public DataCollectionResponse<Order> GetByPopOrderId(string popOrderId)
        {
            try
            {
                var item = this.dao.GetAllByField("PopOrderId", popOrderId, 0, 0).Datas;
                return new DataCollectionResponse<Order>(item);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbydeliverynumber.html")]
        public DataCollectionResponse<Order> GetByDeliveryNumber(string deliveryNumber)
        {
            try
            {
                var item = this.dao.GetAllByField("DeliveryNumber", deliveryNumber, 0, 0).Datas;
                return new DataCollectionResponse<Order>(item);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/save.html")]
        public LongResponse Save(Order value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value.PopOrderId) == false)
                {
                    //������ݿ��Ƿ����
                    var count = this.dao.GetColumnValueBySqlQuery<long>("select count(Id) from `Order` where PopOrderId='" + value.PopOrderId + "'").First();
                    if (count > 0)
                    {
                        throw new Exception("������ţ�" + value.PopOrderId + "�Ѿ�����");
                    }
                }
                FillEmptyAndParseGoods(value);
                this.dao.Save(value);
                if (value.OrderGoodss != null && value.OrderGoodss.Count > 0)
                {
                    foreach (var v in value.OrderGoodss)
                    {
                        v.OrderId = value.Id;
                    }
                    this.dao.Save(value.OrderGoodss.ToArray());
                }
                return new LongResponse(value.Id);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/update.html")]
        public ResponseBase Update(Order value)
        {
            try
            {
                if (value.Id < 1)
                {
                    throw new Exception("����δ�����������ֱ�Ӹ���");
                }

                FillEmptyAndParseGoods(value);
                this.dao.Update(value);

                //ɾ����ǰ�ģ����ڶ�����û����Ʒ
                var ogs = ogDao.GetAllByField("OrderId", value.Id, 0, 0).Datas;
                var toDelete = value.OrderGoodss == null ? ogs.ToArray() : ogs.Where(obj => value.OrderGoodss.FirstOrDefault(o => o.Id == obj.Id) == null).ToArray();
                if (toDelete.Length > 0)
                {
                    ogDao.Delete(toDelete);
                }

                if (value.OrderGoodss != null && value.OrderGoodss.Count > 0)
                {
                    foreach (var v in value.OrderGoodss)
                    {
                        v.OrderId = value.Id;
                    }
                    this.dao.SaveOrUpdateById(value.OrderGoodss.ToArray());
                }
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/delete.html")]
        public ResponseBase Delete(long id)
        {
            try
            {
                this.dao.ExcuteSqlUpdate("delete from `order` where Id=" + id);
                this.dao.ExcuteSqlUpdate("delete from `OrderGoods` where OrderId=" + id);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/updatedelivery.html")]
        public ResponseBase UpdateDelivery(long id, long deliveryTemplateId, string deliveryCompany, string deliveryNumber, PaperType paperyType, DateTime printTime)
        {
            try
            {
                var or = this.GetByIdWithException(id);
                or.DeliveryCompany = deliveryCompany;
                or.DeliveryNumber = deliveryNumber;
                or.PrintPaperType = paperyType;
                or.DeliveryTemplateId = deliveryTemplateId;
                //��ݵ���Ϊ�գ���ʾ��Ҫ���ô�ӡ
                if (string.IsNullOrWhiteSpace(deliveryNumber))
                {
                    foreach (var og in or.OrderGoodss)
                    {
                        if (og.State == OrderState.PRINTED)
                        {
                            og.State = OrderState.PAYED;
                            this.dao.Update(og);
                        }
                    }
                    if ((int)or.State >= (int)OrderState.PRINTED && (int)or.State < (int)OrderState.SHIPPED)
                    {
                        or.State = OrderState.PAYED;
                    }
                    or.PrintTime = this.GetDbMinTime();
                    or.PrintOperator = "";
                    this.dao.Update(or);
                }
                else
                {
                    foreach (var og in or.OrderGoodss)
                    {
                        if (og.State == OrderState.PAYED || (this.IsDbMinTime(or.DeliveryTime) && or.State == OrderState.RETURNING))
                        {
                            og.State = OrderState.PRINTED;
                            this.dao.Update(og);
                        }
                    }
                    if (or.State == OrderState.PAYED || (this.IsDbMinTime(or.DeliveryTime) && or.State == OrderState.RETURNING))
                    {
                        or.State = OrderState.PRINTED;
                    }
                    or.PrintOperator = "";
                    or.PrintTime = printTime;
                    this.dao.Update(or);
                }
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }

        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/markdelivery.html")]
        public DataCollectionResponse<Order> MarkDelivery(string deliveryNumber, float weight, bool ingorePopError, bool ingoreWeightDetect, bool ingoreStateCheck)
        {
            try
            {
                string op = ServiceContainer.GetCurrentLoginInfo().op.Number;
                var orders = this.GetByAll("", "", "", "", "", 0, DateTime.Now.AddDays(-90), DateTime.MinValue, "", deliveryNumber, OrderState.NONE, PopPayType.None, "", "", null, -1, "", 0, OrderCreateType.NONE, OrderType.NONE, 0, 0).Datas;

                if (orders == null || orders.Count < 1)
                {
                    throw new Exception("����������");
                }

                //���˵�ϵͳ����ǰ�رյĶ������浥�п�������
                if (orders.Count > 1 && ingoreStateCheck == false)
                {
                    orders = orders.Where(obj => obj.State != OrderState.RETURNING && obj.State != OrderState.CANCLED && obj.State != OrderState.CLOSED && obj.State != OrderState.CLOSEDAFTERTRADE).ToList();
                }

                if (orders.Count < 1)
                {
                    throw new Exception("����״̬����");
                }

                //������������
                int normalOrderCount = orders.Count(obj => obj.Type == OrderType.NORMAL);

                //��������Ϣ��״̬
                if (orders.Select(obj => obj.ShopId).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ�����ҵ��̲�һ��");
                }

                if (orders.Select(obj => obj.PopBuyerId).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ����������˺Ų�һ��");
                }

                if (orders.Select(obj => obj.ReceiverName).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ�������ջ���������һ��");
                }

                if (orders.Select(obj => obj.ReceiverPhone).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ�������ջ��˵绰��һ��");
                }

                if (orders.Select(obj => obj.ReceiverMobile).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ�������ջ����ֻ���һ��");
                }

                if (orders.Select(obj => obj.ReceiverAddress.Trim()).Distinct().Count() > 1 && normalOrderCount > 1)
                {
                    throw new Exception("�����ʵ�������ջ��˵�ַ��һ��");
                }

                if (orders.Any(obj => (int)obj.State < (int)OrderState.PRINTED || (int)obj.State > (int)OrderState.SHIPPED) && ingoreStateCheck == false)
                {
                    throw new Exception("����״̬����ȷ");
                }

                //������⣬ˢ���Ķ��������ų�����
                var totalOgs = new List<OrderGoods>();
                foreach (var or in orders.Where(obj => obj.Type != OrderType.SHUA))
                {
                    if (ingoreStateCheck == false)
                    {
                        totalOgs.AddRange(or.OrderGoodss.Where(obj => (int)obj.State <= (int)OrderState.SHIPPED));
                    }
                    else
                    {
                        totalOgs.AddRange(or.OrderGoodss.Where(obj => obj.State != OrderState.CLOSED && obj.State != OrderState.SPILTED && obj.State != OrderState.CANCLED && obj.State != OrderState.CLOSEDAFTERTRADE && obj.State != OrderState.NOTSALE));
                    }
                }
                float totalOrderWeight = totalOgs.Select(obj => obj.Weight * obj.Count).Sum();
                int unWeightCount = totalOgs.Where(obj => obj.Weight <= 0).Select(obj => obj.Count).Sum();
                int totalCount = totalOgs.Select(obj => obj.Count).Sum();

                if (ingoreWeightDetect == false && totalCount > 0)
                {
                    if (unWeightCount == 0)
                    {
                        // ������Ʒ��������
                        if (Math.Abs(totalOrderWeight - weight) > 0.2)
                        {
                            throw new Exception(string.Format("����������ǰ:{0:F2},ϵͳ:{1:F2}", weight, totalOrderWeight));
                        }
                    }
                    else
                    {
                        if (weight < totalOrderWeight + unWeightCount * 0.2)
                        {
                            throw new Exception(string.Format("������Ʒ����Ӧ�ô��ڵ�ǰ:{0:F2},Ԥ��ֵ:{1:F2}", weight, totalOrderWeight + unWeightCount * 0.2));
                        }
                    }
                }
                //������ֻ��һ����Ʒû���������Ҳ����������
                if (totalOgs.Count == 1 && totalOgs[0].IsPeijian == false && ingoreWeightDetect == false)
                {
                    var gu = ServiceContainer.GetService<GoodsService>().GetById(totalOgs[0].NumberId).First;
                    if (gu != null)
                    {
                        float w = (float)Math.Round(weight / totalOgs[0].Count, 2);
                        float nw = (float)Math.Round((w + gu.Weight) / 2, 2);
                        ServiceContainer.GetService<GoodsService>().UpdateWeight(totalOgs[0].NumberId, nw);
                    }
                }

                //��������
                double deliveryMoney = ServiceContainer.GetService<DeliveryTemplateService>().ComputeDeliveryMoneyImpl(orders[0].DeliveryCompany, orders[0].ReceiverAddress, orders[0].Type == OrderType.SHUA, orders[0].PrintPaperType, orders[0].PopPayType, weight);

                //���¶���״̬���˷ѽ����Ϣ
                List<object> objsToUpdate = new List<object>();
                foreach (var or in orders)
                {
                    if (ingoreStateCheck == false)
                    {
                        objsToUpdate.AddRange(or.OrderGoodss.Where(obj => (int)obj.State <= (int)OrderState.SHIPPED));
                    }
                    else
                    {
                        objsToUpdate.AddRange(or.OrderGoodss.Where(obj => obj.State != OrderState.CLOSED && obj.State != OrderState.SPILTED && obj.State != OrderState.CANCLED && obj.State != OrderState.CLOSEDAFTERTRADE && obj.State != OrderState.NOTSALE));
                    }
                }

                foreach (OrderGoods og in objsToUpdate)
                {
                    og.State = OrderState.SHIPPED;
                    og.Comment = "";
                }

                foreach (var order in orders)
                {
                    order.DeliveryMoney = orders.Count > 1 ? order.DeliveryMoney : (float)deliveryMoney;
                    order.Weight = orders.Count > 1 ? order.Weight : (float)weight;
                    order.DeliveryTime = DateTime.Now;
                    order.DeliveryOperator = op;
                    order.State = (int)order.State < (int)OrderState.SHIPPED || order.State == OrderState.RETURNING ? OrderState.SHIPPED : order.State;
                    objsToUpdate.Add(order);
                    if (order.ShopId < 1 || string.IsNullOrWhiteSpace(order.PopOrderId))
                    {
                        continue;
                    }
                    //���ƽ̨����
                    try
                    {
                        Shop s = ServiceContainer.GetService<ShopService>().GetById(order.ShopId).First;
                        if (s == null)
                        {
                            throw new Exception("����:" + order.Id + "������Ϣ������");
                        }

                        if (s.AppEnabled == false)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(s.AppKey) || string.IsNullOrWhiteSpace(s.AppSecret) || string.IsNullOrWhiteSpace(s.AppAccessToken))
                        {
                            throw new Exception("������Ȩ��Ϣ������");
                        }

                        this.ps.MarkDelivery(s, order.PopOrderId, order.PopPayType, order.DeliveryCompany, order.DeliveryNumber);
                        if (order.PopPayType == PopPayType.ONLINE && order.PopDeliveryTime <= GetDbMinTime())//����֧���Ÿ���
                        {
                            order.PopDeliveryTime = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ingorePopError == false)
                        {
                            throw ex;
                        }
                    }
                }
                //ɾ����ǰ��ͬ�ķ�����Ϣ
                ServiceContainer.GetService<DeliveryOutService>().DeleteOrderDeliveryOut(deliveryNumber);
                //���ɷ�����Ϣ
                var deliveryOut = new DeliveryOut
                {
                    CreateTime = DateTime.Now,
                    DeliveryCompany = orders[0].DeliveryCompany,
                    DeliveryNumber = orders[0].DeliveryNumber,
                    Operator = op,
                    OrderId = string.Join(",", orders.Select(obj => obj.Id.ToString())),
                    ERPDeliveryMoney = (float)deliveryMoney,
                    ERPGoodsMoney = totalOgs.Select(obj => obj.Price * obj.Count).Sum(),
                    PopGoodsMoney = orders.Where(obj => obj.Type != OrderType.SHUA).Select(obj => obj.PopSellerGetMoney).Sum(),
                    PopPayType = orders[0].PopPayType,
                    PopType = orders[0].PopType,
                    ReceiverAddress = orders[0].ReceiverAddress,
                    ShopId = orders[0].ShopId,
                    Weight = (float)weight,
                    PopCodSevFee = orders.Select(obj => obj.PopCodSevFee).Sum(),
                    GoodsInfo = string.Join(",", totalOgs.Select(obj => VendorService.FormatVendorName(obj.Vendor) + " " + obj.Number + " " + obj.Edtion + " " + obj.Color + " " + obj.Size + " " + obj.Count)),
                };
                if (deliveryOut.GoodsInfo.Length > 1000)
                {
                    deliveryOut.GoodsInfo = deliveryOut.GoodsInfo.Substring(0, 1000);
                }
                this.dao.Update(objsToUpdate.ToArray());
                this.dao.Save(deliveryOut);
                return new DataCollectionResponse<Order>(orders);
            }
            catch (WebFaultException<ResponseBase> ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/markpopdelivery.html")]
        public ResponseBase MarkPopDelivery(long id)
        {
            try
            {
                var or = this.GetByIdWithException(id);
                if (string.IsNullOrWhiteSpace(or.DeliveryCompany) || string.IsNullOrWhiteSpace(or.DeliveryNumber))
                {
                    throw new Exception("���ض���������ϢΪ��");
                }

                if (string.IsNullOrWhiteSpace(or.PopOrderId))
                {
                    throw new Exception("����ƽ̨���Ϊ��");
                }

                if ((int)or.State >= (int)OrderState.PRINTED && (int)or.State <= (int)OrderState.SHIPPED)
                {
                    Shop s = ServiceContainer.GetService<ShopService>().GetById(or.ShopId).First;
                    if (s == null)
                    {
                        throw new Exception("����������Ϣ������");
                    }

                    if (s.AppEnabled == false)
                    {
                        throw new Exception("���̽ӿ��ѽ��ã��޷�������Ӧ�ӿڲ���");
                    }

                    if (string.IsNullOrWhiteSpace(s.AppKey) || string.IsNullOrWhiteSpace(s.AppSecret) || string.IsNullOrWhiteSpace(s.AppAccessToken))
                    {
                        throw new Exception("����������Ȩ��ϢΪ��");
                    }

                    this.ps.MarkDelivery(s, or.PopOrderId, or.PopPayType, or.DeliveryCompany, or.DeliveryNumber);
                    if (this.dao.IsLessDBMinDate(or.PopDeliveryTime))
                    {
                        this.dao.ExcuteSqlUpdate("update `Order` set PopDeliveryTime='" + this.FormatTime(DateTime.Now) + "' where Id=" + id);
                    }
                }
                else
                {
                    throw new Exception("���ض���״̬����");
                }
                return ResponseBase.SUCCESS;
            }
            catch (WebFaultException<ResponseBase> ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbyall.html")]
        public DataCollectionResponse<Order> GetByAll(string popBuyerId, string receiverPhone, string receiverMobile, string ReceiverName, string receiverAddress,
                    int timeType, DateTime startTime, DateTime endTime, string deliveryCompany, string deliveryNumber,
                    OrderState state, PopPayType payType, string vendorName, string number,
                    ColorFlag[] ofs, int parseResult, string comment, long shopId, OrderCreateType createType, OrderType type, int pageIndex, int pageSize)
        {
            try
            {
                return this.dao.GetByAll(popBuyerId, receiverPhone, receiverMobile, ReceiverName, receiverAddress, timeType, startTime, endTime, deliveryCompany, deliveryNumber, state, payType, vendorName, number, ofs, parseResult, comment, shopId, createType, type, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getpayedandprintedorders.html")]
        public DataCollectionResponse<Order> GetPayedAndPrintedOrders(long[] shopId, OrderCreateType createType, PopPayType payType, int pageIndex, int pageSize)
        {
            try
            {
                return this.dao.GetPayedAndPrintedOrders(shopId, createType, payType, pageIndex, pageSize);
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getordersbyinfoidnotequal.html")]
        public DataCollectionResponse<Order> GetOrdersByInfoIdNotEqual(string popBuyerId, string receiverPhone, string receiverMobile, string receiverAddress, long id)
        {
            try
            {
                return this.dao.GetOrdersByInfoIDNotEqual(popBuyerId, receiverPhone, receiverMobile, receiverAddress, id);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getorderpopcodenumber.html")]
        public StringResponse GetOrderPopCodNumber(long id)
        {
            try
            {
                var or = this.GetByIdWithException(id);
                Shop s = ServiceContainer.GetService<ShopService>().GetById(or.ShopId).First;
                if (s == null)
                {
                    throw new Exception("������Ϣ������");
                }
                var deliveryInfo = this.ps.GetDeliveryInfo(s, or.PopOrderId);
                if (deliveryInfo != null && or.PopCodNumber != deliveryInfo.PopCodNumber)
                {
                    or.PopCodNumber = deliveryInfo.PopCodNumber;
                    this.Update(or);
                }
                return new StringResponse(deliveryInfo == null ? "" : deliveryInfo.PopCodNumber);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/closeorder.html")]
        public ResponseBase CloseOrder(long orderId, long orderGoodsId, int count)
        {
            try
            {
                string op = ServiceContainer.GetCurrentLoginInfo().op.Number;
                Order or = this.GetByIdWithException(orderId);
                if (or == null)
                {
                    throw new Exception("ָ���Ķ���������");
                }

                if (or.State == OrderState.SHIPPED)
                {
                    if (DateTime.Now.Subtract(or.DeliveryTime).TotalHours >= 24)
                    {
                        throw new Exception("����24ʱСʱ��Ķ������ܹر�");
                    }
                }

                if ((int)or.State > (int)OrderState.SHIPPED)
                {
                    throw new Exception("��״̬�²��ܹرն���");
                }

                var ogs = or.OrderGoodss.Where(obj => obj.State != OrderState.SPILTED && obj.State != OrderState.CLOSED && obj.State != OrderState.CANCLED).ToList();
                var og = ogs.FirstOrDefault(obj => obj.Id == orderGoodsId);

                if (orderGoodsId > 0 && og == null)
                {
                    throw new Exception("�����е���Ʒ������");
                }

                //�رյ�����Ʒ
                if (orderGoodsId > 0 && (ogs.Count > 1 || count < og.Count))
                {
                    if (count > og.Count)
                    {
                        throw new Exception("Ҫ�رյ��������ܴ�����Ʒ����");
                    }
                    if (count >= og.Count)
                    {
                        //������Ʒȫ��
                        og.State = OrderState.CLOSED;
                        og.CloseTime = DateTime.Now;
                        og.CloseOperator = op;
                    }
                    else
                    {
                        //�رղ���
                        og.Count -= count;
                        og.GetedCount = og.GetedCount > og.Count ? og.Count : og.GetedCount;
                    }
                    og.StockOperator = op;
                    og.StockTime = DateTime.Now;
                    this.dao.Update(og, or);
                }
                else
                {
                    foreach (var ogg in ogs)
                    {
                        ogg.State = OrderState.CLOSED;
                        ogg.CloseTime = DateTime.Now;
                        ogg.CloseOperator = op;
                    }
                    this.dao.Update(ogs.ToArray());
                    or.State = OrderState.CLOSED;
                    or.CloseTime = DateTime.Now;
                    or.CloseOperator = op;
                    if (this.IsDbMinTime(or.DeliveryTime) == false)
                    {
                        //ɾ��������¼
                        ServiceContainer.GetService<DeliveryOutService>().DeleteOrderDeliveryOut(or.DeliveryNumber);
                    }
                    or.DeliveryTime = this.GetDbMinTime();
                    or.DeliveryOperator = "";
                    this.dao.Update(or);
                }
                return ResponseBase.SUCCESS;
            }
            catch (WebFaultException<ResponseBase> ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }

        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/markordergoodsstate.html")]
        public ResponseBase MarkOrderGoodsState(long orderId, long orderGoodsId, OrderState state, string stockComment)
        {
            try
            {
                string op = ServiceContainer.GetCurrentLoginInfo().op.Number;
                Order order = this.GetByIdWithException(orderId);
                OrderGoods og = null;

                if ((int)state < (int)OrderState.PRINTED && state != OrderState.NONE)
                {
                    throw new Exception("���ܽ������޸ĳɴ�ӡ����ǰ��״̬");
                }

                if (state >= OrderState.SHIPPED && state != OrderState.NONE)
                {
                    throw new Exception("���ܽ������޸ĳɷ���״̬");
                }

                if ((int)order.State < (int)OrderState.PAYED)
                {
                    throw new Exception("δ��������ܸ���");
                }

                if ((int)order.State >= (int)OrderState.SHIPPED && state != OrderState.NONE)
                {
                    throw new Exception("�Ѿ����������ܸ���");
                }
                List<OrderGoods> ogs = order.OrderGoodss.Where(obj => (int)obj.State < (int)OrderState.SHIPPED).ToList();
                foreach (OrderGoods o in ogs)
                {
                    if (o.Id == orderGoodsId)
                    {
                        og = o;
                        break;
                    }
                }

                if (og == null)
                {
                    throw new Exception("������Ʒ�����ڻ���״̬����ȷ");
                }

                if (state != OrderState.NONE)
                {
                    og.State = state;
                }
                og.Comment = stockComment;
                og.StockTime = DateTime.Now;
                og.StockOperator = op;

                if (state == OrderState.GETED)
                {
                    int val = int.Parse(stockComment.Replace("����", "").Replace("˫", ""));
                    if (val > og.Count)
                    {
                        throw new Exception("�û��������ܱ���������");
                    }
                    og.GetedCount = val;
                }
                else if (state != OrderState.NONE)
                {
                    og.GetedCount = 0;
                }

                bool allTheSame = true;

                for (int i = 1; i < ogs.Count; i++)
                {
                    if (ogs[i].State != ogs[0].State)
                    {
                        allTheSame = false;
                        break;
                    }
                }

                if (allTheSame && (int)order.State >= (int)OrderState.PRINTED)
                {
                    order.State = ogs[0].State;
                }

                this.dao.Update(og, order);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }

        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/spilteordergoods.html")]
        public ResponseBase SpilteOrderGoods(long orderId, OrderSpilteInfo[] infos)
        {
            try
            {
                string op = ServiceContainer.GetCurrentLoginInfo().op.Number;
                Order or = this.GetByIdWithException(orderId);
                // �����Ϣ�Ϸ���
                var ogs = or.OrderGoodss.Where(obj => (int)obj.State < (int)OrderState.SHIPPED).ToList();
                if (ogs.Count < 1)
                {
                    throw new Exception("û����Ʒ���Բ��");
                }

                foreach (var spilteInfo in infos)
                {
                    OrderGoods og = ogs.FirstOrDefault(obj => obj.Id == spilteInfo.OrderGoodsId);
                    if (og == null)
                    {
                        throw new Exception("������Ʒ������:" + spilteInfo.OrderGoodsId);
                    }

                    if ((int)og.State < (int)OrderState.PAYED || (int)og.State > (int)OrderState.SHIPPED)
                    {
                        throw new Exception("������Ʒ״̬���ܱ����:" + og.Id);
                    }

                    if (og.Count < spilteInfo.Count)
                    {
                        throw new Exception("Ҫ��ֵ���Ʒʵ��������:" + og.Id);
                    }
                }
                // �����¶���
                Order nor = new Order
                {
                    PopBuyerComment = or.PopBuyerComment,
                    CloseOperator = "",
                    CloseTime = this.GetDbMinTime(),
                    State = OrderState.PAYED,
                    PrintTime = this.GetDbMinTime(),
                    ParseResult = true,
                    CreateTime = DateTime.Now,
                    DeliveryCompany = "",
                    DeliveryNumber = "",
                    DeliveryOperator = "",
                    DeliveryTime = this.GetDbMinTime(),
                    DeliveryMoney = 0,
                    PopDeliveryTime = or.PopDeliveryTime,
                    PopPayTime = or.PopPayTime,
                    OrderGoodss = new List<OrderGoods>(),
                    PopBuyerId = or.PopBuyerId,
                    PopCodNumber = or.PopCodNumber,
                    PopCreateTime = or.PopCreateTime,
                    PopFlag = or.PopFlag,
                    PopOrderId = "",
                    PopOrderTotalMoney = 0,
                    PopPayType = or.PopPayType,
                    PopSellerComment = or.PopSellerComment + " ԭ����:" + or.Id,
                    PopState = or.PopState,
                    PopType = or.PopType,
                    PrintOperator = "",
                    ReceiverAddress = or.ReceiverAddress,
                    ReceiverMobile = or.ReceiverMobile,
                    ReceiverName = or.ReceiverName,
                    ReceiverPhone = or.ReceiverPhone,
                    ShopId = or.ShopId,
                    Weight = 0,
                    CreateOperator = op,
                    PopCodSevFee = 0,
                    CreateType = OrderCreateType.MANUAL,
                    Type = or.Type,
                };

                List<Object> objsUpdate = new List<Object>();
                foreach (OrderSpilteInfo cuInfo in infos)
                {
                    OrderGoods og = ogs.FirstOrDefault(obj => obj.Id == cuInfo.OrderGoodsId);
                    OrderGoods nog = new OrderGoods
                    {
                        OrderId = 0,
                        Id = 0,
                        Count = cuInfo.Count,
                        State = OrderState.PAYED,
                        GetedCount = 0,
                        Price = og.Price,
                        CloseOperator = "",
                        CloseTime = this.GetDbMinTime(),
                        StockOperator = og.StockOperator,
                        StockTime = og.StockTime,
                        Comment = og.Comment,
                        Color = og.Color,
                        Edtion = og.Edtion,
                        Image = og.Image,
                        Number = og.Number,
                        NumberId = og.NumberId,
                        PopInfo = og.PopInfo,
                        PopOrderSubId = og.PopOrderSubId,
                        PopPrice = og.PopPrice,
                        PopUrl = og.PopUrl,
                        Size = og.Size,
                        Vendor = og.Vendor,
                        Weight = og.Weight,
                        PopNumber = og.PopNumber,
                    };
                    nor.OrderGoodss.Add(nog);

                    if (og.Count <= cuInfo.Count)
                    {
                        og.State = OrderState.SPILTED;
                    }
                    else
                    {
                        og.Count -= cuInfo.Count;
                    }
                    og.CloseOperator = op;
                    og.CloseTime = DateTime.Now;
                }

                //�¶�����Ʒ�ܶ�
                nor.Weight = nor.OrderGoodss.Select(obj => obj.Weight * obj.Count).Sum();
                nor.ParseResult = nor.OrderGoodss.Count(obj => obj.NumberId <= 0) > 0 ? false : true;

                //�ɶ���
                ogs = or.OrderGoodss.Where(obj => obj.State != OrderState.SPILTED).ToList();
                or.Weight = ogs.Select(obj => obj.Weight * obj.Count).Sum();
                or.ParseResult = ogs.Count(obj => obj.NumberId <= 0) > 0 ? false : true;

                // ��������
                try
                {
                    List<object> objs = new List<object>();
                    objs.Add(or);
                    objs.AddRange(or.OrderGoodss.ToArray());
                    this.dao.Update(objs.ToArray());
                    var nOgs = nor.OrderGoodss.ToArray();
                    nor.OrderGoodss.Clear();
                    this.Save(nor);
                    foreach (var og in nOgs)
                    {
                        og.OrderId = nor.Id;
                    }
                    this.dao.Save(nOgs);
                }
                catch (Exception ex)
                {
                    if (nor.Id > 0)
                    {
                        this.dao.Delete(nor);
                    }
                    throw ex;
                }
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }

        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/resetprintstate.html")]
        public ResponseBase ResetPrintState(long orderId)
        {
            var or = this.GetByIdWithException(orderId);
            this.UpdateDelivery(orderId, -1, "", "", PaperType.NONE, this.GetDbMinTime());
            return ResponseBase.SUCCESS;
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/modifypopsellercomment.html")]
        public ResponseBase ModifyPopSellerComment(long orderId, ColorFlag flag, string comment)
        {
            try
            {
                Order os = this.dao.GetById(orderId);
                if (os == null)
                {
                    throw new Exception("����������");
                }
                Shop s = ServiceContainer.GetService<ShopService>().GetById(os.ShopId).First;
                if (s == null)
                {
                    throw new Exception("������Ϣ������");
                }
                if ((s.PopType == PopType.TMALL || s.PopType == PopType.TAOBAO) && string.IsNullOrWhiteSpace(s.AppAccessToken) == false && string.IsNullOrWhiteSpace(os.PopOrderId) == false && s.AppEnabled)
                {
                    this.ps.ModifyComment(s, os.PopOrderId, comment, flag);
                }
                os.PopFlag = flag;
                os.PopSellerComment = comment;
                this.dao.Update(os);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, RequestFormat = WebMessageFormat.Json, UriTemplate = "/updateordertogeted.html")]
        public ResponseBase UpdateOrderToGeted(long orderId)
        {
            try
            {
                string op = ServiceContainer.GetCurrentLoginInfo().op.Number;
                var or = this.GetByIdWithException(orderId);
                if ((int)or.State >= (int)OrderState.SHIPPED)
                {
                    throw new Exception("�����Ѿ������޷����");
                }
                if ((int)or.State < (int)OrderState.PRINTED)
                {
                    throw new Exception("����δ��ӡ�޷����");
                }

                if (or.OrderGoodss == null || or.OrderGoodss.Count < 1)
                {
                    return ResponseBase.SUCCESS;
                }
                List<object> objs = new List<object>();
                or.State = OrderState.GETED;
                foreach (var og in or.OrderGoodss)
                {
                    if (((int)or.State < (int)OrderState.PAYED || (int)or.State >= (int)OrderState.SHIPPED) && or.State != OrderState.NOTSALE)
                    {
                        continue;
                    }
                    og.State = OrderState.GETED;
                    og.GetedCount = og.Count;
                    og.Comment = "����" + og.Count + "˫";
                    og.StockOperator = op;
                    og.StockTime = DateTime.Now;
                    objs.Add(og);
                }
                objs.Add(or);
                this.dao.Update(objs.ToArray());
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, RequestFormat = WebMessageFormat.Json, UriTemplate = "/getpopwaitsendorders.html")]
        public OrderDownloadCollectionResponse GetPopWaitSendOrders(Shop shop, PopPayType payType, int pageIndex, int pageSize)
        {
            try
            {
                var ret = this.ps.GetOrders(shop, payType == PopPayType.COD ? PopService.QUERY_STATE_WAITSHIP_COD : PopService.QUERY_STATE_WAITSHIP, pageIndex, pageSize);
                foreach (var or in ret.Datas)
                {
                    if (or.Order != null)
                    {
                        try
                        {
                            or.Order.ShopId = shop.Id;
                            or.Order.PopType = shop.PopType;
                            //��鶩���Ƿ���ڴ�
                            var count = GetColumnValueBySqlQuery<long>("select count(Id) from `Order` where PopOrderId='" + or.Order.PopOrderId + "'").First();
                            if (count > 1)
                            {
                                or.Error = new OrderDownloadError(or.Order.PopOrderId, or.Order.ReceiverName, "ϵͳ�д���2����������ͬ����");
                                or.Order = null;
                            }
                            else if (count < 1)
                            {
                                Save(or.Order);
                            }
                            else
                            {
                                string upRet = UpdateOrderStateWithGoods(or.Order, null, shop).data;
                                or.Order = GetByPopOrderId(or.Order.PopOrderId).First;
                            }
                        }
                        catch (WebFaultException<ResponseBase> we)
                        {
                            or.Error = new OrderDownloadError { Error = "�����ӽӿڳɹ����ش���ʱ����" + we.Detail.error, PopOrderId = or.Order.PopOrderId ?? "", ReceiverName = or.Order.ReceiverName ?? "", ShopId = shop.Id };
                            or.Order = null;
                        }
                        catch (Exception ex)
                        {
                            or.Error = new OrderDownloadError { Error = "�����ӽӿڳɹ����ش���ʱ����" + ex.Message, PopOrderId = or.Order.PopOrderId ?? "", ReceiverName = or.Order.ReceiverName ?? "", ShopId = shop.Id };
                            or.Order = null;
                        }
                    }
                    else if (or.Error != null)
                    {
                        or.Error.ShopId = shop.Id;
                    }
                    else
                    {
                        or.Error = new OrderDownloadError { Error = "�ӿڳ�����󶩵�����Order��Error��Ϊ��", PopOrderId = "", ReceiverName = "", ShopId = shop.Id };
                    }
                }
                return ret;
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// ���¶���״̬
        /// </summary>
        /// <param name="orderOnline">���������ص����¶���</param>
        /// <param name="orderInDb">���ض���,��������ֵ��������ݿ��ȡ</param>
        /// <returns></returns>
        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, RequestFormat = WebMessageFormat.Json, UriTemplate = "/updateorderstatewithgoods.html")]
        public StringResponse UpdateOrderStateWithGoods(Order orderOnline, OrderUpdate orderInDb, Shop shop)
        {
            OrderUpdateService ous = ServiceContainer.GetService<OrderUpdateService>();
            if (orderInDb == null)
            {
                //������ݿ��Ƿ����
                var ret = ous.GetByAll(null, orderOnline.PopOrderId, DateTime.MinValue, DateTime.Now.AddDays(1), 0, 0);
                if (ret == null || ret.Datas.Count < 1)
                {
                    return new StringResponse(UPDATE_RET_NOEXIST);
                }
                orderInDb = ret.Datas[0];
            }

            if (orderOnline.State == orderInDb.State &&
                orderOnline.PopState == orderInDb.PopState &&
                (orderOnline.OrderGoodss == null || orderOnline.OrderGoodss.Count == 1) &&
                Math.Abs(orderOnline.PopOrderTotalMoney - orderInDb.PopOrderTotalMoney) < 0.01F &&
                Math.Abs(orderOnline.PopCodSevFee - orderInDb.PopCodSevFee) < 0.01F)
            {
                return new StringResponse(UPDATE_RET_NOUPDATED);
            }

            orderInDb.PopState = orderOnline.PopState;
            orderInDb.PopCodNumber = orderOnline.PopCodNumber;
            orderInDb.PopPayTime = orderOnline.PopPayTime;
            orderInDb.PopOrderTotalMoney = orderOnline.PopOrderTotalMoney;
            orderInDb.PopCodSevFee = orderOnline.PopCodSevFee;
            var onlineState = orderOnline.State;
            OrderState targetState = orderInDb.State, dbState = orderInDb.State;

            if (onlineState == OrderState.WAITPAY)
            {
                //���������Ҫ����״̬
            }
            else if (onlineState == OrderState.PAYED)
            {
                if (orderInDb.State == OrderState.WAITPAY)
                {
                    targetState = OrderState.PAYED;
                }
                else if ((orderInDb.State == OrderState.RETURNING || orderInDb.State == OrderState.WAIT_REFUNED || orderInDb.State == OrderState.UNKNOWN) &&
                         ous.IsDbMinTime(orderInDb.DeliveryTime))
                {
                    if (ous.IsDbMinTime(orderInDb.PrintTime) == false)
                    {
                        targetState = OrderState.PRINTED;
                    }
                    else
                    {
                        targetState = onlineState;
                    }
                }
            }
            else if (onlineState == OrderState.SHIPPED)
            {
                //������˿��У�����Ϊ�ѷ���
                if (orderInDb.State == OrderState.RETURNING || orderInDb.State == OrderState.WAIT_REFUNED || orderInDb.State == OrderState.UNKNOWN)
                {
                    if (IsDbMinTime(orderInDb.DeliveryTime))
                    {
                        targetState = OrderState.PRINTED;
                    }
                    else
                    {
                        targetState = onlineState;
                    }
                }

                //�ѷ���,��ϵͳ�еĴ�ӡʱ�䣬��˵���ö�������ϵͳ��ӡ�ģ���Ҫ����״̬����ͬ������
                if (ous.IsDbMinTime(orderInDb.PrintTime))
                {
                    targetState = onlineState;
                }
            }
            else if (onlineState == OrderState.SUCCESS)
            {
                //�Ǳ��ش�ӡ
                if (ous.IsDbMinTime(orderInDb.PrintTime))
                {
                    targetState = onlineState;
                }
                else
                {
                    //�����Ѿ�����������Ϊ������ɣ���ֹ�û���ȷ���յ�������ϵͳ�޷�ͳ�Ʒ���
                    //�����ӡʱ�䳬��15��û�з���һ�㲻���ܵģ��п�����û��ɨ�赽����������Ҳ���Ը���
                    //������ô�죬ʱ�����ǳ���15��Ļ���20��ģ�
                    if (ous.IsDbMinTime(orderInDb.DeliveryTime) == false)
                    {
                        targetState = onlineState;
                    }
                }
            }
            else if ((int)onlineState > (int)OrderState.SHIPPED)
            {
                targetState = onlineState;
            }
            else
            {
                Logger.Log("���¶���ʧ��δ֪״̬[" + onlineState + "]");
                return new StringResponse("���¶���ʧ��δ֪״̬[" + onlineState + "]");
            }

            if (targetState == OrderState.NONE)
            {
                throw new Exception("Ҫ���³ɵĶ���״̬����Ϊ:" + targetState);
            }

            //�����Ѿ��رյĶ�������������״̬
            if (orderInDb.State != OrderState.CLOSED &&
                orderInDb.State != OrderState.CANCLED &&
                orderInDb.State != OrderState.CLOSEDAFTERTRADE &&
                orderInDb.State != OrderState.REFUSED)
            {
                //�ж����Ʒ������Ҫ����˻���ȡ���˻���Щ���
                if (orderOnline.OrderGoodss != null && orderOnline.OrderGoodss.Count > 1)
                {
                    foreach (var ogOnline in orderOnline.OrderGoodss)
                    {
                        if (ogOnline.State == OrderState.NONE)
                        {
                            throw new Exception("Ҫ���³ɵĶ�����Ʒ״̬����Ϊ:" + targetState);
                        }

                        //����û�з������˿�
                        if (ogOnline.PopRefundState == PopRefundState.NOT)
                        {
                            //ƽ̨״̬Ϊ�Ѹ���ұ���״̬�Ѿ������Ѹ�����ø���
                            if (ogOnline.State == OrderState.PAYED && (int)orderInDb.State >= (int)OrderState.PAYED)
                            {
                                continue;
                            }
                            //ƽ̨�ѷ������򱾵�δ����,���ø���
                            if (ogOnline.State == OrderState.SHIPPED && (int)orderInDb.State >= (int)OrderState.PRINTED && (int)orderInDb.State < (int)OrderState.SHIPPED)
                            {
                                continue;
                            }

                            //��ȷ���ջ����ұ��ش�ӡδ���������ø���
                            if (ogOnline.State == OrderState.SUCCESS && ous.IsDbMinTime(orderInDb.PrintTime) == false && ous.IsDbMinTime(orderInDb.DeliveryTime))
                            {
                                continue;
                            }
                            ous.UpdateOrderGoodsState(orderInDb.Id, ogOnline.PopInfo, ogOnline.Count, ogOnline.State);
                        }
                        else
                        {
                            if (ogOnline.PopRefundState == PopRefundState.ACCEPT || ogOnline.PopRefundState == PopRefundState.OK)
                            {
                                ous.UpdateOrderGoodsState(orderInDb.Id, ogOnline.PopInfo, ogOnline.Count, ogOnline.State);
                            }
                            else if (ogOnline.PopRefundState == PopRefundState.CANCEL || ogOnline.PopRefundState == PopRefundState.REJECT)
                            {
                                //��ȡ�����Ӷ���
                                var ogInDb = ServiceContainer.GetService<OrderGoodsService>().GetByOrderId(orderInDb.Id).Datas.FirstOrDefault(obj => obj.PopOrderSubId == ogOnline.PopOrderSubId);
                                var ogState = OrderState.NONE;
                                if (ogInDb != null)
                                {
                                    if (ogInDb.State == OrderState.WAITPAY || ogInDb.State == OrderState.PAYED)
                                    {
                                        ogState = OrderState.PAYED;
                                    }
                                    else if (ogInDb.State == OrderState.PRINTED)
                                    {

                                    }
                                    else if (ogInDb.State == OrderState.RETURNING || ogInDb.State == OrderState.WAIT_REFUNED || ogInDb.State == OrderState.UNKNOWN)
                                    {
                                        if (ous.IsDbMinTime(orderInDb.DeliveryTime) == false)
                                        {
                                            ogState = ogOnline.State;
                                        }
                                        else if (ous.IsDbMinTime(orderInDb.PrintTime) == false)
                                        {
                                            ogState = OrderState.PRINTED;
                                        }
                                        else
                                        {
                                            ogState = ogOnline.State;
                                        }
                                    }
                                    else
                                    {
                                        ogState = ogOnline.State;
                                    }

                                    if (ogState != OrderState.NONE)
                                    {
                                        ous.UpdateOrderGoodsState(orderInDb.Id, ogOnline.PopInfo, ogOnline.Count, ogState);
                                    }
                                }
                            }
                            else
                            {
                                throw new Exception("�������·���δʶ���ƽ̨�˿�״̬:" + ogOnline.PopRefundState);
                            }
                        }
                    }
                }
                else
                {
                    if (targetState != orderInDb.State)
                    {
                        ous.UpdateOrderGoodsStateByOrderId(orderInDb.Id, targetState);
                    }
                }
                orderInDb.State = targetState;
            }
            ous.UpdateEx(orderInDb, true);
            return new StringResponse(UPDATE_RET_UPDATED);
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, RequestFormat = WebMessageFormat.Json, UriTemplate = "/updateorderstate.html")]
        public StringResponse UpdateOrderState(PopOrderState orderStateOnline, OrderUpdate orderInDb, Shop shop)
        {
            try
            {
                var ous = ServiceContainer.GetService<OrderUpdateService>();
                if (orderInDb == null)
                {
                    //������ݿ��Ƿ����
                    var ret = ous.GetByAll(null, orderStateOnline.PopOrderId, DateTime.MinValue, DateTime.Now.AddDays(1), 0, 0);
                    if (ret == null || ret.Datas == null || ret.Datas.Count < 1)
                    {
                        return new StringResponse(UPDATE_RET_NOEXIST);
                    }
                    orderInDb = ret.Datas[0];
                }

                if (orderStateOnline.State == orderInDb.State &&
                    orderStateOnline.PopOrderStateValue == orderInDb.PopState)
                {
                    return new StringResponse(UPDATE_RET_NOUPDATED);
                }

                orderInDb.PopState = orderStateOnline.PopOrderStateValue;
                var onlineState = orderStateOnline.State;
                OrderState targetState = orderInDb.State, dbState = orderInDb.State;

                if (onlineState == OrderState.WAITPAY)
                {
                    //���������Ҫ����״̬
                }
                else if (onlineState == OrderState.PAYED)
                {
                    if (orderInDb.State == OrderState.WAITPAY)
                    {
                        targetState = OrderState.PAYED;
                    }
                    else if ((orderInDb.State == OrderState.RETURNING || orderInDb.State == OrderState.WAIT_REFUNED || orderInDb.State == OrderState.UNKNOWN) &&
                             ous.IsDbMinTime(orderInDb.DeliveryTime))
                    {
                        if (ous.IsDbMinTime(orderInDb.PrintTime) == false)
                        {
                            targetState = OrderState.PRINTED;
                        }
                        else
                        {
                            targetState = onlineState;
                        }
                    }
                }
                else if (onlineState == OrderState.SHIPPED)
                {
                    //������˿��У�����Ϊ�ѷ���
                    if (orderInDb.State == OrderState.RETURNING || orderInDb.State == OrderState.WAIT_REFUNED || orderInDb.State == OrderState.UNKNOWN)
                    {
                        if (IsDbMinTime(orderInDb.DeliveryTime))
                        {
                            targetState = OrderState.PRINTED;
                        }
                        else
                        {
                            targetState = onlineState;
                        }
                    }

                    //�ѷ���,��ϵͳ�еĴ�ӡʱ�䣬��˵���ö�������ϵͳ��ӡ�ģ���Ҫ����״̬����ͬ������
                    if (ous.IsDbMinTime(orderInDb.PrintTime))
                    {
                        targetState = onlineState;
                    }
                }
                else if (onlineState == OrderState.SUCCESS)
                {
                    //�Ǳ��ش�ӡ
                    if (ous.IsDbMinTime(orderInDb.PrintTime))
                    {
                        targetState = onlineState;
                    }
                    else
                    {
                        //�����Ѿ�����������Ϊ������ɣ���ֹ�û���ȷ���յ�������ϵͳ�޷�ͳ�Ʒ���
                        //�����ӡʱ�䳬��15��û�з���һ�㲻���ܵģ��п�����û��ɨ�赽����������Ҳ���Ը���
                        //������ô�죬ʱ�����ǳ���15��Ļ���20��ģ�
                        if (ous.IsDbMinTime(orderInDb.DeliveryTime) == false || DateTime.Now.Subtract(orderInDb.PopPayTime).TotalDays >= 30)
                        {
                            targetState = onlineState;
                        }
                    }
                }
                else if ((int)onlineState > (int)OrderState.SHIPPED)
                {
                    targetState = onlineState;
                }
                else
                {
                    Logger.Log("���¶���ʧ��δ֪״̬[" + onlineState + "]");
                    return new StringResponse("���¶���ʧ��δ֪״̬[" + onlineState + "]");
                }

                if (targetState == OrderState.NONE)
                {
                    throw new Exception("Ҫ���³ɵĶ���״̬����Ϊ:" + targetState);
                }

                //�����Ѿ��رյĶ�������������״̬
                if (orderInDb.State != OrderState.CLOSED &&
                    orderInDb.State != OrderState.CANCLED &&
                    orderInDb.State != OrderState.CLOSEDAFTERTRADE &&
                    orderInDb.State != OrderState.REFUSED)
                {
                    if (targetState != orderInDb.State)
                    {
                        ous.UpdateOrderGoodsStateByOrderId(orderInDb.Id, targetState);
                    }
                    orderInDb.State = targetState;
                }
                ous.UpdateEx(orderInDb, true);
                return new StringResponse(UPDATE_RET_UPDATED);
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }
        }
    }
}
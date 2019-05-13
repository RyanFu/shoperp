using System;
using System.Collections.Generic;
using System.Linq;
using ShopErp.Domain;
using ShopErp.Domain.Pop;
using ShopErp.Domain.RestfulResponse;
using ShopErp.Domain.RestfulResponse.DomainResponse;

namespace ShopErp.App.Service.Restful
{
    public class OrderService : ServiceBase<Order>
    {
        public DataCollectionResponse<Order> GetById(string id)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["id"] = id;
            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public DataCollectionResponse<Order> GetByPopOrderId(string popOrderId)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["popOrderId"] = popOrderId;
            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public DataCollectionResponse<Order> GetByDeliveryNumber(string deliveryNumber)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["deliveryNumber"] = deliveryNumber;
            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public ResponseBase UpdateDelivery(long id, long deliveryTemplateId, string deliveryCompany, string deliveryNumber, DateTime printTime)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["id"] = id;
            para["deliveryTemplateId"] = deliveryTemplateId;
            para["deliveryCompany"] = deliveryCompany;
            para["deliveryNumber"] = deliveryNumber;
            para["printTime"] = printTime;
            return DoPost<ResponseBase>(para);
        }

        public Order[] MarkDelivery(string deliveryNumber, float weight, bool chkWeight, bool chkPopState, bool chkLocalState)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["deliveryNumber"] = deliveryNumber;
            para["weight"] = weight;
            para["chkWeight"] = chkWeight;
            para["chkPopState"] = chkPopState;
            para["chkLocalState"] = chkLocalState;
            return DoPost<DataCollectionResponse<Order>>(para).Datas.ToArray();
        }

        /// <summary>
        /// ���TIME����Ϊ�գ��򽫵���ƽ̨�ӿڣ���Ƿ���������ֻ����ƽ̨����ʱ��
        /// </summary>
        /// <param name="id"></param>
        /// <param name="time">���TIME����Ϊ�գ��򽫵���ƽ̨�ӿڣ���Ƿ���������ֻ����ƽ̨����ʱ��</param>
        public void MarkPopDelivery(long id, string time)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["id"] = id;
            para["time"] = time;
            DoPost<ResponseBase>(para);
        }

        /// <summary>
        /// ��ѯ����
        /// </summary>
        /// <param name="popBuyerId">����ǳƣ�ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="receiverPhone">���������ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="receiverMobile">����ֻ���ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="receiverName">���������ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="receiverAddress">��ҵ�ַ��ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="timeType">ʱ�����ͣ�0 PopCreateTime ƽ̨����ʱ�䣬1 PopPayTime ƽ̨����ʱ�䣬2 PopDeliveryTime ƽ̨����ʱ�䣬3 CreateTime ���ش���ʱ�䣬4 PrintTime ���ش�ӡʱ�䣬5 DeliveryTime ���ط���ʱ�䣬6 CloseTime ���عر�ʱ��</param>
        /// <param name="startTime">��ʼʱ�䣬���Ϊ1970-01-01 ��ʾ��ʹ��</param>
        /// <param name="endTime">����ʱ�䣬���Ϊ1970-01-01 ��ʾ��ʹ��</param>
        /// <param name="deliveryCompany">��ݹ�˾����ȷƥ��</param>
        /// <param name="deliveryNumber">��ݵ��ţ���ȷƥ��</param>
        /// <param name="state">����״̬��NONE��ʾ����ѯ</param>
        /// <param name="payType">�������ͣ�NONE��ʾ����ѯ</param>
        /// <param name="vendorName">�������ƣ�ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="number">���ţ�ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="ofs">��ɫ���ģ����Ϊ��NULL���߿����飬��ʾ����ѯ</param>
        /// <param name="parseResult">���������-1��ʾ����ѯ</param>
        /// <param name="comment">���ұ�ע��ģ��ƥ�䣬Ϊ�ջ���NULL��ʾ����ѯ</param>
        /// <param name="shopId">���̱�ţ�0��ʾ��ѯ</param>
        /// <param name="createType">�������ͣ�NONE��ʾ����ѯ</param>
        /// <param name="type">�������ͣ� NONE��ʾ����ѯ</param>
        /// <param name="pageIndex">ҳ�±꣬��0��ʼ</param>
        /// <param name="pageSize">ÿҳ���ݴ�С��0��ʾ����ҳ</param>
        /// <returns></returns>
        public DataCollectionResponse<Order> GetByAll(string popBuyerId, string receiverPhone, string receiverMobile,
            string receiverName, string receiverAddress,
            int timeType, DateTime startTime, DateTime endTime, string deliveryCompany, string deliveryNumber,
            OrderState state, PopPayType payType, string vendorName, string number,
            ColorFlag[] ofs, int parseResult, string comment, long shopId, OrderCreateType createType, OrderType type,
            int pageIndex, int pageSize)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["popBuyerId"] = popBuyerId;
            para["receiverPhone"] = receiverPhone;
            para["receiverMobile"] = receiverMobile;
            para["ReceiverName"] = receiverName;
            para["receiverAddress"] = receiverAddress;
            para["timeType"] = timeType;

            para["startTime"] = startTime;
            para["endTime"] = endTime;
            para["deliveryCompany"] = deliveryCompany;
            para["deliveryNumber"] = deliveryNumber;
            para["state"] = state;
            para["payType"] = payType;

            para["vendorName"] = vendorName;
            para["number"] = number;
            para["ofs"] = ofs;
            para["parseResult"] = parseResult;
            para["comment"] = comment;
            para["shopId"] = shopId;

            para["createType"] = createType;
            para["type"] = type;
            para["pageIndex"] = pageIndex;
            para["pageSize"] = pageSize;

            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public DataCollectionResponse<Order> GetPayedAndPrintedOrders(long[] shopId, OrderCreateType createType, PopPayType payType, int pageIndex, int pageSize)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["shopId"] = shopId;
            para["createType"] = createType;
            para["payType"] = payType;
            para["pageIndex"] = pageIndex;
            para["pageSize"] = pageSize;
            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public DataCollectionResponse<Order> GetOrdersByInfoIdNotEqual(string popBuyerId, string receiverPhone, string receiverMobile, string receiverAddress, long id)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["popBuyerId"] = popBuyerId;
            para["receiverPhone"] = receiverPhone;
            para["receiverMobile"] = receiverMobile;
            para["receiverAddress"] = receiverAddress;
            para["id"] = id;

            return DoPost<DataCollectionResponse<Order>>(para);
        }

        public ResponseBase CloseOrder(long orderId, long orderGoodsId, int count)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            para["orderGoodsId"] = orderGoodsId;
            para["count"] = count;
            return DoPost<ResponseBase>(para);
        }

        public ResponseBase SpilteOrderGoods(long orderId, OrderSpilteInfo[] infos)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            para["infos"] = infos;
            return DoPost<ResponseBase>(para);
        }

        public ResponseBase ResetPrintState(long orderId)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            return DoPost<ResponseBase>(para);
        }

        public ResponseBase ModifyPopSellerComment(long orderId, ColorFlag flag, string comment)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            para["flag"] = flag;
            para["comment"] = comment;
            return DoPost<ResponseBase>(para);
        }

        public OrderDownloadCollectionResponse GetPopWaitSendOrders(Shop shop, PopPayType payType, int pageIndex, int pageSize)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["shop"] = shop;
            para["payType"] = payType;
            para["pageIndex"] = pageIndex;
            para["pageSize"] = pageSize;
            return DoPost<OrderDownloadCollectionResponse>(para);
        }

        public OrderDownloadCollectionResponse SaveOrUpdateOrdersByPopOrderId(Shop shop, List<OrderDownload> orders)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["shop"] = shop;
            para["orders"] = orders;
            return DoPost<OrderDownloadCollectionResponse>(para);
        }

        public StringResponse UpdateOrderStateWithGoods(Order orderOnline, OrderUpdate orderInDb, Shop shop)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderOnline"] = orderOnline;
            para["orderInDb"] = orderInDb;
            para["shop"] = shop;
            return DoPost<StringResponse>(para);
        }

        public ResponseBase UpdateOrderGoodsState(long orderId, long orderGoodsId, OrderState state, string stockComment)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            para["orderGoodsId"] = orderGoodsId;
            para["state"] = state;
            para["stockComment"] = stockComment;
            return DoPost<ResponseBase>(para);
        }

        public StringResponse UpdateOrderState(PopOrderState orderStateOnline, OrderUpdate orderInDb, Shop shop)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderStateOnline"] = orderStateOnline;
            para["orderInDb"] = orderInDb;
            para["shop"] = shop;
            return DoPost<StringResponse>(para);
        }

        public ResponseBase UpdateOrderToGeted(long orderId)
        {
            Dictionary<string, object> para = new Dictionary<string, object>();
            para["orderId"] = orderId;
            return DoPost<ResponseBase>(para);
        }
    }
}
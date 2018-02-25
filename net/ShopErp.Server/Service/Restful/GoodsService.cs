﻿using ShopErp.Domain;
using ShopErp.Domain.RestfulResponse;
using ShopErp.Server.Dao.NHibernateDao;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Web;
using ShopErp.Domain.Pop;
using ShopErp.Server.Service.Pop;
using ShopErp.Server.Utils;

namespace ShopErp.Server.Service.Restful
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, AddressFilterMode = AddressFilterMode.Exact)]
    public class GoodsService : ServiceBase<Goods, GoodsDao>
    {

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbyid.html")]
        public DataCollectionResponse<Goods> GetById(long id)
        {
            try
            {
                var item = this.dao.GetById(id);
                return new DataCollectionResponse<Goods>(item);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/save.html")]
        public LongResponse Save(Goods value)
        {
            try
            {
                value.CreateTime = DateTime.Now;
                value.UpdateTime = DateTime.Now;
                this.dao.Save(value);
                ServiceContainer.GetService<VendorService>().UpdateCountAndAvgPriceAll();
                return new LongResponse(value.Id);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }


        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/update.html")]
        public ResponseBase Update(Goods value)
        {
            try
            {
                if (value.Id < 1)
                {
                    throw new Exception("数据未保存过，不能直接更新");
                }
                value.UpdateTime = DateTime.Now;
                this.dao.Update(value);
                ServiceContainer.GetService<VendorService>().UpdateCountAndAvgPriceAll();
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
                this.dao.ExcuteSqlUpdate("delete from Goods where Id=" + id);
                ServiceContainer.GetService<VendorService>().UpdateCountAndAvgPriceAll();
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbyall.html")]
        public DataCollectionResponse<Goods> GetByAll(long shopId, GoodsState state, int timeType, DateTime start, DateTime end, string vendor, string number, GoodsType type, string comment, ColorFlag flag, string order, int pageIndex, int pageSize)
        {
            try
            {
                return this.dao.GetByAll(shopId, state, timeType, start, end, vendor, number, type, comment, flag, order, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// 根据厂家货号获取，其实货号全匹配，
        /// 厂家名称或者拼音部分匹配
        /// </summary>
        /// <param name="number"></param>
        /// <param name="vendorNameOrPingName"></param>
        /// <returns></returns>
        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbynumberandvendornamelike.html")]
        public DataCollectionResponse<Goods> GetByNumberAndVendorNameLike(string number, string vendorNameOrPingName, int pageIndex, int pageSize)
        {
            try
            {
                return this.dao.GetByNumberAndVendorNameLike(number, vendorNameOrPingName, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/updateweight.html")]
        public ResponseBase UpdateWeight(long id, float weight)
        {
            try
            {
                this.dao.UpdateWeight(id, weight);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/updatelastselltime.html")]
        public ResponseBase UpdateLastSellTime(int id, DateTime time)
        {
            try
            {
                this.dao.UpdateLastSellTime(id, time);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/parsepopordergoodsnumber.html")]
        public ResponseBase ParsePopOrderGoodsNumber(OrderGoods og)
        {
            try
            {
                //上货时通常用&号，或者空格分开厂家货号
                string stock = "";
                if (og.Number.Contains("&") || og.Number.Contains(" "))
                {
                    stock = og.Number;
                }
                else
                {
                    stock = og.Vendor + "&" + og.Number;
                }
                string[] stocks = stock.Split(new char[] { '&', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string rawVendor = null, rawNumber = null;
                if (stocks.Length != 2)
                {
                    throw new Exception("无法解析厂家货号" + og.Number);
                }
                rawVendor = stocks[0].Trim();
                rawNumber = stocks[1].Trim();
                var g = ParseGoods(rawVendor, rawNumber).First;
                if (g == null)
                {
                    throw new Exception("未能解析出商品货号:" + og.Number);
                }
                og.Number = g.Number;
                og.NumberId = g.Id;
                og.Image = g.Image;
                og.Weight = g.Weight;
                og.Price = g.Price;
                og.Vendor = ServiceContainer.GetService<VendorService>().GetVendorName(g.VendorId).data;
                if (g.IgnoreEdtion)
                {
                    og.Edtion = "";
                }
                return ResponseBase.SUCCESS;
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/parsegoods.html")]
        public DataCollectionResponse<Goods> ParseGoods(string vendorNameOrPingName, string number)
        {
            try
            {
                IList<Goods> gus = ServiceContainer.GetService<GoodsService>().GetByNumberAndVendorNameLike(number, vendorNameOrPingName, 0, 0).Datas;
                var vendorService = ServiceContainer.GetService<VendorService>();
                //只找到一个
                if (gus != null && gus.Count == 1)
                {
                    return new DataCollectionResponse<Goods>(gus[0]);
                }
                //找到多个，匹配厂家名称最短匹配的那个，如果有多个名称相同，则认为有冲突，返回失败
                if (gus != null && gus.Count > 1)
                {
                    var vendor = GetMostMatchVendor(gus.Select(obj => obj.VendorId).ToArray(), vendorNameOrPingName);
                    if (vendor == null)
                    {
                        throw new Exception(string.Format("解析失败:{0}&{1}找到多个匹配", vendorNameOrPingName, number));
                    }
                    return new DataCollectionResponse<Goods>(gus.FirstOrDefault(obj => obj.VendorId == vendor.Id));
                }

                //查找映射
                IList<GoodsMap> goodsMaps = ServiceContainer.GetService<GoodsMapService>().GetByAll(vendorNameOrPingName, number, 0, 0, 0).Datas;
                if (goodsMaps != null && goodsMaps.Count > 0)
                {
                    var vendor = GetMostMatchVendor(goodsMaps.Select(obj => obj.VendorId).ToArray(), vendorNameOrPingName);
                    if (vendor == null)
                    {
                        throw new Exception(string.Format("解析失败:{0}&{1}找到多个匹配", vendorNameOrPingName, number));
                    }
                    var matchGoodsMap = goodsMaps.FirstOrDefault(obj => obj.VendorId == vendor.Id);
                    var g = goodsMaps.Select(obj => ServiceContainer.GetService<GoodsService>().GetById(obj.TargetNumberId)).FirstOrDefault(obj => obj != null).First;
                    if (g == null)
                    {
                        return null;
                    }
                    g.IgnoreEdtion = matchGoodsMap.IgnoreEdtion;
                    g.Price = matchGoodsMap.Price;
                    g.VendorId = matchGoodsMap.VendorId;
                    if (matchGoodsMap.ShowTargetNumber == false)
                    {
                        g.Number = matchGoodsMap.Number;
                    }
                    return new DataCollectionResponse<Goods>(g);
                }
                return new DataCollectionResponse<Goods>();
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }

        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/searchpopgoods.html")]
        public DataCollectionResponse<PopGoods> SearchPopGoods(Shop shop, PopGoodsState state, int pageIndex, int pageSize)
        {
            try
            {
                return new DataCollectionResponse<PopGoods>((new PopService().SearchPopGoods(shop, state, pageIndex, pageSize)));
            }
            catch (Exception e)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(e.Message), HttpStatusCode.OK);
            }
        }

        private Vendor GetMostMatchVendor(long[] vendorIds, string vendor)
        {
            vendorIds = vendorIds.Distinct().ToArray();
            var vendorService = ServiceContainer.GetService<VendorService>();
            var vendors = vendorIds.Select(obj => vendorService.GetById(obj).First).ToList();
            var vendorLen = vendors.Select(obj => obj.PingyingName.Contains(vendor) ? obj.PingyingName.Length : obj.Name.Length).ToList();
            var lenDic = vendorLen.GroupBy(obj => obj).OrderBy(obj => obj.Key).ToArray();
            if (lenDic.Count() > 0 && lenDic[0].Count() <= 1)//最小长度匹配的只有一个，则成功，否则失败
            {
                return vendors[vendorLen.IndexOf(lenDic[0].Key)];
            }
            return null;
        }

    }
}

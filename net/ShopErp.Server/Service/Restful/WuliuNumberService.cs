﻿using ShopErp.Domain;
using ShopErp.Domain.RestfulResponse;
using ShopErp.Server.Dao.NHibernateDao;
using ShopErp.Server.Service.Pop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Top.Api;
using Top.Api.Request;
using Top.Api.Response;

namespace ShopErp.Server.Service.Restful
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, AddressFilterMode = AddressFilterMode.Exact)]
    class WuliuNumberService : ServiceBase<WuliuNumber, WuliuNumberDao>
    {
        /// <summary>
        /// 淘宝平台接口
        /// </summary>
        private const string API_SERVER_URL = "https://eco.taobao.com/router/rest";
        private static List<CainiaoCloudprintStdtemplatesGetResponse.StandardTemplateResultDomain> caiNiaoTemplates;

        private bool MatchAddres(CainiaoPrintDataDataRecipientAddress address, string receiverAddress)
        {
            address.district = address.district ?? "";
            address.town = address.town ?? "";
            var tbAdd = ParseTaobaoAddress(receiverAddress);
            return tbAdd.Province.Equals(address.province) &&
               tbAdd.City.Equals(address.city) &&
               tbAdd.District.Equals(address.district) &&
               tbAdd.Town.Equals(address.town) &&
               tbAdd.Detail.Equals(address.detail);
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/delete.html")]
        public ResponseBase Delete(long id)
        {
            try
            {
                this.dao.ExcuteSqlUpdate("delete from `WuliuNumber` where Id=" + id);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getbyall.html")]
        public DataCollectionResponse<WuliuNumber> GetByAll(string wuliuIds, string deliveryCompany, string deliveryNumber, DateTime start, DateTime end, int pageIndex, int pageSize)
        {
            try
            {
                return this.dao.GetByAll(wuliuIds, deliveryCompany, deliveryNumber, "", start, end, pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// 获取菜鸟电子面单
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/gencainiaowuliunumber.html")]
        public DataCollectionResponse<WuliuNumber> GenCainiaoWuliuNumber(string deliveryCompany, Order order, string[] wuliuIds, string packageId, string senderName, string senderPhone, string senderAddress)
        {
            try
            {
                //初始化信息及检查，这些信息有可能会随便更新，所以每次都要获取最新的
                var popSellerNumberId = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_SELLER_ID, "");
                var appKey = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_KEY, "");
                var appSecret = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SECRET, "");
                var appSession = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SESSION, "");

                if (string.IsNullOrWhiteSpace(senderName) || string.IsNullOrWhiteSpace(senderPhone))
                {
                    throw new Exception("淘宝接口发货人不完整请配置");
                }

                if (string.IsNullOrWhiteSpace(popSellerNumberId))
                {
                    throw new Exception("淘宝卖家数据编号为空");
                }

                if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(appSession))
                {
                    throw new Exception("淘宝菜鸟接口授权信息不完整请配置");
                }

                //拉取单号的时候，必须要有一个模板地址  获取菜鸟系统里面的模板，
                if (caiNiaoTemplates == null || caiNiaoTemplates.Count < 1)
                {
                    var reqT = new CainiaoCloudprintStdtemplatesGetRequest();
                    var rspT = InvokeOpenApi<CainiaoCloudprintStdtemplatesGetResponse>(appKey, appSecret, appSession, reqT);
                    if (rspT.Result.Datas.Count < 1)
                    {
                        throw new Exception("菜鸟系统中没有默认打印模板无法打印");
                    }
                    caiNiaoTemplates = rspT.Result.Datas;
                }
                var templte = caiNiaoTemplates.FirstOrDefault(obj => obj.CpCode == GetCPCodeCN(deliveryCompany));
                if (templte == null)
                {
                    throw new Exception("菜鸟系统中没有默认打印模板无法打印,快递公司:" + GetCPCodeCN(deliveryCompany) + " " + deliveryCompany);
                }
                if (templte.StandardTemplates == null || templte.StandardTemplates.Count < 1)
                {
                    throw new Exception("菜鸟系统中对应的标准模板没有具体模板,快递公司:" + GetCPCodeCN(deliveryCompany) + " " + deliveryCompany);
                }
                string printData = "";
                string cpCode = GetCPCodeCN(deliveryCompany);
                string wuliuId = string.Join(",", wuliuIds);
                //检查以前是否打印过
                var wuliuNumber = this.dao.GetByAll(wuliuId, deliveryCompany, "", packageId, DateTime.MinValue, DateTime.MinValue, 0, 0).First;
                if (wuliuNumber != null)
                {
                    if (wuliuId != wuliuNumber.WuliuIds)
                    {
                        //这种情况是属于以前合并打印后，某个订单又拆分出来,此时需要增加包裹编号，否则菜鸟会返回相同的快递信息
                        packageId = string.IsNullOrWhiteSpace(packageId) ? "1" : packageId + "1";
                    }
                    else
                    {
                        //有数据，则检查是否更新，
                        if (wuliuNumber.ReceiverAddress == order.ReceiverAddress && wuliuNumber.ReceiverName == order.ReceiverName && wuliuNumber.ReceiverPhone == wuliuNumber.ReceiverPhone && wuliuNumber.ReceiverMobile == wuliuNumber.ReceiverMobile)
                        {
                            ContactRouteCodeAndSortationName(wuliuNumber);
                            return new DataCollectionResponse<WuliuNumber>(wuliuNumber);
                        }
                        //需要更新菜鸟面单以打印正确的信息
                        var updateReq = new CainiaoWaybillIiUpdateRequest { };
                        var updateReqBody = new CainiaoWaybillIiUpdateRequest.WaybillCloudPrintUpdateRequestDomain
                        {
                            CpCode = cpCode,
                            WaybillCode = wuliuNumber.DeliveryNumber,
                            Recipient = ParseTaobaoAddressUpdate(order.ReceiverAddress, order.ReceiverName, order.ReceiverPhone, order.ReceiverMobile),
                        };
                        updateReq.ParamWaybillCloudPrintUpdateRequest_ = updateReqBody;
                        var updateResp = InvokeOpenApi<CainiaoWaybillIiUpdateResponse>(appKey, appSecret, appSession, updateReq);
                        printData = updateResp.PrintData;
                    }
                }
                if (string.IsNullOrWhiteSpace(printData))
                {
                    //生成请求参数
                    CainiaoWaybillIiGetRequest req = new CainiaoWaybillIiGetRequest();
                    var reqBody = new CainiaoWaybillIiGetRequest.WaybillCloudPrintApplyNewRequestDomain();
                    reqBody.CpCode = GetCPCodeCN(deliveryCompany);
                    reqBody.Sender = new CainiaoWaybillIiGetRequest.UserInfoDtoDomain { Phone = "", Name = senderName, Mobile = senderPhone, Address = GetShippingAddress(senderAddress) };
                    //订单信息，一个请求里面可以包含多个订单，我们系统里面，默认一个
                    reqBody.TradeOrderInfoDtos = new List<CainiaoWaybillIiGetRequest.TradeOrderInfoDtoDomain>();
                    var or = new CainiaoWaybillIiGetRequest.TradeOrderInfoDtoDomain { ObjectId = Guid.NewGuid().ToString() };
                    or.UserId = long.Parse(popSellerNumberId);
                    or.TemplateUrl = templte.StandardTemplates.First().StandardTemplateUrl;
                    or.Recipient = new CainiaoWaybillIiGetRequest.UserInfoDtoDomain { Phone = order.ReceiverPhone, Mobile = order.ReceiverMobile, Name = order.ReceiverName, Address = ParseTaobaoAddress(order.ReceiverAddress), };
                    or.OrderInfo = new CainiaoWaybillIiGetRequest.OrderInfoDtoDomain { OrderChannelsType = GetOrderChannleTypeCN(order.PopType), TradeOrderList = new List<string>(wuliuIds) };
                    or.PackageInfo = new CainiaoWaybillIiGetRequest.PackageInfoDtoDomain { Id = packageId == "" ? null : packageId, Items = new List<CainiaoWaybillIiGetRequest.ItemDomain>() };
                    or.PackageInfo.Items.AddRange(order.OrderGoodss.Where(obj => (int)obj.State >= (int)OrderState.PAYED && (int)obj.State <= (int)OrderState.SUCCESS).Select(obj => new CainiaoWaybillIiGetRequest.ItemDomain { Name = obj.Number + "," + obj.Edtion + "," + obj.Color + "," + obj.Size, Count = obj.Count }));
                    if (or.PackageInfo.Items.Count < 1)
                    {
                        or.PackageInfo.Items.Add(new CainiaoWaybillIiGetRequest.ItemDomain { Name = "没有商品或者其它未定义商品", Count = 1 });
                    }
                    reqBody.TradeOrderInfoDtos.Add(or);
                    req.ParamWaybillCloudPrintApplyNewRequest_ = reqBody;
                    var rsp = InvokeOpenApi<CainiaoWaybillIiGetResponse>(appKey, appSecret, appSession, req);
                    if (rsp.Modules == null || rsp.Modules.Count < 1)
                    {
                        throw new Exception("菜鸟电子面单未返回数据:" + rsp.ErrMsg);
                    }
                    printData = rsp.Modules[0].PrintData;
                    wuliuNumber = new WuliuNumber { CreateTime = DateTime.Now };
                }
                CainiaoPrintData resp = Newtonsoft.Json.JsonConvert.DeserializeObject<CainiaoPrintData>(printData);
                wuliuNumber.ReceiverAddress = order.ReceiverAddress;
                wuliuNumber.ReceiverMobile = order.ReceiverMobile;
                wuliuNumber.ReceiverName = order.ReceiverName;
                wuliuNumber.ReceiverPhone = order.ReceiverPhone;
                wuliuNumber.DeliveryCompany = deliveryCompany;
                wuliuNumber.DeliveryNumber = resp.data.waybillCode;
                wuliuNumber.ConsolidationCode = resp.data.routingInfo.consolidation.code ?? "";
                wuliuNumber.ConsolidationName = resp.data.routingInfo.consolidation.name ?? "";
                wuliuNumber.OriginCode = resp.data.routingInfo.origin.code ?? "";
                wuliuNumber.OriginName = resp.data.routingInfo.origin.name ?? "";
                wuliuNumber.RouteCode = resp.data.routingInfo.routeCode ?? "";
                wuliuNumber.SortationName = resp.data.routingInfo.sortation.name ?? "";
                wuliuNumber.SortationNameAndRouteCode = "";
                wuliuNumber.WuliuIds = wuliuId;
                wuliuNumber.PackageId = packageId;
                wuliuNumber.PrintData = printData;
                ContactRouteCodeAndSortationName(wuliuNumber);
                if (wuliuNumber.Id > 0)
                {
                    this.dao.Update(wuliuNumber);
                }
                else
                {
                    this.dao.Save(wuliuNumber);
                }
                return new DataCollectionResponse<WuliuNumber>(wuliuNumber);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// 获取菜鸟电子面单
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/cancelcainiaowuliunumber.html")]
        public ResponseBase CancelCainiaoWuliuNumber(string deliveryNumber)
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/getwuliubrachs.html")]
        public DataCollectionResponse<WuliuBranch> GetWuliuBrachs(string type, string code)
        {
            try
            {
                var appKey = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_KEY, "");
                var appSecret = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SECRET, "");
                var appSession = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SESSION, "");
                if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(appSession))
                {
                    throw new Exception("淘宝菜鸟接口授权信息不完整请配置");
                }
                var req = new CainiaoWaybillIiSearchRequest() { CpCode = code };
                var rep = InvokeOpenApi<CainiaoWaybillIiSearchResponse>(appKey, appSecret, appSession, req);
                var datas = new List<WuliuBranch>();

                foreach (var v in rep.WaybillApplySubscriptionCols)
                {
                    foreach (var vv in v.BranchAccountCols)
                    {
                        foreach (var vvv in vv.ShippAddressCols)
                        {
                            var data = new WuliuBranch();
                            data.Type = v.CpCode;
                            data.Name = vv.BranchName;
                            data.Number = vv.BranchCode;
                            data.Quantity = vv.Quantity;
                            data.SenderName = "";
                            data.SenderPhone = "";
                            data.SenderAddress = vvv.Province + " " + vvv.City + " " + vvv.District + " " + vvv.Detail;
                            datas.Add(data);
                        }
                    }
                }
                return new DataCollectionResponse<WuliuBranch>(datas);
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        /// <summary>
        /// 更新淘宝地址库
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        [OperationContract]
        [WebInvoke(ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest, UriTemplate = "/updateaddressarea.html")]
        public ResponseBase UpdateAddressArea()
        {
            try
            {
                var appKey = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_KEY, "");
                var appSecret = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SECRET, "");
                var appSession = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_APP_SESSION, "");

                if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(appSession))
                {
                    throw new Exception("淘宝菜鸟接口授权信息不完整请配置");
                }
                var req = new Top.Api.Request.AreasGetRequest { Fields = "id,type,name,parent_id,zip" };
                var resp = InvokeOpenApi<Top.Api.Response.AreasGetResponse>(appKey, appSecret, appSession, req);
                XDocument xDoc = XDocument.Parse("<?xml version=\"1.0\" encoding=\"utf - 8\"?><Address/>");
                var newList = new List<Top.Api.Domain.Area>(resp.Areas);
                FindSub(xDoc.Root, 1, newList);
                if (newList.Count == resp.Areas.Count)
                {
                    throw new Exception("更新失败：未更新任何数据，请联系技术人员");
                }
                AddressService.UpdateAndSaveAreas(xDoc);
                return ResponseBase.SUCCESS;
            }
            catch (Exception ex)
            {
                throw new WebFaultException<ResponseBase>(new ResponseBase(ex.Message), System.Net.HttpStatusCode.OK);
            }
        }

        string GetNodeName(long type)
        {
            if (type == 1)
            {
                return "Country";
            }
            if (type == 2)
            {
                return "Province";
            }
            if (type == 3)
            {
                return "City";
            }
            if (type == 4)
            {
                return "District";
            }
            if (type == 5)
            {
                return "Town";
            }
            throw new Exception("未知的行政级别");
        }

        private void FindSub(XElement parent, long parentId, List<Top.Api.Domain.Area> areas)
        {
            var aa = areas.Where(obj => parentId == obj.ParentId).ToArray();
            if (aa.Length < 1)
            {
                return;
            }

            foreach (var a in aa)
            {
                string sn = a.Type == 2 ? AddressService.GetProvinceShortName(a.Name) : AddressService.GetCityShortName(a.Name);
                var xe = new XElement(GetNodeName(a.Type), new XAttribute("Name", a.Name.Trim()), new XAttribute("ShortName", sn));
                areas.Remove(a);
                parent.Add(xe);
                if (a.Type <= 3)
                    FindSub(xe, a.Id, areas);
            }
        }

        #region 菜鸟电子面单相关方法

        public static T InvokeOpenApi<T>(string appKey, string appSecret, string session, ITopRequest<T> request) where T : TopResponse
        {
            var topClient = new DefaultTopClient(API_SERVER_URL, appKey, appSecret);
            var ret = topClient.Execute<T>(request, session, DateTime.Now);
            if (ret.IsError)
            {
                throw new Exception("执行淘宝请求出错:" + ret.ErrCode + "," + ret.ErrMsg + ret.SubErrMsg);
            }
            return ret;
        }

        private static CainiaoWaybillIiGetRequest.AddressDtoDomain GetShippingAddress(string address)
        {
            var p = AddressService.ParseProvince(address);
            var c = AddressService.ParseCity(address);
            var a = AddressService.ParseRegion(address);
            if (p == null)
            {
                throw new Exception("地址解析失败未找出省");
            }
            if (c == null)
            {
                throw new Exception("地址解析失败未找出市");
            }

            string ad = AddressService.TrimStart(address, p.Name, 2);
            ad = AddressService.TrimStart(ad, c.Name, 2);
            if (a != null)
            {
                ad = AddressService.TrimStart(ad, a.Name, 2);
            }
            var wd = new CainiaoWaybillIiGetRequest.AddressDtoDomain
            {
                Province = p.Name,
                City = c.Name,
                District = a == null ? "" : a.Name,
                Detail = ad,
                Town = "",
            };
            return wd;
        }

        /// <summary>
        /// 获取菜鸟规定的快递公司简码
        /// </summary>
        /// <param name="deliveryCompany"></param>
        /// <returns></returns>
        private static string GetCPCodeCN(string deliveryCompany)
        {
            var dc = ServiceContainer.GetService<DeliveryCompanyService>().GetDeliveryCompany(deliveryCompany);
            return dc.First.PopMapTaobao;
        }

        private static string GetOrderChannleTypeCN(PopType type)
        {
            return "OTHERS";
        }

        private static CainiaoWaybillIiGetRequest.AddressDtoDomain ParseTaobaoAddress(string address)
        {
            var p = AddressService.ParseProvince(address);
            var c = AddressService.ParseCity(address);
            var a = AddressService.ParseRegion(address);
            if (p == null)
            {
                throw new Exception("地址解析失败未找出省");
            }
            if (c == null)
            {
                throw new Exception("地址解析失败未找出市");
            }

            string ad = AddressService.TrimStart(address, p.Name, 2);
            ad = AddressService.TrimStart(ad, c.Name, 2);
            if (a != null)
            {
                ad = AddressService.TrimStart(ad, a.Name, 2);
            }
            var wd = new CainiaoWaybillIiGetRequest.AddressDtoDomain
            {
                Province = p.Name,
                City = c.Name,
                District = a == null ? "" : a.Name,
                Detail = ad,
                Town = "",
            };
            return wd;
        }

        private static CainiaoWaybillIiUpdateRequest.UserInfoDtoDomain ParseTaobaoAddressUpdate(string address, string name, string phone, string mobile)
        {
            var p = AddressService.ParseProvince(address);
            var c = AddressService.ParseCity(address);
            var a = AddressService.ParseRegion(address);
            if (p == null)
            {
                throw new Exception("地址解析失败未找出省");
            }
            if (c == null)
            {
                throw new Exception("地址解析失败未找出市");
            }

            string ad = AddressService.TrimStart(address, p.Name, 2);
            ad = AddressService.TrimStart(ad, c.Name, 2);
            if (a != null)
            {
                ad = AddressService.TrimStart(ad, a.Name, 2);
            }
            var wd = new CainiaoWaybillIiUpdateRequest.UserInfoDtoDomain
            {
                Address = new CainiaoWaybillIiUpdateRequest.AddressDtoDomain
                {
                    Province = p.Name,
                    City = c.Name,
                    District = a == null ? "" : a.Name,
                    Detail = ad,
                    Town = "",
                },
                Name = name,
                Phone = phone,
                Mobile = mobile,
            };
            return wd;
        }

        private static void ContactRouteCodeAndSortationName(WuliuNumber wn)
        {
            if (wn.DeliveryCompany.Contains("圆通"))
            {
                if (string.IsNullOrWhiteSpace(wn.RouteCode) == false)
                {
                    wn.SortationNameAndRouteCode = wn.RouteCode;
                    return;
                }
                wn.SortationNameAndRouteCode = wn.SortationName;
                return;
            }
            var cainiaoContactDeliveryCompanies = ServiceContainer.GetService<SystemConfigService>().GetEx(-1, SystemNames.CONFIG_CAINIAO_CONTACT_DELIVERYCOMPANIES, "");
            if (string.IsNullOrWhiteSpace(cainiaoContactDeliveryCompanies))
            {
                throw new Exception("系统中没有配置需要拼接大头笔与三段码的快递公司");
            }

            string[] dcs = cainiaoContactDeliveryCompanies.Split(new char[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            if (dcs.Any(obj => obj == wn.DeliveryCompany))
            {
                wn.SortationNameAndRouteCode = wn.SortationName + " " + wn.RouteCode;
            }
            else
            {
                wn.SortationNameAndRouteCode = wn.SortationName;
            }
        }

        #endregion
    }

    #region 菜鸟电子面单相关的类

    /// <summary>
    /// 淘宝官网打印数据解释http://open.taobao.com/docs/doc.htm?spm=a219a.7629140.0.0.wIXAaM&docType=1&articleId=106054
    /// </summary>
    public class CainiaoPrintData
    {
        public CainiaoPrintDataData data;
        public string signature;
        public string templateURL;
    }

    public class CainiaoPrintDataData
    {
        public string cpCode;

        public string waybillCode;

        public CainiaoPrintDataRoutingInfo routingInfo;

        public CainiaoPrintDataDataRecipient recipient;
    }

    public class CainiaoPrintDataRoutingInfo
    {
        public CainiaoPrintDataRoutingInfoConsolidation consolidation;
        public CainiaoPrintDataRoutingInfoOrigin origin;
        public string routeCode;
        public CainiaoPrintDataRoutingInfoSortation sortation;
    }

    public class CainiaoPrintDataRoutingInfoConsolidation
    {
        public string code;
        public string name;
    }

    public class CainiaoPrintDataRoutingInfoOrigin
    {
        public string code;
        public string name;
    }

    public class CainiaoPrintDataRoutingInfoSortation
    {
        public string name;
    }

    public class CainiaoPrintDataDataRecipient
    {
        public string mobile;

        public string name;

        public string phone;

        public CainiaoPrintDataDataRecipientAddress address;
    }

    public class CainiaoPrintDataDataRecipientAddress
    {
        public string province;
        public string city;
        public string district;
        public string town;
        public string detail;
    }

    #endregion


}

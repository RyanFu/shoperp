﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShopErp.App.ViewModels
{
    class DeliveryScanViewModel
    {
        public string OrderId { get; set; }

        public string DeliveryCompany { get; set; }

        public string DeliveryNumber { get; set; }

        public int GoodsCount { get; set; }

        public DateTime Time { get; set; }

        public string OrderGoodsInfo { get; set; }

        public string ReceiverInfo { get; set; }
    }
}
﻿namespace ShopErp.Domain
{
    public class GoodsMap
    {
        public long Id { get; set; }

        public long VendorId { get; set; }

        public string Number { get; set; }

        public long TargetGoodsId { get; set; }

        public float Price { get; set; }

        public bool IgnoreEdtion { get; set; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShopErp.App.Service.Print.PrintDocument.DeliveryPrintDocument.TaobaoCainiao
{
    public class CainiaoPrintDocumentRequestPrintTask
    {
        public string taskID = Guid.NewGuid().ToString();

        public bool preview = false;

        public string previewType = "image";

        public string printer;

        public int firstDocumentNumber = 0;

        public int totalDocumentCount = 0;

        public CainiaoPrintDocumentRequestPrintTaskDocument[] documents;
    }
}

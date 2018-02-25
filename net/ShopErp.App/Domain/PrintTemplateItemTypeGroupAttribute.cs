﻿using System;

namespace ShopErp.App.Domain
{
    [AttributeUsage(AttributeTargets.Field)]
    public class PrintTemplateItemTypeGroupAttribute : Attribute
    {
        public PrintTemplateItemTypeGroup Group { get; set; }

        public PrintTemplateItemTypeGroupAttribute(PrintTemplateItemTypeGroup group)
        {
            this.Group = group;
        }
    }
}
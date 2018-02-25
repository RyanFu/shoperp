﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShopErp.Domain;

namespace ShopErp.App.Utils
{
    public class EnumUtil
    {
        static Dictionary<Type, FieldInfo[]> typeFieldInfoCaches = new Dictionary<Type, FieldInfo[]>();
        static Dictionary<Type, string[]> typefieldDescriptionCaches = new Dictionary<Type, string[]>();

        static FieldInfo[] GetFiledInfos(Type t)
        {
            lock (typeFieldInfoCaches)
            {
                if (typeFieldInfoCaches.ContainsKey(t))
                {
                    return typeFieldInfoCaches[t];
                }
                var fields = t.GetFields().ToList();
                fields.RemoveAt(0); //第一个，是C#编译器生成的隐藏字段，需要移出
                typeFieldInfoCaches[t] = fields.ToArray();
                return typeFieldInfoCaches[t];
            }
        }

        public static string GetEnumValueDescription(Enum en)
        {
            var filesInfo = GetFiledInfos(en.GetType());
            var matchItem = filesInfo.FirstOrDefault(obj => obj.Name.Equals(en.ToString()));
            if (matchItem != null)
            {
                var atts = matchItem.GetCustomAttributes(typeof(EnumDescriptionAttribute), true);
                if (atts.Length > 0)
                {
                    return ((EnumDescriptionAttribute) atts[0]).Description;
                }
            }
            throw new Exception("无法解析枚举信息:" + en.GetType().FullName + "  " + en);
        }


        public static string[] GetEnumDescriptions<T>()
        {
            lock (typefieldDescriptionCaches)
            {
                if (typefieldDescriptionCaches.ContainsKey(typeof(T)))
                {
                    return typefieldDescriptionCaches[typeof(T)];
                }
                var filesInfo = GetFiledInfos(typeof(T));
                string[] descriptions = new string[filesInfo.Length];
                for (int i = 0; i < descriptions.Length; i++)
                {
                    var atts = filesInfo[i].GetCustomAttributes(typeof(EnumDescriptionAttribute), true);
                    if (atts.Length < 1)
                    {
                        throw new Exception("类型: " + typeof(T).FullName + " 值 " + filesInfo[i].ToString() + " 解析失败");
                    }
                    descriptions[i] = ((EnumDescriptionAttribute) atts[0]).Description;
                }
                typefieldDescriptionCaches[typeof(T)] = descriptions;
                return descriptions;
            }
        }

        public static T GetEnumValueByDesc<T>(string desc)
        {
            var descs = GetEnumDescriptions<T>().ToList();
            var values = Enum.GetValues(typeof(T));

            int index = descs.IndexOf(desc);

            if (index < 0)
            {
                throw new Exception("未能识别:" + typeof(T).FullName + "中的描述:" + desc);
            }
            return (T) values.GetValue(index);
        }
    }
}
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CEF.Common
{
    public static class EnumHelper
    {
        /// <summary>
        /// 将枚举类型转为选项列表
        /// 注：value为值,text为显示内容
        /// </summary>
        /// <param name="enumType">枚举类型</param>
        /// <returns></returns>
        public static List<SelectOption> ToOptionList(Type enumType)
        {
            var values = Enum.GetValues(enumType);
            List<SelectOption> list = new List<SelectOption>();
            foreach (var aValue in values)
            {
                list.Add(new SelectOption
                {
                    value = ((int)aValue).ToString(),
                    text = aValue.ToString()
                });
            }

            return list;
        }

        /// <summary>
        /// 多选枚举转为对应文本,逗号隔开
        /// </summary>
        /// <param name="values">多个值</param>
        /// <param name="enumType">枚举类型</param>
        /// <returns></returns>
        public static string ToMultipleText(List<int> values, Type enumType)
        {
            if (values == null)
                return string.Empty;

            List<string> textList = new List<string>();

            var allValues = Enum.GetValues(enumType);
            foreach (var aValue in allValues)
            {
                if (values.Contains((int)aValue))
                    textList.Add(aValue.ToString());
            }

            return string.Join(",", textList);
        }

        /// <summary>
        /// 多选枚举转为对应文本,逗号隔开
        /// </summary>
        /// <param name="values">多个值逗号隔开</param>
        /// <param name="enumType">枚举类型</param>
        /// <returns></returns>
        public static string ToMultipleText(string values, Type enumType)
        {
            return ToMultipleText(values?.Split(',')?.Select(x => x.ToInt())?.ToList(), enumType);
        }

        /// <summary>
        /// 获取枚举item，值，描述
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Task<object> GetEnum(string typeName)
        {
            List<dynamic> list = new List<dynamic>();
            Assembly assemblyArray = GlobalConfigure.AllAssemblies.Where(x=>x.FullName.Contains("CEF.Common")).FirstOrDefault();
            Type enumType = assemblyArray.GetType(typeName);          
            if (enumType == null)
                throw new Exception($"未能发现该类型{typeName}");
            Array values = Enum.GetValues(enumType);
           
                for(int i=0; i < values.Length; i++)
                {
                    var item = (int)values.GetValue(i);
                    list.Add(new { Item = item, Value = values.GetValue(i).ToString(), Description = (values.GetValue(i) as Enum).GetDescription() });
                }
            return Task.FromResult(list as object);
        }
    }
}

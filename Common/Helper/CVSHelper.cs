using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Helper
{
    public class CVSHelper
    {
        public static MemoryStream ExportCSV<T>(List<T> list, Dictionary<string, string> headers, string tab = ",")
        {
            var stream = new MemoryStream();
            var csvWriter = new StreamWriter(stream, Encoding.UTF8);
            csvWriter.AutoFlush = true;
            var isFirst = true;
            foreach (KeyValuePair<string, string> entry in headers)
            {
                if (isFirst)
                {
                    csvWriter.Write(entry.Value);
                    isFirst = false;
                }
                else
                {
                    csvWriter.Write(tab + entry.Value);
                }
            }
            csvWriter.Write(Environment.NewLine);
            Type type = typeof(T);
            foreach (var item in list)
            {
                isFirst = true;
                foreach (var entry in headers)
                {
                    var property = type.GetProperty(entry.Key);
                    if (property == null)
                        continue;
                    var entryValue = property.GetValue(item, null);
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        csvWriter.Write(tab);
                    }
                    if (property.PropertyType == typeof(DateTime) || GetTypeName(property.PropertyType) == "DateTime")
                    {
                        csvWriter.Write((entryValue as DateTime?)?.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else
                    {
                        csvWriter.Write(entryValue?.ToString());
                    }
                }
                csvWriter.Write(Environment.NewLine);
            }
            //csvWriter.Flush();
            stream.Position = 0;
            return stream;
        }

        public static MemoryStream ExportExcell<T>(List<T> list, Dictionary<string, string> headers)
        {
            var stream = new MemoryStream();
            using (ExcelPackage package = new ExcelPackage(stream))
            {
                // 添加worksheet
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Sheet1");
                var colIndex = 1;
                foreach (KeyValuePair<string, string> entry in headers)
                {
                    worksheet.Cells[1, colIndex++].Value = entry.Value;
                }
                var rowIndex = 2;
                colIndex = 0;
                Type type = typeof(T);
                foreach (var item in list)
                {
                    foreach (KeyValuePair<string, string> entry in headers)
                    {
                        colIndex++;
                        var property = type.GetProperty(entry.Key);
                        if (property == null)
                            continue;
                        worksheet.Cells[rowIndex, colIndex].Value = property.GetValue(item, null);
                        if (property.PropertyType == typeof(DateTime) || GetTypeName(property.PropertyType) == "DateTime")
                        {
                            worksheet.Cells[rowIndex, colIndex].Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
                        }
                    }
                    colIndex = 0;
                    rowIndex++;
                }
                //自动列宽
                worksheet.Cells.AutoFitColumns();
                stream = new MemoryStream(package.GetAsByteArray());
            }

            return stream;
        }

        public static List<T> Import<T>(Dictionary<string, string> headers, Stream stream)
        {
            if (headers == null || headers.Count == 0 || headers.Count(a => string.IsNullOrWhiteSpace(a.Key) || string.IsNullOrWhiteSpace(a.Value)) > 0)
                throw new Exception("未正确设置需要导入的列");
            using (ExcelPackage package = new ExcelPackage(stream))
            {
                if (package.Workbook.Worksheets == null || package.Workbook.Worksheets.Count == 0)
                    throw new Exception("导入的数据为空(提示:仅支持xlsx格式文件,不支持xls格式文件)");
                ExcelWorksheet worksheet = package.Workbook.Worksheets[1];
                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;
                if (rowCount == 0 || colCount == 0)
                {
                    throw new Exception("没有需要导入的数据");
                }
                Dictionary<int, string> keys = new Dictionary<int, string>();
                Dictionary<int, string> types = new Dictionary<int, string>();
                Type type = typeof(T);
                for (int col = 1; col <= colCount; col++)
                {
                    var obj = GetValue(worksheet.Cells[1, col].Value, null);
                    if (!string.IsNullOrWhiteSpace(obj))
                    {
                        var e = headers.FirstOrDefault(a => a.Value.ToLower() == obj.ToLower());
                        if (!e.Equals(default(KeyValuePair<string, string>)))
                        {
                            keys[col] = e.Key;
                            var property = type.GetProperty(e.Key);
                            if (property == null)
                                types[col] = "string";
                            else
                                types[col] = property.PropertyType.Name.ToLower();
                        }
                    }
                }
                if (keys.Count == 0)
                {
                    throw new Exception("导入的模板列不正确");
                }
                StringBuilder json = new StringBuilder();

                for (int row = 2; row <= rowCount; row++)
                {
                    if (json.Length > 0)
                        json.Append(",");
                    json.Append("{");
                    var index = 1;
                    foreach (var key in keys.Keys)
                    {
                        var obj = GetValue(worksheet.Cells[row, key].Value, types[key]);
                        json.AppendFormat(@"""{0}"":{1}", keys[key], JsonConvert.ToString(obj));
                        if (index < keys.Count)
                            json.Append(",");
                        index++;
                    }
                    json.Append("}");
                }
                if (json.Length > 0)
                {
                    List<T> list = JsonConvert.DeserializeObject<List<T>>($"[{json.ToString()}]");
                    return list;
                }
            }
            return null;
        }

        private static string GetValue(object val, string property)
        {
            if (val == null || val == DBNull.Value)
            {
                if (property == null)
                {
                    return string.Empty;
                }
                if (property == "int" || property == "decimal" || property == "double" || property == "float" || property == "single")
                {
                    return "0.00";
                }
                else if (property == "boolean" || property == "bool")
                {
                    return "false";
                }
                return string.Empty;
            }
            if (val is DateTime)
            {
                return ((DateTime)val).ToString("yyyy-MM-dd HH:mm:ss");
            }
            else if (property == "decimal" || property == "double" || property == "float" || property == "single")
            {
                return ((double)val).ToString("#.##########");
            }
            return val.ToString();
        }

        public static string GetTypeName(Type type)
        {
            var nullableType = Nullable.GetUnderlyingType(type);

            bool isNullableType = nullableType != null;

            if (isNullableType)
                return nullableType.Name;
            else
                return type.Name;
        }
    }
}

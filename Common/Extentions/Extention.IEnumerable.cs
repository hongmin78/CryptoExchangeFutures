using CEF.Common.Primitives;
using CEF.Common;
using System.Collections;
using System.Data;
using System.Linq.Dynamic.Core; 

namespace CEF.Common.Extentions
{
    public static partial class Extention
    {
        /// <summary>
        /// 复制序列中的数据
        /// </summary>
        /// <typeparam name="T">泛型</typeparam>
        /// <param name="iEnumberable">原数据</param>
        /// <param name="startIndex">原数据开始复制的起始位置</param>
        /// <param name="length">需要复制的数据长度</param>
        /// <returns></returns>
        public static IEnumerable<T> Copy<T>(this IEnumerable<T> iEnumberable, int startIndex, int length)
        {
            var sourceArray = iEnumberable.ToArray();
            T[] newArray = new T[length];
            Array.Copy(sourceArray, startIndex, newArray, 0, length);

            return newArray;
        }

        /// <summary>
        /// 给IEnumerable拓展ForEach方法
        /// </summary>
        /// <typeparam name="T">模型类</typeparam>
        /// <param name="iEnumberable">数据源</param>
        /// <param name="func">方法</param>
        public static void ForEach<T>(this IEnumerable<T> iEnumberable, Action<T> func)
        {
            foreach (var item in iEnumberable)
            {
                func(item);
            }
        }

        /// <summary>
        /// 给IEnumerable拓展ForEach方法
        /// </summary>
        /// <typeparam name="T">模型类</typeparam>
        /// <param name="iEnumberable">数据源</param>
        /// <param name="func">方法</param>
        public static void ForEach<T>(this IEnumerable<T> iEnumberable, Action<T, int> func)
        {
            var array = iEnumberable.ToArray();
            for (int i = 0; i < array.Count(); i++)
            {
                func(array[i], i);
            }
        }

        /// <summary>
        /// IEnumerable转换为List'T'
        /// </summary>
        /// <typeparam name="T">参数</typeparam>
        /// <param name="source">数据源</param>
        /// <returns></returns>
        public static List<T> CastToList<T>(this IEnumerable source)
        {
            return new List<T>(source.Cast<T>());
        }

        /// <summary>
        /// 将IEnumerable'T'转为对应的DataTable
        /// </summary>
        /// <typeparam name="T">数据模型</typeparam>
        /// <param name="iEnumberable">数据源</param>
        /// <returns>DataTable</returns>
        public static DataTable ToDataTable<T>(this IEnumerable<T> iEnumberable)
        {
            return iEnumberable.ToJson().ToDataTable();
        }

        /// <summary>
        /// 分页数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="pageInput"></param>
        /// <param name="totalCount"></param>
        /// <returns></returns>
        public static PageResult<T> PageData<T>(this IEnumerable<T> source, PageInput pageInput, int totalCount = 0)
        {
            if (pageInput.PageRows > 2000)
                throw new ArgumentException("页记录数不能超过2000");

            if (totalCount == 0)
                totalCount = source.Count();

            return new PageResult<T>
            {
                Total = totalCount,
                Data = source
                .Skip((pageInput.PageIndex - 1) * pageInput.PageRows)
                .Take(pageInput.PageRows)
                .ToList()
            };

        }

        /// <summary>
        /// 逐页读取数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="pageRows"></param>
        /// <returns></returns>
        public static IEnumerable<PageResult<T>> PageByPageData<T>(this IEnumerable<T> source, int pageRows = 2000)
        {
            if (pageRows > 2000)
                throw new ArgumentException("页记录数不能超过2000");

            var pagedData = source.PageData(new PageInput { PageRows = pageRows });
            int pagesCount = (pagedData.Total / pageRows) + 1;
            int total = pagedData.Total;
            //返回第一页
            yield return pagedData;

            for (int pageIndex = 2; pageIndex <= pagesCount; pageIndex++)
            {
                var pageInput = new PageInput { PageIndex = pageIndex, PageRows = pageRows };
                yield return source.PageData(pageInput, total);
            }
        }

        /// <summary>
        /// 从分页结果中创建新结果
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="pagedResult"></param>
        /// <param name="newfunc"></param>
        /// <returns></returns>
        public static PageResult<TResult> NewResult<TSource, TResult>(this PageResult<TSource> pagedResult,
            Func<IEnumerable<TSource>, IEnumerable<TResult>> newfunc)
        {
            return new PageResult<TResult>
            {
                Total = pagedResult.Total,
                Data = newfunc(pagedResult.Data)
                .ToList()
            };
        }
        /// <summary>
        /// 获取分页数据(包括总数量)
        /// </summary>
        /// <typeparam name="T">泛型</typeparam>
        /// <param name="iEnumberable">数据源</param>
        /// <param name="pageInput">分页参数</param>
        /// <returns></returns>
        public static PageResult<T> GetPageResult<T>(this IEnumerable<T> iEnumberable, PageInput pageInput)
        {
            int count = iEnumberable.Count();

            var list = iEnumberable.AsQueryable()
                .OrderBy($@"{pageInput.SortField} {pageInput.SortType}")
                .Skip((pageInput.PageIndex - 1) * pageInput.PageRows)
                .Take(pageInput.PageRows)
                .ToList();

            return new PageResult<T> { Data = list, Total = count };
        }

        /// <summary>
        /// 获取分页数据(仅获取列表,不获取总数量)
        /// </summary>
        /// <typeparam name="T">泛型</typeparam>
        /// <param name="iEnumberable">数据源</param>
        /// <param name="pageInput">分页参数</param>
        /// <returns></returns>
        public static List<T> GetPageList<T>(this IEnumerable<T> iEnumberable, PageInput pageInput)
        {
            var list = iEnumberable.AsQueryable()
                .OrderBy($@"{pageInput.SortField} {pageInput.SortType}")
                .Skip((pageInput.PageIndex - 1) * pageInput.PageRows)
                .Take(pageInput.PageRows)
                .ToList();

            return list;
        }
    }
}

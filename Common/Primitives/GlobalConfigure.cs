using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public static class GlobalConfigure
    {
        static Assembly[] _allAssemblies;
        /// <summary>
        /// 解决方案所有程序集
        /// </summary>
        public static Assembly[] AllAssemblies
        {
            get
            {
                if (_allAssemblies == null)
                {
                    string rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    _allAssemblies = Directory.GetFiles(rootPath, "*.dll")
                        .Where(x => new FileInfo(x).Name.Contains(FXASSEMBLY_PATTERN))
                        .Select(x => Assembly.LoadFrom(x))
                        .Where(x => !x.IsDynamic)
                        .ToArray();
                }
                return _allAssemblies;
            }
        }

        /// <summary>
        /// 解决方案所有自定义类
        /// </summary>
        public static readonly Type[] AllTypes = AllAssemblies.SelectMany(x => x.GetTypes()).ToArray();
        /// <summary>
        /// 解决方案程序集匹配名
        /// </summary>
        public const string FXASSEMBLY_PATTERN = "CEF.";
        /// <summary>
        /// 超级管理员UserIId
        /// </summary>
        public const string ADMINID = "Admin";
        /// <summary>
        /// 依赖注入实体
        /// </summary>
        public static IServiceProvider ServiceLocatorInstance { get; set; } 
    }
}

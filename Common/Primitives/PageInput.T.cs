using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public class PageInput<T> : PageInput where T : new()
    {
        public T Search { get; set; } = new T();      
    }
}

using CEF.Common.Exchange;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Entity
{
    [Table("Order")]
    public class Order
    { 
        [Key]
        public long Id { get; set; }

        public string Symbol { set; get; } 

        public string ClientOrderId { set; get; }

        public decimal? Price { set; get; }

        public decimal? AvgPrice { set; get; }

        public decimal Quantity { set; get; }

        public string OrderSide { set; get; }


        public string Status { set; get; }

        public string Type { set; get; }

        public string PositionSide { set; get; }

        public string CreateTime { set; get; }

        public string UpdateTime { set; get; }

        public string Side { set; get; }
    }
}

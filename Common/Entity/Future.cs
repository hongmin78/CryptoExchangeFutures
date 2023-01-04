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
    [Table("Future")]
    public class Future
    {
        [Key]
        public long Id { get; set; }

        public string Symbol { set; get; }

        public int PositionSide { set; get; }

        public string UpdateTime { set; get; }

        public decimal Size { set; get; }

        public decimal AbleSize { set; get; }

        public decimal EntryPrice { set; get; }

        public decimal BaseOrderSize { set; get; }
        /// <summary>
        /// 止赢%
        /// </summary>
        public decimal TargetProfit { set; get; }
        /// <summary>
        /// 第一笔安全单数量
        /// </summary>
        public decimal SafetyOrderSize { set; get; }
        /// <summary>
        /// 最大安全单笔数
        /// </summary>
        public int MaxSafetyOrdersCount { set; get; }
        /// <summary>
        /// 安全单量偏差倍数
        /// </summary>
        public decimal SafetyOrderVolumeScale { set; get; }
        /// <summary>
        /// 安全单价格偏差倍数
        /// </summary>
        public decimal SafetyOrderPriceScale { set; get; }
        /// <summary>
        /// 安全单价格偏差
        /// </summary>
        public decimal SafetyOrderPriceDeviation { set; get; }
        /// <summary>
        /// 最后一笔开仓价
        /// </summary>
        public decimal LastTransactionOpenPrice { set; get; }
        /// <summary>
        /// 最后一笔开仓量
        /// </summary>
        public decimal LastTransactionOpenSize { set; get; }
        /// <summary>
        /// 当前第几笔开仓
        /// </summary>
        public int OrdersCount { set; get; }

        public int IsEnabled { set; get; }

        public FutureStatus Status { set; get; }
    }

    public enum FutureStatus
    {
        [Description("无")]
        None,
        [Description("开仓中")]
        Openning,
        [Description("平仓中")]
        Closing
    }
}

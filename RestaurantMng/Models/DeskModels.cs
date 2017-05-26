using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RestaurantMng.Models
{
    //台桌信息模型，某区有多少行多少列台桌,用于可视化选桌
    public class DeskInfoModel
    {
        public string belongTo { get; set; }
        public string zone { get; set; }
        public int maxRow { get; set; }
        public int maxCol { get; set; }
    }

    //台桌模型，用于可视化选卓
    public class VisualDeskModel
    {
        //编号
        public string number { get; set; }
        //名称
        public string name { get; set; }
        //属于大堂或包间
        public string belongTo { get; set; }
        //区域
        public string zone { get; set; }
        //可坐数量
        public int? seatQty { get; set; }
        //是否作废
        public bool? isCancel { get; set; }
        //已占用数量
        public int? seatHasTaken { get; set; }
        //当前是否可用
        public bool nowCanUse { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RestaurantMng.Models
{
    public class UserInfo
    {
        public int id { get; set; }
        public string userName { get; set; }
        public string realName { get; set; }

    }
    public class MenuItemModel
    {
        public string name { get; set; }
        public decimal? number { get; set; }
        public string action_name { get; set; }
        public string controller_name { get; set; }
        public string iconcls { get; set; }
    }

    public class SimpleResultModel
    {
        public bool suc { get; set; }
        public string msg { get; set; }
        public object extra { get; set; }

    }

    public class comboResultModel
    {
        public string text { get; set; }
        public string value { get; set; }
    }

    public class DepTreeModel
    {
        public int id { get; set; }
        public string text { get; set; }
        public string state { get; set; }
        public string iconCls { get; set; }
    }

}
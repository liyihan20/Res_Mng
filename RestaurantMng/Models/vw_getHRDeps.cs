//------------------------------------------------------------------------------
// <auto-generated>
//    此代码是根据模板生成的。
//
//    手动更改此文件可能会导致应用程序中发生异常行为。
//    如果重新生成代码，则将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace RestaurantMng.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class vw_getHRDeps
    {
        public int id { get; set; }
        public string short_name { get; set; }
        public string long_name { get; set; }
        public Nullable<int> parent_id { get; set; }
        public string child_ids { get; set; }
        public string charge_no { get; set; }
        public string charge_name { get; set; }
        public int is_leaf { get; set; }
    }
}

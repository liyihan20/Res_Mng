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
    
    public partial class dn_pointsForDish
    {
        public dn_pointsForDish()
        {
            this.dn_pointsForDishRecord = new HashSet<dn_pointsForDishRecord>();
        }
    
        public int id { get; set; }
        public int points_need { get; set; }
        public Nullable<int> dish_id { get; set; }
        public Nullable<System.DateTime> from_date { get; set; }
        public Nullable<System.DateTime> end_date { get; set; }
        public string create_no { get; set; }
        public Nullable<System.DateTime> create_date { get; set; }
        public Nullable<bool> is_selling { get; set; }
        public Nullable<int> has_fullfill { get; set; }
        public string res_no { get; set; }
    
        public virtual dn_dishes dn_dishes { get; set; }
        public virtual ICollection<dn_pointsForDishRecord> dn_pointsForDishRecord { get; set; }
    }
}
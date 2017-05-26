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
    
    public partial class dn_dishes
    {
        public dn_dishes()
        {
            this.dn_deleted_images = new HashSet<dn_deleted_images>();
            this.dn_orderEntry = new HashSet<dn_orderEntry>();
            this.dn_pointsForDish = new HashSet<dn_pointsForDish>();
        }
    
        public int id { get; set; }
        public string number { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public Nullable<decimal> price { get; set; }
        public Nullable<decimal> discount_price { get; set; }
        public string discount_weekday { get; set; }
        public string discount_time { get; set; }
        public string sell_weekday { get; set; }
        public string sell_time { get; set; }
        public Nullable<bool> can_delivery { get; set; }
        public string description { get; set; }
        public byte[] image_1 { get; set; }
        public byte[] image_2 { get; set; }
        public byte[] image_3 { get; set; }
        public Nullable<System.DateTime> create_time { get; set; }
        public Nullable<System.DateTime> last_update_time { get; set; }
        public Nullable<int> create_user { get; set; }
        public Nullable<bool> is_selling { get; set; }
        public string image_1_name { get; set; }
        public string image_2_name { get; set; }
        public string image_3_name { get; set; }
        public Nullable<bool> is_on_top { get; set; }
        public byte[] image_1_thumb { get; set; }
        public byte[] image_2_thumb { get; set; }
        public byte[] image_3_thumb { get; set; }
        public Nullable<bool> is_birthday_meal { get; set; }
        public string res_no { get; set; }
    
        public virtual ICollection<dn_deleted_images> dn_deleted_images { get; set; }
        public virtual dn_users dn_users { get; set; }
        public virtual ICollection<dn_orderEntry> dn_orderEntry { get; set; }
        public virtual ICollection<dn_pointsForDish> dn_pointsForDish { get; set; }
    }
}
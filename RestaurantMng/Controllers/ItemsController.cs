using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Filters;
using RestaurantMng.Models;
using System.IO;
using RestaurantMng.Utils;
using System.Drawing;
using System.Text.RegularExpressions;

namespace RestaurantMng.Controllers
{
    [SessionTimeOutFilter]
    public class ItemsController : BaseController
    {
        //获取所有用户
        public JsonResult GetAllUsers()
        {
            var users = from u in db.dn_users
                        select new
                        {
                            id = u.id,
                            name = u.real_name
                        };
            return Json(users);
        }

        //获取某组可以拥有的其它用户，不包含已拥有用户
        public JsonResult GetUsersNotInGroup(int groupId)
        {
            var users = from u in db.dn_users
                        where !(db.dn_groupUser.Where(g => g.group_id == groupId).Select(g => g.user_id).Contains(u.id))
                        select new
                        {
                            id = u.id,
                            name = u.real_name
                        };
            return Json(users);
        }

        //获取某组可以拥有的其它权限，不包含已拥有权限
        public JsonResult GetAuthoritiesNotInGroup(int groupId)
        {
            var auts = from a in db.dn_authority
                       where !(db.dn_groupAuthority.Where(g => g.group_id == groupId).Select(g => g.authority_id).Contains(a.id))
                       orderby a.number
                       select new
                       {
                           id = a.id,
                           name = a.name
                       };
            return Json(auts);
        }

        //获取可以选择的食堂列表
        public JsonResult GetResList()
        {
            var res = (from r in db.dn_Restaurent
                       where ResPowers.Contains(r.no)
                       select new comboResultModel()
                       {
                           text = r.name,
                           value = r.no
                       }).ToList();
            return Json(res);
        }

        //获取台桌或者房间
        public JsonResult GetDeskByType(string time,string isInRoom)
        {
            string weekday = time.Substring(0, 2);
            string segment = time.Substring(2, 2);
            string roomType = isInRoom.Equals("是") ? "包间" : "大堂";
            var desks = (from d in db.dn_desks
                            where (d.is_cancel == false
                            || d.is_cancel == null)
                            && d.belong_to == roomType
                            && (d.open_weekday == "每天"
                            || d.open_weekday.Contains(weekday))
                            && (d.open_time == "全天"
                            || d.open_time.Contains(segment))
                            select new comboResultModel
                            {
                                text = d.number,
                                value = d.number
                            }).ToList();
            return Json(desks);
        }

        //获取菜式列表(当前食堂权限的菜式）
        public JsonResult GetAllSellingDishName(string res_no="")
        {
            var dishes = from d in db.dn_dishes
                         where d.is_selling == true
                         select d;
            if (!string.IsNullOrEmpty(res_no)) {
                dishes = dishes.Where(d => d.res_no == res_no);
            }
            var result = (from d in dishes                                                
                          orderby d.number
                          select new comboResultModel()
                          {
                              value = d.name
                          }).ToList();
            return Json(result);
        }

        //获取餐别时间段
        public JsonResult GetResTimeSeg(string res_no)
        {
            List<comboResultModel> list = new List<comboResultModel>();
            list.Add(new comboResultModel() { value = "全天" });
            var seg = (from i in db.dn_items
                       where i.comment.EndsWith("时间段")
                       && i.res_no == res_no
                       && i.value != "无"
                       select new comboResultModel()
                       {
                           value = i.comment.Substring(0, 2)
                       }).ToList();
            list.AddRange(seg);

            return Json(list);
        }

        #region 菜式管理

        [AuthorityFilter]
        public ActionResult DishesManagement()
        {
            ViewData["disable_power"] = HasGotPower("Dishes_manage_op") ? "false" : "true";
            return View();
        }

        public JsonResult GetDishes(int page, int rows, string searchValue)
        {
            if (string.IsNullOrEmpty(searchValue)) searchValue = "";
            var dishes = (from d in db.dn_dishes
                          where (d.name.Contains(searchValue)
                          || d.number.Contains(searchValue)
                          || d.type.Contains(searchValue))
                          && ResPowers.Contains(d.res_no)
                          orderby d.is_on_top descending, d.is_selling descending, d.number
                          select d).ToList();
            int total = dishes.Count();
            var list = (from d in dishes.Skip((page - 1) * rows).Take(rows)
                        select new
                        {
                            d.id,
                            d.type,
                            d.name,
                            d.number,
                            d.price,
                            can_delivery = d.can_delivery == true ? "是" : "否",
                            create_time = ((DateTime)d.create_time).ToString("yyyy-MM-dd HH:mm"),
                            update_time = ((DateTime)d.last_update_time).ToString("yyyy-MM-dd HH:mm"),
                            is_selling = d.is_selling == true ? "在售" : "下架",
                            d.sell_weekday,
                            d.sell_time,
                            d.description,
                            d.image_1_name,
                            d.image_2_name,
                            d.image_3_name,
                            is_on_top = d.is_on_top == true ? @"<i class='fa fa-thumbs-up'></i>" : "",
                            is_birthday_meal = d.is_birthday_meal == true ? "是" : "否",
                            d.res_no,
                            res_name = GetResNameByNo(d.res_no)
                        }).ToList();

            WriteEventLog("菜式管理", "获取列表：searchValue:" + searchValue);

            return Json(new { rows = list, total = total });
        }

        [HttpPost]
        public JsonResult SaveDish(FormCollection fc)
        {
            string id = fc.Get("id");
            string type = fc.Get("type");
            string number = fc.Get("number");
            string name = fc.Get("name");
            string price = fc.Get("price");
            string sell_weekday = fc.Get("sell_weekday");
            string sell_time = fc.Get("sell_time");
            string is_selling = fc.Get("is_selling");
            string can_delivery = fc.Get("can_delivery");
            string description = fc.Get("description");
            string is_birthday_meal = fc.Get("is_birthday_meal");
            string res_no = fc.Get("res_no");

            dn_dishes dish;
            int idInt = 0; //id为0表示是新增菜式，否则是编辑菜式，id为菜式id
            if (!int.TryParse(id, out idInt)) {
                dish = new dn_dishes();
            }
            else {
                dish = db.dn_dishes.Single(d => d.id == idInt);
            }


            //验证&赋值
            decimal priceD;

            dish.type = type;
            if (number.Length <= 25) {
                if (idInt == 0 && db.dn_dishes.Where(d => d.number == number && d.res_no == res_no).Count() > 0) {
                    return Json(new { suc = false, msg = "序号已存在，不能重复保存" }, "text/html");
                }
                dish.number = number;
            }
            else {
                return Json(new { suc = false, msg = "序号的字数必须在25个字符之内，保存失败" }, "text/html");
            }
            if (name.Length <= 25) {
                if (idInt == 0 && db.dn_dishes.Where(d => d.name == name && d.res_no == res_no).Count() > 0) {
                    return Json(new { suc = false, msg = "名称已存在，不能重复保存" }, "text/html");
                }
                dish.name = name;
            }
            else {
                return Json(new { suc = false, msg = "名称的字数必须在25个字符之内，保存失败" }, "text/html");
            }
            if (decimal.TryParse(price, out priceD)) {
                dish.price = priceD;
            }
            else {
                return Json(new { suc = false, msg = "单价不合法，保存失败" }, "text/html");
            }
            dish.sell_weekday = sell_weekday.Contains("每天") ? "每天" : sell_weekday;
            dish.sell_time = sell_time.Equals("全天") ? "全天" : sell_time;
            dish.is_selling = is_selling.Equals("在售") ? true : false;
            dish.can_delivery = can_delivery.Equals("是") ? true : false;
            if (description.Length <= 500) {
                dish.description = description;
            }
            else {
                return Json(new { suc = false, msg = "简介的字数必须在500个字符之内，保存失败" }, "text/html");
            }
            if (idInt == 0) {
                dish.create_user = userInfo.id;
                dish.create_time = DateTime.Now;
            }
            dish.last_update_time = DateTime.Now;
            dish.is_birthday_meal = "是".Equals(is_birthday_meal) ? true : false;
            dish.res_no = res_no;

            //保存上传的3张图片
            byte[] image = null;
            string fileNames = "";
            foreach (string file in Request.Files) {
                HttpPostedFileBase uploadFile = Request.Files[file] as HttpPostedFileBase;
                if (uploadFile == null || uploadFile.ContentLength == 0) {
                    //没有上传,是主图的话返回，次图则继续
                    if (idInt == 0 && file.Equals("pic1")) {
                        return Json(new { suc = false, msg = "主图必须上传，保存失败。" }, "text/html");
                    }
                    continue;
                }

                if (fileNames.Contains(uploadFile.FileName + ";")) {
                    return Json(new { suc = false, msg = "图片名称有重复，保存失败" }, "text/html");
                }
                else {
                    fileNames += uploadFile.FileName + ";";
                }

                if (!uploadFile.ContentType.StartsWith("image")) {
                    return Json(new { suc = false, msg = "上传文件不是图片格式,保存失败:" + file }, "text/html");
                }

                image = new byte[uploadFile.ContentLength];
                uploadFile.InputStream.Read(image, 0, uploadFile.ContentLength);
                switch (file) {
                    case "pic1":
                        if (idInt != 0) {
                            //备份被替换图片
                            PicInsertToBackup(idInt, dish.image_1, dish.image_1_name);
                        }
                        dish.image_1 = image;
                        dish.image_1_name = uploadFile.FileName;
                        dish.image_1_thumb = ImageUtil.MakeThumbnail(ImageUtil.BytesToImage(image));
                        break;
                    case "pic2":
                        if (idInt != 0) {
                            //备份被替换图片
                            PicInsertToBackup(idInt, dish.image_2, dish.image_2_name);
                        }
                        dish.image_2 = image;
                        dish.image_2_name = uploadFile.FileName;
                        dish.image_2_thumb = ImageUtil.MakeThumbnail(ImageUtil.BytesToImage(image));
                        break;
                    case "pic3":
                        if (idInt != 0) {
                            //备份被替换图片
                            PicInsertToBackup(idInt, dish.image_3, dish.image_3_name);
                        }
                        dish.image_3 = image;
                        dish.image_3_name = uploadFile.FileName;
                        dish.image_3_thumb = ImageUtil.MakeThumbnail(ImageUtil.BytesToImage(image));
                        break;
                }
            }

            if (idInt == 0) {
                db.dn_dishes.Add(dish);
            }
            db.SaveChanges();

            WriteEventLog("菜式管理", idInt == 0 ? "新增菜式：" : "编辑菜式：" + name);
            return Json(new { suc = true, msg = "保存成功" }, "text/html");
        }

        //备份被替换的菜式图片
        private void PicInsertToBackup(int dish_id, byte[] deleted, string deleted_name)
        {
            if (deleted == null) return;
            dn_deleted_images di = new dn_deleted_images();
            di.deleted_image = deleted;
            di.deleted_image_name = deleted_name;
            di.dishes_id = dish_id;
            di.op_time = DateTime.Now;

            db.dn_deleted_images.Add(di);
            db.SaveChanges();
        }

        public void CheckPic(int id, int which)
        {
            var dishes = db.dn_dishes.Where(d => d.id == id);
            if (dishes.Count() == 1) {
                var dish = dishes.First();
                switch (which) {
                    case 1:
                        if (dish.image_1 == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_1.ToArray());
                        break;
                    case 2:
                        if (dish.image_2 == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_2.ToArray());
                        break;
                    case 3:
                        if (dish.image_3 == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_3.ToArray());
                        break;
                }
            }
        }

        public void CheckPicThumb(int id, int which)
        {
            var dishes = db.dn_dishes.Where(d => d.id == id);
            if (dishes.Count() == 1) {
                var dish = dishes.First();
                switch (which) {
                    case 1:
                        if (dish.image_1_thumb == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_1_thumb.ToArray());
                        break;
                    case 2:
                        if (dish.image_2_thumb == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_2_thumb.ToArray());
                        break;
                    case 3:
                        if (dish.image_3_thumb == null)
                            getDefaultImg();
                        else
                            Response.BinaryWrite(dish.image_3_thumb.ToArray());
                        break;
                }
            }
        }

        private void getDefaultImg()
        {
            Response.WriteFile(HttpContext.Server.MapPath("~/Content/imgs/none.png"));
        }

        //切换菜式的状态，在售/下架
        public JsonResult ToggleDishStatus(int id)
        {
            try {
                var dish = db.dn_dishes.Single(d => d.id == id);
                dish.is_selling = !(bool)dish.is_selling;
                db.SaveChanges();
                WriteEventLog("菜式管理", "成功切换状态：" + id);
                return Json(new SimpleResultModel() { suc = true, msg = "状态切换成功" });
            }
            catch (Exception ex) {
                WriteEventLog("菜式管理", "状态切换失败：" + id + ";reason:" + ex.Message, -100);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        //今日推荐
        public JsonResult ToggleDishOnTop(int id)
        {
            int maxTopNum = 10;
            if (db.dn_dishes.Where(d => d.is_on_top == true).Count() >= maxTopNum) {
                return Json(new SimpleResultModel() { suc = false, msg = "今日推荐数量已达到最大数量：" + maxTopNum + ";请取消一个再操作。" });
            }
            try {
                var dish = db.dn_dishes.Single(d => d.id == id);
                dish.is_on_top = dish.is_on_top == true ? false : true;
                db.SaveChanges();
                WriteEventLog("菜式管理", "成功切换今日推荐：" + id);
                return Json(new SimpleResultModel() { suc = true, msg = "设置成功" });
            }
            catch (Exception ex) {
                WriteEventLog("菜式管理", "今日推荐切换失败：" + id + ";reason:" + ex.Message, -100);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        #endregion

        #region 台桌管理

        public ActionResult DesksManagement()
        {
            ViewData["disable_power"] = HasGotPower("Desks_manage_op") ? "false" : "true";
            return View();
        }

        public JsonResult GetDesks(int page, int rows, string searchValue)
        {
            searchValue = searchValue ?? "";
            var desks = (from d in db.dn_desks
                         where d.number.Contains(searchValue)
                         || d.name.Contains(searchValue)
                         || d.belong_to.Contains(searchValue)                         
                         select d).ToList();

            int total = desks.Count();
            var list = (from d in desks
                        orderby d.number.Substring(0,1), //先按首字母排序
                        int.Parse(d.number.Substring(1,d.number.IndexOf('-')-1)), //再按第几行排序
                        int.Parse(d.number.Substring(d.number.IndexOf('-')+1)) //最后按第几列排序
                        select new
                        {
                            d.id,
                            d.number,
                            d.name,
                            d.seat_qty,
                            d.belong_to,
                            create_time = ((DateTime)d.create_time).ToString("yyyy-MM-dd HH:mm"),
                            last_update_time = ((DateTime)d.last_update_time).ToString("yyyy-MM-dd HH:mm"),
                            d.open_weekday,
                            d.open_time,
                            is_cancel = d.is_cancel == false ? "正常" : "作废"
                        }).Skip((page - 1) * rows).Take(rows).ToList();

            WriteEventLog("台桌管理", "获取列表：searchValue:" + searchValue);

            return Json(new { rows = list, total = total });
        }

        public JsonResult SaveDesk(FormCollection fc)
        {
            string id = fc.Get("id");
            string belong_to = fc.Get("belong_to");
            string number = fc.Get("number");
            string name = fc.Get("name");
            string seat_qty = fc.Get("seat_qty");
            string open_weekday = fc.Get("open_weekday");
            string open_time = fc.Get("open_time");
            string is_cancel = fc.Get("is_cancel");

            dn_desks desk;
            int idInt = 0; //id为0表示是新增台桌，否则是编辑台桌，id为台桌id
            if (!int.TryParse(id, out idInt)) {
                desk = new dn_desks();
            }
            else {
                desk = db.dn_desks.Single(d => d.id == idInt);
            }


            //验证&赋值
            int seatQtyD;

            desk.belong_to = belong_to;
            number = number.Trim().ToUpper();
            if (number.Length <= 25 && number.Length>=4) {
                if (idInt == 0 && db.dn_desks.Where(d => d.number == number).Count() > 0) {
                    return Json(new { suc = false, msg = "编号已存在，不能重复保存" }, "text/html");
                }
                //编号规则：字母数字-数字，例如A1-1，B10-12，表示A区第一行第一位，B区第10行第12位
                Regex rg = new Regex(@"^[A-Z]\d{1,2}-\d{1,2}$");
                if (!rg.IsMatch(number)) {
                    return Json(new { suc = false, msg = "编号不合规则，必须是字母数字-数字，例如A1-2" }, "text/html");
                }

                desk.number = number;
            }
            else {
                return Json(new { suc = false, msg = "编号的字数必须在4~25个字符之内，保存失败" }, "text/html");
            }
            if (name.Length <= 25) {
                desk.name = name;
            }
            else {
                return Json(new { suc = false, msg = "名称的字数必须在25个字符之内，保存失败" }, "text/html");
            }
            if (Int32.TryParse(seat_qty, out seatQtyD)) {
                desk.seat_qty = seatQtyD;
            }
            else {
                return Json(new { suc = false, msg = "座位数不合法，保存失败" }, "text/html");
            }
            desk.open_weekday = open_weekday.Contains("每天") ? "每天" : open_weekday;
            desk.open_time = open_time.Equals("全天") ? "全天" : open_time;
            desk.is_cancel = is_cancel.Equals("作废") ? true : false;
            desk.last_update_time = DateTime.Now;
            if (idInt == 0) {
                desk.create_user = userInfo.id;
                desk.create_time = DateTime.Now;
                db.dn_desks.Add(desk);
            }
            
            db.SaveChanges();

            WriteEventLog("台桌管理", idInt == 0 ? "新台桌：" : "编辑台桌：" + number);
            return Json(new { suc = true, msg = "保存成功" }, "text/html");

        }

        public JsonResult ToggleDeskStatus(int id)
        {
            try {
                var desk = db.dn_desks.Single(d => d.id == id);
                desk.is_cancel = !(bool)desk.is_cancel;
                db.SaveChanges();
                WriteEventLog("台桌管理", "成功切换状态：" + id);
                return Json(new SimpleResultModel() { suc = true, msg = "状态切换成功" });
            }
            catch (Exception ex) {
                WriteEventLog("台桌管理", "状态切换失败：" + id + ";reason:" + ex.Message, -100);
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        //加载所有台桌和已占用数量
        public ActionResult VisibleDesks(string orderNo)
        {
            dn_order order = db.dn_order.Single(o => o.order_no == orderNo);
            string mealPlace = "";
            if (order.is_delivery == true) {
                ViewBag.tip = "配送不需要选择台桌";
                return View("Error");
            }

            //获取预约时间间隔
            int intervalMinutes = 0;
            if (order.is_in_room == true) {
                intervalMinutes = int.Parse(GetParamSettingValue("IntervalMinutesRoomInt"));
                mealPlace = "包间";
            }
            else {
                intervalMinutes = int.Parse(GetParamSettingValue("IntervalMinutesHallInt"));
                mealPlace = "大堂";
            }

            DateTime minTime = ((DateTime)order.arrive_day).AddMinutes(-intervalMinutes);
            DateTime maxTime = ((DateTime)order.arrive_day).AddMinutes(intervalMinutes);

            string weekday = order.arrive_time.Substring(0, 2);  //表示周几
            string timeSeg = order.arrive_time.Substring(2);  //表示餐别

            var allDesks = (from d in db.dn_desks
                            where d.belong_to == mealPlace
                            select new VisualDeskModel()
                            {
                                number = d.number,
                                name = d.name,
                                belongTo = d.belong_to,
                                isCancel = d.is_cancel,
                                seatQty = d.seat_qty,
                                zone = d.number.Substring(0, 1),
                                seatHasTaken = (from o in db.dn_order
                                                where o.end_flag == 0
                                                && o.desk_num == d.number
                                                && o.arrive_day >= minTime
                                                && o.arrive_day <= maxTime
                                                select o.people_num).Sum(),
                                nowCanUse = (d.open_weekday == "每天" || d.open_weekday.Contains(weekday))
                                            && (d.open_time == "全天" || d.open_time.Contains(timeSeg))
                            }).ToList();
            var deskInfo = (from d in allDesks
                            orderby d.zone
                            group d by new
                            {
                                d.belongTo,
                                d.zone
                            } into dk
                            select new DeskInfoModel()
                            {
                                belongTo = dk.Key.belongTo,
                                zone = dk.Key.zone,
                                maxRow = dk.Max(k => int.Parse(k.number.Substring(1, k.number.IndexOf('-') - 1))), //最大行
                                maxCol = dk.Max(k => int.Parse(k.number.Substring(k.number.IndexOf('-') + 1))) //最大列
                            }).ToList();
            //allDesks.Skip(20).Take(10).ToList().ForEach(a => a.seatHasTaken = 3);
            //allDesks.Skip(40).Take(10).ToList().ForEach(a => a.seatHasTaken = 6);
            ViewData["desks"] = allDesks;
            ViewData["deskInfo"] = deskInfo;
            return View();
        }

        #endregion

        #region 参数设置
                
        [AuthorityFilter]
        public ActionResult ParamsSetting()
        {
            ViewData["addParamPower"] = HasGotPower("Params_setting_add") ? "1" : "";
            ViewData["removeParamPower"] = HasGotPower("Params_setting_remove") ? "1" : "";

            WriteEventLog("参数设置", "打开页面");
            return View();
        }

        public JsonResult GetParamsSetting()
        {
            var list = db.dn_items.ToList();
            var result = (from l in list
                          where ResPowers.Contains(l.res_no)
                          orderby l.res_no
                          select new
                          {
                              l.id,
                              l.name,
                              l.value,
                              l.res_no,
                              res_name=GetResNameByNo(l.res_no),
                              l.create_username,
                              create_date = ((DateTime)l.create_date).ToString("yyyy-MM-dd HH:mm"),
                              modify_date = ((DateTime)l.modify_date).ToString("yyyy-MM-dd HH:mm"),
                              l.comment
                          }).ToList();
            return Json(result);
        }

        public JsonResult SaveParamSetting(FormCollection fc)
        {
            string id = fc.Get("id");
            string name = fc.Get("name");
            string value = fc.Get("value");
            string res_no = fc.Get("res_no");
            string comment = fc.Get("comment");

            int idInt = 0;
            dn_items param;
            if (!int.TryParse(id, out idInt)) {
                param = new dn_items();
            }
            else {
                param = db.dn_items.Single(i => i.id == idInt);
            }
            if (idInt == 0 && db.dn_items.Where(i => i.name == name && i.res_no==res_no).Count() > 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "参数名已存在，不能重复，保存失败" }, "text/html");
            }
            //如果name的后缀有Int，表示value必须是Int类型
            if (name.EndsWith("Int")) {
                int tmp;
                if (!int.TryParse(value, out tmp)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "参数值必须是整数，保存失败" }, "text/html");
                }
            }
            else if (name.EndsWith("TimeSeg") && !"无".Equals(value)) {
                //表示时间段，格式：9:00~10:00
                value = value.Replace('：', ':');  //先将中文冒号替换成英文冒号，如有的话      
                var timeArr = value.Split('~');                
                
                if (timeArr.Count() != 2) {
                    return Json(new SimpleResultModel() { suc = false, msg = "参数值必须是时间段，例如（8:00~10:00），保存失败" }, "text/html");
                }

                //转换成日期验证 9-23修改
                DateTime time1, time2;
                if (!DateTime.TryParse("2016-9-23 " + timeArr[0],out time1)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "第一个时间格式不正确，保存失败" }, "text/html");
                }
                if (!DateTime.TryParse("2016-9-23 " + timeArr[1], out time2)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "第二个时间格式不正确，保存失败" }, "text/html");
                }
                if (time1 >= time2) {
                    return Json(new SimpleResultModel() { suc = false, msg = "第二个时间必须大于第一个时间，保存失败" }, "text/html");
                }

                //拆分字符串逐一验证 9-22加入
                //List<int> totalMinutesList = new List<int>(2); //用于比较两个时间的大小
                //foreach (var singleTime in timeArr) {
                //    var hourMinute = singleTime.Split(':');
                //    if (hourMinute.Count() != 2) {
                //        return Json(new SimpleResultModel() { suc = false, msg = errorMsg }, "text/html");
                //    }
                //    var hour = hourMinute[0];  //表示小时
                //    var minute = hourMinute[1];  //表示分钟
                //    int hourInt, minuteInt;
                //    if (!int.TryParse(hour, out hourInt)) {
                //        return Json(new SimpleResultModel() { suc = false, msg = errorMsg }, "text/html");
                //    }
                //    if (!int.TryParse(minute, out minuteInt)) {
                //        return Json(new SimpleResultModel() { suc = false, msg = errorMsg }, "text/html");
                //    }
                //    if (hourInt < 0 || hourInt > 23) {
                //        return Json(new SimpleResultModel() { suc = false, msg = errorMsg }, "text/html");
                //    }
                //    if (minuteInt < 0 || minuteInt > 59) {
                //        return Json(new SimpleResultModel() { suc = false, msg = errorMsg }, "text/html");
                //    }
                //    totalMinutesList.Add(hourInt * 60 + minuteInt);
                //}
                //if (totalMinutesList[0] >= totalMinutesList[1]) {
                //    return Json(new SimpleResultModel() { suc = false, msg = "第二个时间必须大于第一个时间，保存失败" }, "text/html");
                //}
            }

            param.name = name;
            param.value = value;
            param.res_no = res_no;
            param.comment = comment;
            param.modify_date = DateTime.Now;
            if (idInt == 0) {
                param.create_username = userInfo.realName;
                param.create_date = DateTime.Now;
                db.dn_items.Add(param);
            }
            db.SaveChanges();

            WriteEventLog("参数设置", "保存参数：name:" + name + ";value:" + value + ";res:" + res_no);
            return Json(new SimpleResultModel() { suc = true, msg = "保存成功" },"text/html");
        }

        public JsonResult RemoveParamSetting(int id)
        {
            var param = db.dn_items.Single(i => i.id == id);
            db.dn_items.Remove(param);
            db.SaveChanges();

            WriteEventLog("参数设置", "删除参数：id:" + id);
            return Json(new SimpleResultModel() { suc = true, msg = "删除成功" });
        }

        #endregion

        //获取一维码
        public void getCode(string code)
        {
            var bmp = ImageUtil.GetCode39(code,46);
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            Response.BinaryWrite(ms.ToArray());
        }

    }
}

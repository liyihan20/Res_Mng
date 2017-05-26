using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Models;
using RestaurantMng.Filters;
using RestaurantMng.Utils;

namespace RestaurantMng.Controllers
{
    public class OrderController : BaseController
    {
        //未完成订单列表
        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult CheckUnfinishedOrders()
        {
            ViewData["disable_power"] = HasGotPower("Unfinished_Order_op") ? "false" : "true";
            return View();
        }

        public JsonResult GetUnfinishedOrders(int page, int rows, string searchValue)
        {
            searchValue = searchValue ?? "";
            var orders = (from o in db.dn_order
                          where o.end_flag==0
                          && (o.user_name.Contains(searchValue)
                          || o.order_no.Contains(searchValue))
                          && (o.cancelled == false || o.cancelled == null)
                          && ResPowers.Contains(o.res_no)
                          orderby o.arrive_day
                          select o).ToList();
            int total = orders.Count();
            //包装一下
            var result = (from o in orders.Skip((page - 1) * rows).Take(rows)
                          select new
                          {
                              o.id,
                              o.order_no,
                              arrive_day = ((DateTime)o.arrive_day).ToString("yyyy-MM-dd HH:mm"),
                              create_time = ((DateTime)o.create_time).ToString("yyyy-MM-dd HH:mm"),
                              o.arrive_time,
                              o.book_card_num,
                              o.desk_num,
                              o.audit_comment,
                              is_delivery = o.is_delivery == true ? "是" : "否",
                              is_in_room = o.is_in_room == true ? "是" : "否",
                              o.payment_type,
                              o.people_num,
                              o.status,
                              o.total_price,
                              o.real_price,
                              o.user_name,
                              o.user_comment,
                              o.delivery_addr,
                              o.recipient,
                              o.recipient_phone,
                              o.order_phone,
                              o.benefit_info,
                              res_name = GetResNameByNo(o.res_no)
                          }).ToList();
            return Json(new { rows = result, total = total });
        }

        public JsonResult GetOrderDetail(int id)
        {
            var result = (from e in db.dn_orderEntry
                          where e.order_id == id
                          select new
                          {
                              type = e.dn_dishes.type,
                              name = e.dn_dishes.name,
                              number = e.dn_dishes.number,
                              qty = e.qty,
                              price = e.price,
                              total = e.qty * e.price,
                              benefit_info = e.benefit_info
                          }).ToList();
            return Json(result);
        }

        //开始审核
        public JsonResult BeginAudit(FormCollection fc)
        {
            int orderId = Int32.Parse(fc.Get("orderId"));
            string auditResult = fc.Get("auditResult");
            string auditOpinion = fc.Get("audit_comment");
            string deskNum = fc.Get("desk_num");
            string bookCardNo = fc.Get("book_card_num");
            string realPrice = fc.Get("real_price");

            bool pass = "on".Equals(auditResult);
            var order = db.dn_order.Single(o => o.id == orderId);

            if (!pass && string.IsNullOrEmpty(auditOpinion)) {
                return Json(new SimpleResultModel() { suc = false, msg = "NG必须填写审核意见" });
            }
            if (pass) {
                if (string.IsNullOrEmpty(deskNum) && order.res_no.Equals("HS")) {
                    return Json(new SimpleResultModel() { suc = false, msg = "台桌/房间号必须选择" });
                }
                if (string.IsNullOrEmpty(realPrice)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "实收金额必须填写" });
                }
                else if (decimal.Parse(realPrice) > order.total_price) {
                    return Json(new SimpleResultModel() { suc = false, msg = "实收金额不能大于应收金额" });
                }

            }
            if (order.cancelled == true) {
                return Json(new SimpleResultModel() { suc = false, msg = "此订单已被取消，请先刷新列表" });
            }
            if (!order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此预约状态不是【等待审核】，请刷新列表" });
            }

            order.audit_comment = auditOpinion;
            order.audit_result = pass;
            order.audit_time = DateTime.Now;
            order.auditor_name = userInfo.realName;
            order.auditor_no = userInfo.userName;
            if (pass) {
                order.book_card_num = bookCardNo;
                if (order.res_no.Equals("HS")) {
                    order.desk_num = deskNum;
                    order.desk_name = db.dn_desks.Single(d => d.number == deskNum).name;
                }
                order.real_price = decimal.Parse(realPrice);
                order.status = "等待打印";
            }
            else {
                order.status = "审核不通过";
                order.end_flag = -1;
            }

            if (!pass) {
                //如果NG了积分换购的，需要返还积分
                var pointsOrders = order.dn_orderEntry.Where(o => o.benefit_info != null && o.benefit_info.Contains("积分换购")).ToList();
                if (pointsOrders.Count() > 0) {
                    foreach (var po in pointsOrders) {
                        int points = Int32.Parse(po.benefit_info.Split(':')[1]);
                        if (!ReturnPointsIfCanceled(order.user_no, order.user_name, points, "审核不通过")) {
                            return Json(new SimpleResultModel() { suc = false, msg = "审核失败，因为积分返还失败" });
                        }
                    }
                }
                //如果是生日餐，要将领取记录作废
                var birthdayOrders = order.dn_orderEntry.Where(o => o.benefit_info != null && o.benefit_info.Contains("生日")).ToList();
                if (birthdayOrders.Count() > 0) {
                    foreach (var bo in birthdayOrders) {
                        string thisYear = DateTime.Now.Year.ToString();
                        var meals = db.dn_birthdayMealLog.Where(b => b.user_no == order.user_no && b.year == thisYear && b.is_cancelled != true);
                        if (meals.Count() > 0) {
                            var meal = meals.First();
                            meal.is_cancelled = true;
                        }
                    }
                }
            }
            db.SaveChanges();

            WriteEventLog("审核预约", "订单编号" + order.order_no + ";审核结果：" + (pass ? "OK" : "NG"));

            return Json(new SimpleResultModel() { suc = true, msg = "审核成功" }, "text/html");
        }

        /// <summary>
        /// 审核不通过，返还积分换购的积分        
        /// </summary>
        /// <param name="userNo">用户厂牌</param>
        /// <param name="userName">用户姓名</param>
        /// <param name="points">积分</param>
        /// <param name="msg">备注信息</param>
        /// <returns></returns>
        public bool ReturnPointsIfCanceled(string userNo,string userName, int points, string msg)
        {
            try {
                var up = db.dn_points.Single(p => p.user_no == userNo);
                up.points += points;

                dn_pointsRecord record = new dn_pointsRecord();
                record.income = points;
                record.info = "积分换购返回:" + msg;
                record.op_date = DateTime.Now;
                record.user_no = userNo;
                record.user_name = userName;
                db.dn_pointsRecord.Add(record);

                db.SaveChanges();
            }
            catch (Exception ex) {
                WriteEventLog("积分返还", "失败：" + ex.Message, -100);
                return false;
            }

            WriteEventLog("积分返还", "成功返回积分：" + points);
            return true;
        }

        //修改支付方式
        public JsonResult ChangePayType(int id)
        {
            var order = db.dn_order.Single(o => o.id == id);
            if (order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此预约还未审核，操作失败" });
            }
            order.payment_type = order.payment_type.Equals("现金") ? "饭卡" : "现金";
            db.SaveChanges();
            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //反审核
        public JsonResult unAuditOrder(int id)
        {
            var order = db.dn_order.Single(o => o.id == id);
            if (order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此预约还未审核，操作失败" });
            }
            order.auditor_no = "";
            order.auditor_name = "";
            order.audit_time = null;
            order.audit_result = null;
            order.audit_comment = "";
            order.desk_num = "";
            order.book_card_num = "";
            order.real_price = null;
            order.status = "等待审核";

            db.SaveChanges();
            WriteEventLog("反审核", "order-no:" + order.order_no);

            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        //现金支付
        public JsonResult payWithCash(int id)
        {
            var order = db.dn_order.Single(o => o.id == id);

            if (order.status.Equals("等待审核")) {
                return Json(new SimpleResultModel() { suc = false, msg = "此预约还未审核，操作失败" });
            }
            if (!order.payment_type.Equals("现金")) {
                return Json(new SimpleResultModel() { suc = false, msg = "支付方式不是【现金】，支付失败" });
            }
            order.status = "已完成";
            order.end_flag = 1;
            order.payment_time = DateTime.Now;

            //积分
            int pointMutiple = int.Parse(GetParamSettingValue("PointsMutipleInt"));
            if (order.real_price > 0) {
                int toAddedPoint = (int)Math.Floor((decimal)order.real_price) * pointMutiple;
                var points = db.dn_points.Where(p => p.user_no == order.user_no && p.res_no==order.res_no);
                dn_points point = null;
                if (points.Count() > 0) {
                    point = points.First();
                    point.points += toAddedPoint;
                }
                else {
                    point = new dn_points();
                    point.res_no = order.res_no;
                    point.user_no = order.user_no;
                    point.user_name = order.user_name;
                    point.points = toAddedPoint;
                    db.dn_points.Add(point);
                }
                db.dn_pointsRecord.Add(new dn_pointsRecord()
                {
                    res_no = order.res_no,
                    user_no = order.user_no,
                    user_name = order.user_name,
                    income = toAddedPoint,
                    op_date = DateTime.Now,
                    info = "订单完成送积分（单号：" + order.order_no + "）"
                });
            }
            db.SaveChanges();
            WriteEventLog("付款", "现金付款完成", 0, order.order_no);

            return Json(new SimpleResultModel() { suc = true, msg = "付款完成" });
        }

        //查看已完成订单列表
        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult CheckFinishedOrders()
        {
            var cookie = Request.Cookies.Get("dn_fo");
            string fromDate = "", toDate = "", searchValue = "";
            if (cookie != null) {
                searchValue = MyUtils.DecodeToUTF8(cookie.Values.Get("sv"));
                fromDate = cookie.Values.Get("fd");
                toDate = cookie.Values.Get("td");
            }
            ViewData["fromDate"] = string.IsNullOrEmpty(fromDate) ? DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd") : fromDate;
            ViewData["toDate"] = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewData["searchValue"] = searchValue;
            return View();
        }

        public JsonResult GetfinishedOrders(int page, int rows, string searchValue, string fromDate, string toDate)
        {
            var cookie = Request.Cookies.Get("dn_fo");
            if (cookie == null) cookie = new HttpCookie("dn_fo");
            cookie.Values.Set("sv", MyUtils.EncodeToUTF8(searchValue));
            cookie.Values.Set("fd", fromDate);
            cookie.Values.Set("td", toDate);
            cookie.Expires = DateTime.Now.AddDays(30);
            Response.AppendCookie(cookie);

            DateTime fromDateDT = DateTime.Parse(fromDate);
            DateTime toDateDT = DateTime.Parse(toDate).AddDays(1);

            var orders = (from o in db.dn_order
                          where o.end_flag != 0
                          && (o.user_name.Contains(searchValue)
                          || o.order_no.Contains(searchValue))
                          && o.arrive_day >= fromDateDT
                          && o.arrive_day <= toDateDT
                          && ResPowers.Contains(o.res_no)
                          orderby o.arrive_day
                          select o).ToList();
            int total = orders.Count();
            //包装一下
            var result = (from o in orders.Skip((page - 1) * rows).Take(rows)
                          select new
                          {
                              o.id,
                              o.order_no,
                              arrive_day = ((DateTime)o.arrive_day).ToString("yyyy-MM-dd HH:mm"),
                              create_time = ((DateTime)o.create_time).ToString("yyyy-MM-dd HH:mm"),
                              o.arrive_time,
                              o.book_card_num,
                              o.desk_num,
                              is_delivery = o.is_delivery == true ? "是" : "否",
                              is_in_room = o.is_in_room == true ? "是" : "否",
                              o.payment_type,
                              o.people_num,
                              o.status,
                              o.total_price,
                              o.real_price,
                              o.user_name,
                              o.user_comment,
                              o.delivery_addr,
                              o.recipient,
                              o.recipient_phone,
                              audit_result = o.audit_result == true ? "true" : "false",
                              o.audit_comment,
                              o.order_phone,
                              o.benefit_info,
                              res_no = GetResNameByNo(o.res_no)
                          }).ToList();
            return Json(new { rows = result, total = total });
        }

        //打印预约订单
        [SessionTimeOutFilter]
        public ActionResult PrintOrder(int id, int? numInAPage)
        {
            var order = db.dn_order.Single(o => o.id == id);
            if (order.status.Equals("等待审核")) {
                ViewBag.tip = "此预约还未审核，操作失败";
                return View("Tip");
            }
            if ("等待打印".Equals(order.status)) {
                order.status = "制作中";
                db.SaveChanges();
            }
            WriteEventLog("会所预约", "打印订单", 0, order.order_no);

            //保存每页打印行数到cookie
            var cookie = Request.Cookies["dn_numInAPage"];
            if (cookie == null) {
                cookie = new HttpCookie("dn_numInAPage");
                numInAPage = numInAPage ?? 4;
                cookie.Values.Add("number", numInAPage.ToString());
            }
            else {
                if (numInAPage == null) {
                    numInAPage = Int32.Parse(cookie.Values.Get("number"));
                }
                else {
                    cookie.Values.Set("number", numInAPage.ToString());
                }
            }
            cookie.Expires = DateTime.Now.AddYears(10);
            Response.AppendCookie(cookie);

            ViewData["order"] = order;
            ViewData["orderEntries"] = order.dn_orderEntry.ToList();
            ViewData["numInAPage"] = numInAPage;
            return View();
        }

        //配餐管理
        [SessionTimeOutFilter]
        [AuthorityFilter]
        public ActionResult DishArrange()
        {
            WriteEventLog("厨房制作", "进入配餐管理");
            return View();
        }

        //今日未制作菜式
        public JsonResult GetDishToDoToday()
        {
            DateTime today = DateTime.Parse(DateTime.Now.ToShortDateString());
            DateTime tomorrow = today.AddDays(1);
            var orders = (from o in db.dn_orderEntry
                          where (o.dn_order.status == "制作中"
                          || o.dn_order.status == "配餐中")
                          && o.dn_order.arrive_day >= today
                          && o.dn_order.arrive_day < tomorrow
                          && (o.is_done == null || o.is_done == false)
                          && o.dn_order.res_no == "HS"
                          select o).ToList();

            var result = (from o in orders
                          select new
                          {
                              o.id,
                              o.dn_order.people_num,
                              o.dn_order.order_no,
                              o.dn_dishes.type,
                              o.dn_dishes.name,
                              o.qty,
                              o.dn_order.user_name,
                              arrive_day = ((DateTime)o.dn_order.arrive_day).ToString("HH:mm")
                          }).OrderBy(o => o.arrive_day).ToList();
            //WriteEventLog("厨房制作", "刷新未制作页面,记录数：" + result.Count());
            return Json(result);
        }
        //今日已制作菜式
        public JsonResult GetDishDoneToday()
        {
            DateTime today = DateTime.Parse(DateTime.Now.ToShortDateString());
            DateTime tomorrow = today.AddDays(1);
            var orders = (from o in db.dn_orderEntry
                          where
                          o.is_done == true
                          && o.done_time >= today
                          && o.done_time < tomorrow
                          && o.dn_order.res_no == "HS"
                          select o).ToList();

            var result = (from o in orders
                          select new
                          {
                              o.id,
                              o.dn_order.people_num,
                              o.dn_order.order_no,
                              o.dn_dishes.type,
                              o.dn_dishes.name,
                              o.qty,
                              o.dn_order.user_name,
                              arrive_day = ((DateTime)o.dn_order.arrive_day).ToString("HH:mm"),
                              done_time = ((DateTime)o.done_time).ToString("HH:mm")
                          }).OrderByDescending(o => o.done_time).ToList();
            //WriteEventLog("厨房制作", "刷新已制作页面,记录数：" + result.Count());
            return Json(result);
        }
        //手动制作完成
        public JsonResult DishIsDone(int id)
        {
            try {
                var orderEntry = db.dn_orderEntry.Single(o => o.id == id);
                orderEntry.is_done = true;
                orderEntry.done_time = DateTime.Now;

                if (orderEntry.dn_order.status.Equals("制作中")) {
                    var order = orderEntry.dn_order;
                    order.status = "配餐中";
                }

                db.SaveChanges();
                WriteEventLog("厨房制作", "手动制作成功，id:" + id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败：" + ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }
        //扫码制作完成
        public JsonResult DishIsDoneByScanner(string code)
        {
            if (code.Split('-').Count() != 2) {
                return Json(new SimpleResultModel() { suc = false, msg = "条形码错误，请重新扫描" });
            }
            try {
                DateTime today = DateTime.Now;
                DateTime tomorrow = DateTime.Now.AddDays(1);
                string orderNo = code.Split('-')[0];
                int entryIndex = Int32.Parse(code.Split('-')[1]);
                dn_orderEntry orderEntry = null;
                try {
                    orderEntry = db.dn_orderEntry
                                .Where(o => o.dn_order.order_no == orderNo && o.dn_order.arrive_day >= today && o.dn_order.arrive_day < tomorrow)
                                .OrderBy(o => o.id).ToList()[entryIndex - 1];
                }
                catch {
                    return Json(new SimpleResultModel() { suc = false, msg = "此条形码不存在，请重新扫描" });
                }
                if (orderEntry.is_done == true) {
                    return Json(new SimpleResultModel() { suc = false, msg = "此条形码已被扫描，请不要重复操作" });
                }
                orderEntry.is_done = true;
                orderEntry.done_time = DateTime.Now;

                if (orderEntry.dn_order.status.Equals("制作中")) {
                    var order = orderEntry.dn_order;
                    order.status = "配餐中";
                }

                db.SaveChanges();
                WriteEventLog("厨房制作", "扫条形码制作成功，code:" + code);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败：" + ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }
        //返回制作
        public JsonResult DishReDone(int id)
        {
            try {
                var orderEntry = db.dn_orderEntry.Single(o => o.id == id);
                orderEntry.is_done = false;
                orderEntry.done_time = null;
                db.SaveChanges();
                WriteEventLog("厨房制作", "重新制作，id:" + id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败：" + ex.Message });
            }

            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }
    }
}

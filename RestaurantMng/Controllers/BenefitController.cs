using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Filters;
using RestaurantMng.Models;
using RestaurantMng.Utils;

namespace RestaurantMng.Controllers
{
    [SessionTimeOutFilter]
    public class BenefitController : BaseController
    {
        #region 积分查询

        public ActionResult CheckAllPoints()
        {
            return View();
        }

        public JsonResult GetAllUserPoints(int page, int rows, string searchValue = "")
        {
            var list = (from p in db.dn_points
                        where (p.user_name.Contains(searchValue)
                        || p.user_no.Contains(searchValue))
                        && ResPowers.Contains(p.res_no)
                        orderby p.points descending
                        select p).ToList();
            var total = list.Count();
            var result = (from l in list.Skip((page - 1) * rows).Take(rows)
                          select new
                          {
                              l.id,
                              l.user_no,
                              l.user_name,
                              l.points,
                              last_change = ((DateTime)(db.dn_pointsRecord.Where(p => p.user_no == l.user_no)
                              .OrderByDescending(p => p.id).First().op_date))
                              .ToString("yyyy-MM-dd HH:mm"),
                              l.res_no,
                              res_name=GetResNameByNo(l.res_no)
                          }).ToList();

            WriteEventLog("积分管理", "打开积分界面:" + searchValue);
            return Json(new { rows = result, total = total });
        }

        public JsonResult GetCertainUserPoint(string userNo,string resNo)
        {
            var list = (from p in db.dn_pointsRecord
                        where p.user_no == userNo
                        && p.res_no == resNo
                        orderby p.id descending
                        select p).ToList();
            var result = (from l in list
                          select new
                          {
                              income = l.income > 0 ? "+" + l.income.ToString() : l.income.ToString(),
                              l.info,
                              op_date = ((DateTime)l.op_date).ToString("yyyy-MM-dd HH:mm")
                          }).ToList();

            WriteEventLog("积分管理", "查看积分记录：" + userNo);
            return Json(result);
        }
        #endregion

        #region 积分换购

        public ActionResult CheckPointsForDish()
        {
            ViewData["disabled_power"] = HasGotPower("Points_For_Dish_op") ? "false" : "true";
            return View();
        }

        public JsonResult GetPointsForDish(string searchValue = "")
        {
            var list = (from p in db.dn_pointsForDish
                       where p.dn_dishes.name.Contains(searchValue)
                       && ResPowers.Contains(p.res_no)
                       select p).ToList();
            var result = (from l in list
                          orderby l.end_date descending
                          select new
                          {
                              id = l.id,
                              dishName = l.dn_dishes.name,
                              pointsNeed = l.points_need,
                              fromDate = ((DateTime)l.from_date).ToString("yyyy-MM-dd"),
                              endDate = ((DateTime)l.end_date).ToString("yyyy-MM-dd"),                              
                              createDate = ((DateTime)l.create_date).ToString("yyyy-MM-dd"),
                              isSelling = l.is_selling == true ? "在售" : "下架",
                              hasFullFill = l.has_fullfill,
                              resNo = l.res_no,
                              resName = GetResNameByNo(l.res_no)
                          }).ToList();
            return Json(result);
        }

        public JsonResult SavePointsForDish(FormCollection fc)
        {
            string id = fc.Get("id");
            string dishName = fc.Get("dishName");
            string points = fc.Get("pointsNeed");
            string fromDate = fc.Get("fromDate");
            string endDate = fc.Get("endDate");
            string isSelling = fc.Get("isSelling");
            string resNo = fc.Get("resNo");

            //赋值&验证
            dn_pointsForDish pfd;
            dn_dishes dish;
            int idInt = 0, pointsInt = 0;
            DateTime fromDateDt, endDateDt;

            try {
                dish = db.dn_dishes.Single(d => d.name == dishName && d.res_no == resNo);
            }
            catch  {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败，菜式不存在" }, "text/html");
            }
            if (!Int32.TryParse(points, out pointsInt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：积分不合法" }, "text/html");
            }
            if (!DateTime.TryParse(fromDate, out fromDateDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：有效起始日期不合法" }, "text/html");
            }
            if (!DateTime.TryParse(endDate, out endDateDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：有效结束日期不合法" }, "text/html");
            }
            if (fromDateDt > endDateDt) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：有效结束日期不能早于起始日期" }, "text/html");
            }
            if (Int32.TryParse(id, out idInt)) {
                pfd = db.dn_pointsForDish.Single(p => p.id == idInt);
            }
            else {
                pfd = new dn_pointsForDish();
            }

            pfd.res_no = resNo;
            pfd.dn_dishes = dish;
            pfd.from_date = fromDateDt;
            pfd.end_date = endDateDt.AddDays(1).AddMinutes(-1);
            pfd.points_need = pointsInt;
            pfd.is_selling = isSelling.Equals("在售") ? true : false;
            if (idInt == 0) {
                pfd.create_no = userInfo.userName;
                pfd.create_date = DateTime.Now;
                pfd.has_fullfill = 0;
                db.dn_pointsForDish.Add(pfd);
            }

            db.SaveChanges();
            WriteEventLog("积分换购", (idInt == 0 ? "新增" : "编辑") + "换购，菜式：" + dishName + ",积分：" + points);
            return Json(new SimpleResultModel() { suc = true, msg = "保存成功" }, "text/html");
        }

        public JsonResult TogglePointsForDish(int id)
        {
            dn_pointsForDish pfd;
            try {
                pfd = db.dn_pointsForDish.Single(p => p.id == id);
            }
            catch {
                return Json(new SimpleResultModel() { suc = false, msg = "操作失败" });
            }
            if (pfd.is_selling == false) {
                pfd.is_selling = true;
            }
            else {
                pfd.is_selling = false;
            }
            db.SaveChanges();

            return Json(new SimpleResultModel() { suc = true, msg = "操作成功" });
        }

        #endregion

        #region 折扣与满减

        public ActionResult CheckDiscount()
        {
            ViewData["disabled_power"] = HasGotPower("Discount_manage_op") ? "false" : "true";
            return View();
        }

        //获取所有折扣优惠
        public JsonResult GetDiscountList()
        {
            var list = (from d in db.dn_discountInfo
                        where ResPowers.Contains(d.res_no)
                        orderby d.end_date descending 
                        select d).ToList();
            var result = (from l in list
                          select new
                          {
                              l.id,
                              discountName = l.discount_name,
                              l.comment,
                              discountRate = l.discount_rate,
                              resumeBiggerThan = l.resume_bigger_than,
                              minusPrice = l.minus_price,
                              forEveryone = l.for_everyone == true ? "所有人" : "指定人",
                              fromDate = ((DateTime)l.from_date).ToString("yyyy-MM-dd"),
                              endDate = ((DateTime)l.end_date).ToString("yyyy-MM-dd"),
                              isOutOfDate = l.end_date < DateTime.Now ? "已过期" : "正常",
                              resName = GetResNameByNo(l.res_no),
                              resNo = l.res_no
                              //createDate = ((DateTime)l.create_date).ToString("yyyy-MM-dd")
                          }).ToList();
            return Json(result);
        }

        //获取折扣组内人员
        public JsonResult GetDiscountPeople(int discountId,string searchValue="")
        {
            var info = db.dn_discountInfo.Single(d => d.id == discountId);
            if (info.for_everyone == true) {
                return Json(new[] { new { userNo = "所有人", userName = "所有人", hasDiscountTime = info.has_benefit_times } });
            }
            var result = (from u in info.dn_discountInfoUsers
                          where (u.suit_user_name.Contains(searchValue) 
                          || u.suit_user_no.Contains(searchValue))
                          select new
                          {
                              userNo = u.suit_user_no,
                              userName = u.suit_user_name,
                              hasDiscountTime = u.has_discount_times
                          }).ToList();
            return Json(result);
        }

        //保存折扣优惠信息
        public JsonResult SaveDiscountInfo(FormCollection fc)
        {
            string id = fc.Get("id");
            string comment = fc.Get("comment");            
            string discountRate = fc.Get("discountRate");
            string resumeBiggerThan = fc.Get("resumeBiggerThan");
            string minusPrice = fc.Get("minusPrice");
            string forEveryone = fc.Get("forEveryone");
            string fromDate = fc.Get("fromDate");
            string endDate = fc.Get("endDate");
            string discountType = fc.Get("discountType");
            string resNo = fc.Get("resNo");

            decimal discountRateD = 0, resumeBiggerThanD = 0, minusPriceD = 0;
            DateTime fromDateDT, endDateDT;
            int idInt = 0;
            dn_discountInfo info;

            if (Int32.TryParse(id,out idInt)) {
                info = db.dn_discountInfo.Single(d => d.id == idInt);//修改
            }
            else {
                info = new dn_discountInfo(); //新增
                info.create_date = DateTime.Now;
                info.create_no = userInfo.userName;
                info.has_benefit_times = 0;
            }

            if ("打折".Equals(discountType)) {
                if (!decimal.TryParse(discountRate, out discountRateD)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "保存失败：折扣额错误" }, "text/html");
                }
                else {
                    info.discount_rate = discountRateD;
                    info.discount_name = discountRate + "折优惠";
                }
            }
            if ("满减".Equals(discountType)) {
                if (!decimal.TryParse(resumeBiggerThan, out resumeBiggerThanD)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "保存失败：满减值错误" }, "text/html");
                }
                else {
                    info.resume_bigger_than = resumeBiggerThanD;
                }
                if (!decimal.TryParse(minusPrice, out minusPriceD)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "保存失败：满减值错误" }, "text/html");
                }
                else {
                    info.minus_price = minusPriceD;
                }
                info.discount_name = string.Format("满{0}减{1}", resumeBiggerThan, minusPrice);
            }
            if (!DateTime.TryParse(fromDate, out fromDateDT)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：起始日期错误" }, "text/html");
            }
            if (!DateTime.TryParse(endDate, out endDateDT)) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：结束日期错误" }, "text/html");
            }
            if (fromDateDT > endDateDT) {
                return Json(new SimpleResultModel() { suc = false, msg = "保存失败：起始日期不能大于结束日期" }, "text/html");
            }
            info.from_date = fromDateDT;
            info.end_date = endDateDT.AddDays(1).AddMinutes(-1);
            info.comment = comment;
            info.for_everyone = "所有人".Equals(forEveryone) ? true : false;
            info.res_no = resNo;

            if (idInt == 0) {
                db.dn_discountInfo.Add(info);
            }
            db.SaveChanges();
            WriteEventLog("优惠管理", "修改/增加折扣：id:" + id);

            return Json(new SimpleResultModel() { suc = true, msg = "保存成功" }, "text/html");
        }

        //获取人事系统厂牌和姓名
        public JsonResult SearchHrEmp(string searchValue)
        {
            var list = db.SearchHREmp(searchValue).ToList();
            var result = (from l in list
                          orderby l.emp_no
                          select new
                          {
                              empNo = l.emp_no,
                              empName = l.emp_name
                          }).ToList();
            return Json(result);
        }

        //加入到折扣组
        public JsonResult AddIntoDiscountGroup(int discountId,string empNos, string empNames)
        {
            if (db.dn_discountInfo.Single(d => d.id == discountId).for_everyone==true) {
                return Json(new SimpleResultModel() { suc = false, msg = "此优惠适用于所有人，不用手动添加人员" });
            }

            var empNoArr = empNos.Split(',');
            var empNameArr = empNames.Split(',');
            string empNo, empName;
            int sucNum = 0;
            for (int i = 0; i < empNoArr.Count(); i++) {
                empNo = empNoArr[i];
                empName = empNameArr[i];
                if (!string.IsNullOrEmpty(empNo)) {
                    var exists = db.dn_discountInfoUsers.Where(d => d.discount_id == discountId && d.suit_user_no == empNo).Count() > 0;
                    if (exists) {
                        continue;
                    }
                    var diu = new dn_discountInfoUsers();
                    diu.suit_user_no = empNo;
                    diu.suit_user_name = empName;
                    diu.discount_id = discountId;
                    diu.has_discount_times = 0;
                    db.dn_discountInfoUsers.Add(diu);
                    sucNum++;
                }
            }            

            db.SaveChanges();

            WriteEventLog("折扣优惠", empNames + "加入到折扣组：" + discountId);
            return Json(new SimpleResultModel() { suc = true, msg = "成功加入人数：" + sucNum });
        }

        //从折扣组中移除
        public JsonResult RemoveFromDiscountGroup(int discountId, string empNos)
        {
            var empNoArr = empNos.Split(',');
            string empNo;
            int sucNum = 0;
            for (int i = 0; i < empNoArr.Count(); i++) {
                empNo = empNoArr[i];
                if (!string.IsNullOrEmpty(empNo)) {
                    var toRemove = db.dn_discountInfoUsers.Where(d => d.discount_id == discountId && d.suit_user_no == empNo).ToList();
                    if (toRemove.Count() == 0) {
                        continue;
                    }
                    db.dn_discountInfoUsers.Remove(toRemove.First());
                    sucNum++;
                }

            }
            db.SaveChanges();

            WriteEventLog("折扣优惠", empNos + "从折扣组移除：" + discountId);
            return Json(new SimpleResultModel() { suc = true, msg = "成功删除人数：" + sucNum });
        }

        #endregion
    }
}

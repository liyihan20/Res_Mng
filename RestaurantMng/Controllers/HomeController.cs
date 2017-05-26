using RestaurantMng.Models;
using RestaurantMng.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Filters;

namespace RestaurantMng.Controllers
{
    [SessionTimeOutFilter]
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            ViewBag.userName = userInfo.userName;
            ViewBag.realName = userInfo.realName;
            var resPowersName = (from r in db.dn_Restaurent
                                     where ResPowers.Contains(r.no)
                                     select r.name).ToArray();
            ViewBag.resPowersName = string.Join(",", resPowersName);
            return View();
        }

        public JsonResult GetMenuItems()
        {
            var powers = (from a in db.dn_authority
                          from u in db.dn_groups
                          from ga in a.dn_groupAuthority
                          from gu in u.dn_groupUser
                          where ga.group_id == u.id
                          && gu.user_id == userInfo.id
                          && (a.number * 100) % 10 == 0
                          orderby a.number
                          select
                          new MenuItemModel
                          {
                              name = a.name,
                              number = a.number,
                              action_name = a.action_name,
                              controller_name = a.controler_name,
                              iconcls = a.iconcls
                          }).Distinct().ToList();
            return Json(powers);
        }

        //修改密码
        public JsonResult ChangePassword(FormCollection fcl)
        {
            string oldPassword = fcl.Get("oldPass");
            string newPassword = fcl.Get("newPass");

            //验证旧密码是否正确，正确的话则更新密码
            try
            {
                var user = db.dn_users.Single(u => u.id == userInfo.id);
                if (!user.password.Equals(MyUtils.getMD5(oldPassword)))
                {
                    WriteEventLog("修改密码", "修改失败：旧密码不正确", 1);
                    return Json(new { success = false, msg = "旧密码不正确" }, "text/html");
                }
                user.password = MyUtils.getMD5(newPassword);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("修改密码", "修改失败：" + ex.Message, 1);
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }
            WriteEventLog("修改密码", "修改成功");
            return Json(new { success = true }, "text/html");
        }

        public ActionResult Main()
        {
            return View();
        }

        public ActionResult NoPowerToVisit(string controlerName, string actionName)
        {
            WriteEventLog("不能访问", controlerName + "/" + actionName, 1000);
            return View();
        }

        public ActionResult Tip(string info)
        {
            ViewBag.tip = info;
            return View();
        }

    }
}

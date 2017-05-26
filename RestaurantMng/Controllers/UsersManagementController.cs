using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RestaurantMng.Models;
using RestaurantMng.Utils;
using RestaurantMng.Filters;
using System.Threading;

namespace RestaurantMng.Controllers
{
    [SessionTimeOutFilter]
    public class UsersManagementController : BaseController
    {
        #region 用户管理
        [AuthorityFilter]
        public ActionResult Users()
        {
            return View();
        }

        //获取用户
        public JsonResult GetUsers(int page, int rows, string searchValue)
        {
            if (string.IsNullOrEmpty(searchValue)) searchValue = "";

            var users = (from u in db.dn_users
                        where u.user_name.Contains(searchValue)
                        || u.real_name.Contains(searchValue)
                        || u.email.Contains(searchValue)
                        || u.role.Contains(searchValue)
                        orderby u.id
                        select u).ToList();

            var userList = (from u in users.Skip((page - 1) * rows).Take(rows)
                            select new
                            {
                                id = u.id,
                                user_name = u.user_name,
                                real_name = u.real_name,
                                role = u.role,
                                email = u.email,
                                is_forbit = u.is_forbit == true ? "Y" : "N",
                                register_date = ((DateTime)u.register_date).ToShortDateString(),
                                last_login_date = u.last_login_date == null ? "" : ((DateTime)u.last_login_date).ToShortDateString(),
                            }).ToList();

            int total = users.Count();
            WriteEventLog("用户管理", "获取用户列表");

            return Json(new { rows = userList, total = total });

        }

        //保存用户
        public JsonResult SaveUser(FormCollection fc)
        {
            string userName = fc.Get("user_name").Trim();
            string realName = fc.Get("real_name").Trim();
            string email = fc.Get("email").Trim();
            string role = fc.Get("role");
            string password = fc.Get("password");

            if (db.dn_users.Where(u => u.user_name == userName).Count() > 0)
            {
                return Json(new { success = false, msg = "该用户已存在，新增失败" }, "text/html");
            }
            if (string.IsNullOrEmpty(password)) {
                password = MyUtils.getMD5("000000");
            }
            db.dn_users.Add(new dn_users()
            {
                user_name = userName,
                real_name = realName,
                role = role,
                email = email,
                password = password,
                register_date = DateTime.Now,
                is_forbit = false
            });

            db.SaveChanges();

            WriteEventLog("用户管理", "新增一个用户:" + realName);
            return Json(new { success = true }, "text/html");
        }

        //更新用户
        public JsonResult UpdateUser(int id, FormCollection fc)
        {

            string userName = fc.Get("user_name").Trim();
            string realName = fc.Get("real_name").Trim();
            string email = fc.Get("email").Trim();
            string role = fc.Get("role");

            try
            {
                var user = db.dn_users.Single(u => u.id == id);
                user.user_name = userName;
                user.real_name = realName;
                user.email = email;
                user.role = role;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }

            WriteEventLog("用户管理", "更新用户,id:" + id);

            return Json(new { success = true }, "text/html");
        }

        //切换禁用标志,激活用户
        public JsonResult ToggleForbitFlag(int id)
        {

            try
            {
                var user = db.dn_users.Single(u => u.id == id);
                if (user.is_forbit == true)
                {
                    user.last_login_date = DateTime.Now;
                }
                user.is_forbit = !user.is_forbit;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }

            WriteEventLog("用户管理", "禁用/反禁用用户：" + id);

            return Json(new { success = true, msg = "操作成功" });
        }

        //重置密码
        public JsonResult ResetPassword(int id, string newPassword)
        {
            try
            {
                var user = db.dn_users.Single(u => u.id == id);
                user.password = MyUtils.getMD5(newPassword);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }

            WriteEventLog("用户管理", "重置用户密码：" + id);

            return Json(new { success = true });
        }

        //从员工个人信息查询系统导入
        public JsonResult GetUserFromEmp(string userNo)
        {
            var empUsers = db.ei_users.Where(u => u.card_number == userNo).ToList();
            if (empUsers.Count() == 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "员工信息查询系统不存在此厂牌，请手动录入" });
            }
            var empUser = empUsers.First();
            var extra = new { userName = empUser.name, email = empUser.email, password = empUser.password };

            return Json(new SimpleResultModel() { suc = true, extra = extra });
        }

        #endregion

        #region 权限管理
        [AuthorityFilter()]
        public ActionResult Authorities()
        {

            return View();
        }

        public JsonResult GetAuthorities(string searchValue)
        {

            var aut = from a in db.dn_authority
                      select new
                      {
                          id = a.id,
                          number = a.number,
                          name = a.name,
                          en_name = a.en_name,
                          controler_name = a.controler_name,
                          action_name = a.action_name,
                          iconcls = a.iconcls,
                          description = a.description
                      };

            if (!string.IsNullOrEmpty(searchValue))
            {
                aut = aut.Where(a => a.name.Contains(searchValue) || a.en_name.Contains(searchValue) || a.description.Contains(searchValue) || a.controler_name.Contains(searchValue) || a.action_name.Contains(searchValue));
            }

            WriteEventLog("权限管理", "获取权限列表");

            return Json(aut.OrderBy(a => a.number));
        }

        public JsonResult SaveAuthority(FormCollection fc)
        {
            string number = fc.Get("number").Trim();
            string name = fc.Get("name").Trim();
            string nameEn = fc.Get("en_name").Trim();
            string controlerName = fc.Get("controler_name").Trim();
            string actionName = fc.Get("action_name").Trim();
            string iconcls = fc.Get("iconcls").Trim();
            string description = fc.Get("description").Trim();

            if (db.dn_authority.Where(a => a.name == name || a.en_name == nameEn).Count() > 0)
            {
                WriteEventLog("权限管理", "该权限已存在，新增失败", -10);
                return Json(new { success = false, msg = "该权限已存在，新增失败" }, "text/html");
            }

            try
            {
                db.dn_authority.Add(new dn_authority()
                {
                    number = decimal.Parse(number),
                    name = name,
                    en_name = nameEn,
                    controler_name = controlerName,
                    action_name = actionName,
                    iconcls = iconcls,
                    description = description
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }

            WriteEventLog("权限管理", "新增权限：" + number + ";" + name);

            return Json(new { success = true }, "text/html");
        }

        public JsonResult UpdateAuthority(int id, FormCollection fc)
        {
            string number = fc.Get("number").Trim();
            string name = fc.Get("name").Trim();
            string nameEn = fc.Get("en_name").Trim();
            string controlerName = fc.Get("controler_name").Trim();
            string actionName = fc.Get("action_name").Trim();
            string iconcls = fc.Get("iconcls").Trim();
            string description = fc.Get("description").Trim();

            try
            {
                var aut = db.dn_authority.Single(a => a.id == id);
                aut.number = decimal.Parse(number);
                aut.name = name;
                aut.en_name = nameEn;
                aut.controler_name = controlerName;
                aut.action_name = actionName;
                aut.iconcls = iconcls;
                aut.description = description;

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("权限管理", "保存失败：" + ex.Message, -10);
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }

            WriteEventLog("权限管理", "更新权限：" + id);

            return Json(new { success = true }, "text/html");
        }

        public JsonResult RemoveAuthority(int id)
        {

            if (db.dn_groupAuthority.Where(g => g.authority_id == id).Count() > 0)
            {
                return Json(new { success = false, msg = "权限存在于分组中，不能删除" });
            }

            try
            {
                var aut = db.dn_authority.Single(a => a.id == id);
                db.dn_authority.Remove(aut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("权限管理", "删除失败：" + ex.Message, -10);
                return Json(new { success = false, msg = ex.Message });
            }

            WriteEventLog("权限管理", "删除权限" + id);

            return Json(new { success = true });
        }
        #endregion

        #region 组管理
        [AuthorityFilter()]
        public ActionResult Groups()
        {

            return View();
        }

        public JsonResult GetGroups(string search_group)
        {
            var groups = from g in db.dn_groups
                         select new
                         {
                             id = g.id,
                             name = g.name,
                             description = g.description
                         };

            if (!string.IsNullOrEmpty(search_group))
            {
                groups = groups.Where(g => g.name.Contains(search_group) || g.description.Contains(search_group));
            }

            WriteEventLog("组别管理", "获取组别列表");

            return Json(groups);
        }

        public JsonResult SaveGroup(FormCollection fc)
        {

            string name = fc.Get("name").Trim();
            string description = fc.Get("description").Trim();

            if (db.dn_groups.Where(a => a.name == name).Count() > 0)
            {
                return Json(new { success = false, msg = "该组别已存在，新增失败" }, "text/html");
            }

            try
            {
                db.dn_groups.Add(new dn_groups()
                {
                    name = name,
                    description = description
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                WriteEventLog("组别管理", "新增失败：" + ex.Message, -10);
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }

            WriteEventLog("组别管理", "新增组别:" + name);

            return Json(new { success = true }, "text/html");
        }

        public JsonResult UpdateGroup(int id, FormCollection fc)
        {
            string name = fc.Get("name").Trim();
            string description = fc.Get("description").Trim();

            try
            {
                var group = db.dn_groups.Single(a => a.id == id);
                group.name = name;
                group.description = description;

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message }, "text/html");
            }

            WriteEventLog("组别管理", "更新组别:" + id);

            return Json(new { success = true }, "text/html");
        }

        public JsonResult RemoveGroup(int id)
        {

            if (db.dn_groupAuthority.Where(g => g.group_id == id).Count() > 0)
            {
                return Json(new { success = false, msg = "分组存在权限，不能删除" });
            }

            if (db.dn_groupUser.Where(g => g.group_id == id).Count() > 0)
            {
                return Json(new { success = false, msg = "分组存在用户，不能删除" });
            }

            try
            {
                var group = db.dn_groups.Single(a => a.id == id);
                db.dn_groups.Remove(group);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }

            WriteEventLog("组别管理", "删除组别:" + id);

            return Json(new { success = true });
        }

        //组内用户管理
        public JsonResult GetGroupUsers(int group_id, string search_groupUser)
        {
            var users = from u in db.dn_groupUser
                        where u.group_id == group_id
                        select new
                        {
                            group_user_id = u.id,
                            user_name = u.dn_users.user_name,
                            real_name = u.dn_users.real_name
                        };
            if (!string.IsNullOrEmpty(search_groupUser))
            {
                users = users.Where(u => u.user_name.Contains(search_groupUser) || u.real_name.Contains(search_groupUser));
            }

            return Json(users);
        }

        public JsonResult SaveGroupUser(int group_id, int user_id)
        {

            if (db.dn_groupUser.Where(gu => gu.group_id == group_id && gu.user_id == user_id).Count() > 0)
            {
                return Json(new { success = false, msg = "分组已存在此用户，不能重复加入" });
            }

            try
            {
                db.dn_groupUser.Add(new dn_groupUser()
                {
                    group_id = group_id,
                    user_id = user_id
                });

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "操作失败:" + ex.Message });
            }

            WriteEventLog("组别管理", "组内加入用户，组别:" + group_id + ";用户:" + user_id);
            return Json(new { success = true, msg = "成功加入用户" });

        }

        public JsonResult removeGroupUser(int group_user_id)
        {

            try
            {
                var groupUser = db.dn_groupUser.Single(gu => gu.id == group_user_id);
                db.dn_groupUser.Remove(groupUser);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "操作失败:" + ex.Message });
            }
            WriteEventLog("分组管理", "移除组内用户：" + group_user_id);
            return Json(new { success = true, msg = "成功移除用户" });
        }

        //组内权限管理
        public JsonResult GetGroupAuts(int group_id, string search_groupAut)
        {
            var auts = from u in db.dn_groupAuthority
                       where u.group_id == group_id
                       orderby u.dn_authority.number
                       select new
                       {
                           group_aut_id = u.id,
                           name = u.dn_authority.name,
                           description = u.dn_authority.description
                       };
            if (!string.IsNullOrEmpty(search_groupAut))
            {
                auts = auts.Where(u => u.name.Contains(search_groupAut) || u.description.Contains(search_groupAut));
            }

            return Json(auts);
        }

        public JsonResult SaveGroupAut(int group_id, int aut_id)
        {

            if (db.dn_groupAuthority.Where(gu => gu.group_id == group_id && gu.authority_id == aut_id).Count() > 0)
            {
                return Json(new { success = false, msg = "分组已存在此权限，不能重复加入" });
            }

            try
            {
                db.dn_groupAuthority.Add(new dn_groupAuthority()
                {
                    group_id = group_id,
                    authority_id = aut_id
                });

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "操作失败:" + ex.Message });
            }

            WriteEventLog("组别管理", "组内加入权限，组别:" + group_id + ";权限:" + aut_id);

            return Json(new { success = true, msg = "成功加入权限" });

        }

        public JsonResult removeGroupAut(int group_aut_id)
        {

            try
            {
                var groupAut = db.dn_groupAuthority.Single(gu => gu.id == group_aut_id);
                db.dn_groupAuthority.Remove(groupAut);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "操作失败:" + ex.Message });
            }

            WriteEventLog("分组管理", "移除组内权限：" + group_aut_id);

            return Json(new { success = true, msg = "成功移除权限" });
        }
        #endregion

        #region 人事部门测试

        public ActionResult DepAndEmp()
        {
            return View();
        }

        //获取部门树，动态一级一级根据部门id获取
        public JsonResult GetDepartment(int id = 0)
        {
            var result = (from d in db.vw_getHRDeps
                          where d.parent_id == id
                          select new DepTreeModel()
                          {
                              id = d.id,
                              text = d.short_name,
                              state = d.is_leaf == 1 ? "open" : "closed",
                              iconCls = d.is_leaf == 1 ? "icon-home" : ""
                          }).ToList();

            return Json(result);
        }

        //获取特定部门的人
        public JsonResult GetDepEmps(int depId)
        {
            var result = (from u in db.vw_getHREmps
                          join d in db.vw_getHRDeps on u.dept_id equals d.id
                          where u.dept_id == depId
                          select new
                          {
                              empNo = u.emp_no,
                              empName = u.emp_name,
                              idCode = u.id_code,
                              depName=d.long_name,
                              job = u.job,
                              jobType = u.job_type,
                              sex = u.sex
                          }).ToList();
            return Json(result);
        }

        //搜索部门树，动态树由于时间差问题，不能逐一打开文件夹
        public JsonResult SearchDepsByName(string depName)
        {
            var depIds = db.vw_getHRDeps.Where(d => d.is_leaf == 1 && d.short_name.Contains(depName)).Select(d => d.id).ToList();
            if (depIds.Count() == 0) {
                return Json(new SimpleResultModel() { suc = false });
            }
            List<string> result = new List<string>();

            foreach (int depId in depIds) {
                Stack<int?> parentStack = new Stack<int?>();
                int? parentId = -1;
                int currentDepId = depId;
                while (parentId != 0) {
                    parentId = db.vw_getHRDeps.Single(d => d.id == currentDepId).parent_id;
                    if (parentId == null || parentId == 0) {
                        break;
                    }
                    currentDepId = (int)parentId;
                    parentStack.Push(parentId);
                }
                result.Add(String.Join(",", parentStack.ToArray()));
            }
            return Json(new SimpleResultModel() { suc = true, extra = result });
        }

        //public JsonResult SearchDepsByName(string depName)
        //{
        //    var depIds = db.vw_getHRDeps.Where(d => d.is_leaf == 1 && d.short_name.Contains(depName)).Select(d => d.id).ToList();
        //    if (depIds.Count() == 0) {
        //        return Json(new SimpleResultModel() { suc = false });
        //    }
        //    List<string> result = new List<string>();

        //    foreach (int depId in depIds) {
        //        Stack<string> parentStack = new Stack<string>();
        //        int? parentId = -1;
        //        int currentDepId = depId;
        //        while (parentId != 0) {
        //            var depModel=db.vw_getHRDeps.Single(d => d.id == currentDepId);
        //            parentId = depModel.parent_id;                    
        //            currentDepId = (int)parentId;
        //            parentStack.Push(depModel.short_name);
        //        }
        //        result.Add(String.Join("/", parentStack.ToArray()));
        //    }
        //    return Json(new SimpleResultModel() { suc = true, extra = result });
        //}
        #endregion

    }
}

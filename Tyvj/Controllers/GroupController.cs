﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Tyvj.DataModels;
using Tyvj.ViewModels;

namespace Tyvj.Controllers
{
    public class GroupController : BaseController
    {
        // GET: Group
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Show(int id)
        {
            var group = DbContext.Groups.Find(id);
            var _contests = (from gc in DbContext.GroupContest
                             where gc.GroupID == id
                             orderby gc.Contest.End descending
                             select gc.Contest).Take(5).ToList();
            var contests = new List<vContest>();
            foreach (var c in _contests)
                contests.Add(new vContest(c));
            ViewBag.Contests = contests;
            return View(group);
        }

        public ActionResult Ratify(int id)
        {
            var group = DbContext.Groups.Find(id);
            var _ratify = (from r in DbContext.GroupJoins
                           where r.GroupID == id
                           orderby r.ID descending
                           select r).ToList();
            var ratify = new List<vGroupJoin>();
            foreach (var r in _ratify)
                ratify.Add(new vGroupJoin(r));
            ViewBag.GroupJoin = ratify;
            return View(group);
        }

        public ActionResult Member(int id)
        {
            var group = DbContext.Groups.Find(id);
            return View(group);
        }

        [Authorize]
        public ActionResult Settings(int id)
        {
            var group = DbContext.Groups.Find(id);
            if (!IsMaster() && CurrentUser.ID != group.UserID)
                return Message("对不起，你所在的用户组没有操作权限。");
            return View(group);
        }

        [HttpGet]
        public ActionResult GetGroups(int? Page)
        {
            if (Page == null) Page = 0;
            var _groups = (from g in DbContext.Groups
                            orderby g.ID descending
                            select g).Skip(10 * Page.Value).Take(10).ToList();
            var groups = new List<vGroup>();
            foreach (var g in _groups)
                groups.Add(new vGroup(g));
            return Json(groups, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetMembers(int? Page, int GroupID)
        {
            if (Page == null) Page = 0;
            var _members = (from m in DbContext.GroupMembers
                              where m.GroupID == GroupID
                              orderby m.ID descending
                              select m).Skip(10 * Page.Value).Take(10).ToList();
            var members = new List<vGroupMember>();
            foreach (var m in _members)
                members.Add(new vGroupMember(m));
            return Json(members, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        public ActionResult Join(int id)
        {
            var group = DbContext.Groups.Find(id);
            if(group.JoinMethod == GroupJoinMethod.Everyone)
            {
                DbContext.GroupMembers.Add(new GroupMember
                { 
                    UserID = CurrentUser.ID,
                    GroupID = id
                });
                DbContext.SaveChanges();
                return RedirectToAction("Group", "Show", new { id = id });
            }
            else if(group.JoinMethod == GroupJoinMethod.Nobody)
            {
                return Message("该团队不允许任何人加入。");
            }
            else
            {
                return View();
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Join(int id, string Content)
        {
            var group = DbContext.Groups.Find(id);
            if (group.JoinMethod == GroupJoinMethod.Ratify)
            {
                DbContext.GroupJoins.Add(new GroupJoin 
                { 
                    GroupID = id,
                    Content = Content,
                    UserID = CurrentUser.ID,
                    Time = DateTime.Now
                });
                DbContext.SaveChanges(); 
            }
            return Message("您已成功提交加入团队申请，请等候团队创始人审核");
        }
        
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Accept(int id)
        {
            var gj = DbContext.GroupJoins.Find(id);
            var group = DbContext.Groups.Find(gj.GroupID);
            if (IsMaster() || CurrentUser.ID == group.UserID)
            {
                DbContext.GroupMembers.Add(new GroupMember
                {
                    UserID = CurrentUser.ID,
                    GroupID = id
                });
                DbContext.GroupJoins.Remove(gj);
                DbContext.SaveChanges();
                return RedirectToAction("Group", "Ratify", new { id = group.ID });
            }
            return Message("对不起，你所在的用户组没有操作权限。");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Decline(int id)
        {
            var gj = DbContext.GroupJoins.Find(id);
            var group = DbContext.Groups.Find(gj.GroupID);
            if (IsMaster() || CurrentUser.ID == group.UserID)
            {
                DbContext.GroupJoins.Remove(gj);
                DbContext.SaveChanges();
                return RedirectToAction("Group", "Ratify", new { id = group.ID });
            }
            return Message("对不起，你所在的用户组没有操作权限。");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddContest(int ContestID, int id)
        {
            var group = DbContext.Groups.Find(id);
            var contest = DbContext.Contests.Find(ContestID);
            DbContext.GroupContest.Add(new GroupContest 
            { 
                ContestID = ContestID,
                GroupID = id
            });
            DbContext.SaveChanges();
            return RedirectToAction("Group", "Show", new { id = id });
        }

        [Authorize]
        public ActionResult AddContest(int id)
        {
            var group = DbContext.Groups.Find(id);
            if (!IsMaster() && CurrentUser.ID != group.UserID)
                return Message("对不起，你所在的用户组没有操作权限。");
            return View(group);
        }

        [HttpGet]
        public ActionResult GetGroupContests(int? Page, int id)
        {
            if (Page == null) Page = 0;
            IEnumerable<Contest> _contests = (from c in DbContext.GroupContest
                                              where c.GroupID == id
                                              && !(DateTime.Now >= c.Contest.Begin && DateTime.Now < c.Contest.End)
                                              select c.Contest);
            _contests = _contests.OrderByDescending(x => x.End).Skip(10 * Page.Value).Take(10).ToList();
            var contests = new List<vContest>();
            foreach (var c in _contests)
                contests.Add(new vContest(c));
            return Json(contests, JsonRequestBehavior.AllowGet);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create()
        {
            var group = new Group { 
                Title = CurrentUser + "的团队",
                Description = "",
                JoinMethod = GroupJoinMethod.Everyone,
                Gravatar = CurrentUser.Gravatar,
                UserID = CurrentUser.ID
            };
            DbContext.Groups.Add(group);
            DbContext.SaveChanges();
            return RedirectToAction("Settings", "Group", new { id = group.ID });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(int id, string Title, string Description, int JoinMethod, string Gravatar)
        {
            var group = DbContext.Groups.Find(id);
            if (!IsMaster() && CurrentUser.ID != group.UserID)
                return Message("对不起，你所在的用户组没有操作权限。");
            group.Title = Title;
            group.Description = Description;
            group.JoinMethodAsInt = JoinMethod;
            group.Gravatar = Gravatar;
            DbContext.SaveChanges();
            return RedirectToAction("Settings", "Group", id);
        }
    }
}
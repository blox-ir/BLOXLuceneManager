using Karan.Data.Repository;
using Karan.IR.EntityObject;
using Karan.IR.Service;
using Karan.Utilities.Enums;
using Karan.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Karan.Helpers;
using Karan.IR.Lucene.Engine;

namespace Karan.Controllers
{
    public class LuceneController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index(string rbLiceneType, string luceneEntity)
        {
            switch (rbLiceneType)
            {
                case "Delete":
                    ViewBag.Result = deleteIndex(luceneEntity);
                    break;
                case "Generate":
                    ViewBag.Result = reGenerateIndex(luceneEntity);
                    break;
                case "Count":
                    ViewBag.Result = getCountIndex(luceneEntity);
                    break;
                case "Sample":
                    List<LuceneSearchEO> list = getSampleIndex(luceneEntity);
                    ViewBag.Result = list.Count;
                    ViewBag.ResultList = list;
                    break;
                default:
                    break;
            }
            return View();
        }
        private List<LuceneSearchEO> getSampleIndex(string luceneEntity)
        {
            LuceneSearchType type = (LuceneSearchType)Enum.Parse(typeof(LuceneSearchType), luceneEntity);
            switch (type)
            {
                case LuceneSearchType.JobRequest:
                    List<LuceneSearchEO> JobRequestlist = LuceneSearchService.GetAllIndexRecords(LuceneSearchType.JobRequest).Take(10).ToList();
                    return JobRequestlist;
                case LuceneSearchType.JobTitle:
                    List<LuceneSearchEO> JobTitlelist = LuceneSearchService.GetAllIndexRecords(LuceneSearchType.JobTitle).Take(10).ToList();
                    return JobTitlelist;
                default:
                    break;
            }
            return new List<LuceneSearchEO>();
        }
        private int getCountIndex(string luceneEntity)
        {
            LuceneSearchType type = (LuceneSearchType)Enum.Parse(typeof(LuceneSearchType), luceneEntity);
            switch (type)
            {
                case LuceneSearchType.JobRequest:
                    return LuceneSearchService.GetAllIndexRecords(LuceneSearchType.JobRequest).ToList().Count;
                case LuceneSearchType.JobTitle:
                    return LuceneSearchService.GetAllIndexRecords(LuceneSearchType.JobTitle).ToList().Count;
                default:
                    return -1;
            }
        }
        private int reGenerateIndex(string luceneEntity)
        {
            IUnitOfWork UnitOfWork = new UnitOfWork();
            LuceneSearchType type = (LuceneSearchType)Enum.Parse(typeof(LuceneSearchType), luceneEntity);
            int indexCount = 0;
            switch (type)
            {
                case LuceneSearchType.JobRequest:
                    List<LuceneSearchEO> jobRequestList = UnitOfWork.RequestRepository.GetSync(a => a.IsActive == true && a.IsApproved == true)
                                            .Select(
                                            a => new LuceneSearchEO
                                            {
                                                Id = a.KRequestId,
                                                Title = a.RequestTitle.ApplyRulesToUserInputText().ApplyCorrectYeKe().Replace("استخدام", string.Empty),
                                                Content = a.RequestBody.ApplyRulesToUserInputText().ApplyCorrectYeKe()
                                            }
                                            ).DistinctBy(a => a.Title).ToList();
                    indexCount = jobRequestList.Count;
                    LuceneSearchService.AddUpdateLuceneIndex(LuceneSearchType.JobRequest, jobRequestList);
                    KJobTitleService jobTitleService = new KJobTitleService();
                    List<LuceneSearchEO> jobTitleList = jobTitleService.GetSync(true)
                                            .Select(
                                            a => new LuceneSearchEO
                                            {
                                                Id = a.ID,
                                                Title = a.Name.ApplyRulesToUserInputText().ApplyCorrectYeKe(),
                                                Content = a.Description.ApplyRulesToUserInputText().ApplyCorrectYeKe()
                                            }
                                            ).DistinctBy(a => a.Title).ToList();
                    indexCount = jobTitleList.Count;
                    LuceneSearchService.AddUpdateLuceneIndex(LuceneSearchType.JobTitle, jobTitleList);
                    break;
                case LuceneSearchType.JobTitle:
                    break;
                default:
                    break;
            }
            return indexCount;
        }
        private bool deleteIndex(string luceneEntity)
        {
            try
            {
                string path = Request.PhysicalApplicationPath + "\\Lucene_Index\\" + luceneEntity + "_index";
                DirectoryInfo di = new DirectoryInfo(path);
                di.Delete(true);
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }
    }
}
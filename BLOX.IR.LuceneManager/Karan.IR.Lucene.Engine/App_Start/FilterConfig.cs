using System.Web;
using System.Web.Mvc;

namespace Karan.IR.Lucene.Engine
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
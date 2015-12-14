using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CAT.Azure.Controllers
{
    public class HomeController : Controller
    {

        public ActionResult Index()
        {
            ViewBag.Message = "Confira abaixo as nossas posições recentes e potenciais:";

            return View();
        }

        public ActionResult Facebook()
        {
            ViewBag.Message = "Introdução de trades:";

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your app description page.";

            return View();
        }

    }
}

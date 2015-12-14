using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Microsoft.AspNet.SignalR;

namespace CAT.Azure
{
    public class Global : HttpApplication
    {
        public static Dictionary<String, String> Users = new Dictionary<string, string>();
    
        protected void Application_Start()
        {
            var hubConfiguration = new HubConfiguration();
            hubConfiguration.EnableJavaScriptProxies = false;

            RouteTable.Routes.MapHubs("/tradeserver", hubConfiguration);

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            AuthConfig.RegisterAuth();
        }

        void Application_End(object sender, EventArgs e)
        {
            //  Code that runs on application shutdown
        }

        void Application_Error(object sender, EventArgs e)
        {
            // Code that runs when an unhandled error occurs
        }
    }
}
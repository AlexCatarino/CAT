using System;
using System.Collections.Concurrent;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using CAT.Model;

namespace CAT.Azure
{
    public class Global : HttpApplication
    {
        public static ConcurrentDictionary<Guid, Trade> Trades = new ConcurrentDictionary<Guid, Trade>();
    
        protected void Application_Start()
        {
            //Trades.TryAdd(Guid.NewGuid(), new Trade { Id = Guid.NewGuid(), Symbol = "VALEG25" });
            //Trades.TryAdd(Guid.NewGuid(), new Trade { Id = Guid.NewGuid(), Symbol = "PETRG15" });

            AreaRegistration.RegisterAllAreas();

            RouteTable.Routes.MapHubs();

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
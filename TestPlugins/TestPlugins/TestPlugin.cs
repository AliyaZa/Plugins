using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPlugins
{
    public sealed class TestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            // Obtain the organization service reference which you will need for  
            // web service calls.  
            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService svc = serviceFactory.CreateOrganizationService(context.UserId);

            Entity dogovor = new Entity("al_dogovor");
            dogovor["al_name"] = "plugin dogovor" + DateTime.Now.ToLongDateString();

            var dogovorId=svc.Create(dogovor);

            Entity currentDogovor = (Entity)context.InputParameters["Target"];
            string currentName = currentDogovor.GetAttributeValue<string>("al_name");

            currentDogovor["al_summa"] = 3000;
            currentDogovor["al_name"] = currentName + " " + 40;

            //throw new InvalidPluginExecutionException(" My first Hello world!-->"+ dogovorId);
        }
    }
}

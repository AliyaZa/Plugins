using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace TestPlugins
{
    public sealed class DataPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService =
              (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (!context.InputParameters.Contains("Target") ||
                 !(context.InputParameters["Target"] is Entity))
            {
                throw new InvalidPluginExecutionException("Target is not Entity");
            }

            Entity targetEntity = (Entity)context.InputParameters["Target"];

            if (targetEntity.LogicalName != "al_dogovor")
            {
                throw new InvalidPluginExecutionException("Target's logical name is not al_dogovor");
            }
            try
            {
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                // заполнение поля Дата первого договора на карточке объекта Контакт             
                tracingService.Trace("SetFirstContractDate: Get first date by contact id");

                // внешний ключ из договора на объект контакт
                //Entity dogovor = new Entity("al_dogovor");
                Entity currentDogovor = (Entity)context.InputParameters["Target"];
                Entity currentContact = currentDogovor.GetAttributeValue<Entity>("al_contact");
                //EntityReference contactRef = dogovor.GetAttributeValue<EntityReference>("al_contact");

                
                // дата договора
                var contractDate = currentDogovor.GetAttributeValue<DateTime>("al_date");

                // поиск даты первого договора для заданного контакта
                QueryExpression queryExpression = new QueryExpression();
                queryExpression.EntityName = "al_dogovor";
                queryExpression.ColumnSet = new ColumnSet(new string[] { "al_date", "al_contact" });
                queryExpression.Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression
                        {
                            AttributeName = "al_contact",
                            Operator = ConditionOperator.Equal,
                            Values =  { /*contactRef*/currentContact }
                        }
                    }
                };
                queryExpression.AddOrder("al_date", OrderType.Ascending);
                queryExpression.TopCount = 1;

                var entityCollection = service.RetrieveMultiple(queryExpression);
                tracingService.Trace("SetFirstContractDate: Get query result");

                // дата первого договора
                var date = entityCollection[0].GetAttributeValue<DateTime>("al_date");

                tracingService.Trace("PostContractCreate: Set first contract date in contact object");

                // обновление даты первого договора
                tracingService.Trace($"Current contact id = {currentContact?.Id} date = {date}");
                Entity contactToUpdate = new Entity("contact", currentContact.Id);
                contactToUpdate["al_date"] = date;
                service.Update(contactToUpdate);

                //ContractService svc = new /*TestPlugins.*/ContractService(service, tracingService);
                //svc.SetFirstContractDate();
            }
            catch (Exception ex)
            {
                tracingService.Trace("PreContact: {0}", ex.ToString());
                throw;
            }
        }
    }
}

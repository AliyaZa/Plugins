using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPlugins
{
    public sealed class ContractService
    {
        private readonly IOrganizationService _Service;
        private readonly ITracingService _TracingService;

        public ContractService(IOrganizationService service, ITracingService tracingService)
        {
            _Service = service;
            _TracingService = tracingService;
        }

        // заполнение поля Дата первого договора на карточке объекта Контакт
        
        public void SetFirstContractDate(/*Entity postImage*/)
        {
            // объект сущности договор
            //Entity contract = postImage;

            // guid договора
            /*Guid contractId = contract.Id;*/

            _TracingService.Trace("SetFirstContractDate: Get first date by contact id");

            // внешний ключ из договора на объект контакт
            // EntityReference contactRef = dogovor.GetAttributeValue<EntityReference>("al_contact");
            Entity dogovor = new Entity("al_dogovor");
            EntityReference contactRef = dogovor.GetAttributeValue<EntityReference>("al_contact");

            // дата договора
            var contractDate = dogovor.GetAttributeValue<DateTime>("al_date");

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
                                            Values =  { contactRef.Id }
                                        }
                                    }
            };
            queryExpression.AddOrder("al_date", OrderType.Ascending);
            queryExpression.TopCount = 1;

            var entityCollection = _Service.RetrieveMultiple(queryExpression);
            _TracingService.Trace("SetFirstContractDate: Get query result");

            // дата первого договора
            var date = entityCollection[0].GetAttributeValue<DateTime>("al_date");

            _TracingService.Trace("PostContractCreate: Set first contract date in contact object");

            // обновление даты первого договора
            Entity contactToUpdate = new Entity("contact", contactRef.Id); 
            contactToUpdate["al_date"] = date;
            _Service.Update(contactToUpdate);
        }
    }
}

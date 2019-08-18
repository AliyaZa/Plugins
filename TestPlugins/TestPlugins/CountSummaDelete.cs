using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPlugins
{
    public sealed class CountSummaDelete : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (!context.PreEntityImages.Contains("image") || !(context.PreEntityImages["image"] is Entity))
            {
                throw new InvalidPluginExecutionException("Context has no post images");
            }


            try
            {
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);


                tracingService.Trace("PostOrderCreate: Get order object values");
                Entity currentOrder = (Entity)context.InputParameters["Target"];
                //EntityReference currentContact = currentDogovor.GetAttributeValue<EntityReference>("al_contact");

                var orderValues = service.Retrieve(currentOrder.LogicalName, currentOrder.Id, new ColumnSet(new string[] { "al_facted", "al_summa", "al_dogovorid" }));

                tracingService.Trace("PostOrderCreate: Check if order is paid");
                var isPaid = currentOrder.GetAttributeValue<bool>("al_facted");

                // проверка статуса оплачено на объекте счет
                if (isPaid)
                {
                    var contractId = currentOrder.GetAttributeValue<EntityReference>("al_dogovorid").Id;
                    tracingService.Trace("PostOrderCreateUpdate: Get paid orders by contract and sums");

                    // поиск всех оплаченных счетов для данного договора
                    QueryExpression queryExpression = new QueryExpression();
                    queryExpression.EntityName = "al_order";
                    queryExpression.ColumnSet = new ColumnSet(new string[] { "al_summa" });
                    queryExpression.Criteria = new FilterExpression
                    {
                        Conditions =
                                {
                                    new ConditionExpression
                                    {
                                        AttributeName = "al_dogovorid",
                                        Operator = ConditionOperator.Equal,
                                        Values =  { contractId }
                                    },
                                    new ConditionExpression
                                    {
                                        AttributeName = "al_facted",
                                        Operator = ConditionOperator.Equal,
                                        Values =  { true }
                                    }
                                }
                    };

                    var entityCollection = service.RetrieveMultiple(queryExpression);

                    // общая сумма счетов
                    var commonSum = 0;

                    // подсчёт общей суммы счетов
                    foreach (var ent in entityCollection.Entities)
                    {
                        commonSum = commonSum + (int)ent.Attributes["al_summa"];
                    }

                    tracingService.Trace("PostOrderCreateUpdate: Set fact summa in contract object.");

                    // объект договор связанный со счетами
                    var contractValues = service.Retrieve("al_dogovor", contractId, new ColumnSet(new string[] { "al_summa" }));

                    Entity contractToUpdate = new Entity("al_dogovor", contractId);

                    // общая сумма в договоре
                    var contractSum = contractValues.GetAttributeValue<Money>("al_summa");

                    // проверка не превышает ли общая сумма счетов общей суммы в договоре
                    if ((decimal)commonSum > contractSum.Value)
                    {
                        throw new InvalidPluginExecutionException("Common sum of bills greater than contract sum");
                    }

                    // обновление общей суммы в объекте договор
                    contractToUpdate["al_factsumma"] = new Money((decimal)commonSum);
                    service.Update(contractToUpdate);
                }

            }

            catch (Exception ex)
            {
                tracingService.Trace("PostOrderDelete in: {0}", ex.ToString());
                throw;
            }
        }
    }
}

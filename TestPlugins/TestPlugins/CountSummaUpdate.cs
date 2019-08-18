using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPlugins
{
    public sealed class CountSummaUpdate : IPlugin
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

            if (!context.PostEntityImages.Contains("image") || !(context.PostEntityImages["image"] is Entity))
            {
                throw new InvalidPluginExecutionException("Context has no post images");
            }

            Entity targetEntity = (Entity)context.InputParameters["Target"];

            if (targetEntity.LogicalName != "al_order" && context.MessageName != "Update")
            {
                throw new InvalidPluginExecutionException("Target's logical name is not order or action is not update");
            }
            try
            {
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                Entity currentOrder = (Entity)context.InputParameters["Target"];
                var isPaidOrder = currentOrder.GetAttributeValue<bool>("al_facted");

                    if (isPaidOrder)
                    {
                        var contractId = currentOrder.GetAttributeValue<EntityReference>("al_dogovorid").Id;
                        tracingService.Trace("PostOrderDelete: Get paid orders by contract and sums");

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
                        var commonSum = 0;
                        foreach (var ent in entityCollection.Entities)
                        {
                            commonSum = commonSum + (int)ent.Attributes["al_summa"];
                        }

                        tracingService.Trace("PostOrderDelete: Set fact summa in contract object.");
                        var res = service.Retrieve("al_dogovor", contractId, new ColumnSet(new string[] { "al_summa" }));

                        Entity contractToUpdate = new Entity("al_dogovor", contractId);

                        var contractSum = res.GetAttributeValue<Money>("al_summa");

                        if (contractSum.Value < (decimal)commonSum)
                        {
                            throw new InvalidPluginExecutionException("Common sum of bills greater than contract sum");
                        }

                        contractToUpdate["al_factsumma"] = new Money((decimal)commonSum);
                        service.Update(contractToUpdate);
                    }
            }
            catch (Exception ex)
            {
                tracingService.Trace("PostOrderUpdate: {0}", ex.ToString());
                throw;
            }
        }
    }
}

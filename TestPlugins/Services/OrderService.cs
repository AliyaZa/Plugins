using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPlugins
{
    public sealed class OrderService
    {
        private readonly IOrganizationService _Service;
        private readonly ITracingService _TracingService;

        public OrderService(IOrganizationService service, ITracingService tracingService)
        {
            _Service = service;
            _TracingService = tracingService;
        }

        /// <summary>
        /// подсчёт оплаченной суммы в объекте договор при обовлении и создании счета со статусом оплачено
        /// </summary>
        /// <param name="image"></param>
        public void CalcSum(Entity image)
        {
            Entity order = image;

            // guid текущего счета
            Guid orderId = order.Id;

            _TracingService.Trace("PostOrderCreate: Get order object values");

            // var orderValues = _service.Retrieve(order.LogicalName, orderId, new ColumnSet(new string[] { "al_fact", "al_summa", "al_dogovorid" }));

            _TracingService.Trace("PostOrderCreate: Check if order is paid");
            var isPaid = order.GetAttributeValue<bool>("al_facted");

            // проверка статуса оплачено на объекте счет
            if (isPaid)
            {
                var contractId = order.GetAttributeValue<EntityReference>("al_dogovorid").Id;
                _TracingService.Trace("PostOrderCreateUpdate: Get paid orders by contract and sums");

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

                var entityCollection = _Service.RetrieveMultiple(queryExpression);

                // общая сумма счетов
                var commonSum = 0;

                // подсчёт общей суммы счетов
                foreach (var ent in entityCollection.Entities)
                {
                    commonSum = commonSum + (int)ent.Attributes["al_summa"];
                }

                _TracingService.Trace("PostOrderCreateUpdate: Set fact summa in contract object.");

                // объект договор связанный со счетами
                var contractValues = _Service.Retrieve("al_dogovor", contractId, new ColumnSet(new string[] { "al_summa" }));

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
                _Service.Update(contractToUpdate);
            }
        }

        // подсчёт оплаченной суммы в объекте договор при удалении оплаченного счета
        public void CalcSumDLT(Entity image)
        {
            Entity order = image;
            Guid orderId = order.Id;
            var isPaidOrder = order.GetAttributeValue<bool>("al_facted");

            if (isPaidOrder)
            {
                var contractId = order.GetAttributeValue<EntityReference>("al_dogovorid").Id;
                _TracingService.Trace("PostOrderDelete: Get paid orders by contract and sums");

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

                var entityCollection = _Service.RetrieveMultiple(queryExpression);
                var commonSum = 0;
                foreach (var ent in entityCollection.Entities)
                {
                    commonSum = commonSum + (int)ent.Attributes["al_summa"];
                }

                _TracingService.Trace("PostOrderDelete: Set fact summa in contract object.");
                var res = _Service.Retrieve("al_dogovor", contractId, new ColumnSet(new string[] { "al_summa" }));

                Entity contractToUpdate = new Entity("al_dogovor", contractId);

                var contractSum = res.GetAttributeValue<Money>("al_summa");

                if (contractSum.Value < (decimal)commonSum)
                {
                    throw new InvalidPluginExecutionException("Common sum of bills greater than contract sum");
                }

                contractToUpdate["al_factsumma"] = new Money((decimal)commonSum);
                _Service.Update(contractToUpdate);
            }
        }
    }
}

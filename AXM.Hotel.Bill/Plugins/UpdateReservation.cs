using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AXM.Hotel.Bill.Plugins
{
    public class UpdateReservation : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)serviceFactory.CreateOrganizationService(context.UserId);
                if (context == null || serviceFactory == null || service == null)
                {
                    throw new InvalidPluginExecutionException("Falied to initialize necessary services.");
                }
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {
                    decimal totalPrice = 0;
                    Entity billEntity = context.InputParameters["Target"] as Entity;
                    if (context.MessageName.ToLower() == "update")
                    {
                        Entity preImage = context.PreEntityImages["PreImage"] as Entity;
                        if (preImage.Contains("axm_price") && billEntity.Contains("axm_price"))
                        {
                            Money previousPrice = (Money)preImage["axm_price"];
                            Money newPrice = (Money)billEntity["axm_price"];
                            if(previousPrice.Value == newPrice.Value)
                            {
                                return;
                            }
                            if (preImage.Attributes.Contains("axm_reservation") && preImage.Attributes["axm_reservation"] is EntityReference)
                            {
                                Guid reservationId = ((EntityReference)preImage["axm_reservation"]).Id;


                                QueryExpression qeBills = new QueryExpression("axm_bill")
                                {
                                    ColumnSet = new ColumnSet("axm_price"),
                                    Criteria = new FilterExpression
                                    {
                                        Conditions =
                                {
                                    new ConditionExpression("axm_reservation", ConditionOperator.Equal, reservationId)
                                }
                                    }
                                };

                                EntityCollection ecBills = service.RetrieveMultiple(qeBills);

                                foreach (var bill in ecBills.Entities)
                                {
                                    if (bill.Attributes.Contains("axm_price"))
                                    {
                                        Money price = (Money)bill["axm_price"];
                                        totalPrice += price.Value;
                                    }
                                }
                                Entity updateReservation = new Entity("axm_reservation")
                                {
                                    Id = reservationId,
                                    ["axm_totalprice"] = new Money(totalPrice)
                                };
                                service.Update(updateReservation);
                            }
                        }
                    }

                    else if (context.MessageName.ToLower() == "create")
                    { 
                        if (billEntity.Attributes.Contains("axm_reservation") && billEntity.Attributes["axm_reservation"] is EntityReference)
                        {
                            Guid reservationId = ((EntityReference)billEntity["axm_reservation"]).Id;

                            QueryExpression qeBills = new QueryExpression("axm_bill")
                            {
                                ColumnSet = new ColumnSet("axm_price"),
                                Criteria = new FilterExpression
                                {
                                    Conditions =
                                {
                                    new ConditionExpression("axm_reservation", ConditionOperator.Equal, reservationId)
                                }
                                }
                            };

                            EntityCollection ecBills = service.RetrieveMultiple(qeBills);

                            foreach (var bill in ecBills.Entities)
                            {
                                if (bill.Attributes.Contains("axm_price"))
                                {
                                    Money price = (Money)bill["axm_price"];
                                    totalPrice += price.Value;
                                }
                            }

                            Entity updateReservation = new Entity("axm_reservation")
                            {
                                Id = reservationId,
                                ["axm_totalprice"] = new Money(totalPrice)
                            };
                            service.Update(updateReservation);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}

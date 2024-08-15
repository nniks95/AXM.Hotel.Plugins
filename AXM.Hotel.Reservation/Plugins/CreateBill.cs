using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Axm.Xrm.Entities;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AXM.Hotel.Reservation.Plugins
{
    public class CreateBill : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)serviceFactory.CreateOrganizationService(context.UserId);
                if (context == null || serviceFactory == null || service == null)
                {
                    throw new InvalidPluginExecutionException("Falied to initialize necessary services.");
                }
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {
                    Entity reservationEntity = context.InputParameters["Target"] as Entity;

                    decimal totalPrice = 0;

                    if (reservationEntity.Attributes.Contains("axm_servicetype") && reservationEntity.Attributes["axm_servicetype"] is EntityReference)
                    {
                        Guid serviceId = ((EntityReference)reservationEntity["axm_servicetype"]).Id;

                        QueryExpression qeServicePrice = new QueryExpression("axm_service")
                        {
                            ColumnSet = new ColumnSet("axm_price"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("axm_serviceid", ConditionOperator.Equal, serviceId)
                                }
                            }
                        };
                        EntityCollection ecServiceTypes = service.RetrieveMultiple(qeServicePrice);
                        foreach (Entity serviceType in ecServiceTypes.Entities)
                        {
                            if (serviceType.Attributes.Contains("axm_price"))
                            {
                                Money price = (Money)serviceType["axm_price"];
                                totalPrice = +price.Value;
                            }
                        }
                    }
                    if (reservationEntity.Attributes.Contains("axm_room") && reservationEntity.Attributes["axm_room"] is EntityReference)
                    {
                        Guid roomId = ((EntityReference)reservationEntity["axm_room"]).Id;

                        Entity roomEntity = service.Retrieve("axm_room", roomId, new ColumnSet("axm_roomtype"));
                        if (roomEntity.Attributes.Contains("axm_roomtype") && roomEntity.Attributes["axm_roomtype"] is EntityReference)
                        {
                            Guid roomTypeId = ((EntityReference)roomEntity["axm_roomtype"]).Id;

                            QueryExpression qeRoomType = new QueryExpression("axm_roomtype")
                            {
                                ColumnSet = new ColumnSet("axm_price"),
                                Criteria = new FilterExpression
                                {
                                    Conditions =
                                        {
                                            new ConditionExpression("axm_roomtypeid", ConditionOperator.Equal, roomTypeId)
                                        }
                                }
                            };
                            EntityCollection ecRoomTypes = service.RetrieveMultiple(qeRoomType);

                            foreach (Entity roomType in ecRoomTypes.Entities)
                            {
                                if (roomType.Attributes.Contains("axm_price"))
                                {
                                    Money price = (Money)roomType["axm_price"];
                                    totalPrice += price.Value;
                                }
                            }
                        }
                    }
                    /*
                    axm_Bill bill = new axm_Bill();
                    bill.AXM_Price = new Money(totalPrice);
                    bill.AXM_Reservation = new EntityReference("axm_reservation", reservationEntity.Id);
                    bill.AXM_Bill1 = "Bill blablalb";
                    bill.AXM_BillType = axm_BilltypeNn.Room;
                    service.Create(bill); - EARLY BIND*/


                    Entity billEntity = new Entity("axm_bill");
                    billEntity["axm_bill"] = "Bill for service costs and room price.";
                    billEntity["axm_price"] = new Money(totalPrice);
                    billEntity["axm_billtype"] = new OptionSetValue(0);
                    billEntity["axm_reservation"] = new EntityReference("axm_reservation", reservationEntity.Id);
                    service.Create(billEntity); // - LATE BIND
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
        }
    }
}
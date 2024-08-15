using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AXM.Hotel.NewBill.Plugins
{
    public class CreateNewBill : IPlugin
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

                    if ((reservationEntity.Attributes.Contains("axm_servicetype") && reservationEntity.Attributes["axm_servicetype"] is EntityReference)&&
                        (reservationEntity.Attributes.Contains("axm_room") && reservationEntity.Attributes["axm_room"] is EntityReference))
                    {
                        Guid serviceId = ((EntityReference)reservationEntity["axm_servicetype"]).Id;
                        Guid roomId = ((EntityReference)reservationEntity["axm_room"]).Id;

                        LinkEntity roomEntityLink = new LinkEntity
                        {
                            LinkFromEntityName = "axm_reservation",
                            LinkToEntityName = "axm_room",
                            LinkFromAttributeName = "axm_room",
                            LinkToAttributeName = "axm_roomid",
                            JoinOperator = JoinOperator.Inner
                        };
                        roomEntityLink.Columns = new ColumnSet("axm_roomtype");
                        roomEntityLink.EntityAlias = "axm_room";

                        LinkEntity roomTypeEntityLink = new LinkEntity
                        {
                            LinkFromEntityName = "axm_room",
                            LinkToEntityName = "axm_roomtype",
                            LinkFromAttributeName = "axm_roomtype",
                            LinkToAttributeName = "axm_roomtypeid",
                            JoinOperator = JoinOperator.Inner
                        };
                        roomTypeEntityLink.Columns = new ColumnSet("axm_price");
                        roomTypeEntityLink.EntityAlias = "axm_roomtype";        

                        LinkEntity serviceEntityLink = new LinkEntity
                        {
                            LinkFromEntityName = "axm_reservation",
                            LinkToEntityName = "axm_service",
                            LinkFromAttributeName = "axm_servicetype",
                            LinkToAttributeName = "axm_serviceid",
                            JoinOperator = JoinOperator.Inner
                        };

                        serviceEntityLink.Columns = new ColumnSet("axm_price");
                        serviceEntityLink.EntityAlias = "axm_servicetype";

                        roomEntityLink.LinkEntities.Add(roomTypeEntityLink);

                        QueryExpression query = new QueryExpression("axm_reservation")
                        {
                            ColumnSet = new ColumnSet("axm_room", "axm_servicetype"),
                            Criteria = new FilterExpression
                            {
                                Conditions =
                                {
                                    new ConditionExpression("axm_servicetype", ConditionOperator.Equal, serviceId),
                                    new ConditionExpression("axm_room", ConditionOperator.Equal, roomId)
                                }
                            },
                            LinkEntities = { 
                                roomEntityLink,
                                serviceEntityLink
                            }
                        };

                        EntityCollection result = service.RetrieveMultiple(query);
                        foreach(var entity in result.Entities)
                        {
                            AliasedValue roomTypeIdAliasedValue = entity.GetAttributeValue<AliasedValue>("axm_roomtype.axm_roomtypeid");
                            Guid roomtypeId = (Guid)roomTypeIdAliasedValue.Value;

                            AliasedValue fieldAliasValueRoom = entity.GetAttributeValue<AliasedValue>("axm_roomtype.axm_price");
                            AliasedValue fieldAliasValueService = entity.GetAttributeValue<AliasedValue>("axm_servicetype.axm_price");
                            Money roomTypePrice = (Money)fieldAliasValueRoom.Value;
                            Money servicePrice = (Money)fieldAliasValueService.Value;
                            totalPrice = roomTypePrice.Value + servicePrice.Value;
                        };
                        Entity billEntity = new Entity("axm_bill");
                        billEntity["axm_bill"] = "Bill for service costs and room price.";
                        billEntity["axm_price"] = new Money(totalPrice);
                        billEntity["axm_billtype"] = new OptionSetValue(0);
                        billEntity["axm_reservation"] = new EntityReference("axm_reservation", reservationEntity.Id);
                        service.Create(billEntity);
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


/*FilterExpression roomFilter = new FilterExpression(LogicalOperator.And);
roomFilter.AddCondition("axm_roomtype", ConditionOperator.Equal, roomTypeId);
roomEntityLink.LinkCriteria.AddFilter(roomFilter);*/

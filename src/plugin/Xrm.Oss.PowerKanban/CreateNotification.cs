﻿using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Xrm.Oss.FluentQuery;

namespace Xrm.Oss.PowerKanban
{

    [DataContract]
    public class CreateNotificationConfig
    {
        [DataMember(Name = "parentLookupName")]
        public string ParentLookupName { get; set; }

        [DataMember(Name = "subscriptionLookupName")]
        public string SubscriptionLookupName { get; set; }

        [DataMember(Name = "notificationLookupName")]
        public string NotificationLookupName { get; set; }

        [DataMember(Name = "notifyCurrentUser")]
        public bool NotifyCurrentUser { get; set; }

        [DataMember(Name = "capturedFields")]
        public List<string> CapturedFields { get; set; }
    }
    
    public enum EventType {
        Update = 863910000,
        Create = 863910001,
        Assign = 863910002,
        Delete = 863910003,
        UserMention = 863910004
    }

    public class CreateNotification : IPlugin
    {
        private readonly CreateNotificationConfig config;

        public CreateNotification(string unsecure, string secure)
        {
            config = JsonDeserializer.Parse<CreateNotificationConfig>(unsecure);
        }

        public Entity GetTarget(IPluginExecutionContext context)
        {
            if (context.InputParameters.ContainsKey("Target"))
            {
                return context.InputParameters["Target"] as Entity;
            }

            return null;
        }

        public EntityReference GetTargetRef(IPluginExecutionContext context)
        {
            if (context.InputParameters.ContainsKey("Target"))
            {
                return context.InputParameters["Target"] as EntityReference;
            }

            return null;
        }

        public EventType GetEventType(IPluginExecutionContext context)
        {
            switch (context.MessageName.ToLowerInvariant())
            {
                case "create":
                    return EventType.Create;
                case "update":
                    return EventType.Update;
                case "assign":
                    return EventType.Assign;
                case "delete":
                    return EventType.Delete;
                default:
                    return EventType.UserMention;
            }
        }

        private T GetValue<T> (string attribute, Entity target, EntityImageCollection preImages)
        {
            if (string.IsNullOrEmpty(attribute))
            {
                return default(T);
            }

            return target.Contains(attribute)
                ? target.GetAttributeValue<T>(attribute)
                : preImages.Select(p => p.Value.GetAttributeValue<T>(config.ParentLookupName)).FirstOrDefault();
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext;
            var crmTracing = serviceProvider.GetService(typeof(ITracingService)) as ITracingService;
            var serviceFactory = serviceProvider.GetService(typeof(IOrganizationServiceFactory)) as IOrganizationServiceFactory;
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            var target = GetTarget(context);
            var targetRef = GetTargetRef(context);

            if (target == null && targetRef == null)
            {
                return;
            }

            var attributes = target != null
                ? target.Attributes.Keys.ToList()
                : null;

            var filteredAttributes = config.CapturedFields != null
                ? attributes.Where(a => config.CapturedFields.Any(f => string.Equals(a, f, StringComparison.InvariantCultureIgnoreCase))).ToList()
                : attributes;

            var eventData = new EventData
            {
                UpdatedFields = filteredAttributes,
                EventRecordReference = target?.ToEntityReference() ?? targetRef
            };

            var eventTarget = string.IsNullOrEmpty(config.ParentLookupName) && eventData.EventRecordReference.Id != Guid.Empty
                ? eventData.EventRecordReference
                : GetValue<EntityReference>(config.ParentLookupName, target, context.PreEntityImages);

            if (eventTarget == null) {
                crmTracing.Trace("Failed to find parent, exiting");
                return;
            }

            var subscriptionsQuery = service.Query("oss_subscription")
                .Where(e => e
                    .Attribute(a => a
                        .Named(config.SubscriptionLookupName)
                        .Is(ConditionOperator.Equal)
                        .To(eventTarget.Id)
                    )
                    .Attribute(a => a
                        .Named("statecode")
                        .Is(ConditionOperator.Equal)
                        .To(0)
                    )
                )
                .IncludeColumns("ownerid");

            if (!config.NotifyCurrentUser) {
                subscriptionsQuery.AddCondition(
                    (a => a
                        .Named("ownerid")
                        .Is(ConditionOperator.NotEqual)
                        .To(context.UserId)
                    )
                );
            }
                
            var subscriptions = subscriptionsQuery.RetrieveAll();

            var serializedNotification = JsonSerializer.Serialize(eventData);
            var eventType = GetEventType(context);

            subscriptions.ForEach(subscription => {
                var notification = new Entity
                {
                    LogicalName = "oss_notification",
                    Attributes = {
                        ["ownerid"] = subscription.GetAttributeValue<EntityReference>("ownerid"),
                        ["oss_event"] = new OptionSetValue((int) eventType),
                        [config.NotificationLookupName] = eventTarget,
                        ["oss_data"] = serializedNotification
                    }
                };

                service.Create(notification);
            });
        }
    }
}
